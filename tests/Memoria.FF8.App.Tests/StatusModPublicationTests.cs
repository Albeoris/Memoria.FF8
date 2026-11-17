using Memoria.FF8.Assets;
using Memoria.FF8.Mods;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class StatusModPublicationTests
{
    private const string PackageId = "Memoria/memoria-meltdown";

    [Fact]
    public void StatusSettingsPublishAndInvalidReloadRetainsTheCommittedRules()
    {
        using IsolatedFieldGame game = new();
        string root = Path.Combine(Path.GetTempPath(), "ff8-status-policy-" + Guid.NewGuid().ToString("N"));
        string mods = Path.Combine(root, "Mods");
        string payload = WritePackage(mods);
        try
        {
            using GameResourceHost host = new(game.GameDirectory, Path.Combine(root, "Application"), watch: false, modsDirectory: mods);
            Assert.Equal(ModEffectiveStatus.Active, Assert.Single(host.Mods.Catalog.Packages).Status);
            Assert.Equal(50, VitalityReduction(host));
            using GameAssets assets = new(game.GameDirectory, resources: host.Resources, modelOverrides: host);
            GameSession session = new(GameSession.CreateColdProcess().State with
            {
                Scene = SceneKind.Field,
                FieldId = 999,
                FieldName = "status-rule-entry-test",
                CharacterVitals = assets.ReadNewGameCharacterVitals().Rest(),
                CharacterAppearance = assets.ReadNewGameCharacterAppearance(),
                InputConfiguration = assets.ReadNewGameInputConfiguration(),
                Economy = assets.ReadNewGameEconomy(),
                BattleVariables = NativeBattleVariableState.CreateNewGame(),
                GlobalRandom = assets.ReadNewGameGlobalRandom(),
                NativeBattleMemory = assets.ReadColdProcessNativeBattleMemory()
            });
            using GameWorld world = new(session, game.GameDirectory, resources: host.Resources, sharedAssets: assets, modelOverrides: host, validateWorldPresentation: false);
            BattleDefinition entered = world.CreateEncounterBattle(512);
            Assert.Equal(50, VitalityReduction(entered));

            Assert.True(host.Mods.SetBehaviorSetting(PackageId, "status", "memoria-meltdown/Vit0/Vitality", 65).Success);
            Assert.True(host.Pump(force: true));
            Assert.Equal(65, VitalityReduction(host));
            Assert.Equal(65, VitalityReduction(world.CreateEncounterBattle(512)));
            Assert.Equal(50, VitalityReduction(world.CreateEncounterBattle(512, restoredDefinition: entered)));

            File.WriteAllText(payload, """{"id":"Vit0","statModifiers":{"Vitality":{"operation":"ReducePercent","value":101}}}""");
            host.Mods.RequestRefresh();
            Assert.False(host.Pump(force: true));
            Assert.Equal(65, VitalityReduction(host));
            Assert.Contains(host.Mods.Diagnostics, diagnostic => diagnostic.Code == "Memoria.InvalidStatusOverride" && diagnostic.PackageId == PackageId);

            Assert.True(host.Mods.SetEnabled(PackageId, false).Success);
            Assert.True(host.Pump(force: true));
            Assert.Equal(100, VitalityReduction(host));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void HardcoreUsesDeclaredDefaultsAndNormalRestoresPreferences()
    {
        using IsolatedFieldGame game = new();
        string root = Path.Combine(Path.GetTempPath(), "ff8-status-difficulty-" + Guid.NewGuid().ToString("N"));
        string mods = Path.Combine(root, "Mods");
        WritePackage(mods);
        try
        {
            using GameResourceHost host = new(game.GameDirectory, Path.Combine(root, "Application"), watch: false, modsDirectory: mods);
            Assert.True(host.Mods.SetBehaviorSetting(PackageId, "status", "memoria-meltdown/Vit0/Vitality", 80).Success);
            Assert.True(host.Pump(force: true));
            Assert.Equal(80, VitalityReduction(host));
            Assert.True(host.Mods.SetEnabled(PackageId, false).Success);
            Assert.True(host.Pump(force: true));
            Assert.Equal(100, VitalityReduction(host));

            Assert.True(host.SetGameplayDifficulty(BattleDifficulty.Hardcore));
            Assert.Equal(50, VitalityReduction(host));
            Assert.Equal(25, Modifier(host, BattleStatId.Spirit));
            Assert.False(host.Mods.SetEnabled(PackageId, false).Success);
            Assert.False(host.Mods.SetBehaviorSetting(PackageId, "status", "memoria-meltdown/Vit0/Vitality", 60).Success);

            Assert.True(host.SetGameplayDifficulty(BattleDifficulty.Normal));
            Assert.Equal(100, VitalityReduction(host));
            Assert.True(host.Mods.SetEnabled(PackageId, true).Success);
            Assert.True(host.Pump(force: true));
            Assert.Equal(80, VitalityReduction(host));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static int VitalityReduction(GameResourceHost host) => Modifier(host, BattleStatId.Vitality);

    private static int VitalityReduction(BattleDefinition battle)
    {
        return StatusStatRules.MaximumPercent - StatusStatRules.Apply(StatusStatRules.Resolve(battle.StatusStatModifiers), BattleStatId.Vitality, StatusStatRules.MaximumPercent, 0, NativeBattleParameterRules.DefenseReductionStatus);
    }

    private static int Modifier(GameResourceHost host, BattleStatId stat)
    {
        return Assert.Single(StatusStatRules.Resolve(host.GameplayPolicy.StatusStatModifiers), rule => rule.Status == NativeStatusId.Vit0 && rule.Stat == stat).Value;
    }

    private static string WritePackage(string mods)
    {
        string root = Path.Combine(mods, "Memoria", "Status tuning");
        string path = Path.Combine(root, "statuses", "defense.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(Path.Combine(root, "mod.json"), """{"id":"memoria-meltdown","name":"Meltdown","package":"Memoria","description":{"en":"Tweaks Vit 0."}}""");
        File.WriteAllText(path, """{"id":"Vit0","statModifiers":{"Vitality":{"operation":"ReducePercent","value":{"default":50}},"Spirit":{"operation":"ReducePercent","value":{"default":25}}}}""");
        return path;
    }
}
