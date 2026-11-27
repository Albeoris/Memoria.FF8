using System.Text.Json;
using Memoria.FF8.Assets;
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

    [Theory]
    [InlineData(750, 33, 10927, -81010, -885)]
    [InlineData(3410, 34, 11332, -63659, -632)]
    public void OriginalForestPacketLoadsRiderShowsTutorialAndRestoresMovement(ushort story, byte spawn, int x, int z, int height)
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed world resources are required.");
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        NativeSavedWorldPose parkedShip = new(10414, -62296, -496, 3577);
        using (GameWorld initialization = new(session, directory))
        {
            session.SetWorldEntryMemory(session.State.WorldEntryMemory!.WriteBytes(256, 2, story).WriteBytes(265, 1, 1));
            session.SetSavedWorldState(session.State.SavedWorld! with { RagnarokPose2 = parkedShip });
            NativePocketStationState pocket = session.State.PocketStation!;
            session.SetPocketStation(pocket with { Raw = pocket.Raw.SetItem(0, 1) });
        }

        WorldTransition packet = new(spawn, 49, spawn);
        using PreparedWorldEntry ticket = WorldEntryPreflight.Prepare(session.State, directory, packet);
        session.CommitPreparedWorldEntry(packet, ticket.InitialState, session.WorldControls!, ticket.ValidateAdoption, ticket.InitialSavedWorld);
        using WorldMapScene scene = ticket.Adopt(session);
        Assert.Equal((x, z, height), (scene.State.X, scene.State.Z, scene.State.NativeHeight));
        Assert.NotNull(scene.State.NativeChocoboRuntime);
        Assert.Null(scene.State.NativeFootRuntime);
        NativeGardenWorldAllocation allocation = scene.State.NativeChocoboRuntime.Objects.Allocation;
        bool hasRagnarok = story == 3410;
        Assert.Equal(hasRagnarok ? new byte[] { 0, 2, 3, 1, 64, 65, 90, 91, 94 } : [0, 2, 3, 64, 65, 94], allocation.Objects.Select(item => item.ShapeId));
        if (hasRagnarok)
        {
            Assert.Equal(new NativeGardenWorldObject(1, 0, new(parkedShip.X, parkedShip.Height, -parkedShip.RawZ), 0, (short)parkedShip.Yaw, 0), allocation.Objects[3]);
            Assert.Equal(new NativeGardenWorldObject(90, 0, new(86016, -1200, -26624), 0, 0, 0), allocation.Objects[6]);
            Assert.Equal(allocation.Objects[6] with { ShapeId = 91 }, allocation.Objects[7]);
        }

        Assert.True(scene.Apply(new(PlayerActionKind.Wait, Frames: 24)).Accepted);
        Assert.Equal(51, scene.CurrentDialogue?.MessageId);
        Assert.True(scene.Apply(new(PlayerActionKind.Confirm)).Accepted);
        Assert.Null(scene.CurrentDialogue);
        PlayerAction ride = new(PlayerActionKind.Move, Horizontal: 1, Frames: 12);
        Assert.True(scene.Apply(ride).Accepted);
        Assert.NotEqual((x, z), (scene.State.X, scene.State.Z));
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

    [Theory]
    [InlineData(740)]
    [InlineData(900)]
    [InlineData(1350)]
    [InlineData(1550)]
    [InlineData(3100)]
    [InlineData(3900)]
    [InlineData(11000)]
    [InlineData(12000)]
    [InlineData(13000)]
    [InlineData(20000)]
    public void EveryNativeMountedAllocationCanEnterFromEveryForest(ushort story)
    {
        string directory = TestResources.GameDirectory;
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        using (GameWorld initialization = new(session, directory))
        {
            session.SetWorldEntryMemory(session.State.WorldEntryMemory!.WriteBytes(256, 2, story).WriteBytes(266, 1, story >= 3900 ? 4 : 0));
            NativePocketStationState pocket = session.State.PocketStation!;
            session.SetPocketStation(pocket with { Raw = pocket.Raw.SetItem(0, 1) });
        }
        using WorldAssets assets = new(directory);
        NativeWorldAllocationClause clause = assets.ReadWorldAllocationClause(story);
        byte[] originalShapes = Enumerable.Range(4, clause.Words.Length / 2 - 5).Select(index => unchecked((byte)clause.Words[2 * index + 1])).ToArray();

        // All seven forest scripts issue this packet with their respective spawn.
        for (byte spawn = 33; spawn <= 39; spawn++)
        {
            WorldTransition packet = new(spawn, 49, spawn);
            using PreparedWorldEntry ticket = WorldEntryPreflight.Prepare(session.State, directory, packet);
            NativeGardenWorldAllocation allocation = ticket.InitialState.NativeChocoboRuntime!.Objects.Allocation;
            Assert.Equal(originalShapes, allocation.Objects.Take(originalShapes.Length).Select(item => item.ShapeId));
            Assert.Equal(Array.IndexOf(originalShapes, (byte)64), allocation.Garden64Index);
            Assert.Equal(Array.IndexOf(originalShapes, (byte)65), allocation.Garden65Index);
            Assert.Equal(Array.IndexOf(originalShapes, (byte)1), allocation.RagnarokIndex);
            Assert.Equal(new NativeWorldPathPosition(assets.Spawns[spawn].X, assets.Spawns[spawn].NativeHeight, -assets.Spawns[spawn].Z), allocation.Objects[allocation.MountIndex].Position);

            GameSession riding = new(session.State);
            riding.CommitPreparedWorldEntry(packet, ticket.InitialState, riding.WorldControls!, ticket.ValidateAdoption, ticket.InitialSavedWorld);
            using WorldMapScene scene = ticket.Adopt(riding);
            Assert.True(scene.Apply(new(PlayerActionKind.Wait, Frames: 24)).Accepted);
            if (scene.CurrentDialogue is not null)
            {
                Assert.True(scene.Apply(new(PlayerActionKind.Confirm)).Accepted);
            }
            Assert.Equal(320 * 224 * 4, scene.Render(320, 224).Length);

            if (spawn == 34)
            {
                Assert.True(scene.Apply(new(PlayerActionKind.Move, Horizontal: 1, Frames: 12)).Accepted);
                GameSession restored = new(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(riding.State))!);
                using WorldMapScene resumed = new(restored, directory);
                Assert.True(resumed.Apply(new(PlayerActionKind.Wait)).Accepted);
            }
        }
    }

    [Theory]
    [InlineData(750)]
    [InlineData(3900)]
    public void BoundConfirmDismountsOnPermittedGroundAndRestoresTheEscapeInProgress(ushort story)
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed world resources are required.");
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        using (GameWorld initialization = new(session, directory))
        {
            session.SetWorldEntryMemory(session.State.WorldEntryMemory!.WriteBytes(256, 2, story).WriteBytes(265, 1, 1));
            NativePocketStationState pocket = session.State.PocketStation!;
            session.SetPocketStation(pocket with { Raw = pocket.Raw.SetItem(0, 1) });
        }
        WorldState pose = new() { X = 17800, Z = -86100, NativeHeight = -414, BodyYaw = 2180, CameraYaw = 2180 };
        NativeSavedWorldFootReturn receipt = NativeSavedWorldFootReturn.Create(pose, 287, story, 0, 0, 0, 49);
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
        Assert.Equal(session.State.SavedWorld!.GardenPose3, GameSession.PrepareGardenParkingForFootReturn(session.State.SavedWorld, scene.State).GardenPose3);
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
