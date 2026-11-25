using System.Text.Json;
using System.Text.Json.Nodes;
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

    [Theory]
    [InlineData(GameDataEdition.Remastered)]
    [InlineData(GameDataEdition.Classic)]
    public void PlayerExportWithoutSourceMetadataResolvesFieldAndTableTargetsByKey(GameDataEdition edition)
    {
        const string fieldKey = "$en_bgroom_1_0000";
        const string itemKey = "$en_items_0000";
        CancellationToken cancellation = TestContext.Current.CancellationToken;
        string input = Path.Combine(root, "input");
        string output = Path.Combine(root, "player");
        LocalizationProject project = new()
        {
            Manifest = new()
            {
                SchemaVersion = 2, TargetLanguage = "ru", SourceEdition = "Remastered", MappingVersion = FieldCorrespondenceCatalog.Version,
                CompatibleVariants = ["Remastered:en", "Classic:en"]
            },
            Entries =
            [
                new()
                {
                    Category = "Fields", File = "bgroom_1", Key = fieldKey, Source = "Original field line", Target = "{X12Y12}Translated field line",
                    ExtensionData = new Dictionary<string, JsonElement>
                    {
                        ["sourceContext"] = JsonSerializer.SerializeToElement(new Dictionary<string, string> { ["Source MSD SHA256"] = new string('A', 64), ["JSM SHA256"] = new string('B', 64) })
                    }
                },
                new() { Category = "Items", File = "items", Key = itemKey, Source = "Original item", Target = "Translated item" }
            ]
        };
        LocalizationProjectIO.WriteNew(project, input, "json", "single", cancellation);
        LocalizationProjectIO.ExportPlayer(input, output, cancellation);
        foreach (LocalizationPayload payload in LocalizationProjectIO.Read(output).Manifest.Payloads)
        {
            string path = Path.Combine(output, payload.Path);
            JsonNode json = JsonNode.Parse(File.ReadAllText(path))!;
            foreach ((_, JsonNode? row) in json[payload.Category!]![payload.File!]!.AsObject())
            {
                JsonObject record = row!.AsObject();
                Assert.True(record.Remove("Source Text SHA256"));
                if (payload.Category == "Fields")
                {
                    Assert.True(record.Remove("sourceContext"));
                }
            }
            File.WriteAllText(path, json.ToJsonString());
        }

        FieldCompatibilityContext call = new(new(0, 0, 0, 0, 7, 28), "AMES", [0, 0, 30, 40], 0, true, false, [], null);
        LocalizationSourceCatalog catalog = new(
            [
                new("Fields", "bgroom_1", fieldKey, "{X30Y40}Different installed field line", "en", new Dictionary<string, string?> { ["Call Contexts"] = JsonSerializer.Serialize(new[] { call }) }),
                new("Items", "items", itemKey, "Different installed item", "en", new Dictionary<string, string?>())
            ], [], []);
        ModPackage package = new("Memoria/test", "Test", ModFormat.Memoria, true, true, ModEffectiveStatus.Active, output) { HasLocalization = true };
        ResourceSourceDescriptor descriptor = new("fixture", edition, [], "en");
        LocalizationPreparation prepared = LocalizationRuntime.Prepare(new(1, [package], []), [package.Id], descriptor, cancellation, nativeCatalog: () => throw new InvalidOperationException("The verified source catalog must not be opened."),
            keyCatalog: () => catalog);
        using var inputs = prepared.InputRevision;
        using var source = new KeyOnlyResourceSource(descriptor);
        Assert.False(LocalizationBindingValidation.RequiresNonFieldSourceCatalog(prepared));
        LocalizationBindingValidation.Validate(prepared, source, cancellation);
        Assert.Equal("Translated item", prepared.Catalog.Resolve(itemKey, "default"));

        LocalizationFieldText? field = prepared.Catalog.ResolveField(fieldKey, 7, () => throw new InvalidOperationException("The script must not be hashed."), null, null, null, null);
        Assert.NotNull(field);
        Assert.Equal("Translated field line", field.Body);
        Assert.Equal(12, field.X);
        Assert.Equal(12, field.Y);
    }

    private sealed class KeyOnlyResourceSource(ResourceSourceDescriptor descriptor) : IResourceSource
    {
        public ResourceSourceDescriptor Descriptor => descriptor;
        public ResourceContent? Find(ResourceKey key) => throw new InvalidOperationException("The resource must not be opened for fingerprint validation.");

        public void Dispose()
        {
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }
}
