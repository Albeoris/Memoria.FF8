using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

/// <summary>Installed-resource component entries exercise the real world ground/contact callbacks.</summary>
public sealed class WorldMovementSpeedTests
{
    [Theory]
    [InlineData(50, 15)]
    [InlineData(100, 31)]
    [InlineData(300, 93)]
    public void Foot_scales_accepted_ground_translation_without_adding_world_callbacks(int percent, int deltaX)
    {
        string directory = GameDirectory();
        GameSession session = CreateSession(directory);
        session.Restore(session.State with { Scene = SceneKind.WorldMap, WorldArrival = new WorldTransition(0, 0, 0) });
        using WorldMapScene scene = new(session, directory);
        CompleteEntryDelay(scene);
        session.SetWorldState(scene.State with { CameraYaw = 0, BodyYaw = 1024 });
        WorldState before = scene.State;
        long callbacks = session.State.WorldProcess!.MainLoopCallbacks;
        scene.PlayerMovementSpeedPercent = percent;
        PlayerActionResult result = scene.Apply(new(PlayerActionKind.Move, Horizontal: 1));
        Assert.True(result.Accepted);
        Assert.Equal(1, result.FramesAdvanced);
        Assert.Equal((before.X + deltaX, before.Z), (scene.State.X, scene.State.Z));
        Assert.Equal(before.Frame + 1, scene.State.Frame);
        Assert.Equal(callbacks + 1, session.State.WorldProcess!.MainLoopCallbacks);
        Assert.NotNull(scene.State.NativeFootRuntime!.GroundCache);
        Assert.Equal(31, scene.State.NativeFootRuntime.MovementMagnitude20409F4);
        Assert.All(scene.State.NativeFootRuntime.GroundCache!.Alternatives, alternative => Assert.Equal(0, alternative.FailedSelections));
    }

    [Fact]
    public void Foot_speed_leaves_native_train_and_world_texture_clocks_at_one_callback()
    {
        string directory = GameDirectory();
        GameSession source = CreateSession(directory);
        source.SetWorldEntryMemory(source.WorldEntryMemory!.WriteBytes(256, 2, 315).WriteBytes(264, 1, 0).WriteBytes(265, 1, 0));
        WorldTransition packet = new(5, 0, -1);
        source.Restore(source.State with { Scene = SceneKind.WorldMap, FieldId = 822, WorldArrival = packet, WorldConfiguration = NativeWorldConfiguration.FromFieldPacket(packet, 822) });
        GameState initial = source.State;
        NativeNormalFootObjectProcess? firstObjects = null;
        NativeWorldTextureAnimationState? firstTexture = null;
        foreach (int percent in new[] { 50, 100, 300 })
        {
            GameSession session = new(initial);
            using WorldMapScene scene = new(session, directory);
            CompleteEntryDelay(scene);
            NativeNormalFootObjectProcess before = scene.State.NativeFootRuntime!.NormalObjects!;
            Assert.NotNull(before);
            long callbacks = session.State.WorldProcess!.MainLoopCallbacks;
            scene.PlayerMovementSpeedPercent = percent;
            Assert.Equal(1, scene.Apply(new(PlayerActionKind.Move, Horizontal: 1)).FramesAdvanced);
            NativeNormalFootObjectProcess after = scene.State.NativeFootRuntime!.NormalObjects!;
            Assert.Equal(before.Callback + 1, after.Callback);
            Assert.Equal(callbacks + 1, session.State.WorldProcess!.MainLoopCallbacks);
            Assert.NotNull(after.Primary);
            Assert.NotNull(after.Alternate);
            if (firstObjects is not null)
            {
                Assert.Equal(firstObjects.Callback, after.Callback);
                Assert.Equal(firstObjects.Primary!.Pause, after.Primary!.Pause);
                Assert.Equal(firstObjects.Alternate!.Pause, after.Alternate!.Pause);
                Assert.Equal(firstObjects.Primary.Nodes.Select(node => node.Distance), after.Primary.Nodes.Select(node => node.Distance));
                Assert.Equal(firstObjects.Alternate.Nodes.Select(node => node.Distance), after.Alternate.Nodes.Select(node => node.Distance));
                Assert.Equal(JsonSerializer.Serialize(firstTexture), JsonSerializer.Serialize(session.State.WorldProcess.TextureAnimation));
            }
            firstObjects = after;
            firstTexture = session.State.WorldProcess.TextureAnimation;
        }
    }

    [Theory]
    [InlineData(50)]
    [InlineData(100)]
    [InlineData(300)]
    public void Car_scales_ground_translation_while_retaining_native_motor_acceleration(int percent)
    {
        string directory = GameDirectory();
        GameSession session = CreateSession(directory);
        NativeWorldEntryMemory memory = session.WorldEntryMemory!.WriteBytes(256, 2, 40).WriteBytes(265, 1, 128);
        session.Restore(session.State with { Scene = SceneKind.WorldMap, FieldId = 262, WorldArrival = new WorldTransition(63, 33, -1), WorldEntryMemory = memory });
        session.Restore(session.State with { SavedWorld = session.State.SavedWorld! with { WorldEventFlags116 = session.State.SavedWorld.WorldEventFlags116!.WithOperand(0x100) } });
        using WorldMapScene scene = new(session, directory);
        CompleteEntryDelay(scene);
        session.SetWorldState(scene.State with { BodyYaw = 0, CameraYaw = 0, NativeCarRuntime = scene.State.NativeCarRuntime! with { Speed = 32 } });
        WorldState before = scene.State;
        long callbacks = session.State.WorldProcess!.MainLoopCallbacks;
        scene.PlayerMovementSpeedPercent = percent;
        Assert.Equal(1, scene.Apply(new(PlayerActionKind.Move, Vertical: -1)).FramesAdvanced);
        Assert.Equal(34, scene.State.NativeCarRuntime!.Speed);
        Assert.Equal(34, scene.State.NativeCarRuntime.MovementMagnitude20409F4);
        Assert.Equal(34, session.State.VibrationProcess!.GraphicsSpeed20409F0);
        Assert.Equal((before.X, before.Z - 34 * percent / 100), (scene.State.X, scene.State.Z));
        Assert.Equal(before.Frame + 1, scene.State.Frame);
        Assert.Equal(callbacks + 1, session.State.WorldProcess!.MainLoopCallbacks);
    }

    [Theory]
    [InlineData(50, false)]
    [InlineData(150, false)]
    [InlineData(300, false)]
    [InlineData(50, true)]
    [InlineData(150, true)]
    [InlineData(300, true)]
    public void Car_automatic_motion_uses_default_speed_until_player_control_returns(int percent, bool entryDelay)
    {
        string directory = GameDirectory();
        GameSession session = CreateSession(directory);
        NativeWorldEntryMemory memory = session.WorldEntryMemory!.WriteBytes(256, 2, 40).WriteBytes(265, 1, 128);
        session.Restore(session.State with { Scene = SceneKind.WorldMap, FieldId = 262, WorldArrival = new WorldTransition(63, 33, -1), WorldEntryMemory = memory });
        session.Restore(session.State with { SavedWorld = session.State.SavedWorld! with { WorldEventFlags116 = session.State.SavedWorld.WorldEventFlags116!.WithOperand(0x100) } });
        using WorldMapScene scene = new(session, directory);
        if (!entryDelay)
        {
            CompleteEntryDelay(scene);
        }
        session.SetWorldState(scene.State with { BodyYaw = 0, CameraYaw = 0, NativeCarRuntime = scene.State.NativeCarRuntime! with { Speed = 32 }, EventUi = scene.State.EventUi!.WithScriptMode(entryDelay ? (byte)0 : (byte)14) });
        GameSession referenceSession = new(session.PrepareSnapshot());
        using WorldMapScene reference = new(referenceSession, directory);
        scene.PlayerMovementSpeedPercent = percent;
        int initialZ = scene.State.Z;
        for (int tick = 0; tick < 3; tick++)
        {
            // Scene input is suppressed; entry coasting also retains the default translation speed.
            PlayerAction move = entryDelay ? new(PlayerActionKind.Wait) : new(PlayerActionKind.Move, Vertical: -1);
            Assert.Equal(reference.Apply(move), scene.Apply(move));
            Assert.Equal(JsonSerializer.Serialize(reference.State), JsonSerializer.Serialize(scene.State));
        }
        Assert.Equal(initialZ - 72, scene.State.Z);
        Assert.Equal(20, scene.State.NativeCarRuntime!.Speed);
        Assert.Equal(percent, scene.PlayerMovementSpeedPercent);

        if (entryDelay)
        {
            CompleteEntryDelay(scene);
            CompleteEntryDelay(reference);
        }
        session.SetWorldState(scene.State with { EventUi = scene.State.EventUi!.WithScriptMode(0), NativeCarRuntime = scene.State.NativeCarRuntime! with { Speed = 32 } });
        referenceSession.SetWorldState(reference.State with { EventUi = reference.State.EventUi!.WithScriptMode(0), NativeCarRuntime = reference.State.NativeCarRuntime! with { Speed = 32 } });
        int beforeZ = scene.State.Z;
        PlayerAction controlled = new(PlayerActionKind.Move, Vertical: -1);
        Assert.Equal(reference.Apply(controlled), scene.Apply(controlled));
        Assert.Equal(34, scene.State.NativeCarRuntime!.Speed);
        Assert.Equal(beforeZ - 34 * percent / 100, scene.State.Z);
        Assert.Equal(beforeZ - 34, reference.State.Z);
    }

    private static void CompleteEntryDelay(WorldMapScene scene)
    {
        while (NativeWorldEntryControl.DelayAfterCallback(scene.State.Frame + 1, 0) > 0)
        {
            Assert.Equal(1, scene.Apply(new(PlayerActionKind.Wait)).FramesAdvanced);
        }
    }

    private static GameSession CreateSession(string directory)
    {
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        using GameAssets assets = new(directory);
        session.InitializeNewGameCharacterVitals(assets.ReadNewGameCharacterVitals());
        session.InitializeNewGameEconomy(assets.ReadNewGameEconomy());
        session.InitializeNewGameSalaryTable(assets.ReadNativeSalaryTable());
        session.InitializeNewGameInputConfiguration(assets.ReadNewGameInputConfiguration());
        return session;
    }

    private static string GameDirectory()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original world resources are required.");
        return directory;
    }
}
