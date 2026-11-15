using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class InstalledWorldEntryReadinessTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void SavedProgressAndOriginalGatewayRetainRailPhaseAcrossEntryAndReload(bool gameplaySave, bool attemptEarlyDraw)
    {
        string directory = TestResources.GameDirectory;
        string source = TestResources.FixturePath(gameplaySave ? "world-entry-readiness-gameplay" : "world-entry-readiness-snapshot");
        using GameAssets assets = new(directory);
        GameState departure = gameplaySave ? GameplaySaveSessionLoader.Load(source, directory, sharedAssets: assets) : GameSession.Load(source);
        Assert.Equal(SceneKind.Field, departure.Scene);
        // Isolated entry component: use the supplied progress owners and the original gateway packet,
        // without replaying field travel or claiming that the source save is already at this gateway.
        FieldAssets field = assets.LoadField(899);
        FieldGateway gateway = Assert.Single(field.Information!.Gateways, item => item.Defined && item.DestinationField < 72);
        WorldTransition packet = new(checked((short)gateway.DestinationField), gateway.DestinationTriangle, gateway.Direction);
        string original = JsonSerializer.Serialize(departure);
        using PreparedWorldEntry entry = WorldEntryPreflight.Prepare(departure, directory, packet, nativeSourceFieldName: field.Name);
        Assert.Equal(original, JsonSerializer.Serialize(departure));
        Assert.Equal(0, entry.InitialState.Frame);
        Assert.NotNull(entry.InitialState.NativeGardenRuntime!.BackgroundRails);
        Assert.Equal(0, entry.InitialState.NativeGardenRuntime.BackgroundRails.Callback);
        Assert.True(entry.InitialState.NativeGardenRuntime.BackgroundRails.HasBanks);
        GameSession session = new(departure);
        session.CommitPreparedWorldEntry(packet, entry.InitialState, session.WorldControls!, entry.ValidateAdoption, entry.InitialSavedWorld, entry.InitialEventMemory);
        using WorldMapScene scene = entry.Adopt(session);
        VerifyDeferredReloadAndContinuation(session, scene, directory, attemptEarlyDraw);
    }

    private static void VerifyDeferredReloadAndContinuation(GameSession session, WorldMapScene scene, string directory, bool attemptEarlyDraw)
    {
        GameSession restored = new(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!);
        using WorldMapScene replay = new(restored, directory);
        Assert.True(scene.IsPresentationDeferred);
        Assert.True(replay.IsPresentationDeferred);
        string initial = JsonSerializer.Serialize(session.State);
        for (int draw = 0; draw < (attemptEarlyDraw ? 2 : 0); draw++)
        {
            Assert.Throws<NativeWorldPresentationDeferredException>(() => scene.Render(160, 120));
            Assert.Throws<NativeWorldPresentationDeferredException>(() => replay.Render(160, 120));
        }
        Assert.Equal(initial, JsonSerializer.Serialize(session.State));
        for (int callback = 1; callback <= 3; callback++)
        {
            PlayerAction action = new(PlayerActionKind.Wait);
            Assert.True(scene.Apply(action).Accepted);
            Assert.True(replay.Apply(action).Accepted);
            Assert.Equal(callback, scene.State.Frame);
            Assert.False(scene.IsPresentationDeferred);
            Assert.False(replay.IsPresentationDeferred);
            Assert.Equal(JsonSerializer.Serialize(session.State), JsonSerializer.Serialize(restored.State));
            byte[] pixels = scene.Render(160, 120, interpolation: 0.5f);
            Assert.Equal(pixels, scene.Render(160, 120, interpolation: 0.5f));
            Assert.Equal(pixels, replay.Render(160, 120, interpolation: 0.5f));
            GameSession committed = new(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!);
            using WorldMapScene reloaded = new(committed, directory);
            Assert.False(reloaded.IsPresentationDeferred);
            Assert.Equal(scene.Render(160, 120), reloaded.Render(160, 120));
        }
    }
}
