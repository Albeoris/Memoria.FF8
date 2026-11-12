using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

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
}
