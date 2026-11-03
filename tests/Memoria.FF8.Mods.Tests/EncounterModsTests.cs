using System.Text;

namespace Memoria.FF8.Mods.Tests;

public sealed class EncounterModsTests
{
    [Fact]
    public void EqualPayloadPathsRetainBothContributionsAndCatalogOrder()
    {
        ModCatalogSnapshot catalog = new(1, [], [File("Memoria/Z", "first"), File("Memoria/A", "second")]);
        Assert.True(EncounterMods.TryPrepare(catalog, out EncounterMods mods, out IReadOnlyList<ModDiagnostic> diagnostics));
        Assert.Empty(diagnostics);
        Assert.Equal(new[] { "Memoria/Z", "Memoria/A" }, mods.Layers.Select(layer => layer.PackageId));
        Assert.Equal(new[] { "first", "second" }, mods.Layers.Select(layer => layer.Document.DocumentId));
    }

    [Fact]
    public void DuplicateDocumentIdentityRejectsWholeCandidateWithOwner()
    {
        ModCatalogSnapshot catalog = new(1, [], [File("Memoria/A", "duplicate"), File("Memoria/B", "duplicate")]);
        Assert.False(EncounterMods.TryPrepare(catalog, out EncounterMods mods, out IReadOnlyList<ModDiagnostic> diagnostics));
        Assert.Empty(mods.Layers);
        ModDiagnostic diagnostic = Assert.Single(diagnostics);
        Assert.Equal("Memoria/B", diagnostic.PackageId);
        Assert.Equal("gameplay/encounters/shared.json", diagnostic.RelativePath);
        Assert.Contains("duplicate", diagnostic.Message);
    }

    [Fact]
    public void UnrelatedJsonAndOtherFormatsAreNotEncounterDocuments()
    {
        ModFile[] files = [new("Memoria/A", ModFormat.Memoria, "gameplay/settings.json", 1, () => "invalid"u8.ToArray()), new("Junction/B", ModFormat.Junction, "gameplay/encounters/shared.json", 1, () => "invalid"u8.ToArray())];
        Assert.True(EncounterMods.TryPrepare(new(1, [], files), out EncounterMods mods, out IReadOnlyList<ModDiagnostic> diagnostics));
        Assert.Empty(mods.Layers);
        Assert.Empty(diagnostics);
    }

    private static ModFile File(string packageId, string documentId)
    {
        string json = $$"""
            {
              "schemaVersion": 1,
              "documentId": "{{documentId}}",
              "encounter": "fixture.encounter",
              "atomic": true,
              "operations": [
                {
                  "id": "hp",
                  "kind": "SetActorProperty",
                  "actor": "original",
                  "property": "stats.maximumHpMultiplier",
                  "expectedValue": 1.0,
                  "value": 1.25
                }
              ]
            }
            """;
        return new(packageId, ModFormat.Memoria, "gameplay/encounters/shared.json", 1, () => Encoding.UTF8.GetBytes(json));
    }
}
