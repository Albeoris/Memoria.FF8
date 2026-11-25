using System.Text.Json;
using Memoria.FF8.Assets.Localization;
using Memoria.FF8.Mods.Localization;
using Memoria.FF8.Resources;
using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8.Mods.Tests;

public sealed class LocalizationPlayerExportTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "memoria-player-export-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void PlayerExportKeepsSplitTargetsAndRebindsBothNativeEditions()
    {
        CancellationToken cancellation = TestContext.Current.CancellationToken;
        string input = Path.Combine(root, "input");
        string output = Path.Combine(root, "player");
        LocalizationProject project = new()
        {
            Manifest = new()
            {
                SchemaVersion = 2, TargetLanguage = "ru", SourceEdition = "Remastered", MappingVersion = FieldCorrespondenceCatalog.Version,
                CompatibleVariants = ["Remastered:en", "Classic:en"], ReferenceLanguages = new Dictionary<string, string> { ["Reference [fr]"] = "fr" }
            },
            Entries =
            [
                new()
                {
                    Category = "Magic", File = "magic", Key = "$en_magic_0000", Source = "Original prose", Target = "Перевод",
                    References = new Dictionary<string, string?> { ["Reference [fr]"] = "Texte secret" },
                    ExtensionData = new Dictionary<string, JsonElement>
                    {
                        ["Edition Targets"] = JsonSerializer.SerializeToElement(new Dictionary<string, string> { ["Classic:en"] = "Классика" })
                    }
                },
                new() { Category = "Items", File = "items", Key = "$en_items_0000", Source = "Original item", Target = "Предмет" }
            ]
        };
        LocalizationProjectIO.WriteNew(project, input, "json", "single", cancellation);
        LocalizationProjectIO.ExportPlayer(input, output, cancellation);

        LocalizationProject exported = LocalizationProjectIO.Read(output);
        Assert.True(exported.Manifest.PlayerExport);
        Assert.Equal(project.Manifest.ProjectId, exported.Manifest.ProjectId);
        Assert.Equal(["Localization/Items/items.json", "Localization/Magic/magic.json"], exported.Manifest.Payloads.Select(payload => payload.Path));
        Assert.All(exported.Entries, row =>
        {
            Assert.Null(row.Source);
            Assert.False(row.SourcePresent);
            Assert.Empty(row.References);
        });
        Assert.False(File.Exists(Path.Combine(output, "mod.ini")));
        Assert.False(File.Exists(Path.Combine(output, "Localization", "localization.extensions.json")));
        string payloadText = File.ReadAllText(Path.Combine(output, "Localization", "Magic", "magic.json"));
        Assert.Contains("Перевод", payloadText);
        Assert.Contains("Классика", payloadText);
        Assert.DoesNotContain("Original prose", payloadText);
        Assert.DoesNotContain("Texte secret", payloadText);

        ModPackage package = new("Memoria/test", "Test", ModFormat.Memoria, true, true, ModEffectiveStatus.Active, output) { HasLocalization = true };
        ModCatalogSnapshot candidate = new(1, [package], []);
        LocalizationPreparation Prepare(GameDataEdition edition, string magicText)
        {
            LocalizationSourceCatalog catalog = new(
                [new("Magic", "magic", "$en_magic_0000", magicText, "en", new Dictionary<string, string?>()), new("Items", "items", "$en_items_0000", "Original item", "en", new Dictionary<string, string?>())],
                [], []);
            return LocalizationRuntime.Prepare(candidate, [package.Id], new("fixture", edition, [], "en"), cancellation, nativeCatalog: () => catalog);
        }

        LocalizationPreparation remastered = Prepare(GameDataEdition.Remastered, "Original prose");
        using (remastered.InputRevision)
        {
            Assert.Equal("Перевод", remastered.Catalog.Resolve("$en_magic_0000", "default"));
            Assert.Equal("Предмет", remastered.Catalog.Resolve("$en_items_0000", "default"));
        }
        LocalizationPreparation classic = Prepare(GameDataEdition.Classic, "Different English prose");
        using (classic.InputRevision)
        {
            Assert.Equal("Классика", classic.Catalog.Resolve("$en_magic_0000", "default"));
            Assert.Equal("Предмет", classic.Catalog.Resolve("$en_items_0000", "default"));
        }
        Assert.Throws<InvalidDataException>(() => Prepare(GameDataEdition.Remastered, "Mismatched resource"));
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }
}
