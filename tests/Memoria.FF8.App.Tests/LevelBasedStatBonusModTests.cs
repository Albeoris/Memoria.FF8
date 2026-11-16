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
        menu.Execute(new("mod-settings-open", ModRuntime.MemoriaPackageId));
        menu.Execute(new("mod-settings-category", "Battle"));
        string description = Assert.Single(menu.Entries, entry => entry.Label == "Level-Based Stat Bonuses").Help;
        Assert.StartsWith(expected, description);
        Assert.Contains("2970", description);
        Assert.Contains("99", description);
        menu.ConnectLocalizationText((key, fallback) => key == SystemTextCatalog.Key("mods.level-based-stat-bonuses.description") ? "Custom description" : fallback, () => language);
        Assert.Equal("Custom description", Assert.Single(menu.Entries, entry => entry.Label == "Level-Based Stat Bonuses").Help);
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
        for (int ability = 65; ability <= 69; ability++)
        {
            vitals = vitals.TeachGuardianForceAbility(0, ability);
        }
        vitals = vitals.ToggleGuardianForceJunction(0, 0) with { LevelBasedStatBonuses = true };
        vitals.Records[0] = vitals.Records[0] with { Experience = 19_000 };
        GameState state = new() { CharacterVitals = vitals, GameplayPreferences = new() { Tuning = new() { LevelBasedStatBonuses = true } } };
        foreach (string language in new[] { "en", "ru", "ja", "jp", "de", "fr", "it", "es" })
        {
            catalog.InterfaceLanguage = () => language;
            MenuData menu = LiveMenuData.Build(state, catalog: catalog, includeJunctionPlanner: false);
            MenuCharacter character = Assert.Single(menu.Characters, entry => entry.Id == "character-0");
            Assert.True(character.Level > 1);
            for (int ability = 65; ability <= 69; ability++)
            {
                int rate = ability == 65 ? 30 : 1;
                int amount = (character.Level!.Value - 1) * rate;
                string id = ability.ToString(System.Globalization.CultureInfo.InvariantCulture);
                Assert.Equal(catalog.AbilityDescription(ability), catalog.ReadBonusAbilityDescription(ability, false, character.Level));
                string expected = catalog.ReadBonusAbilityDescription(ability, true, character.Level)!;
                Assert.Contains(amount.ToString(System.Globalization.CultureInfo.InvariantCulture), expected);
                Assert.Contains($"+{rate}", expected);
                Assert.Equal(expected, Assert.Single(character.AvailableAbilities, choice => choice.Id == id).Description);
                MenuGuardianForce guardian = Assert.Single(menu.GuardianForces, entry => entry.Id == "gf-0");
                Assert.Equal(expected, Assert.Single(guardian.LearningChoices, choice => choice.Id == id).Description);
            }
            Assert.Equal(catalog.AbilityDescription(64), catalog.ReadBonusAbilityDescription(64, true, character.Level));
            Assert.Equal(catalog.AbilityDescription(70), catalog.ReadBonusAbilityDescription(70, true, character.Level));
        }
        catalog.InterfaceLanguage = () => "en";
        Assert.Equal("Increases Vit by 0 (+1 per level).", catalog.ReadBonusAbilityDescription(67, true, 1));
        Assert.Equal("Increases Vit by 19 (+1 per level).", catalog.ReadBonusAbilityDescription(67, true, 20));
        Assert.Equal("Increases HP by 2970 (+30 per level).", catalog.ReadBonusAbilityDescription(65, true, 100));
        NativeCharacterVitals unassigned = vitals.ToggleGuardianForceJunction(0, 0);
        MenuGuardianForce unassignedGuardian = Assert.Single(LiveMenuData.Build(state with { CharacterVitals = unassigned }, catalog: catalog, includeJunctionPlanner: false).GuardianForces, entry => entry.Id == "gf-0");
        Assert.Equal("Increases Vit by 1 per level.", Assert.Single(unassignedGuardian.LearningChoices, choice => choice.Id == "67").Description);
        catalog.InterfaceLanguage = () => "ru";
        Assert.Equal("Повышает Vit на 19 (+1 за уровень).", catalog.ReadBonusAbilityDescription(67, true, 20));
        string key = SystemTextCatalog.Key("mods.level-based-stat-bonuses.ability.65.current");
        resources.PrepareLocalization(new LocalizationCatalog(new Dictionary<string, LocalizationValue> { [key] = new("Translated HP bonus: {0} (+{1})", "fixture", "fixture") })).Commit();
        Assert.Equal("Translated HP bonus: 570 (+30)", catalog.ReadBonusAbilityDescription(65, true, 20));
        Assert.Equal(catalog.AbilityDescription(65), catalog.ReadBonusAbilityDescription(65, false, 20));
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
