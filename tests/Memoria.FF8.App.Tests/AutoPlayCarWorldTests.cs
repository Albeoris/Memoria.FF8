using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayCarWorldTests
{
    [Fact]
    public void PreparingAndPublishingStationaryCarPlacementPreservesEveryNonspatialOwner()
    {
        string directory = TestResources.GameDirectory;
        GameSession session = new(GameSession.Load(TestResources.FixturePath("App.Tests.AutoPlayCarWorldTests.1")));
        using WorldMapScene scene = new(session, directory);
        GameState before = session.State;
        WorldState candidate = scene.PrepareCurrentCarPlacement(12390 + 262144, -26709 - 196608);
        Assert.Same(before, session.State);
        Assert.Equal((12390, -26709), (candidate.X, candidate.Z));
        Assert.Equal(before.World!.Frame, candidate.Frame);
        Assert.Equal(before.World.CameraYaw, candidate.CameraYaw);
        Assert.Null(candidate.NativeFootRuntime);
        Assert.Equal(before.World.NativeCarRuntime!.EntityType, candidate.NativeCarRuntime!.EntityType);
        Assert.Equal(before.World.NativeCarRuntime.ShapeId, candidate.NativeCarRuntime.ShapeId);
        Assert.Equal(before.World.NativeCarRuntime.CameraFollowSpeed, candidate.NativeCarRuntime.CameraFollowSpeed);
        Assert.Equal(0, candidate.NativeCarRuntime.Speed);
        Assert.Equal(0, candidate.NativeCarRuntime.MovementMagnitude20409F4);
        Assert.All(candidate.NativeCarRuntime.GroundCache!.Alternatives, alternative =>
        {
            Assert.NotEqual(before.World.NativeCarRuntime.GroundCache!.Alternatives[0].CurrentGround, alternative.CurrentGround);
            Assert.Equal(alternative.CurrentGround, alternative.PreviousGround);
        });
        scene.DebugPlaceCurrentCar(12390, -26709, expectedNativeHeight: candidate.NativeHeight);
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(candidate), JsonSerializer.SerializeToElement(session.State.World)));
        Assert.Same(before.SavedWorld, session.State.SavedWorld);
        Assert.Same(before.WorldArrival, session.State.WorldArrival);
        Assert.Same(before.WorldProcess, session.State.WorldProcess);
        Assert.Same(before.WorldControls, session.State.WorldControls);
        Assert.Same(before.EventMemory, session.State.EventMemory);
        Assert.Same(before.WorldEntryMemory, session.State.WorldEntryMemory);
        Assert.Same(before.Economy, session.State.Economy);
        Assert.Equal(before.FieldId, session.State.FieldId);
        Assert.Equal(before.Variables.GetValueOrDefault(256), session.State.Variables.GetValueOrDefault(256));
    }

    [Fact]
    public void NativeGuardAndSavedReturnOwnersMatchAfterColdRestore()
    {
        string directory = TestResources.GameDirectory;
        GameSession session = new(GameSession.Load(TestResources.FixturePath("App.Tests.AutoPlayCarWorldTests.1")));
        using WorldMapScene scene = new(session, directory);
        NativeSavedWorldState savedBefore = session.State.SavedWorld!;
        scene.DebugPlaceCurrentCar(12390, -26709);
        string path = TestResources.ArtifactFile("stationary-car-placement.json");
        session.Save(path);
        GameSession restored = new(GameSession.Load(path));
        using GameWorld first = new(session, directory);
        using GameWorld second = new(restored, directory);
        first.EnsureField();
        second.EnsureField();
        PlayerActionResult entered = first.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: 24));
        PlayerActionResult repeated = second.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: 24));
        Assert.Equal(PlayerTriggerKind.Dialogue, entered.Trigger.Kind);
        Assert.Equal(49, entered.Trigger.MessageId);
        Assert.Equal(entered, repeated);
        Assert.Equal(SceneKind.WorldMap, session.State.Scene);
        Assert.Equal((byte)14, session.State.World!.EventUi!.ScriptMode);
        Assert.Equal(savedBefore.CarReturn, session.State.SavedWorld!.CarReturn);
        Assert.Equal(savedBefore.FootReturn, session.State.SavedWorld.FootReturn);
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State), JsonSerializer.SerializeToElement(restored.State)));
    }

    [Fact]
    public void WrongHeightAndMovingOrDisposedCarCannotPublishPartialPlacement()
    {
        string directory = TestResources.GameDirectory;
        GameSession session = new(GameSession.Load(TestResources.FixturePath("App.Tests.AutoPlayCarWorldTests.1")));
        using WorldMapScene scene = new(session, directory);
        GameState before = session.State;
        int height = scene.PrepareCurrentCarPlacement(12390, -26709).NativeHeight;
        Assert.Throws<InvalidOperationException>(() => scene.DebugPlaceCurrentCar(12390, -26709, expectedNativeHeight: height + 1));
        Assert.Same(before, session.State);
        session.SetWorldState(before.World! with { NativeCarRuntime = before.World!.NativeCarRuntime! with { Speed = 1 } });
        GameState moving = session.State;
        Assert.Throws<NotSupportedException>(() => scene.DebugPlaceCurrentCar(12390, -26709));
        Assert.Same(moving, session.State);
        scene.Dispose();
        Assert.Throws<ObjectDisposedException>(() => scene.DebugPlaceCurrentCar(12390, -26709));
        Assert.Same(moving, session.State);
    }
}
