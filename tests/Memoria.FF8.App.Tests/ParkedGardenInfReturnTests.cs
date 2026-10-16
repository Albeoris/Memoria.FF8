using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class ParkedGardenInfReturnTests
{
    [Fact]
    public void Actual505InfExitKeepsItsZeroExtraByteAndContinuesAfterStandaloneRestore()
    {
        string source = FindRootFile("../fixtures/external-fixture.json");
        GameSession session = new(GameSession.Load(source));
        using GameWorld world = new(session, TestResources.GameDirectory);
        NativeSavedWorldPose parked = session.State.SavedWorld!.GardenPose3!;
        Assert.Equal(505, session.State.FieldId);
        Assert.True(world.Apply(new(PlayerActionKind.Move, Horizontal: 0.131f, Vertical: 0.99138f, Frames: 4)).Accepted);
        Assert.Equal(SceneKind.WorldMap, session.State.Scene);
        Assert.Equal(new WorldTransition(18, 0, 0), session.State.WorldArrival);
        Assert.Equal(parked, session.State.World!.NativeGardenRuntime!.Allocation.GardenPose);
        Assert.Equal(new WorldTransition(18, 0, 0), session.State.World.NativeGardenRuntime.Allocation.Packet);
        string snapshot = Path.Combine(Path.GetTempPath(), "Memoria-FF8-parked-Garden-INF-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            session.Save(snapshot);
            GameSession restoredSession = new(GameSession.Load(snapshot));
            using GameWorld restored = new(restoredSession, TestResources.GameDirectory);
            Assert.True(world.Apply(new(PlayerActionKind.Wait)).Accepted);
            Assert.True(restored.Apply(new(PlayerActionKind.Wait)).Accepted);
            Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State), JsonSerializer.SerializeToElement(restoredSession.State)));
        }
        finally
        {
            File.Delete(snapshot);
        }
    }

    private static string FindRootFile(string relative)
    {
        return TestResources.FixtureFile(relative);
    }
}
