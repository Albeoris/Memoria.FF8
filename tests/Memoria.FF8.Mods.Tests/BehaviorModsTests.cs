using System.Text;
using System.Text.Json;

namespace Memoria.FF8.Mods.Tests;

public sealed class BehaviorModsTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "ff8-behavior-mods-" + Guid.NewGuid().ToString("N"));
    private const string LegacyPayload = "{\"schemaVersion\":1,\"meltdown\":{\"defenseReductionPercent\":50}}";
    private const string Vitality = BehaviorMods.VitalityReductionParameter;
    private const string Spirit = BehaviorMods.SpiritReductionParameter;
    private static readonly string Payload = CreatePayload();

    [Fact]
    public void AbsentBehaviorLeavesNativePolicyUnspecified()
    {
        Assert.True(BehaviorMods.TryPrepare(new(1, [], []), out BehaviorMods mods, out IReadOnlyList<ModDiagnostic> diagnostics));
        Assert.Null(mods.MeltdownVitalityReductionPercent);
        Assert.Null(mods.MeltdownSpiritReductionPercent);
        Assert.Null(mods.MeltdownPackageId);
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void UnrelatedPackageConfigurationIsNotABehaviorDocument()
    {
        ModFile[] files = [File("Memoria/Other", BehaviorMods.ConfigurationPath, "unrelated format")];
        Assert.True(BehaviorMods.TryPrepare(new(1, [], files), out BehaviorMods mods, out IReadOnlyList<ModDiagnostic> diagnostics));
        Assert.Null(mods.MeltdownVitalityReductionPercent);
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void InactivePackageCannotApplyBehaviorEvenIfItsFilesAreIncluded()
    {
        ModPackage package = new("Memoria/A", "A", ModFormat.Memoria, false, false, ModEffectiveStatus.Disabled, root);
        Assert.True(BehaviorMods.TryPrepare(new(1, [package], [File(package.Id, BehaviorMods.PayloadPath, Payload)]), out BehaviorMods mods, out IReadOnlyList<ModDiagnostic> diagnostics));
        Assert.Null(mods.MeltdownVitalityReductionPercent);
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void TwoDefaultsInOnePackageAreIndependentAndDoNotConflict()
    {
        Assert.True(BehaviorMods.TryPrepare(new(1, [], [File("Memoria/A", BehaviorMods.PayloadPath, Payload)]), out BehaviorMods mods, out IReadOnlyList<ModDiagnostic> diagnostics));
        Assert.Equal(50, mods.MeltdownVitalityReductionPercent);
        Assert.Equal(25, mods.MeltdownSpiritReductionPercent);
        Assert.Equal("Memoria/A", mods.MeltdownVitalityPackageId);
        Assert.Equal("Memoria/A", mods.MeltdownSpiritPackageId);
        Assert.Empty(diagnostics);
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(65, 35)]
    [InlineData(100, 0)]
    public void IntegerOverridesArePreparedIndependently(int vitality, int spirit)
    {
        string configuration = JsonSerializer.Serialize(new { meltdown = new { VitalityReductionPercent = vitality, SpiritReductionPercent = spirit } });
        ModFile[] files = [File("Memoria/A", BehaviorMods.PayloadPath, Payload), File("Memoria/A", BehaviorMods.ConfigurationPath, configuration)];
        Assert.True(BehaviorMods.TryPrepare(new(1, [], files), out BehaviorMods mods, out _));
        Assert.Equal(vitality, mods.MeltdownVitalityReductionPercent);
        Assert.Equal(spirit, mods.MeltdownSpiritReductionPercent);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LegacyScalarConfigurationMapsToBothParameters(bool legacyPayload)
    {
        ModFile[] files = [File("Memoria/A", BehaviorMods.PayloadPath, legacyPayload ? LegacyPayload : Payload), File("Memoria/A", BehaviorMods.ConfigurationPath, "{\"meltdown\":{\"defenseReductionPercent\":65}}")];
        Assert.True(BehaviorMods.TryPrepare(new(1, [], files), out BehaviorMods mods, out _));
        Assert.Equal(65, mods.MeltdownVitalityReductionPercent);
        Assert.Equal(65, mods.MeltdownSpiritReductionPercent);
    }

    [Fact]
    public void LegacyPayloadDefaultsMapToBothParameters()
    {
        Assert.True(BehaviorMods.TryPrepare(new(1, [], [File("Memoria/A", BehaviorMods.PayloadPath, LegacyPayload)]), out BehaviorMods mods, out _));
        Assert.Equal(50, mods.MeltdownVitalityReductionPercent);
        Assert.Equal(50, mods.MeltdownSpiritReductionPercent);
    }

    [Theory]
    [InlineData("{\"schemaVersion\":2,\"schemaVersion\":2}")]
    [InlineData("{\"schemaVersion\":3}")]
    [InlineData("{\"schemaVersion\":\"2\"}")]
    [InlineData("{\"schemaVersion\":2,\"unknown\":1}")]
    [InlineData("{\"schemaVersion\":1,\"meltdown\":{\"defenseReductionPercent\":-1}}")]
    [InlineData("{\"schemaVersion\":1,\"meltdown\":{\"defenseReductionPercent\":101}}")]
    [InlineData("{\"schemaVersion\":1,\"meltdown\":{\"defenseReductionPercent\":50.5}}")]
    [InlineData("{\"schemaVersion\":1,\"meltdown\":{\"defenseReductionPercent\":\"50\"}}")]
    [InlineData("{\"schemaVersion\":1,\"meltdown\":{\"defenseReductionPercent\":50,\"defenseReductionPercent\":65}}")]
    [InlineData("{\"schemaVersion\":2,\"meltdown\":{\"expression\":\"native()\"}}")]
    public void InvalidPayloadRejectsCandidate(string json)
    {
        Assert.False(BehaviorMods.TryPrepare(new(1, [], [File("Memoria/A", BehaviorMods.PayloadPath, json)]), out BehaviorMods mods, out IReadOnlyList<ModDiagnostic> diagnostics));
        Assert.Null(mods.MeltdownVitalityReductionPercent);
        Assert.Single(diagnostics);
    }

    [Theory]
    [InlineData("\"description\":\"Useful description\"", "\"description\":\" \"")]
    [InlineData("\"control\":\"slider\"", "\"control\":\"script\"")]
    [InlineData("\"unit\":\"%\"", "\"unit\":\"points\"")]
    [InlineData("\"min\":0", "\"min\":-1")]
    [InlineData("\"max\":100", "\"max\":101")]
    [InlineData("\"min\":0", "\"min\":75")]
    [InlineData("\"step\":1", "\"step\":0")]
    [InlineData("\"step\":1", "\"step\":3")]
    [InlineData("\"default\":50", "\"default\":101")]
    [InlineData("\"default\":50", "\"default\":50,\"default\":40")]
    [InlineData("\"default\":50", "\"default\":50,\"unknown\":0")]
    public void DescriptorMetadataIsStrictlyValidated(string before, string after)
    {
        string payload = Payload.Replace(before, after, StringComparison.Ordinal);
        Assert.NotEqual(Payload, payload);
        Assert.False(BehaviorMods.TryPrepare(new(1, [], [File("Memoria/A", BehaviorMods.PayloadPath, payload)]), out _, out IReadOnlyList<ModDiagnostic> diagnostics));
        Assert.Single(diagnostics);
    }

    [Theory]
    [InlineData("{\"schemaVersion\":2}")]
    [InlineData("{\"meltdown\":{\"VitalityReductionPercent\":101}}")]
    [InlineData("{\"meltdown\":{\"SpiritReductionPercent\":-1}}")]
    [InlineData("{\"meltdown\":{\"SpiritReductionPercent\":35.5}}")]
    [InlineData("{\"meltdown\":{\"VitalityReductionPercent\":65,\"VitalityReductionPercent\":75}}")]
    [InlineData("{\"meltdown\":{\"defenseReductionPercent\":65,\"VitalityReductionPercent\":75}}")]
    [InlineData("{\"unknown\":{\"value\":65}}")]
    public void InvalidConfigurationRejectsCandidate(string json)
    {
        ModFile[] files = [File("Memoria/A", BehaviorMods.PayloadPath, Payload), File("Memoria/A", BehaviorMods.ConfigurationPath, json)];
        Assert.False(BehaviorMods.TryPrepare(new(1, [], files), out _, out IReadOnlyList<ModDiagnostic> diagnostics));
        Assert.Single(diagnostics);
    }

    [Fact]
    public void ConfigurationCannotDeclareBehavior()
    {
        ModFile[] files = [File("Memoria/A", BehaviorMods.PayloadPath, "{\"schemaVersion\":2}"), File("Memoria/A", BehaviorMods.ConfigurationPath, "{\"meltdown\":{\"VitalityReductionPercent\":65}}")];
        Assert.False(BehaviorMods.TryPrepare(new(1, [], files), out _, out _));
    }

    [Fact]
    public void SeparateParametersCanHaveSeparatePackageOwners()
    {
        ModFile[] files = [File("Memoria/A", BehaviorMods.PayloadPath, CreatePayload(Vitality)), File("Memoria/B", BehaviorMods.PayloadPath, CreatePayload(Spirit))];
        Assert.True(BehaviorMods.TryPrepare(new(1, [], files), out BehaviorMods mods, out _));
        Assert.Equal("Memoria/A", mods.MeltdownVitalityPackageId);
        Assert.Equal("Memoria/B", mods.MeltdownSpiritPackageId);
        Assert.Null(mods.MeltdownPackageId);
    }

    [Fact]
    public void ConflictsNameBothOwnersAndRejectWholeCandidate()
    {
        ModFile[] files = [File("Memoria/A", BehaviorMods.PayloadPath, CreatePayload(Vitality)), File("Memoria/B", BehaviorMods.PayloadPath, Payload)];
        Assert.False(BehaviorMods.TryPrepare(new(1, [], files), out BehaviorMods mods, out IReadOnlyList<ModDiagnostic> diagnostics));
        Assert.Null(mods.MeltdownVitalityReductionPercent);
        Assert.Equal(new[] { "Memoria/A", "Memoria/B" }, diagnostics.Select(item => item.PackageId));
        Assert.All(diagnostics, diagnostic =>
        {
            Assert.Equal("Memoria.BehaviorConflict", diagnostic.Code);
            Assert.Contains("Memoria/A", diagnostic.Message);
            Assert.Contains("Memoria/B", diagnostic.Message);
        });
    }

    [Fact]
    public void DisabledBundledPackageSettingsWriteOnlyUserValueOverrides()
    {
        Write("BundledMods/Memoria/Meltdown/mod.ini", "enabled=0\n");
        Write("BundledMods/Memoria/Meltdown/gameplay/behaviors.json", Payload);
        using ModRuntime runtime = new(root, watch: false, debounce: TimeSpan.Zero);
        Publish(runtime);
        IReadOnlyList<BehaviorModSetting> settings = runtime.ReadBehaviorSettings("Memoria/Meltdown");
        Assert.Equal(2, settings.Count);
        BehaviorModSetting initial = settings.Single(item => item.Parameter == Vitality);
        Assert.Equal(50, initial.DefaultValue);
        Assert.Equal("Useful description", initial.Description);
        Assert.Equal("slider", initial.Control);
        Assert.Equal("%", initial.Unit);
        Assert.Equal(1, initial.Step);
        Assert.True(runtime.SetBehaviorSetting("Memoria/Meltdown", "meltdown", Vitality, 65).Success);
        Assert.True(runtime.IsReloadPending);
        Assert.False(System.IO.File.Exists(Path.Combine(root, "BundledMods/Memoria/Meltdown/config.json")));
        string configuration = System.IO.File.ReadAllText(Path.Combine(root, "Mods/Memoria/Meltdown/config.json"));
        Assert.Contains(Vitality, configuration);
        Assert.DoesNotContain(Spirit, configuration);
        Assert.DoesNotContain("description", configuration);
        Assert.Equal(65, runtime.ReadBehaviorSettings("Memoria/Meltdown").Single(item => item.Parameter == Vitality).Value);
        Publish(runtime);
        Assert.True(runtime.SetEnabled("Memoria/Meltdown", true).Success);
        Publish(runtime);
        Assert.True(BehaviorMods.TryPrepare(runtime.Catalog, out BehaviorMods mods, out _));
        Assert.Equal(65, mods.MeltdownVitalityReductionPercent);
        Assert.Equal(25, mods.MeltdownSpiritReductionPercent);
        Assert.Equal(Payload, System.IO.File.ReadAllText(Path.Combine(root, "BundledMods/Memoria/Meltdown/gameplay/behaviors.json")));
        Assert.True(runtime.SetBehaviorSetting("Memoria/Meltdown", "meltdown", Vitality, 50).Success);
        Assert.Equal("{}", System.IO.File.ReadAllText(Path.Combine(root, "Mods/Memoria/Meltdown/config.json")));
    }

    [Fact]
    public void EditingLegacyConfigurationPreservesOtherSavedPercentage()
    {
        Write("Mods/Memoria/Meltdown/gameplay/behaviors.json", Payload);
        Write("Mods/Memoria/Meltdown/config.json", "{\"meltdown\":{\"defenseReductionPercent\":65}}");
        using ModRuntime runtime = new(root, watch: false, debounce: TimeSpan.Zero);
        Publish(runtime);
        Assert.True(runtime.SetBehaviorSetting("Memoria/Meltdown", "meltdown", Vitality, 75).Success);
        string configuration = System.IO.File.ReadAllText(Path.Combine(root, "Mods/Memoria/Meltdown/config.json"));
        Assert.DoesNotContain("defenseReductionPercent", configuration);
        using JsonDocument document = JsonDocument.Parse(configuration);
        Assert.Equal(75, document.RootElement.GetProperty("meltdown").GetProperty(Vitality).GetInt32());
        Assert.Equal(65, document.RootElement.GetProperty("meltdown").GetProperty(Spirit).GetInt32());
    }

    [Fact]
    public void SettingValidatesDeclarationRangeStepAndMalformedExistingConfiguration()
    {
        Write("Mods/Memoria/Meltdown/gameplay/behaviors.json", Payload.Replace("\"step\":1", "\"step\":5", StringComparison.Ordinal));
        using ModRuntime runtime = new(root, watch: false, debounce: TimeSpan.Zero);
        Publish(runtime);
        Assert.False(runtime.SetBehaviorSetting("Memoria/Missing", "meltdown", Vitality, 65).Success);
        Assert.False(runtime.SetBehaviorSetting("Memoria/Meltdown", "meltdown", "unknown", 65).Success);
        Assert.False(runtime.SetBehaviorSetting("Memoria/Meltdown", "meltdown", Vitality, 101).Success);
        Assert.False(runtime.SetBehaviorSetting("Memoria/Meltdown", "meltdown", Vitality, 66).Success);
        Write("Mods/Memoria/Meltdown/config.json", "{\"unknown\":42}");
        Assert.False(runtime.SetBehaviorSetting("Memoria/Meltdown", "meltdown", Vitality, 65).Success);
        Assert.Equal("{\"unknown\":42}", System.IO.File.ReadAllText(Path.Combine(root, "Mods/Memoria/Meltdown/config.json")));
        Assert.Contains(runtime.Diagnostics, item => item.Code == "configuration-write-failed");
    }

    [Fact]
    public void ConflictingCandidateDoesNotReplacePublishedBehavior()
    {
        Write("Mods/Memoria/A/gameplay/behaviors.json", Payload);
        BehaviorMods published = BehaviorMods.Empty;
        using ModRuntime runtime = new(root, watch: false, debounce: TimeSpan.Zero, prepare: candidate =>
        {
            bool success = BehaviorMods.TryPrepare(candidate, out BehaviorMods prepared, out IReadOnlyList<ModDiagnostic> diagnostics);
            return new(success, Diagnostics: diagnostics, Commit: () => published = prepared);
        });
        Publish(runtime);
        Write("Mods/Memoria/B/gameplay/behaviors.json", Payload);
        runtime.Refresh();
        Assert.False(runtime.PreparePending(force: true));
        Assert.Equal(50, published.MeltdownVitalityReductionPercent);
        Assert.Equal(25, published.MeltdownSpiritReductionPercent);
        Assert.Equal("Memoria/A", published.MeltdownPackageId);
    }

    private static string CreatePayload(string? parameter = null)
    {
        Dictionary<string, object> parameters = new(StringComparer.Ordinal);
        foreach (string name in new[] { Vitality, Spirit })
        {
            if (parameter is not null && name != parameter)
            {
                continue;
            }
            parameters.Add(name, new { label = name == Vitality ? "Vitality reduction" : "Spirit reduction", description = "Useful description", control = "slider", unit = "%", min = 0, max = 100, step = 1, @default = name == Vitality ? 50 : 25 });
        }
        return JsonSerializer.Serialize(new { schemaVersion = 2, meltdown = parameters });
    }

    private static ModFile File(string packageId, string path, string json)
    {
        return new(packageId, ModFormat.Memoria, path, 1, () => Encoding.UTF8.GetBytes(json));
    }

    private void Write(string path, string contents)
    {
        string absolute = Path.Combine(root, path);
        Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
        System.IO.File.WriteAllText(absolute, contents);
    }

    private static void Publish(ModRuntime runtime)
    {
        Assert.True(runtime.PreparePending(force: true));
        Assert.True(runtime.CommitPending());
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }
}
