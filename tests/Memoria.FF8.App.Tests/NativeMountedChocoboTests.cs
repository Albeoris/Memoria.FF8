using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8.App.Tests;

public sealed class NativeMountedChocoboTests
{
    [Fact]
    public void DismountMusicStopsTheMountTrackAndResumesAfterTheHandoff()
    {
        string directory = TestResources.GameDirectory;
        string source = TestResources.FixturePath("App.Tests.NativeMountedChocoboTests.Music");
        GameSession session = new(PlayerStateLoader.Load(source, directory));
        using GameWorld world = new(session, directory);
        world.EnsureField();
        Assert.Equal(81, session.GlobalSoundProcess!.MusicPlayback!.CurrentMusicId0);
        byte bank = world.Events.Scene.MusicProcess!.Bank201!.Value;
        List<NativeWorldMusicLoad> commands = [];
        world.Events.AudioCommandExecuted += command =>
        {
            if (command is NativeWorldMusicLoad music)
            {
                commands.Add(music);
            }
        };

        Assert.Equal(24, world.Apply(new(PlayerActionKind.Wait, Frames: 24)).FramesAdvanced);
        Assert.Empty(commands);
        Assert.True(world.Apply(new(PlayerActionKind.Confirm)).Accepted);
        Assert.Equal(0, session.GlobalSoundProcess!.MusicPlayback!.Gain0);
        Assert.Equal(81, session.GlobalSoundProcess.MusicPlayback.CurrentMusicId0);
        Assert.Equal(bank, world.Events.Scene.MusicProcess.Bank201);
        Assert.Single(commands);

        Assert.Equal(2, world.Apply(new(PlayerActionKind.Wait, Frames: 2)).FramesAdvanced);
        Assert.Equal(NativeMusicPlaybackMode.Stopped, session.GlobalSoundProcess!.MusicPlayback!.Mode0);
        Assert.Equal((byte)(bank ^ 1), world.Events.Scene.MusicProcess.Bank201);
        Assert.Equal(2, commands.Count);

        GameSession restored = new(session.PrepareSnapshot());
        using GameWorld resumed = new(restored, directory);
        resumed.EnsureField();
        PlayerAction finish = new(PlayerActionKind.Wait, Frames: 61);
        Assert.Equal(61, world.Apply(finish).FramesAdvanced);
        Assert.Equal(61, resumed.Apply(finish).FramesAdvanced);
        Assert.NotNull(session.State.World!.NativeFootRuntime);
        Assert.Equal(2, commands.Count);

        Assert.True(world.Apply(new(PlayerActionKind.Wait)).Accepted);
        Assert.True(resumed.Apply(new(PlayerActionKind.Wait)).Accepted);
        NativeWorldMusicLoad completed = commands[2];
        Assert.Equal(3, commands.Count);
        Assert.Equal(0, completed.Resource!.File!.Index);
        Assert.Equal((byte)41, world.Events.Scene.MusicChannel108!.MusicId);
        Assert.Equal((byte)(bank ^ 1), world.Events.Scene.MusicProcess.Bank201);
        Assert.Equal(41, session.GlobalSoundProcess!.MusicPlayback!.CurrentMusicId0);
        Assert.Equal(127, session.GlobalSoundProcess.MusicPlayback.Gain0);
        Assert.Equal(session.GlobalSoundProcess.MusicPlayback, restored.GlobalSoundProcess!.MusicPlayback);
        Assert.Equal(completed, resumed.Events.Scene.WorldMusicLoad);
        Assert.True(world.Apply(new(PlayerActionKind.Wait)).Accepted);
        Assert.Equal(3, commands.Count);
    }

    [Fact]
    public void OriginalForestPacketLoadsRiderShowsTutorialAndRestoresMovement()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed world resources are required.");
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        using (GameWorld initialization = new(session, directory))
        {
            session.SetWorldEntryMemory(session.State.WorldEntryMemory!.WriteBytes(256, 2, 750).WriteBytes(265, 1, 1));
            NativePocketStationState pocket = session.State.PocketStation!;
            session.SetPocketStation(pocket with { Raw = pocket.Raw.SetItem(0, 1) });
        }
        WorldTransition packet = new(33, 49, 33);
        using PreparedWorldEntry ticket = WorldEntryPreflight.Prepare(session.State, directory, packet);
        session.CommitPreparedWorldEntry(packet, ticket.InitialState, session.WorldControls!, ticket.ValidateAdoption, ticket.InitialSavedWorld);
        using WorldMapScene scene = ticket.Adopt(session);
        Assert.Equal((10927, -81010, -885), (scene.State.X, scene.State.Z, scene.State.NativeHeight));
        Assert.NotNull(scene.State.NativeChocoboRuntime);
        Assert.Null(scene.State.NativeFootRuntime);

        Assert.True(scene.Apply(new(PlayerActionKind.Wait, Frames: 24)).Accepted);
        Assert.Equal(51, scene.CurrentDialogue?.MessageId);
        Assert.True(scene.Apply(new(PlayerActionKind.Confirm)).Accepted);
        Assert.Null(scene.CurrentDialogue);
        PlayerAction ride = new(PlayerActionKind.Move, Horizontal: 1, Frames: 12);
        Assert.True(scene.Apply(ride).Accepted);
        Assert.NotEqual((10927, -81010), (scene.State.X, scene.State.Z));
        Assert.Equal(6, scene.State.AnimationId);
        Assert.Equal(320 * 224 * 4, scene.Render(320, 224).Length);
        Assert.True(scene.Apply(new(PlayerActionKind.Wait, WorldButtons: 2)).Accepted);
        Assert.Equal((byte)1, scene.State.NativeChocoboRuntime.Objects.AnimationVariant);
        Assert.Equal((byte)49, session.State.SavedWorld!.FootReturn!.EntityType105);

        GameSession restored = new(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!);
        using WorldMapScene resumed = new(restored, directory);
        Assert.True(scene.Apply(ride).Accepted);
        Assert.True(resumed.Apply(ride).Accepted);
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(scene.State), JsonSerializer.SerializeToElement(resumed.State)));
    }

    [Fact]
    public void BoundConfirmDismountsOnPermittedGroundAndRestoresTheEscapeInProgress()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed world resources are required.");
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        using (GameWorld initialization = new(session, directory))
        {
            session.SetWorldEntryMemory(session.State.WorldEntryMemory!.WriteBytes(256, 2, 750).WriteBytes(265, 1, 1));
            NativePocketStationState pocket = session.State.PocketStation!;
            session.SetPocketStation(pocket with { Raw = pocket.Raw.SetItem(0, 1) });
        }
        WorldState pose = new() { X = 17800, Z = -86100, NativeHeight = -414, BodyYaw = 2180, CameraYaw = 2180 };
        NativeSavedWorldFootReturn receipt = NativeSavedWorldFootReturn.Create(pose, 287, 750, 0, 0, 0, 49);
        session.SetSavedWorldState(session.State.SavedWorld!.WithFootReturn(receipt));
        WorldTransition packet = new(62, 49, -1);
        using PreparedWorldEntry ticket = WorldEntryPreflight.Prepare(session.State, directory, packet);
        session.CommitPreparedWorldEntry(packet, ticket.InitialState, session.WorldControls!, ticket.ValidateAdoption, ticket.InitialSavedWorld);
        using WorldMapScene scene = ticket.Adopt(session);
        Assert.Equal(24, scene.Apply(new(PlayerActionKind.Wait, Frames: 24)).FramesAdvanced);
        if (scene.CurrentDialogue is not null)
        {
            Assert.True(scene.Apply(new(PlayerActionKind.Confirm)).Accepted);
            Assert.Null(scene.CurrentDialogue);
            Assert.True(scene.Apply(new(PlayerActionKind.Wait)).Accepted);
        }

        KeyboardInput keyboard = new();
        InputFrame input = keyboard.Read(new KeyboardState(Keys.Enter), true);
        Assert.True(input.Confirm);
        PlayerAction action = ControllerGameplayInput.CreateWorldAction(input, session.State, keyboard.HeldControlButtons);
        Assert.Equal(0x40, action.WorldButtons);
        Assert.True(scene.Apply(action).Accepted);
        Assert.Equal(1, scene.State.ChocoboDismount!.Counter);
        Assert.Equal(7, scene.State.EventUi!.ScriptMode);

        Assert.Equal(40, scene.Apply(new(PlayerActionKind.Wait, Frames: 40)).FramesAdvanced);
        Assert.Equal(NativeChocoboEscapePhase.Run, scene.State.ChocoboDismount!.Phase);
        Assert.NotEmpty(scene.State.ChocoboDismount.Route!.BestPath);
        GameSession restored = new(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!);
        using WorldMapScene resumed = new(restored, directory);

        PlayerAction finish = new(PlayerActionKind.Wait, Frames: 23);
        Assert.Equal(23, scene.Apply(finish).FramesAdvanced);
        Assert.Equal(23, resumed.Apply(finish).FramesAdvanced);
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(scene.State), JsonSerializer.SerializeToElement(resumed.State)));
        Assert.Null(scene.State.NativeChocoboRuntime);
        Assert.Equal(0, scene.State.NativeFootRuntime!.EntityType);
        Assert.Equal(0, scene.State.EventUi!.ScriptMode);
        Assert.Equal(320 * 224 * 4, scene.Render(320, 224).Length);

        GameSession restoredFoot = new(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!);
        using WorldMapScene resumedFoot = new(restoredFoot, directory);
        int x = scene.State.X;
        PlayerAction walk = new(PlayerActionKind.Move, Horizontal: 1, Frames: 12);
        Assert.True(scene.Apply(walk).Accepted);
        Assert.True(resumedFoot.Apply(walk).Accepted);
        Assert.NotEqual(x, scene.State.X);
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(scene.State), JsonSerializer.SerializeToElement(resumedFoot.State)));
        Assert.True(scene.Apply(new(PlayerActionKind.Wait, Frames: 140)).Accepted);
        Assert.Null(scene.State.ChocoboDismount);
    }
}
