using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeTrainRideTests
{
    private static string GameDirectory => TestResources.GameDirectory;

    [Fact]
    public void ActualModeThreeAllocatesBothBanksFootBodyAndCameraThenRestoresTheNextCallback()
    {
        Assert.SkipUnless(Directory.Exists(GameDirectory), "Installed original world resources are required.");
        using GameAssets initialization = new(GameDirectory);
        GameSession session = CreateComponent(initialization);
        var packet = new WorldTransition(64, 16, -1);
        GameState departure = session.State;
        using PreparedWorldEntry prepared = WorldEntryPreflight.Prepare(departure, GameDirectory, packet);
        Assert.Same(departure, session.State);
        NativeTrainWorldRuntimeState allocation = prepared.InitialState.NativeTrainRuntime!;
        Assert.Equal(0, allocation.Callback);
        Assert.Equal(0, allocation.Primary.TrackIndex);
        Assert.Equal(1, allocation.Alternate!.TrackIndex);
        Assert.Null(allocation.Primary.Status);
        Assert.Null(allocation.Alternate.Status);
        Assert.Equal(9, allocation.Load.Allocation.Objects.Length);
        Assert.Equal((byte)16, allocation.EntityType);
        Assert.Equal((byte)0, allocation.ControlledObjectEntityType);
        Assert.Equal(allocation.Primary.Nodes[2].Position, allocation.BodyPosition);
        Assert.Equal(1, unchecked((byte)(allocation.Forest.RandomCursor - allocation.Forest.SeedCounter)));
        session.CommitPreparedWorldEntry(packet, prepared.InitialState, session.WorldControls!, prepared.ValidateAdoption, prepared.InitialSavedWorld);
        using WorldMapScene scene = prepared.Adopt(session);
        Assert.True(scene.IsPresentationDeferred);
        Assert.Throws<NativeWorldPresentationDeferredException>(() => scene.Render(320, 224));
        PlayerActionResult first = scene.Apply(new PlayerAction(PlayerActionKind.Wait));
        Assert.Equal(1, first.FramesAdvanced);
        NativeTrainWorldRuntimeState live = scene.State.NativeTrainRuntime!;
        Assert.Equal(29, live.Primary.Pause);
        Assert.Equal(0, live.Primary.Speed);
        Assert.All(live.Primary.Presentation, pose => Assert.NotNull(pose.Projection));
        Assert.Equal(1, live.Callback);
        Assert.Equal(allocation.BodyPosition, live.BodyPosition);
        byte[] pixels = scene.Render(320, 224);
        Assert.Equal(320 * 224 * 4, pixels.Length);
        GameState snapshot = JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!;
        var restoredSession = new GameSession(snapshot);
        using WorldMapScene restored = new(restoredSession, GameDirectory);
        Assert.Equal(pixels, restored.Render(320, 224));
        PlayerAction wait = new(PlayerActionKind.Wait, Frames: 30);
        Assert.Equal(scene.Apply(wait), restored.Apply(wait));
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State), JsonSerializer.SerializeToElement(restoredSession.State)));
        Assert.Equal(scene.Render(320, 224), restored.Render(320, 224));
        NativeTrainWorldRuntimeState after = scene.State.NativeTrainRuntime!;
        Assert.Equal(31, after.Callback);
        Assert.Equal(allocation.Forest.RandomCursor, after.Forest.RandomCursor);
        Assert.NotEqual(allocation.Primary.Nodes[2].Position, after.Primary.Nodes[2].Position);
        Assert.NotEqual(allocation.BodyPosition, after.BodyPosition);
        Assert.NotNull(after.Primary.Status);
        Assert.Equal(0u, session.SeeD!.SalaryMovementCounter);
        var altered = new GameSession(session.State with { World = scene.State with { NativeTrainRuntime = after with { Primary = after.Primary with { Pause = 30 } } } });
        GameState unchanged = altered.State;
        Assert.Throws<InvalidDataException>(() => new WorldMapScene(altered, GameDirectory));
        Assert.Same(unchanged, altered.State);
        NativeEventRandomState? eventRandom = session.EventRandom;
        uint salaryCounter = session.SeeD.SalaryMovementCounter;
        PlayerActionResult held = scene.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: 1));
        PlayerActionResult idle = restored.Apply(new PlayerAction(PlayerActionKind.Wait));
        Assert.True(held.Accepted);
        Assert.Equal(held.FramesAdvanced, idle.FramesAdvanced);
        Assert.Equal(JsonSerializer.Serialize(scene.State.NativeTrainRuntime), JsonSerializer.Serialize(restored.State.NativeTrainRuntime));
        Assert.Equal(eventRandom, session.EventRandom);
        Assert.Equal(salaryCounter, session.SeeD.SalaryMovementCounter);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    public void LoaderCopiesFieldByte265ToWorldHeader103ForTrackMaskAndCannotBorrowModeTwoEndpointSemantics(byte field265, byte field103)
    {
        Assert.SkipUnless(Directory.Exists(GameDirectory), "Installed original world resources are required.");
        using GameAssets initialization = new(GameDirectory);
        GameSession session = CreateComponent(initialization);
        session.SetWorldControls(session.WorldControls! with { EventFlags103 = field103 });
        session.SetWorldEntryMemory(session.WorldEntryMemory!.WriteBytes(265, 1, field265));
        using PreparedWorldEntry ticket = WorldEntryPreflight.Prepare(session.State, GameDirectory, new(64, 16, -1));
        NativeTrainWorldRuntimeState train = ticket.InitialState.NativeTrainRuntime!;
        Assert.Equal(field265, train.Load.Allocation.EventFlags103);
        Assert.Equal(0, train.Primary.TrackIndex);
        if (field265 == 0)
        {
            Assert.NotNull(train.Alternate);
            Assert.Equal(1, train.Alternate.TrackIndex);
        }
        else
        {
            Assert.Null(train.Alternate);
        }
        Assert.Null(ticket.InitialState.NativeScriptedRuntime);
        Assert.Throws<NotSupportedException>(() => WorldEntryPreflight.Prepare(session.State, GameDirectory, new(64, 16, 53)));
        Assert.Equal(SceneKind.Field, session.State.Scene);
        Assert.Null(session.State.World);
    }

    private static GameSession CreateComponent(GameAssets assets)
    {
        //Declared world-return component boundary, not a live NewGame journey or patched historical checkpoint.
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        NativeSavedWorldState initial = NativeSavedWorldState.CreateNewGame();
        var car = NativeCarWorldRuntimeState.Create(initial, NativeWorldGroundCacheState.CreateFresh(new(0, 0, 0, 0)), NativeWorldProcess.FromColdProcess());
        var previous = new WorldState { Frame = 500, X = 12390, Z = -26709, NativeHeight = -301, BodyYaw = 2256, CameraYaw = 2256, NativeCarRuntime = car };
        NativeSavedWorldCarReturn receipt = NativeSavedWorldCarReturn.Create(previous, 262, 40, 1);
        GameState departure = session.State with { Scene = SceneKind.Field, FieldId = 265, FieldLoadContext = null, WorldEntryMemory = session.State.WorldEntryMemory!.WriteBytes(256, 2, 290), CharacterVitals = assets.ReadNewGameCharacterVitals(), Economy = assets.ReadNewGameEconomy(), DepartingWorldState = previous, SavedWorld = initial.WithOrdinaryCarEntry().WithCarReturn(receipt) };
        session.Restore(departure);
        return session;
    }
}
