using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Assets.Localization;
using Memoria.FF8.Mods.Localization;
using Memoria.FF8.Resources;
using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8.Mods.Tests;

public sealed class LocalizationAcceptanceTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "Memoria.Localization.Acceptance", Guid.NewGuid().ToString("N"));
    private const string Hash = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
    private const string FieldKey = "$en_bgroom_6_0000";
    private static ResourceSourceDescriptor Source => new("fixture", GameDataEdition.Classic, [], "en");

    [Fact]
    public void ADeclaredBankWithoutAScriptRetainsItsTranslationButCannotBindAnExecutingCall()
    {
        LocalizationRecord dormant = DormantField(Hash);
        ModPackage package = Package("bank-only", dormant, MenuRow("menu translation"));
        LocalizationPreparation prepared = Prepare(package);
        Assert.Equal([FieldKey], prepared.DormantFieldKeys);
        Assert.Equal("Dormant translation", prepared.Catalog.Values[FieldKey].Text);
        Assert.Null(prepared.Catalog.Values[FieldKey].Field);
        Assert.Null(prepared.Catalog.ResolveField(FieldKey, 0, Hash, null, null, null, null));
        Assert.Equal("menu translation", prepared.Catalog.Resolve("$en_sample_0000", "native menu"));
        Assert.Equal("Dormant translation", Assert.Single(prepared.Layers).Project.Entries.Single(row => row.Key == FieldKey).Target);
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("GGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGG")]
    public void BankOnlyRowsStillRequireAValidSha256Fingerprint(string fingerprint)
    {
        ModPackage package = Package("unverified-bank", DormantField(fingerprint));
        Assert.Throws<InvalidDataException>(() => Prepare(package));
    }

    [Fact]
    public void AnExplicitEmptyOrderDoesNotAutomaticallyActivateTheOnlyEnabledPackage()
    {
        ModPackage package = Package("single", MenuRow("pending translation"));
        ModCatalogSnapshot candidate = new(1, [package], []);
        LocalizationPreparation automatic = LocalizationRuntime.Prepare(candidate, null, Source, TestContext.Current.CancellationToken);
        Assert.False(automatic.RequiresSelection);
        Assert.Equal("pending translation", automatic.Catalog.Resolve("$en_sample_0000", "native"));
        LocalizationPreparation empty = LocalizationRuntime.Prepare(candidate, [], Source, TestContext.Current.CancellationToken);
        Assert.True(empty.RequiresSelection);
        Assert.Empty(empty.RequestedOrder);
        Assert.Empty(empty.Catalog.Values);
        Assert.Equal("native", empty.Catalog.Resolve("$en_sample_0000", "native"));
        Assert.Single(empty.Layers);
    }

    [Theory]
    [InlineData("edit")]
    [InlineData("add")]
    [InlineData("remove")]
    [InlineData("replace")]
    public void APreparedInputRevisionRejectsChangesBeforeThePublicationBoundary(string operation)
    {
        if (operation == "replace")
        {
            Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows file identity is required to detect equal-metadata replacement.");
        }
        ModPackage package = Package("revision", MenuRow("candidate translation"));
        LocalizationPreparation prepared = Prepare(package);
        Assert.NotNull(prepared.InputRevision);
        prepared.InputRevision.Validate();
        string payload = Path.Combine(package.RootDirectory, "Localization", "localization.json");
        switch (operation)
        {
            case "edit":
                File.AppendAllText(payload, "\n");
                break;
            case "add":
                File.WriteAllText(Path.Combine(package.RootDirectory, "Localization", "added.json"), "{}");
                break;
            case "remove":
                File.Delete(payload);
                break;
            case "replace":
                DateTime timestamp = File.GetLastWriteTimeUtc(payload);
                byte[] content = File.ReadAllBytes(payload);
                string replacement = payload + ".replacement";
                File.WriteAllBytes(replacement, content);
                File.SetLastWriteTimeUtc(replacement, timestamp);
                File.Move(replacement, payload, true);
                Assert.Equal(content.Length, new FileInfo(payload).Length);
                Assert.Equal(timestamp, File.GetLastWriteTimeUtc(payload));
                break;
        }
        IOException error = Assert.Throws<IOException>(() => prepared.InputRevision.Validate());
        Assert.Contains("changed after preparation", error.Message);
    }

    [Fact]
    public void PublicationChecksAWatchedVersionWithoutReopeningThePayload()
    {
        ModPackage package = Package("watched", MenuRow("candidate"));
        using LocalizationInputRevision input = LocalizationInputRevision.Capture([package.RootDirectory], watchForPublication: true);
        input.Validate();
        string path = Path.Combine(package.RootDirectory, "Localization", "localization.json");
        using (FileStream locked = new(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            input.ValidatePublication();
        }
        File.AppendAllText(path, "\n");
        Assert.True(SpinWait.SpinUntil(() =>
        {
            try
            {
                input.ValidatePublication();
                return false;
            }
            catch (IOException)
            {
                return true;
            }
        }, TimeSpan.FromSeconds(5)), "The watched generation must reject a changed payload before publication.");
    }

    [Fact]
    public void ResourceAndCompanionFingerprintsAreCheckedAgainWithoutReplacingTheLastPublishedCatalog()
    {
        byte[] text = Encoding.UTF8.GetBytes("synthetic original text table");
        byte[] header = [1, 2, 3, 4];
        LocalizationRecord row = MenuRow("candidate") with
        {
            ExtensionData = Context(new Dictionary<string, string>
            {
                ["Resource"] = "menu/mngrp.bin",
                ["Source SHA256"] = Convert.ToHexString(SHA256.HashData(text)),
                ["Companion Resource"] = "menu/mngrphd.bin",
                ["Companion SHA256"] = Convert.ToHexString(SHA256.HashData(header))
            })
        };
        LocalizationPreparation prepared = Prepare(Package("fingerprint", row));
        using InMemoryResourceSource source = new(Source);
        source.Set(new("menu/mngrp.bin"), text, "binary");
        source.Set(new("menu/mngrphd.bin"), header, "binary");
        ResourceService resources = new(source);
        resources.Prepare(null, localization: new(new Dictionary<string, LocalizationValue> { [row.Key] = new("last good", "old", "old") })).Commit();
        long generation = resources.Generation;
        IReadOnlyDictionary<string, LocalizationSourceEntry> known = Known(row);
        LocalizationBindingValidation.Validate(prepared, source, TestContext.Current.CancellationToken, known);
        source.Set(new("menu/mngrphd.bin"), [4, 3, 2, 1], "binary");
        InvalidDataException changed = Assert.Throws<InvalidDataException>(() => LocalizationBindingValidation.Validate(prepared, source, TestContext.Current.CancellationToken, known));
        Assert.Contains(row.Key, changed.Message);
        Assert.Contains("menu/mngrphd.bin", changed.Message);
        Assert.Equal(generation, resources.Generation);
        Assert.Equal("last good", resources.Localize(row.Key, "native"));
        source.Set(new("menu/mngrphd.bin"), header, "binary");
        source.Set(new("menu/mngrp.bin"), Encoding.UTF8.GetBytes("changed original text table"), "binary");
        InvalidDataException sourceChanged = Assert.Throws<InvalidDataException>(() => LocalizationBindingValidation.Validate(prepared, source, TestContext.Current.CancellationToken, known));
        Assert.Contains("menu/mngrp.bin", sourceChanged.Message);
    }

    [Fact]
    public void AuthenticatedNativeTablesRequireBothTheExpectedProfileAndAKnownLogicalIdentity()
    {
        LocalizationRecord row = MenuRow("native translation") with { ExtensionData = Context(new Dictionary<string, string> { ["Native Source Fingerprint"] = Hash }) };
        LocalizationPreparation prepared = Prepare(Package("native-profile", row));
        using NativeSource matching = new(Hash);
        LocalizationBindingValidation.Validate(prepared, matching, TestContext.Current.CancellationToken, Known(row));
        using NativeSource wrong = new(new string('B', 64));
        Assert.Throws<InvalidDataException>(() => LocalizationBindingValidation.Validate(prepared, wrong, TestContext.Current.CancellationToken, Known(row)));
        using InMemoryResourceSource unavailable = new(Source);
        Assert.Throws<InvalidDataException>(() => LocalizationBindingValidation.Validate(prepared, unavailable, TestContext.Current.CancellationToken, Known(row)));
        InvalidDataException unknown = Assert.Throws<InvalidDataException>(() => LocalizationBindingValidation.Validate(prepared, matching, TestContext.Current.CancellationToken, new Dictionary<string, LocalizationSourceEntry>()));
        Assert.Contains("no supported original text identity", unknown.Message);
    }

    private LocalizationPreparation Prepare(ModPackage package) => LocalizationRuntime.Prepare(new(1, [package], []), [package.Id], Source, TestContext.Current.CancellationToken);

    private ModPackage Package(string name, params LocalizationRecord[] rows)
    {
        string path = Path.Combine(root, name);
        LocalizationProject project = new() { Manifest = new() { TargetLanguage = "ru (acceptance)", MappingVersion = FieldCorrespondenceCatalog.Version, CompatibleVariants = ["Classic:en"] }, Entries = rows };
        LocalizationProjectIO.WriteNew(project, path, "json", "single", TestContext.Current.CancellationToken);
        return new("Memoria/" + name, name, ModFormat.Memoria, true, true, ModEffectiveStatus.Active, path) { HasLocalization = true, LocalizationOnly = true };
    }

    private static LocalizationRecord MenuRow(string target) => new() { Category = "Menus", File = "sample", Key = "$en_sample_0000", Source = "source", Target = target };

    private static LocalizationRecord DormantField(string fingerprint) => new()
    {
        Category = "Fields", File = "bgroom_6", Key = FieldKey, Source = "source", Target = "Dormant translation",
        ExtensionData = Context(new Dictionary<string, string> { ["JSM SHA256"] = "", ["Call Contexts"] = "[]", ["Source MSD SHA256"] = fingerprint })
    };

    private static IReadOnlyDictionary<string, JsonElement> Context(Dictionary<string, string> values) => new Dictionary<string, JsonElement> { ["sourceContext"] = JsonSerializer.SerializeToElement(values) };

    private static IReadOnlyDictionary<string, LocalizationSourceEntry> Known(LocalizationRecord row) => new Dictionary<string, LocalizationSourceEntry> { [row.Key] = new(row.Category, row.File, row.Key, "source", "en", new Dictionary<string, string?>()) };

    private sealed class NativeSource(string fingerprint) : IResourceSource, IGameNativeMemorySource
    {
        public ResourceSourceDescriptor Descriptor => Source;
        public string SourceHash => fingerprint;
        public ResourceContent? Find(ResourceKey key) => null;
        public byte[] Read(uint address, int count) => throw new InvalidOperationException("Presentation profile validation must not execute native memory reads.");
        public byte[] ReadLoader(uint address, int count) => throw new InvalidOperationException("Presentation profile validation must not execute loader reads.");
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
