using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class LegacyWorldControlProofTests
{
    [Fact]
    public void ExactFirstFloorUpgradeAddsOnlyTheSeparatelyProvedControlFlags()
    {
        DirectoryInfo? repository = new(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "docs", "recreate-checkpoint.md")))
        {
            repository = repository.Parent;
        }
        string source = Path.Combine(repository!.FullName, "../fixtures/external-fixture.json");
        byte[] bytes = File.ReadAllBytes(source);
        GameState original = GameSession.Load(source);
        GameState upgraded = LegacyWorldControlProof.Upgrade(bytes);
        Assert.Equal(NativeWorldControlState.CreateVerifiedLegacyNewGame(NativeWorldControlState.LegacyCheckpoint172PreinitSha256), upgraded.WorldControls);
        Assert.Equal(original.SavedWorld, upgraded.SavedWorld);
        Assert.True(upgraded.WorldControls!.SourceDisabled);
        Assert.Equal((byte)0, upgraded.WorldControls.Menu209);
        JsonNode before = JsonNode.Parse(JsonSerializer.Serialize(original))!;
        JsonNode after = JsonNode.Parse(JsonSerializer.Serialize(upgraded))!;
        before.AsObject().Remove("WorldControls");
        after.AsObject().Remove("WorldControls");
        Assert.True(JsonNode.DeepEquals(before, after));
        Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize(upgraded with { WorldControls = null }));
        Assert.Throws<NotSupportedException>(() => LegacyWorldControlProof.Upgrade([.. bytes, (byte)' ']));
        Assert.Throws<InvalidDataException>(() => NativeWorldControlState.CreateVerifiedLegacyNewGame(NativeSavedWorldState.LegacyCheckpoint300Sha256));
    }
}
