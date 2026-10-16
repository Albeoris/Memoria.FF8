using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeGardenTransactionTests
{
    [Fact]
    public void GardenFieldEntryPublishesItsSaveAndConsumedRequestOnlyAfterDestinationValidation()
    {
        GameSession session = Session(GardenWorld());
        GameState before = session.State;
        Assert.Equal((byte)3, before.SavedWorld!.WorldEventVariable125);
        WorldState final = before.World! with { Frame = 1 };
        NativeSavedWorldGardenReturn receipt = NativeSavedWorldGardenReturn.Create(final, 257, 750, 3);
        NativeWorldSalaryState batch = NativeWorldSalaryState.Begin(before).ConsumeGardenScriptRequest().Advance(null, null, null, 0, 2);
        Assert.Equal((byte)0x49, before.WorldEntryMemory!.ReplacementFlags264);
        Assert.Equal((byte)9, batch.WorldEntryMemory!.ReplacementFlags264);
        Assert.Same(batch, batch.ConsumeGardenScriptRequest());
        FieldTransition transition = new(106, 1, 2, 0, 0);
        Assert.Throws<InvalidOperationException>(() => session.CommitPreparedGardenFieldEntry(before, final, transition, receipt, batch, candidate =>
        {
            Assert.Same(before, session.State);
            Assert.Equal(receipt, candidate.SavedWorld!.GardenReturn);
            Assert.Equal((byte)0, candidate.SavedWorld.WorldEventVariable125);
            Assert.Equal(before.SavedWorld.WorldEventFlags116, candidate.SavedWorld.WorldEventFlags116);
            Assert.Equal((byte)9, candidate.WorldEntryMemory!.ReplacementFlags264);
            throw new InvalidOperationException("Declared destination refusal.");
        }));
        Assert.Same(before, session.State);
        Assert.Equal((byte)3, session.State.SavedWorld!.WorldEventVariable125);
        session.CommitPreparedGardenFieldEntry(before, final, transition, receipt, batch, Accept);
        Assert.Equal(SceneKind.Field, session.State.Scene);
        Assert.Equal(receipt.Pose3, session.State.SavedWorld!.GardenPose3);
        Assert.Equal((byte)48, session.State.WorldConfiguration!.EntityByte);
        Assert.Equal((byte)9, session.State.WorldEntryMemory!.ReplacementFlags264);
        Assert.Equal(before.SavedWorld!.Slot4, session.State.SavedWorld.Slot4);
        Assert.Equal(before.SavedWorld.Slot5, session.State.SavedWorld.Slot5);
        Assert.Equal((byte)0, session.State.SavedWorld.WorldEventVariable125);
        string path = Path.Combine(Path.GetTempPath(), "ff8-garden-atomic-return-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            session.Save(path);
            GameState restored = GameSession.Load(path);
            Assert.Equal(receipt, restored.SavedWorld!.GardenReturn);
            Assert.Equal((byte)0, restored.SavedWorld.WorldEventVariable125);
            Assert.Equal((byte)9, restored.WorldEntryMemory!.ReplacementFlags264);
            Assert.Equal(final.NativeGardenRuntime!.Objects, restored.DepartingWorldState!.NativeGardenRuntime!.Objects);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void GardenFieldEntryRejectsChangedReceiptWithoutPublishingItsStagedLatch()
    {
        GameSession session = Session(GardenWorld());
        GameState before = session.State;
        WorldState final = before.World! with { Frame = 1 };
        NativeSavedWorldGardenReturn receipt = NativeSavedWorldGardenReturn.Create(final, 257, 750, 3) with { CameraYaw96 = 16 };
        NativeWorldSalaryState batch = NativeWorldSalaryState.Begin(before).ConsumeGardenScriptRequest().Advance(null, null, null, 0, 2);
        Assert.Throws<InvalidOperationException>(() => session.CommitPreparedGardenFieldEntry(before, final, new(106, 1, 2, 0, 0), receipt, batch, Accept));
        Assert.Same(before, session.State);
        Assert.Equal((byte)0x49, session.State.WorldEntryMemory!.ReplacementFlags264);
    }

    [Fact]
    public void FootFieldExitSavesItsOwnPoseAndTheActuallyParkedGardenSeparately()
    {
        WorldState source = GardenWorld();
        NativeGardenWorldRuntimeState garden = source.NativeGardenRuntime!;
        NativeGardenTransferState transfer = NativeGardenTransferState.Create(0, garden.Allocation.Garden64Index, 0, new(21180, 0, 4600));
        NativeWorldPathPosition position = garden.Objects[garden.Allocation.Garden64Index].Position;
        for (int index = 0; index < 31; index++)
        {
            NativeGardenTransferStep step = transfer.StepLowering(index, position, 0);
            transfer = step.State;
            position = step.GardenPosition;
        }
        garden = garden with { Transfer = transfer, Objects = garden.Objects.SetItem(garden.Allocation.Garden64Index, garden.Objects[garden.Allocation.Garden64Index] with { Position = position }) };
        NativeWorldProcess process = NativeWorldProcess.FromColdProcess().AdvanceMainLoopCallbacks(31);
        NativeFootWorldRuntimeState foot = NativeFootWorldRuntimeState.CreateGardenDismount(garden, transfer, process, 0);
        garden = garden.PublishBody(transfer.TargetPosition, 0, 0, 0, gardenControlled: false) with { GroundCache = foot.GroundCache! };
        WorldState world = source with { Frame = 31, X = 21180, NativeHeight = 0, NativeGardenRuntime = garden, NativeFootRuntime = foot };
        GameSession session = Session(world, process);
        GameState before = session.State;
        WorldState final = world with { Frame = 32 };
        NativeSavedWorldFootReturn receipt = NativeSavedWorldFootReturn.RecordFootDeparture(final, 257, 750, 3);
        NativeWorldSalaryState batch = NativeWorldSalaryState.Begin(before).Advance(null, null, null, 0, 2);
        session.CommitPreparedWorldFieldEntry(before, final, new(106, 1, 2, 0, 0), receipt, batch, Accept);
        Assert.Equal(new NativeSavedWorldPose(20480, -4600, 14, 0), session.State.SavedWorld!.GardenPose3);
        Assert.Equal(new NativeSavedWorldPose(21180, -4600, 0, 0), session.State.SavedWorld.FootReturn!.Pose0);
        Assert.Null(session.State.SavedWorld.GardenReturn);
        Assert.Equal((byte)0, session.State.WorldConfiguration!.EntityByte);
        Assert.Equal((byte)0, session.State.SavedWorld.WorldEventVariable125);
    }

    private static GameSession Session(WorldState world, NativeWorldProcess? process = null)
    {
        // Declared component owners exercise atomic publication, not a gameplay route.
        return new GameSession(new GameState
        {
            Scene = SceneKind.WorldMap,
            FieldId = 257,
            WorldArrival = new(12, 48, -1),
            World = world,
            SavedWorld = NativeSavedWorldState.CreateNewGame() with { WorldEventVariable125 = 3 },
            WorldControls = NativeWorldControlState.CreateNewGame(),
            WorldProcess = process ?? NativeWorldProcess.FromColdProcess(),
            WorldEntryMemory = NativeWorldEntryMemory.CreateNewGame().WriteBytes(256, 2, 750).WriteBytes(264, 1, 0x49),
            WorldConfiguration = NativeWorldConfiguration.FromFieldPacket(new(12, 48, -1), 257)
        });
    }

    private static WorldState GardenWorld()
    {
        NativeSavedWorldPose pose = new(20480, -4600, 200, 0);
        NativeWorldPathPosition position = new(20480, 200, 4600);
        NativeGardenWorldObject[] objects = [new(0, 0, position, 0, 0, 0), new(2, 0, default, 0, 0, 0), new(64, 0, position, 0, 0, 0), new(65, 2, position, 0, 0, 0)];
        ushort[] words = [0xFF01, 0, 0xFF02, 740, 0xFF03, 900, 0xFF04, 0, 0xFF13, 0, 0xFF13, 2, 0xFF13, 3, 0xFF13, 64, 0xFF13, 65, 0xFF05, 0];
        string hash = new('A', 64);
        NativeGardenWorldAllocation allocation = new(1, NativeGardenWorldAllocation.AllocationRule, hash, hash, hash, 8, 740, 900, 750, 0, 0, words.ToImmutableArray(), objects.ToImmutableArray(), 0, 2, 3, new(12, 48, -1), pose, pose, 0, 0, 0) { GuardianForce13Flags = 1 };
        NativeGardenWorldRuntimeState garden = NativeGardenWorldRuntimeState.CreateFresh(NativeSavedWorldState.CreateNewGame(), allocation, NativeWorldGroundCacheState.CreateFresh(new(0, 0, 0, 0)), NativeWorldProcess.FromColdProcess(), new(NativeFieldRuntimeProfile.FF8EnExecutableHash, -112, 0, -341, -7168), currentGroundHeight: 0);
        return new WorldState { X = pose.X, Z = pose.RawZ, NativeHeight = pose.Height, GroundType = 6, NativeGardenRuntime = garden };
    }

    private static void Accept(GameState state)
    {
    }
}
