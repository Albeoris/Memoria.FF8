using System.Text;
using System.Text.Json;

namespace Memoria.FF8.Mods.Tests;

public sealed class BehaviorModsTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "ff8-behavior-mods-" + Guid.NewGuid().ToString("N"));
    private const string Payload = "{\"schemaVersion\":1,\"meltdown\":{\"defenseReductionPercent\":50}}";

    [Fact]
    public void AbsentBehaviorLeavesNativePolicyUnspecified()
    {
        Assert.True(BehaviorMods.TryPrepare(new(1, [], []), out BehaviorMods mods, out IReadOnlyList<ModDiagnostic> diagnostics));
        Assert.Null(mods.MeltdownDefenseReductionPercent);
        Assert.Null(mods.MeltdownPackageId);
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void UnrelatedPackageConfigurationIsNotABehaviorDocument()
    {
        ModFile[] files = [File("Memoria/Other", BehaviorMods.ConfigurationPath, "unrelated format")];
        Assert.True(BehaviorMods.TryPrepare(new(1, [], files), out BehaviorMods mods, out IReadOnlyList<ModDiagnostic> diagnostics));
        Assert.Null(mods.MeltdownDefenseReductionPercent);
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void InactivePackageCannotApplyBehaviorEvenIfItsFilesAreIncluded()
    {
        ModPackage package = new("Memoria/A", "A", ModFormat.Memoria, false, false, ModEffectiveStatus.Disabled, root);
        Assert.True(BehaviorMods.TryPrepare(new(1, [package], [File(package.Id, BehaviorMods.PayloadPath, Payload)]), out BehaviorMods mods, out IReadOnlyList<ModDiagnostic> diagnostics));
        Assert.Null(mods.MeltdownDefenseReductionPercent);
        Assert.Empty(diagnostics);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    [InlineData(100)]
    public void PackageDefaultAndIntegerOverrideArePrepared(int value)
    {
        ModFile[] files = [File("Memoria/A", BehaviorMods.PayloadPath, Payload), File("Memoria/A", BehaviorMods.ConfigurationPath, JsonSerializer.Serialize(new { meltdown = new { defenseReductionPercent = value } }))];
        Assert.True(BehaviorMods.TryPrepare(new(1, [], files), out BehaviorMods mods, out IReadOnlyList<ModDiagnostic> diagnostics));
        Assert.Equal(value, mods.MeltdownDefenseReductionPercent);
        Assert.Equal("Memoria/A", mods.MeltdownPackageId);
        Assert.Empty(diagnostics);
    }

    [Theory]
    [InlineData("{\"schemaVersion\":1,\"schemaVersion\":1}")]
    [InlineData("{\"schemaVersion\":2}")]
    [InlineData("{\"schemaVersion\":\"1\"}")]
    [InlineData("{\"schemaVersion\":1,\"unknown\":1}")]
    [InlineData("{\"schemaVersion\":1,\"meltdown\":{\"defenseReductionPercent\":-1}}")]
    [InlineData("{\"schemaVersion\":1,\"meltdown\":{\"defenseReductionPercent\":101}}")]
    [InlineData("{\"schemaVersion\":1,\"meltdown\":{\"defenseReductionPercent\":50.5}}")]
    [InlineData("{\"schemaVersion\":1,\"meltdown\":{\"defenseReductionPercent\":\"50\"}}")]
    [InlineData("{\"schemaVersion\":1,\"meltdown\":{\"defenseReductionPercent\":50,\"defenseReductionPercent\":65}}")]
    [InlineData("{\"schemaVersion\":1,\"meltdown\":{\"expression\":\"native()\"}}")]
    public void InvalidPayloadRejectsCandidate(string json)
    {
        Assert.False(BehaviorMods.TryPrepare(new(1, [], [File("Memoria/A", BehaviorMods.PayloadPath, json)]), out BehaviorMods mods, out IReadOnlyList<ModDiagnostic> diagnostics));
        Assert.Null(mods.MeltdownDefenseReductionPercent);
        Assert.Single(diagnostics);
    }

    [Theory]
    [InlineData("{\"schemaVersion\":1}")]
    [InlineData("{\"meltdown\":{\"defenseReductionPercent\":101}}")]
    [InlineData("{\"meltdown\":{\"defenseReductionPercent\":65,\"defenseReductionPercent\":75}}")]
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
        ModFile[] files = [File("Memoria/A", BehaviorMods.PayloadPath, "{\"schemaVersion\":1}"), File("Memoria/A", BehaviorMods.ConfigurationPath, "{\"meltdown\":{\"defenseReductionPercent\":65}}")];
        Assert.False(BehaviorMods.TryPrepare(new(1, [], files), out _, out _));
    }

    [Fact]
    public void ConflictsNameBothOwnersAndRejectWholeCandidate()
    {
        ModFile[] files = [File("Memoria/A", BehaviorMods.PayloadPath, Payload), File("Memoria/B", BehaviorMods.PayloadPath, Payload)];
        Assert.False(BehaviorMods.TryPrepare(new(1, [], files), out BehaviorMods mods, out IReadOnlyList<ModDiagnostic> diagnostics));
        Assert.Null(mods.MeltdownDefenseReductionPercent);
        Assert.Equal(new[] { "Memoria/A", "Memoria/B" }, diagnostics.Select(item => item.PackageId));
        Assert.All(diagnostics, diagnostic =>
        {
            Assert.Equal("Memoria.BehaviorConflict", diagnostic.Code);
            Assert.Contains("Memoria/A", diagnostic.Message);
            Assert.Contains("Memoria/B", diagnostic.Message);
        });
    }

    [Fact]
    public void DisabledBundledPackageSettingsWriteOnlyUserConfiguration()
    {
        Write("BundledMods/Memoria/Meltdown/mod.ini", "enabled=0\n");
        Write("BundledMods/Memoria/Meltdown/gameplay/behaviors.json", Payload);
        using ModRuntime runtime = new(root, watch: false, debounce: TimeSpan.Zero);
        Publish(runtime);
        BehaviorModSetting initial = Assert.Single(runtime.ReadBehaviorSettings("Memoria/Meltdown"));
        Assert.Equal(50, initial.DefaultValue);
        Assert.Equal(50, initial.Value);
        Assert.True(runtime.SetBehaviorSetting("Memoria/Meltdown", "meltdown", "defenseReductionPercent", 65).Success);
        Assert.True(runtime.IsReloadPending);
        Assert.False(System.IO.File.Exists(Path.Combine(root, "BundledMods/Memoria/Meltdown/config.json")));
        Assert.Contains("65", System.IO.File.ReadAllText(Path.Combine(root, "Mods/Memoria/Meltdown/config.json")));
        Assert.Equal(65, Assert.Single(runtime.ReadBehaviorSettings("Memoria/Meltdown")).Value);
        Publish(runtime);
        Assert.True(runtime.SetEnabled("Memoria/Meltdown", true).Success);
        Publish(runtime);
        Assert.True(BehaviorMods.TryPrepare(runtime.Catalog, out BehaviorMods mods, out _));
        Assert.Equal(65, mods.MeltdownDefenseReductionPercent);
        Assert.Equal(Payload, System.IO.File.ReadAllText(Path.Combine(root, "BundledMods/Memoria/Meltdown/gameplay/behaviors.json")));
        Assert.True(runtime.SetBehaviorSetting("Memoria/Meltdown", "meltdown", "defenseReductionPercent", 50).Success);
        Assert.Equal("{}", System.IO.File.ReadAllText(Path.Combine(root, "Mods/Memoria/Meltdown/config.json")));
        Assert.Equal(50, Assert.Single(runtime.ReadBehaviorSettings("Memoria/Meltdown")).Value);
        Publish(runtime);
        Assert.True(BehaviorMods.TryPrepare(runtime.Catalog, out mods, out _));
        Assert.Equal(50, mods.MeltdownDefenseReductionPercent);
    }

    [Fact]
    public void SettingValidatesDeclarationRangeAndMalformedExistingConfiguration()
    {
        Write("Mods/Memoria/Meltdown/gameplay/behaviors.json", Payload);
        using ModRuntime runtime = new(root, watch: false, debounce: TimeSpan.Zero);
        Publish(runtime);
        Assert.False(runtime.SetBehaviorSetting("Memoria/Missing", "meltdown", "defenseReductionPercent", 65).Success);
        Assert.False(runtime.SetBehaviorSetting("Memoria/Meltdown", "meltdown", "unknown", 65).Success);
        Assert.False(runtime.SetBehaviorSetting("Memoria/Meltdown", "meltdown", "defenseReductionPercent", 101).Success);
        Write("Mods/Memoria/Meltdown/config.json", "{\"unknown\":42}");
        Assert.False(runtime.SetBehaviorSetting("Memoria/Meltdown", "meltdown", "defenseReductionPercent", 65).Success);
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
        Assert.Equal(50, published.MeltdownDefenseReductionPercent);
        Write("Mods/Memoria/B/gameplay/behaviors.json", Payload);
        runtime.Refresh();
        Assert.False(runtime.PreparePending(force: true));
        Assert.Equal(50, published.MeltdownDefenseReductionPercent);
        Assert.Equal("Memoria/A", published.MeltdownPackageId);
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
