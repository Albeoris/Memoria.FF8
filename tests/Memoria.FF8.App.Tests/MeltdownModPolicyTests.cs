using System.Text;
using Memoria.FF8.Mods;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class MeltdownModPolicyTests
{
    [Fact]
    public void BehaviorOverridePreservesOtherConfiguredRulesAndReleasesOnDisable()
    {
        ModPackage package = new("Memoria/Meltdown", "Meltdown", ModFormat.Memoria, true, true, ModEffectiveStatus.Active, string.Empty);
        ModFile payload = new(package.Id, ModFormat.Memoria, BehaviorMods.PayloadPath, 1, () => Encoding.UTF8.GetBytes("{\"schemaVersion\":1,\"meltdown\":{\"defenseReductionPercent\":50}}"));
        ModFile config = new(package.Id, ModFormat.Memoria, BehaviorMods.ConfigurationPath, 1, () => Encoding.UTF8.GetBytes("{\"meltdown\":{\"defenseReductionPercent\":65}}"));
        GameplayTuning configured = new() { DrawBonus = 7, ApMultiplier = 3 };
        GameplayTuningPolicy policy = GameplayTuningPolicy.FromCatalog(new(1, [package], [payload, config]));
        GameplayTuning effective = policy.Apply(configured);
        Assert.Equal(65, effective.MeltdownDefenseReductionPercent);
        Assert.Equal(7, effective.DrawBonus);
        Assert.Equal(3, effective.ApMultiplier);
        Assert.Contains("Meltdown", policy.UnavailableReason(nameof(GameplayTuning.MeltdownDefenseReductionPercent))!);
        Assert.Equal(configured, policy.EditConfigured(configured, effective));
        Assert.Throws<InvalidOperationException>(() => policy.EditConfigured(configured, effective with { MeltdownDefenseReductionPercent = 70 }));
        GameplayTuningPolicy disabled = GameplayTuningPolicy.FromCatalog(new(2, [package with { Status = ModEffectiveStatus.Disabled, RequestedEnabled = false }], []));
        Assert.Equal(configured, disabled.Apply(configured));
    }

    [Fact]
    public void InstalledHostPublishesConfigAndRetainsCommittedBehaviorOnConflict()
    {
        using IsolatedFieldGame game = new();
        string root = Path.Combine(Path.GetTempPath(), "ff8-meltdown-policy-" + Guid.NewGuid().ToString("N"));
        string mods = Path.Combine(root, "Mods");
        string payload = Path.Combine(mods, "Memoria", "Meltdown", "gameplay", "behaviors.json");
        Directory.CreateDirectory(Path.GetDirectoryName(payload)!);
        File.WriteAllText(payload, "{\"schemaVersion\":1,\"meltdown\":{\"defenseReductionPercent\":50}}");
        try
        {
            using GameResourceHost host = new(game.GameDirectory, Path.Combine(root, "Application"), watch: false, modsDirectory: mods);
            Assert.Equal(ModEffectiveStatus.Active, Assert.Single(host.Mods.Catalog.Packages).Status);
            Assert.Equal(50, host.GameplayPolicy.Apply(new()).MeltdownDefenseReductionPercent);
            Assert.True(host.Mods.SetBehaviorSetting("Memoria/Meltdown", "meltdown", "defenseReductionPercent", 65).Success);
            Assert.True(host.Pump(true));
            Assert.Equal(65, host.GameplayPolicy.Apply(new()).MeltdownDefenseReductionPercent);
            string conflicting = Path.Combine(mods, "Memoria", "Other", "gameplay", "behaviors.json");
            Directory.CreateDirectory(Path.GetDirectoryName(conflicting)!);
            File.WriteAllText(conflicting, "{\"schemaVersion\":1,\"meltdown\":{\"defenseReductionPercent\":20}}");
            host.Mods.RequestRefresh();
            host.Pump(true);
            Assert.Equal(65, host.GameplayPolicy.Apply(new()).MeltdownDefenseReductionPercent);
            Assert.Contains(host.Mods.Diagnostics, diagnostic => diagnostic.Code == "Memoria.BehaviorConflict" && diagnostic.PackageId == "Memoria/Meltdown");
            Assert.Contains(host.Mods.Diagnostics, diagnostic => diagnostic.Code == "Memoria.BehaviorConflict" && diagnostic.PackageId == "Memoria/Other");
            Assert.True(host.Mods.SetEnabled("Memoria/Other", false).Success);
            Assert.True(host.Pump(true));
            Assert.True(host.Mods.SetEnabled("Memoria/Meltdown", false).Success);
            Assert.True(host.Pump(true));
            Assert.Equal(100, host.GameplayPolicy.Apply(new()).MeltdownDefenseReductionPercent);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
