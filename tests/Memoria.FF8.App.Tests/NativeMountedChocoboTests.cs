using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8.App.Tests;

public sealed class NativeMountedChocoboTests
{
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
