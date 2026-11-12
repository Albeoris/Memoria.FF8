using Memoria.FF7.Runtime;
using System.Text;

namespace Memoria.FF8.Mods.Tests;

public sealed class Ff7EnemyExtensionModsTests
{
    [Fact]
    public void ShippedContentPreparesAllIdentitiesWithoutNativeReplacementFields()
    {
        byte[] payload = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Ff7EnemyIntegration.json"));
        ModFile file = new("Memoria/FF7EnemyIntegration", ModFormat.Memoria, Ff7EnemyExtensionMods.PayloadDirectory + "enemies.json", 1, () => payload);
        Assert.True(Ff7EnemyExtensionMods.TryPrepare(new(1, [], [file]), out Ff7EnemyExtensionCatalog catalog, out var diagnostics));
        Assert.Empty(diagnostics);
        Assert.Equal(352, catalog.Count);
        Assert.StartsWith("A small, furious ember", catalog.Find(105)!.ScanDescription);
        Assert.Null(catalog.Find(0)!.Growth);
        Assert.NotEmpty(catalog.Find(105)!.DrawTiers!);
    }

    [Fact]
    public void Ff8PayloadsAreNotInterpretedAsFf7Extensions()
    {
        ModFile unrelated = new("Memoria/Other", ModFormat.Memoria, "gameplay/encounters/encounter.json", 1, () => "not FF7 JSON"u8.ToArray());
        Assert.True(Ff7EnemyExtensionMods.TryPrepare(new(1, [], [unrelated]), out var catalog, out var diagnostics));
        Assert.Equal(0, catalog.Count);
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void CompetingPropertyWritersRejectTheWholeCandidateWithBothOwners()
    {
        ModFile Make(string id) => new(id, ModFormat.Memoria, Ff7EnemyExtensionMods.PayloadDirectory + "scan.json", 1, () => Encoding.UTF8.GetBytes($$"""{"schemaVersion":1,"documentId":"{{id}}","enemies":[{"enemyId":105,"scanDescription":"A restless ember."}]}"""));
        Assert.False(Ff7EnemyExtensionMods.TryPrepare(new(1, [], [Make("first"), Make("second")]), out var catalog, out var diagnostics));
        Assert.Equal(0, catalog.Count);
        string message = Assert.Single(diagnostics).Message;
        Assert.Contains("first", message);
        Assert.Contains("second", message);
        Assert.Contains("105", message);
    }
}
