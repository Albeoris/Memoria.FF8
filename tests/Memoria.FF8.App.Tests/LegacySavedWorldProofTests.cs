using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class LegacySavedWorldProofTests
{
    [Theory]
    [InlineData("player-garden-join137-boundary-preboundary-checkpoint.json", NativeSavedWorldState.LegacyCheckpoint265Sha256, 265)]
    [InlineData("player-garden-push137-boundary-preboundary-checkpoint.json", NativeSavedWorldState.LegacyCheckpoint300Sha256, 300)]
    public void ExactLegacyUpgradePreservesEveryPreexistingStateValue(string file, string sha256, long frame)
    {
        string source = Path.Combine(Repository(), "../fixtures", file);
        byte[] bytes = File.ReadAllBytes(source);
        Assert.Equal(new LegacySavedWorldCheckpointProof(sha256, 137, frame), LegacySavedWorldProof.Resolve(bytes));
        GameState original = GameSession.Load(source);
        GameState upgraded = LegacySavedWorldProof.Upgrade(bytes);
        Assert.Equal(NativeSavedWorldState.CreateVerifiedLegacyNewGame(sha256), upgraded.SavedWorld);
        JsonNode before = JsonNode.Parse(JsonSerializer.Serialize(original))!;
        JsonNode after = JsonNode.Parse(JsonSerializer.Serialize(upgraded))!;
        before.AsObject().Remove("SavedWorld");
        after.AsObject().Remove("SavedWorld");
        Assert.True(JsonNode.DeepEquals(before, after));
        Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize(upgraded with { SavedWorld = null }));
        byte[] modified = [.. bytes, (byte)' '];
        Assert.Null(LegacySavedWorldProof.Resolve(modified));
        Assert.Throws<NotSupportedException>(() => LegacySavedWorldProof.Upgrade(modified));
    }

    [Fact]
    public void UnknownWorldMetadataStopsBeforeAssetsOrSceneMutation()
    {
        var session = new GameSession(new GameState { Scene = SceneKind.WorldMap, WorldArrival = new WorldTransition(0, 0, 0) });
        string before = JsonSerializer.Serialize(session.State);
        Assert.Throws<NotSupportedException>(() => new WorldMapScene(session, "missing corpus"));
        Assert.Equal(before, JsonSerializer.Serialize(session.State));
    }

    private static string Repository()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "docs", "recreate-checkpoint.md")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ?? throw new DirectoryNotFoundException("Research repository not found.");
    }
}
