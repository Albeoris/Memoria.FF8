using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

/// <summary>Actual original-resource callbacks from a declared component foundation, not a Ragnarok acquisition playthrough.</summary>
public sealed class NativeRagnarokFootTests
{
    private static string GameDirectory => TestResources.GameDirectory;

    [Fact]
    public void OriginalLandingHandsOffToCurrentFootControllerAndRestoresIndependently()
    {
        GameSession session = Fixture();
        using WorldMapScene scene = new(session, GameDirectory);
        Land(scene);
        NativeRagnarokWorldRuntimeState landed = scene.State.NativeRagnarokRuntime!;
        NativeRagnarokWorldObject ship = landed.Objects[landed.Allocation.RagnarokIndex];
        Assert.Equal((byte)0, scene.State.NativeFootRuntime!.EntityType);
        Assert.Equal(61, scene.State.NativeFootRuntime.RagnarokDismount!.Landing.Counter);
        Assert.Equal((short)-7077, scene.State.NativeFootRuntime.CameraOffsetZ);
        Assert.Equal(5229, landed.TransferCamera!.CurveThreshold);
        Assert.NotEqual(ship.Position, new NativeWorldPathPosition(scene.State.X, scene.State.NativeHeight, -scene.State.Z));
        NativeWorldRagnarokDismountReceipt receipt = scene.State.NativeFootRuntime.RagnarokDismount!;
        Assert.Throws<InvalidDataException>(() => (receipt with { Camera = receipt.Camera with { CurveThreshold = 5216 } }).Validate(landed));
        Assert.Throws<InvalidDataException>(() => receipt.Validate(landed with { Objects = landed.Objects.SetItem(landed.Allocation.RagnarokIndex, ship with { Position = ship.Position with { X = ship.Position.X + 1 } }) }));
        string path = Path.Combine(Path.GetTempPath(), "Memoria-FF8-ragnarok-foot-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            session.Save(path);
            GameSession restoredSession = new(GameSession.Load(path));
            using WorldMapScene restored = new(restoredSession, GameDirectory);
            scene.Apply(new(PlayerActionKind.Move, Horizontal: 0, Vertical: -1, Frames: 4));
            restored.Apply(new(PlayerActionKind.Move, Horizontal: 0, Vertical: -1, Frames: 4));
            Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State), JsonSerializer.SerializeToElement(restoredSession.State)));
            Assert.Equal(ship, scene.State.NativeRagnarokRuntime!.Objects[landed.Allocation.RagnarokIndex]);
            NativeSavedWorldFootReturn footReturn = NativeSavedWorldFootReturn.Create(scene.State, 257, 3500, 0, 0, 0);
            NativeSavedWorldState saved = session.State.SavedWorld!.WithRagnarokParking(scene.State.NativeRagnarokRuntime).WithFootReturn(footReturn);
            Assert.Equal(footReturn.Pose0, saved.FootReturn!.Pose0);
            Assert.Equal(new NativeSavedWorldPose(ship.Position.X, -ship.Position.Z, checked((short)ship.Position.Y), checked((ushort)ship.Yaw)), saved.RagnarokPose2);
            Assert.NotEqual(saved.RagnarokPose2, saved.FootReturn.Pose0);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void RealBroadContactAndBoardingGateReboardTheParkedShipThroughSixtyOneTakeoffCallbacks()
    {
        GameSession session = Fixture(3600);
        using WorldMapScene scene = new(session, GameDirectory);
        Land(scene);
        scene.Apply(new(PlayerActionKind.Wait));
        for (int callback = 0; callback < 48 && scene.State.NativeRagnarokRuntime!.BroadContact != scene.State.NativeRagnarokRuntime.Allocation.RagnarokIndex; callback++)
        {
            scene.Apply(new(PlayerActionKind.Move, Horizontal: 0, Vertical: 1));
        }
        Assert.Equal(scene.State.NativeRagnarokRuntime!.Allocation.RagnarokIndex, scene.State.NativeRagnarokRuntime.BroadContact);
        scene.Apply(new(PlayerActionKind.Wait));
        Assert.True(scene.Apply(new(PlayerActionKind.Wait, WorldButtons: 0x100)).Accepted);
        Assert.Equal((byte)1, scene.State.NativeFootRuntime!.MapDisplayMode);
        Assert.Equal((byte)1, scene.State.NativeRagnarokRuntime.MapDisplayMode);
        Assert.Equal((byte)0, scene.State.NativeRagnarokRuntime.Allocation.MapDisplayMode);
        scene.Apply(new(PlayerActionKind.Wait));
        scene.Apply(new(PlayerActionKind.Confirm));
        Assert.Null(scene.State.NativeFootRuntime);
        Assert.Equal((byte)1, scene.State.NativeRagnarokRuntime!.MapDisplayMode);
        Assert.Equal(NativeRagnarokTransferKind.Takeoff, scene.State.NativeRagnarokRuntime!.Transfer!.Kind);
        Assert.Equal(1, scene.State.NativeRagnarokRuntime.Transfer.Counter);
        scene.Apply(new(PlayerActionKind.Wait, Frames: 60));
        Assert.Null(scene.State.NativeFootRuntime);
        Assert.Equal((short)-14336, scene.State.NativeRagnarokRuntime!.TransferCamera!.OffsetZ);
        Assert.Equal(6144, scene.State.NativeRagnarokRuntime.TransferCamera.CurveThreshold);
        Assert.Equal(new NativeWorldPathPosition(scene.State.X, scene.State.NativeHeight, -scene.State.Z), scene.State.NativeRagnarokRuntime.Objects[scene.State.NativeRagnarokRuntime.Allocation.RagnarokIndex].Position);
        scene.Apply(new(PlayerActionKind.Wait, Frames: 2));
        Assert.Equal((short)-14336, scene.State.NativeRagnarokRuntime!.TransferCamera!.OffsetZ);
    }

    [Fact]
    public void FirstBoardingCabinDeparturePreservesFoot0AndParkedShip2AndTheirIndependentReload()
    {
        GameSession session = Fixture();
        using (WorldMapScene scene = new(session, GameDirectory))
        {
            Land(scene);
            ApproachShip(scene);
            scene.PrepareFieldEntry = (selection, finalWorld, location, salary) =>
            {
                Assert.Equal(44, selection.Entry.Index);
                Assert.Equal(827, selection.Entry.FieldId);
                NativeSavedWorldFootReturn receipt = NativeSavedWorldFootReturn.RecordFootDeparture(finalWorld, checked((ushort)session.State.FieldId), session.State.WorldEntryMemory!.StoryWord, location);
                FieldTransition transition = new(selection.Entry.FieldId, selection.Entry.X, selection.Entry.Y, selection.Entry.Triangle, selection.Entry.Direction);
                GameState departure = session.State;
                return () => session.CommitPreparedWorldFieldEntry(departure, finalWorld, transition, receipt, salary, _ => { });
            };
            scene.Apply(new(PlayerActionKind.Confirm));
        }
        Assert.Equal(SceneKind.Field, session.State.Scene);
        Assert.Equal(827, session.State.FieldId);
        Assert.NotNull(session.State.SavedWorld!.FootReturn);
        Assert.Null(session.State.SavedWorld.RagnarokReturn);
        NativeSavedWorldPose parked = session.State.SavedWorld.RagnarokPose2!;
        NativeSavedWorldPose footPose = session.State.SavedWorld.FootReturn!.Pose0;
        Assert.NotEqual(parked, footPose);
        Assert.Equal((byte)0, session.State.WorldConfiguration!.EntityByte);
        //Declare the original general saved-foot packet for an isolated reload
        //component. This does not execute or replace field827's exit scripts.
        session.RequestWorld(new(62, 0, -1));
        using WorldMapScene reload = new(session, GameDirectory);
        Assert.Equal(NativeFootWorldRuntimeState.SavedReloadRule, reload.State.NativeFootRuntime!.Rule);
        Assert.Equal(new NativeWorldPathPosition(footPose.X, footPose.Height, -footPose.RawZ), reload.State.NativeRagnarokRuntime!.Objects[0].Position);
        Assert.Equal(new NativeWorldPathPosition(parked.X, parked.Height, -parked.RawZ), reload.State.NativeRagnarokRuntime.Objects[reload.State.NativeRagnarokRuntime.Allocation.RagnarokIndex].Position);
        Assert.Equal(5216, reload.State.NativeRagnarokRuntime.TransferCamera!.CurveThreshold);
        Assert.Equal((short)-7168, reload.State.NativeFootRuntime.CameraOffsetZ);
        Assert.Equal(24, reload.Apply(new(PlayerActionKind.Wait, Frames: 24)).FramesAdvanced);
        string path = Path.Combine(Path.GetTempPath(), "Memoria-FF8-ragnarok-foot-reload-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            session.Save(path);
            GameSession restoredSession = new(GameSession.Load(path));
            using WorldMapScene restored = new(restoredSession, GameDirectory);
            reload.Apply(new(PlayerActionKind.Wait));
            restored.Apply(new(PlayerActionKind.Wait));
            Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State), JsonSerializer.SerializeToElement(restoredSession.State)));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static void ApproachShip(WorldMapScene scene)
    {
        scene.Apply(new(PlayerActionKind.Wait));
        for (int callback = 0; callback < 48 && scene.State.NativeRagnarokRuntime!.BroadContact != scene.State.NativeRagnarokRuntime.Allocation.RagnarokIndex; callback++)
        {
            scene.Apply(new(PlayerActionKind.Move, Horizontal: 0, Vertical: 1));
        }
        Assert.Equal(scene.State.NativeRagnarokRuntime!.Allocation.RagnarokIndex, scene.State.NativeRagnarokRuntime.BroadContact);
    }

    private static void Land(WorldMapScene scene)
    {
        scene.Apply(new(PlayerActionKind.Wait, Frames: 24));
        scene.Apply(new(PlayerActionKind.Confirm));
        Assert.True(scene.State.NativeRagnarokRuntime!.Transfer is not null, $"Landing not admitted at({scene.State.X},{scene.State.Z},{scene.State.NativeHeight}) ground={scene.State.GroundType}, frame={scene.State.Frame}, groundHeight={scene.State.NativeRagnarokRuntime.CurrentGroundHeight}, contacts={scene.State.NativeRagnarokRuntime.PrimaryContact}/{scene.State.NativeRagnarokRuntime.BroadContact}.");
        Assert.Equal(NativeRagnarokTransferKind.Landing, scene.State.NativeRagnarokRuntime.Transfer.Kind);
        scene.Apply(new(PlayerActionKind.Wait, Frames: 60));
        Assert.NotNull(scene.State.NativeFootRuntime);
    }

    private static GameSession Fixture(ushort story = 3500)
    {
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        using GameWorld initialization = new(session, GameDirectory);
        NativeWorldEntryMemory entry = session.State.WorldEntryMemory!.WriteBytes(256, 2, story);
        session.Restore(session.State with { Scene = SceneKind.WorldMap, WorldArrival = new(32, 50, -1), WorldEntryMemory = entry, SavedWorld = session.State.SavedWorld! with { WorldEventFlags116 = NativeWorldEventFlags.CreateNewGame().WithOperand(260) } });
        return session;
    }
}
