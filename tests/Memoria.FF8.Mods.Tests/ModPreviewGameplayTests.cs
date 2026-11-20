namespace Memoria.FF8.Mods.Tests;

public sealed class ModPreviewGameplayTests
{
    [Fact]
    public async Task DisabledNativeStatusComparesItsRulesWithoutAnInstallation()
    {
        await using PreviewFixture fixture = new();
        fixture.Write("mod.json", """{"id":"preview-status","name":"Status preview","package":"Memoria","description":{"en":"Status preview fixture."}}""");
        fixture.Write("statuses/vit0.json", """
            {
              "id": "Vit0",
              "statModifiers": {
                "Vitality": { "operation": "ReducePercent", "value": 45 },
                "Strength": { "operation": "ReducePercent", "value": 15 }
              }
            }
            """);
        ModPackage package = new("Memoria/preview-status", "Status preview", ModFormat.Memoria, false, false, ModEffectiveStatus.Disabled, fixture.PackagePath)
        {
            Descriptor = NativeModDescriptor.Read(Path.Combine(fixture.PackagePath, "mod.json"))
        };
        ModPreviewDocument document = await fixture.Open(package);
        ModPreviewEntry entry = Assert.Single(document.Entries);
        Assert.Equal("Statuses", entry.Group);

        ModPreviewComparison result = await fixture.Compare(document, entry);
        Assert.Contains("Vitality  −100%", result.Before.Text);
        Assert.Contains("Vitality  −45%", result.After.Text);
        Assert.Contains("Strength  −0%", result.Before.Text);
        Assert.Contains("Strength  −15%", result.After.Text);
    }

    [Fact]
    public async Task BundledLevelBonusPackageUsesRuntimeScalingForExamples()
    {
        await using PreviewFixture fixture = new();
        ModPackage package = new("Memoria/Level-Based Stat Bonuses", "Level-Based Stat Bonuses", ModFormat.Memoria, false, false, ModEffectiveStatus.Disabled, fixture.PackagePath) { IsBundled = true };
        ModPreviewDocument document = await fixture.Open(package);
        ModPreviewEntry entry = Assert.Single(document.Entries);
        ModPreviewTreeNode category = Assert.Single(document.Tree.Branches);
        Assert.Equal("Gameplay (1)", category.Label);
        ModPreviewTreeNode group = Assert.Single(category.Children);
        Assert.Equal("Character growth (1)", group.Label);
        Assert.Same(entry, Assert.Single(group.Children).Entry);

        ModPreviewComparison result = await fixture.Compare(document, entry);
        Assert.Contains("permanently", result.Before.Text);
        Assert.Contains("Lv 30  ·  HP +870", result.After.Text);
        Assert.Contains("Lv 100  ·  HP +2970", result.After.Text);
    }

    [Fact]
    public async Task Ff7EnemyExtensionsUseTypedGrowthAndDrawWithoutAnInstallation()
    {
        await using PreviewFixture fixture = new();
        fixture.Write("ff7/gameplay/enemy-extensions/fixture.json", """
            {
              "schemaVersion": 1,
              "documentId": "preview.fixture",
              "enemies": [
                {
                  "enemyId": 12,
                  "scanDescription": "A fixture enemy.",
                  "growth": { "hp": [1000000, 1000000, 0, 0] },
                  "drawTiers": [{ "minimumDelta": 5, "slots": [{ "magicId": 1, "resistance": 15 }] }]
                }
              ]
            }
            """);
        ModPackage package = new("Memoria/fixture", "Fixture", ModFormat.Memoria, false, false, ModEffectiveStatus.Disabled, fixture.PackagePath);
        ModPreviewDocument document = await fixture.Open(package);
        ModPreviewEntry entry = Assert.Single(document.Entries);
        Assert.Equal("FFVII enemies", entry.Group);

        ModPreviewComparison result = await fixture.Compare(document, entry);
        Assert.Contains("Enemy 12", result.Before.Text);
        Assert.Contains("HP  ·  200% of base", result.After.Text);
        Assert.Contains("Draw at +5 levels  ·  Magic 1 (15 resistance)", result.After.Text);
    }

    [Fact]
    public async Task FieldPatchListsActorAndMovementAlongsideItsScene()
    {
        await using PreviewFixture fixture = new();
        fixture.Write("fields/test01.json", """
            {
              "schemaVersion": 1,
              "fieldId": 51,
              "fieldName": "test01",
              "backgrounds": [{ "layer": 0, "moduleId": 5, "state": 0, "visible": false }],
              "actors": [{ "objectId": 8, "model": { "fieldId": 51, "modelId": 9 }, "animationId": 2, "when": { "moduleId": 5, "state": 1 } }],
              "movement": { "rotationDegrees": -90, "cameraIndex": 0 }
            }
            """);
        ModPackage package = new("Memoria/fixture", "Fixture", ModFormat.Memoria, false, false, ModEffectiveStatus.Disabled, fixture.PackagePath);
        ModPreviewDocument document = await fixture.Open(package);

        Assert.Contains(document.Entries, entry => entry.Group == "test01" && entry.Name == "Object 8 model and animation");
        Assert.Contains(document.Entries, entry => entry.Group == "test01" && entry.Name == "Movement direction");
        Assert.Contains(document.Entries, entry => entry.Group == "test01" && entry.Name == "Compare entire field");
        Assert.Contains(document.Entries, entry => entry.Group == "test01" && entry.Name == "Module 5, state 0");
        Assert.Contains(document.Entries, entry => entry.Group == "test01" && entry.Name == "Module 5, state 1");
    }

    private sealed class PreviewFixture : IAsyncDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), $"mod-preview-gameplay-{Guid.NewGuid():N}");
        private readonly ModRuntime mods;
        private readonly ModLibraryService library;

        public PreviewFixture()
        {
            Directory.CreateDirectory(PackagePath);
            mods = new(root, watch: false);
            ModPackageInstaller installer = new(Path.Combine(root, "state"), Path.Combine(root, "Mods"), "Classic/en/test");
            library = new(Path.Combine(root, "cache"), installer, mods, "Classic/en/test", false);
        }

        public string PackagePath => Path.Combine(root, "package");

        public void Write(string relativePath, string content)
        {
            string path = Path.Combine(PackagePath, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }

        public Task<ModPreviewDocument> Open(ModPackage package) => library.OpenPreviewAsync(null, package, TestContext.Current.CancellationToken);

        public Task<ModPreviewComparison> Compare(ModPreviewDocument document, ModPreviewEntry entry) => document.CompareAsync(entry, new(root), TestContext.Current.CancellationToken);

        public async ValueTask DisposeAsync()
        {
            await library.StopAsync();
            library.Dispose();
            mods.Dispose();
            Directory.Delete(root, true);
        }
    }
}
