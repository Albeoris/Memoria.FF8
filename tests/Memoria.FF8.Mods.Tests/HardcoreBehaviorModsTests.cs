using System.Text.Json;

namespace Memoria.FF8.Mods.Tests;

public sealed class HardcoreBehaviorModsTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "ff8-hardcore-behaviors-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void RequiredPackageIgnoresSavedDisableLocksBothOptionsAndRestoresNormalPreferences()
    {
        string package = Path.Combine(root, "Mods", "Memoria", "Meltdown");
        Directory.CreateDirectory(Path.Combine(package, "gameplay"));
        File.WriteAllText(Path.Combine(package, "gameplay", "behaviors.json"), "{\"schemaVersion\":1,\"meltdown\":{\"defenseReductionPercent\":50}}");
        File.WriteAllText(Path.Combine(package, "mod.ini"), "enabled = 0");
        File.WriteAllText(Path.Combine(root, "Mods", "mods.ini"), "auto_enable_new_mods = 0");
        string configuration = "{\"meltdown\":{\"VitalityReductionPercent\":73,\"SpiritReductionPercent\":37}}";
        string configPath = Path.Combine(package, "config.json");
        File.WriteAllText(configPath, configuration);
        using ModRuntime runtime = new(root, watch: false);
        Publish(runtime);
        Assert.False(Assert.Single(runtime.Catalog.Packages).RequestedEnabled);
        runtime.SetHardcoreMode(true);
        Publish(runtime);
        Assert.True(runtime.Catalog.Hardcore);
        Assert.Equal(ModEffectiveStatus.Active, Assert.Single(runtime.Catalog.Packages).Status);
        Assert.True(Assert.Single(runtime.InstalledPackages).RequestedEnabled);
        Assert.False(Assert.Single(runtime.View.Packages).CanToggle);
        Assert.Contains("Hardcore", runtime.GetPackageLockReason("Memoria/Meltdown")!);
        IReadOnlyList<BehaviorModSetting> settings = runtime.ReadBehaviorSettings("Memoria/Meltdown");
        Assert.Equal(50, settings.Single(setting => setting.Parameter == BehaviorMods.VitalityReductionParameter).Value);
        Assert.Equal(25, settings.Single(setting => setting.Parameter == BehaviorMods.SpiritReductionParameter).Value);
        Assert.False(runtime.SetEnabled("Memoria/Meltdown", false).Success);
        Assert.False(runtime.SetBehaviorSetting("Memoria/Meltdown", "meltdown", BehaviorMods.VitalityReductionParameter, 99).Success);
        Assert.False(runtime.SetBehaviorSetting("Memoria/Meltdown", "meltdown", BehaviorMods.SpiritReductionParameter, 99).Success);
        Assert.Equal(configuration, File.ReadAllText(configPath));
        Assert.Equal("enabled = 0", File.ReadAllText(Path.Combine(package, "mod.ini")));
        Assert.True(runtime.SetAutoEnableNewMods(false).Success);
        runtime.PreparePending(force: true);
        runtime.CommitPending();
        Assert.True(Assert.Single(runtime.Catalog.Packages).RequestedEnabled);
        runtime.SetHardcoreMode(false);
        Publish(runtime);
        Assert.False(runtime.Catalog.Hardcore);
        Assert.False(Assert.Single(runtime.Catalog.Packages).RequestedEnabled);
        Assert.True(Assert.Single(runtime.View.Packages).CanToggle);
        Assert.Null(runtime.GetPackageLockReason("Memoria/Meltdown"));
        settings = runtime.ReadBehaviorSettings("Memoria/Meltdown");
        Assert.Equal(73, settings.Single(setting => setting.Parameter == BehaviorMods.VitalityReductionParameter).Value);
        Assert.Equal(37, settings.Single(setting => setting.Parameter == BehaviorMods.SpiritReductionParameter).Value);
    }

    [Fact]
    public void DeletingTheRequiredPackageDoesNotRecreateItOrPublishAnIntrinsicRule()
    {
        string package = Path.Combine(root, "Mods", "Memoria", "Meltdown");
        Directory.CreateDirectory(Path.Combine(package, "gameplay"));
        File.WriteAllText(Path.Combine(package, "gameplay", "behaviors.json"), "{\"schemaVersion\":1,\"meltdown\":{\"defenseReductionPercent\":50}}");
        using ModRuntime runtime = new(root, watch: false);
        runtime.SetHardcoreMode(true);
        Publish(runtime);
        Directory.Delete(package, true);
        runtime.RequestRefresh();
        Publish(runtime);
        Assert.Empty(runtime.Catalog.Packages);
        Assert.False(Directory.Exists(package));
        Assert.True(BehaviorMods.TryPrepare(runtime.Catalog, out BehaviorMods rules, out _));
        Assert.Null(rules.MeltdownVitalityReductionPercent);
        Assert.Null(rules.MeltdownSpiritReductionPercent);
    }

    private static void Publish(ModRuntime runtime)
    {
        Assert.True(runtime.PreparePending(force: true), JsonSerializer.Serialize(runtime.Diagnostics));
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
