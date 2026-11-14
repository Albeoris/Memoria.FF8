using Memoria.FF8.GameUi;
using Memoria.FF8.Mods;
using Memoria.FF8.Resources.Localization;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class LevelBasedStatBonusModTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "ff8-level-stat-bonus-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void DefaultOffHardcoreLockAndPreferenceRestorationFollowPackageActivation()
    {
        using ModRuntime runtime = CreateRuntime();
        Assert.False(Effective(runtime).LevelBasedStatBonuses);
        Assert.False(GameplayTuningPolicy.FromCatalog(runtime.Catalog).Apply(new() { LevelBasedStatBonuses = true }).LevelBasedStatBonuses);
        Assert.False(Assert.Single(runtime.Catalog.Packages).RequestedEnabled);
        runtime.SetHardcoreMode(true);
        Publish(runtime);
        Assert.True(Effective(runtime).LevelBasedStatBonuses);
        Assert.False(Assert.Single(runtime.View.Packages).CanToggle);
        Assert.Contains("Hardcore", runtime.GetPackageLockReason(GameplayTuningPolicy.LevelBasedStatBonusesPackageId)!);
        Assert.False(runtime.SetEnabled(GameplayTuningPolicy.LevelBasedStatBonusesPackageId, false).Success);
        runtime.SetHardcoreMode(false);
        Publish(runtime);
        Assert.False(Effective(runtime).LevelBasedStatBonuses);
        Assert.True(runtime.SetEnabled(GameplayTuningPolicy.LevelBasedStatBonusesPackageId, true).Success);
        Publish(runtime);
        Assert.True(Effective(runtime).LevelBasedStatBonuses);
        GameplayTuningPolicy policy = GameplayTuningPolicy.FromCatalog(runtime.Catalog);
        Assert.Throws<InvalidOperationException>(() => policy.EditConfigured(new(), new()));
        Assert.False(policy.EditConfigured(new(), policy.Apply(new())).LevelBasedStatBonuses);
        Assert.NotEqual(new GameplayTuning(), new GameplayTuning { LevelBasedStatBonuses = true });
    }

    [Theory]
    [InlineData("en", "While equipped")]
    [InlineData("ru", "Пока способность")]
    [InlineData("ja", "装備中のみ")]
    [InlineData("jp", "装備中のみ")]
    [InlineData("de", "Solange ausgerüstet")]
    [InlineData("fr", "Tant que")]
    [InlineData("it", "Finché equipaggiata")]
    [InlineData("es", "Mientras está equipada")]
    public void DetailsRenderBundledTranslationAndAllowExternalOverride(string language, string expected)
    {
        using ModRuntime runtime = CreateRuntime();
        MenuController menu = new();
        menu.ConnectMods(runtime);
        menu.ConnectLocalizationText((key, fallback) => fallback, () => language);
        menu.Open("mods");
        menu.Execute(new("mod-clear"));
        menu.Execute(new("mod-inspect"));
        string description = Assert.Single(menu.CurrentScreen.Details(menu), line => line.StartsWith(expected, StringComparison.Ordinal));
        Assert.Contains("2970", description);
        Assert.Contains("99", description);
        menu.ConnectLocalizationText((key, fallback) => key == SystemTextCatalog.Key("mods.level-based-stat-bonuses.description") ? "Custom description" : fallback, () => language);
        Assert.Contains("Custom description", menu.CurrentScreen.Details(menu));
    }

    [Fact]
    public void UnbundledLookalikeDoesNotEnableBuiltInRule()
    {
        ModPackage package = new(GameplayTuningPolicy.LevelBasedStatBonusesPackageId, "Level-Based Stat Bonuses", ModFormat.Memoria, true, true, ModEffectiveStatus.Active, string.Empty);
        Assert.False(GameplayTuningPolicy.FromCatalog(new(1, [package], [])).Apply(new()).LevelBasedStatBonuses);
        Assert.False(GameplayTuningPolicy.Empty.Apply(new() { LevelBasedStatBonuses = true }).LevelBasedStatBonuses);
    }

    [Fact]
    public void MissingPackageInHardcoreDoesNotPublishIntrinsicRuleOrRecreatePackage()
    {
        using ModRuntime runtime = CreateRuntime();
        runtime.SetHardcoreMode(true);
        Publish(runtime);
        Assert.True(Effective(runtime).LevelBasedStatBonuses);
        string package = Assert.Single(runtime.Catalog.Packages).RootDirectory;
        Directory.Delete(package, true);
        runtime.RequestRefresh();
        Publish(runtime);
        Assert.Empty(runtime.Catalog.Packages);
        Assert.False(Effective(runtime).LevelBasedStatBonuses);
        Assert.False(Directory.Exists(package));
    }

    [Fact]
    public void BonusAbilityDescriptionsFollowActiveRuleLanguageAndTranslationOverride()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed native menu resources are required.");
        using Memoria.FF8.Resources.IResourceSource source = Memoria.FF8.Assets.GameDataSourceFactory.Open(directory);
        Memoria.FF8.Resources.ResourceService resources = new(source);
        using Memoria.FF8.Assets.GameAssets assets = new(directory, resources: resources);
        LiveMenuCatalog catalog = LiveMenuCatalog.ReadInstalled(assets);
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals();
        foreach (int guardian in Enumerable.Range(0, 16))
        {
            vitals = vitals.AcquireGuardianForce(guardian);
        }
        foreach (string language in new[] { "en", "ru", "ja", "de", "fr", "it", "es" })
        {
            catalog.InterfaceLanguage = () => language;
            foreach (int ability in Enumerable.Range(65, 5))
            {
                Assert.Equal(catalog.AbilityDescription(ability), catalog.ReadBonusAbilityDescription(ability, false));
                string expected = SystemTextCatalog.LevelBasedBonusAbilityDescription(ability, language)!;
                Assert.Equal(expected, catalog.ReadBonusAbilityDescription(ability, true));
                Assert.Contains(ability == 65 ? "2970" : "99", expected);
            }
            GameState state = new() { CharacterVitals = vitals, GameplayPreferences = new() { Tuning = new() { LevelBasedStatBonuses = true } } };
            MenuAbilityChoice[] bonusChoices = LiveMenuData.Build(state, catalog: catalog, includeJunctionPlanner: false).GuardianForces.SelectMany(guardian => guardian.LearningChoices).Where(choice => int.Parse(choice.Id) is >= 65 and <= 69).ToArray();
            Assert.NotEmpty(bonusChoices);
            Assert.All(bonusChoices, choice => Assert.Equal(catalog.ReadBonusAbilityDescription(int.Parse(choice.Id), true), choice.Description));
            Assert.Equal(catalog.AbilityDescription(64), catalog.ReadBonusAbilityDescription(64, true));
            Assert.Equal(catalog.AbilityDescription(70), catalog.ReadBonusAbilityDescription(70, true));
        }
        string key = SystemTextCatalog.Key("mods.level-based-stat-bonuses.ability.65");
        resources.PrepareLocalization(new LocalizationCatalog(new Dictionary<string, LocalizationValue> { [key] = new("Translated equipped HP bonus", "fixture", "fixture") })).Commit();
        Assert.Equal("Translated equipped HP bonus", catalog.ReadBonusAbilityDescription(65, true));
        Assert.Equal(catalog.AbilityDescription(65), catalog.ReadBonusAbilityDescription(65, false));
    }

    private ModRuntime CreateRuntime()
    {
        string package = Path.Combine(root, "BundledMods", "Memoria", "Level-Based Stat Bonuses");
        Directory.CreateDirectory(package);
        File.WriteAllText(Path.Combine(package, "mod.ini"), "enabled = 0\n");
        File.WriteAllText(Path.Combine(package, "README.md"), "Level-Based Stat Bonuses");
        ModRuntime runtime = new(root, watch: false);
        Publish(runtime);
        return runtime;
    }

    private static GameplayTuning Effective(ModRuntime runtime) => GameplayTuningPolicy.FromCatalog(runtime.Catalog).Apply(new());

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
