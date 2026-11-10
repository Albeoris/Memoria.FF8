using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class VehicleCockpitInputTests
{
    [Theory]
    [InlineData(false, 65, 257)]
    [InlineData(true, 44, 827)]
    public void MenuReturnsToTheOriginalCockpitThroughTheWorldCallback(bool ragnarok, int entry, int field)
    {
        GameSession session = CreateVehicle(ragnarok);
        Assert.True(ControllerGameplayInput.WorldOwnsMenu(session.State));
        using WorldMapScene scene = new(session, TestResources.GameDirectory);
        scene.Apply(new(PlayerActionKind.Wait, Frames: 60));
        if (scene.CurrentDialogue is not null)
        {
            scene.Apply(new(PlayerActionKind.Confirm));
        }
        scene.Apply(new(PlayerActionKind.Wait));
        Assert.True(ControllerGameplayInput.WorldOwnsMenu(session.State));
        bool prepared = false;
        scene.PrepareFieldEntry = (selection, finalWorld, location, salary) =>
        {
            Assert.Equal(entry, selection.Entry.Index);
            Assert.Equal(field, selection.Entry.FieldId);
            Assert.Null(finalWorld.NativeFootRuntime);
            prepared = true;
            return () => { };
        };
        PlayerAction action = ControllerGameplayInput.CreateWorldAction(default, session.State, 0x20);
        PlayerActionResult result = scene.Apply(action);
        Assert.True(prepared);
        Assert.Equal(PlayerTriggerKind.FieldTransition, result.Trigger.Kind);
    }

    [Fact]
    public void PhysicalBoardingButtonReachesTheParkedGarden()
    {
        GameSession session = WorldGardenCameraSceneTests.Fixture();
        using WorldMapScene scene = new(session, TestResources.GameDirectory);
        scene.Apply(new(PlayerActionKind.Wait, Frames: 24));
        scene.Apply(new(PlayerActionKind.WorldButtons, 0x40));
        scene.Apply(new(PlayerActionKind.Wait, Frames: 31));
        scene.Apply(new(PlayerActionKind.Wait));
        Assert.NotNull(scene.State.NativeFootRuntime);
        PlayerAction action = ControllerGameplayInput.CreateWorldAction(default, session.State, 0x40);
        Assert.Equal((ushort)0x40, action.WorldButtons);
        scene.Apply(action);
        Assert.Null(scene.State.NativeFootRuntime);
        Assert.Null(scene.State.NativeGardenRuntime!.Transfer);
        Assert.True(ControllerGameplayInput.WorldOwnsMenu(session.State));
    }

    [Fact]
    public void PhysicalBoardingButtonReachesTheParkedRagnarokAndCompletesTakeoff()
    {
        GameSession session = NativeRagnarokFootTests.Fixture(3600);
        using WorldMapScene scene = new(session, TestResources.GameDirectory);
        NativeRagnarokFootTests.Land(scene);
        scene.Apply(new(PlayerActionKind.Wait));
        for (int callback = 0; callback < 48 && scene.State.NativeRagnarokRuntime!.BroadContact != scene.State.NativeRagnarokRuntime.Allocation.RagnarokIndex; callback++)
        {
            scene.Apply(new(PlayerActionKind.Move, Vertical: 1));
        }
        scene.Apply(new(PlayerActionKind.Wait));
        Assert.False(ControllerGameplayInput.WorldOwnsMenu(session.State));
        PlayerAction action = ControllerGameplayInput.CreateWorldAction(default, session.State, 0x40);
        Assert.Equal((ushort)0x40, action.WorldButtons);
        scene.Apply(action);
        Assert.Null(scene.State.NativeFootRuntime);
        Assert.Equal(NativeRagnarokTransferKind.Takeoff, scene.State.NativeRagnarokRuntime!.Transfer!.Kind);
        scene.Apply(new(PlayerActionKind.Wait, Frames: 60));
        Assert.Null(scene.State.NativeRagnarokRuntime.Transfer);
    }

    private static GameSession CreateVehicle(bool ragnarok)
    {
        if (ragnarok)
        {
            return NativeRagnarokFootTests.Fixture(3600);
        }
        GameSession source = GameSession.CreateColdProcess();
        source.StartNewGame();
        using GameWorld initialization = new(source, TestResources.GameDirectory);
        GameState departure = source.State with { WorldEntryMemory = source.State.WorldEntryMemory!.WriteBytes(256, 2, 750).WriteBytes(264, 1, 9).WriteBytes(265, 1, 1) };
        WorldTransition packet = new(12, 48, -1);
        using PreparedWorldEntry prepared = WorldEntryPreflight.Prepare(departure, TestResources.GameDirectory, packet, validatePresentation: false);
        GameSession committed = new(departure);
        committed.CommitPreparedWorldEntry(packet, prepared.InitialState, departure.WorldControls!, prepared.ValidateAdoption, prepared.InitialSavedWorld, prepared.InitialEventMemory);
        return committed;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DismountedPlayerSaveRestoresTheFootOwnerAndTheSameParkedVehicle(bool ragnarok)
    {
        string directory = TestResources.GameDirectory;
        GameSession session = ragnarok ? NativeRagnarokFootTests.Fixture(3600) : WorldGardenCameraSceneTests.Fixture();
        using GameWorld world = new(session, directory);
        world.EnsureField();
        WorldMapScene scene = world.WorldScene!;
        if (ragnarok)
        {
            NativeRagnarokFootTests.Land(scene);
        }
        else
        {
            scene.Apply(new(PlayerActionKind.Wait, Frames: 24));
            scene.Apply(new(PlayerActionKind.WorldButtons, 0x40));
            scene.Apply(new(PlayerActionKind.Wait, Frames: 31));
        }
        Assert.NotNull(scene.State.NativeFootRuntime);
        GameplaySaveComponentDocument document = world.CaptureGameplaySave();
        Assert.Equal(0, document.Resume.World!.EntityType);
        string path = Path.Combine(Path.GetTempPath(), "Memoria-FF8-parked-save-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            world.WriteGameplaySave(path);
            GameSession restoredSession = new(PlayerStateLoader.Load(path, directory));
            using GameWorld restored = new(restoredSession, directory);
            restored.EnsureField();
            WorldState state = restoredSession.State.World!;
            Assert.NotNull(state.NativeFootRuntime);
            Assert.Equal(scene.State.X, state.X);
            Assert.Equal(scene.State.Z, state.Z);
            if (ragnarok)
            {
                Assert.Equal(scene.State.NativeRagnarokRuntime!.Objects[scene.State.NativeRagnarokRuntime.Allocation.RagnarokIndex].Position, state.NativeRagnarokRuntime!.Objects[state.NativeRagnarokRuntime.Allocation.RagnarokIndex].Position);
            }
            else
            {
                Assert.Equal(scene.State.NativeGardenRuntime!.Objects[scene.State.NativeGardenRuntime.Allocation.Garden64Index].Position, state.NativeGardenRuntime!.Objects[state.NativeGardenRuntime.Allocation.Garden64Index].Position);
            }
            Assert.True(restored.Apply(new(PlayerActionKind.Wait)).Accepted);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
