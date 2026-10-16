using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeWorldAuxiliaryFootEntryTests
{
    [Fact]
    public void Actual547ExitRelocatesParkedGardenAndColdRestoresWithoutConsumingSelectorsTwice()
    {
        string root = AppContext.BaseDirectory;
        GameSession session = new(GameSession.Load(TestResources.FixturePath("App.Tests.NativeWorldAuxiliaryFootEntryTests.1")));
        NativeSavedWorldPose originalParking = session.State.SavedWorld!.GardenPose3!;
        Assert.Equal(7, session.State.EventMemory.Read(269, 1).RequireExact());
        using GameWorld world = new(session, TestResources.GameDirectory);
        Assert.True(world.Apply(new(PlayerActionKind.Move, Horizontal: -0.31577f, Vertical: -0.94884f, Frames: 6)).Accepted);
        Assert.Equal(SceneKind.WorldMap, session.State.Scene);
        WorldState current = session.State.World!;
        Assert.Equal(new WorldTransition(24, 0, -1), session.State.WorldArrival);
        Assert.NotNull(current.NativeFootRuntime);
        NativeGardenWorldRuntimeState garden = current.NativeGardenRuntime!;
        Assert.Equal(7, garden.AuxiliaryEntry!.Before269);
        Assert.Equal(new NativeWorldPathPosition(20360, 200, 3850), garden.Objects[garden.Allocation.Garden64Index].Position);
        Assert.Equal(garden.Objects[garden.Allocation.Garden64Index].Position, garden.Objects[garden.Allocation.Garden65Index].Position);
        Assert.Equal((short)0, garden.Objects[garden.Allocation.Garden64Index].RotationY);
        Assert.Equal(originalParking, session.State.SavedWorld.GardenPose3);
        Assert.Equal(0, session.State.EventMemory.Read(269, 2).RequireExact());
        Assert.NotEqual(new NativeWorldPathPosition(current.X, current.NativeHeight, -current.Z), garden.Objects[garden.Allocation.Garden64Index].Position);
        string snapshot = Path.Combine(Path.GetTempPath(), "Memoria-FF8-foot-aux-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            world.EnsureField();
            session.SetEventState(world.Events.Capture());
            session.Save(snapshot);
            GameSession restoredSession = new(GameSession.Load(snapshot));
            using GameWorld restored = new(restoredSession, TestResources.GameDirectory);
            restored.EnsureField();
            Assert.Equal(0, restoredSession.State.EventMemory.Read(269, 2).RequireExact());
            Assert.True(world.Apply(new(PlayerActionKind.Wait)).Accepted);
            Assert.True(restored.Apply(new(PlayerActionKind.Wait)).Accepted);
            Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State), JsonSerializer.SerializeToElement(restoredSession.State)));
        }
        finally
        {
            File.Delete(snapshot);
        }
    }
}

