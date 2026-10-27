using System.Text.Json;
using Memoria.FF8.Mods;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class GameplayTuningPolicyTests
{
    [Fact]
    public void OnlyActiveJunctionPackagesApplyAndCatalogFirstPackageOwnsTheRule()
    {
        ModPackage disabled = Package("disabled", ModEffectiveStatus.Disabled, new JunctionGameplayOverride(JunctionGameplaySetting.DrawBonus, 99));
        ModPackage unrelated = Package("other format", ModEffectiveStatus.Active, new JunctionGameplayOverride(JunctionGameplaySetting.DrawBonus, 88)) with { Format = ModFormat.Memoria };
        ModPackage first = Package("first", ModEffectiveStatus.Active, new JunctionGameplayOverride(JunctionGameplaySetting.DrawBonus, 3)) with { Priority = 20 };
        ModPackage later = Package("later", ModEffectiveStatus.Active, new JunctionGameplayOverride(JunctionGameplaySetting.DrawBonus, 70), new JunctionGameplayOverride(JunctionGameplaySetting.ApMultiplier, 4)) with { Priority = 0 };
        GameplayTuningPolicy policy = GameplayTuningPolicy.FromCatalog(new(1, [disabled, unrelated, first, later], []));
        GameplayTuning effective = policy.Apply(new());
        Assert.Equal(3, effective.DrawBonus);
        Assert.Equal(4, effective.ApMultiplier);
        Assert.Contains("first", policy.UnavailableReason(nameof(GameplayTuning.DrawBonus))!);
        Assert.Contains("later", policy.UnavailableReason(nameof(GameplayTuning.ApMultiplier))!);
        Assert.Null(policy.UnavailableReason(nameof(GameplayTuning.GuardianExperienceMultiplier)));
        Assert.Equal(new GameplayTuning(), GameplayTuningPolicy.FromCatalog(new(1, [disabled, unrelated], [])).Apply(new()));
    }

    [Theory]
    [InlineData(JunctionGameplaySetting.DrawBonus, -1)]
    [InlineData(JunctionGameplaySetting.DrawBonus, 101)]
    [InlineData(JunctionGameplaySetting.MaximumDamage, 100001)]
    [InlineData(JunctionGameplaySetting.ApMultiplier, 101)]
    [InlineData(JunctionGameplaySetting.ExperienceMultiplier, -1)]
    [InlineData(JunctionGameplaySetting.GuardianExperienceMultiplier, 101)]
    [InlineData(JunctionGameplaySetting.UnlimitedMagic, 2)]
    [InlineData(JunctionGameplaySetting.UnrestrictedScanRotation, -1)]
    public void InvalidReviewedNumericValuesFailBeforeProducingAnEffectivePolicy(JunctionGameplaySetting setting, int value)
    {
        Assert.Throws<InvalidDataException>(() => Policy(new JunctionGameplayOverride(setting, value)));
    }

    [Fact]
    public void FractionalIntegerRulesAndInvalidRareTotalsAreRejected()
    {
        Assert.Throws<InvalidDataException>(() => Policy(new JunctionGameplayOverride(JunctionGameplaySetting.ApMultiplier, 1.5m)));
        Assert.Throws<InvalidDataException>(() => Policy(new JunctionGameplayOverride(JunctionGameplaySetting.UnlimitedMagic, 0.5m)));
        Assert.Throws<InvalidDataException>(() => Policy(new JunctionGameplayOverride(JunctionGameplaySetting.RareItemFirst, 25m)));
    }

    [Fact]
    public void EditingUnlockedFieldsPreservesThePersonalLockedBaselineWithoutMutatingSubmittedValues()
    {
        GameplayTuning previous = new() { DrawBonus = 7, ApMultiplier = 2, RareItem = new() { First = 25, Second = 25, Third = 25, Fourth = 25 } };
        GameplayTuningPolicy policy = Policy(new JunctionGameplayOverride(JunctionGameplaySetting.DrawBonus, 100), new JunctionGameplayOverride(JunctionGameplaySetting.RareItemFirst, 50), new JunctionGameplayOverride(JunctionGameplaySetting.RareItemSecond, 50), new JunctionGameplayOverride(JunctionGameplaySetting.RareItemThird, 0), new JunctionGameplayOverride(JunctionGameplaySetting.RareItemFourth, 0));
        GameplayTuning submitted = policy.Apply(previous) with { ApMultiplier = 6, MaximumDamage = 100000 };
        string submittedBefore = JsonSerializer.Serialize(submitted);
        GameplayTuning configured = policy.EditConfigured(previous, submitted);
        Assert.Equal(previous.DrawBonus, configured.DrawBonus);
        Assert.Equal(previous.RareItem, configured.RareItem);
        Assert.Equal(6, configured.ApMultiplier);
        Assert.Equal(100000, configured.MaximumDamage);
        Assert.Equal(submitted, policy.Apply(configured));
        Assert.Equal(submittedBefore, JsonSerializer.Serialize(submitted));
        Assert.Equal(2, previous.ApMultiplier);
        Assert.Contains("rules", policy.UnavailableReason(nameof(GameplayTuning.RareItem))!);
        Assert.Throws<InvalidOperationException>(() => policy.EditConfigured(previous, submitted with { DrawBonus = 99 }));
        Assert.Throws<InvalidOperationException>(() => policy.EditConfigured(previous, submitted with { RareItem = new() { First = 49, Second = 51, Third = 0, Fourth = 0 } }));
    }

    [Fact]
    public void CharacterExperienceOverrideLeavesGuardianExperienceAndPersonalChoicesIndependent()
    {
        GameplayTuning personal = new() { ExperienceMultiplier = 7, GuardianExperienceMultiplier = 3 };
        GameplayTuningPolicy policy = Policy(new JunctionGameplayOverride(JunctionGameplaySetting.ExperienceMultiplier, 0));
        GameplayTuning effective = policy.Apply(personal);
        Assert.Equal(0, effective.ExperienceMultiplier);
        Assert.Equal(3, effective.GuardianExperienceMultiplier);
        GameplayTuning configured = policy.EditConfigured(personal, effective with { GuardianExperienceMultiplier = 5 });
        Assert.Equal(7, configured.ExperienceMultiplier);
        Assert.Equal(5, configured.GuardianExperienceMultiplier);
        Assert.Equal(configured, GameplayTuningPolicy.Empty.Apply(configured));
    }

    [Fact]
    public void InstalledReviewedPackagePublishesDefaultsRefreshesMetadataOnlyOptionsAndReleasesLocksWhenDisabled()
    {
        string packageSource = TestResources.FixturePath("junction-gameplay");
        using IsolatedFieldGame game = new();
        string root = Path.Combine(Path.GetTempPath(), "ff8-gameplay-package-" + Guid.NewGuid().ToString("N"));
        string mods = Path.Combine(root, "Mods");
        string archive = Path.Combine(mods, "Junction", "gameplay.iroj");
        Directory.CreateDirectory(Path.GetDirectoryName(archive)!);
        File.Copy(packageSource, archive);
        try
        {
            File.WriteAllText(archive + ".ini", "enabled = 1");
            using GameResourceHost host = new(game.GameDirectory, Path.Combine(root, "Application"), watch: false, modsDirectory: mods);
            AssertActive("default");
            GameplayTuning defaults = host.GameplayPolicy.Apply(new());
            Assert.Equal(1, defaults.ApMultiplier);
            Assert.Equal(1, defaults.ExperienceMultiplier);
            Assert.Equal(1, defaults.GuardianExperienceMultiplier);
            Assert.Equal(9999, defaults.MaximumDamage);
            Assert.False(defaults.UnlimitedMagic);
            Assert.True(defaults.UnrestrictedScanRotation);
            Assert.Equal(46.875m, defaults.RareItem.First);
            Assert.NotNull(host.GameplayPolicy.UnavailableReason(nameof(GameplayTuning.RareItem)));
            string[] originalFiles = host.Mods.Catalog.Files.Select(file => file.RelativePath).Order().ToArray();
            File.WriteAllLines(archive + ".ini", ["enabled = 1", "option.DamageCapIncreased = 1", "option.NoMagicDepletion = 1", "option.AP = 3", "option.EXP = 5", "option.DrawRework = 2"]);
            host.Mods.RequestRefresh();
            Assert.True(host.Pump(true));
            AssertActive("enabled");
            Assert.Equal(originalFiles, host.Mods.Catalog.Files.Select(file => file.RelativePath).Order());
            GameplayTuning enabled = host.GameplayPolicy.Apply(new());
            Assert.Equal(8, enabled.ApMultiplier);
            Assert.Equal(16, enabled.ExperienceMultiplier);
            Assert.Equal(1, enabled.GuardianExperienceMultiplier);
            Assert.Equal(100, enabled.DrawBonus);
            Assert.Equal(65535, enabled.MaximumDamage);
            Assert.True(enabled.UnlimitedMagic);
            Assert.NotNull(host.GameplayPolicy.UnavailableReason(nameof(GameplayTuning.MaximumDamage)));
            File.WriteAllLines(archive + ".ini", ["enabled = 1", "option.DamageCapIncreased = 0", "option.NoMagicDepletion = 0", "option.AP = 0", "option.EXP = 1", "option.DrawRework = 0"]);
            host.Mods.RequestRefresh();
            Assert.True(host.Pump(true));
            AssertActive("options removed");
            Assert.Equal(defaults, host.GameplayPolicy.Apply(new()));
            Assert.Null(host.GameplayPolicy.UnavailableReason(nameof(GameplayTuning.MaximumDamage)));
            Assert.True(host.Mods.SetEnabled("Junction/gameplay.iroj", false).Success);
            Assert.True(host.Pump(true));
            Assert.Equal(ModEffectiveStatus.Disabled, Assert.Single(host.Mods.Catalog.Packages).Status);
            Assert.Equal(new GameplayTuning(), host.GameplayPolicy.Apply(new()));
            Assert.Null(host.GameplayPolicy.UnavailableReason(nameof(GameplayTuning.RareItem)));

            void AssertActive(string phase)
            {
                string report = JsonSerializer.Serialize(new { phase, host.Mods.Catalog.Packages, host.Mods.Diagnostics, effective = host.GameplayPolicy.Apply(new()) });
                string? evidence = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAMEPLAY_PACKAGE_OUTPUT");
                if (!string.IsNullOrWhiteSpace(evidence))
                {
                    Directory.CreateDirectory(evidence);
                    File.WriteAllText(Path.Combine(evidence, phase.Replace(' ', '-') + ".json"), report);
                }
                Assert.True(host.Mods.Catalog.Packages.Count == 1 && host.Mods.Catalog.Packages[0].Status == ModEffectiveStatus.Active, report);
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static GameplayTuningPolicy Policy(params JunctionGameplayOverride[] settings)
    {
        return GameplayTuningPolicy.FromCatalog(new(1, [Package("rules", ModEffectiveStatus.Active, settings)], []));
    }

    private static ModPackage Package(string name, ModEffectiveStatus status, params JunctionGameplayOverride[] settings)
    {
        return new(name, name, ModFormat.Junction, true, true, status, string.Empty) { JunctionMetadata = new() { GameplayOverrides = settings } };
    }
}
