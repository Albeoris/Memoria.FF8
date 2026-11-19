using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Mods;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class GuardianForceRulePublicationTests
{
    [Fact]
    public void AbilityCostAndLocalizedDescriptionFollowTheAcceptedPackageWithoutRewritingProgress()
    {
        const int boost = 91;
        const string packageId = "Memoria/guardian-rule-fixture";
        using IsolatedFieldGame game = new();
        string root = Path.Combine(Path.GetTempPath(), "ff8-guardian-rule-" + Guid.NewGuid().ToString("N"));
        string mods = Path.Combine(root, "Mods");
        string package = Path.Combine(mods, "Memoria", "Fixture");
        string payload = Path.Combine(package, GuardianForceRules.PayloadPath);
        Directory.CreateDirectory(Path.GetDirectoryName(payload)!);
        try
        {
            File.WriteAllText(Path.Combine(package, "mod.json"), """{"id":"guardian-rule-fixture","name":"Guardian rules","package":"Fixtures","description":{"en":"Test guardian rules."}}""");
            File.WriteAllText(payload, """{"schemaVersion":1,"abilities":[{"id":91,"apCost":200,"description":{"en":"Summon damage can exceed 9,999.","ru":"Урон призыва может превышать 9999."}}]}""");
            using GameResourceHost host = new(game.GameDirectory, Path.Combine(root, "Application"), watch: false, modsDirectory: mods);
            using GameAssets assets = new(game.GameDirectory, resources: host.Resources, modelOverrides: host);
            NativeCharacterVitals original = assets.ReadNewGameCharacterVitals().AcquireGuardianForce(0).SelectGuardianForceLearning(0, boost);
            Assert.Equal(10, Boost(original).RequiredPoints);
            GameplaySaveCharacters oldSave = GameplaySaveCharacters.Capture(original.AwardGuardianForceAbilityPoints(0, 9));
            GameSession session = new(GameSession.CreateColdProcess().State with
            {
                Scene = SceneKind.Field,
                FieldId = 999,
                FieldName = "guardian-rule-test",
                CharacterVitals = oldSave.Restore(original)
            });
            using GameWorld world = new(session, game.GameDirectory, resources: host.Resources, sharedAssets: assets, modelOverrides: host, validateWorldPresentation: false);
            Assert.Equal(ModEffectiveStatus.Active, Assert.Single(host.Mods.Catalog.Packages).Status);
            Assert.Equal(200, Boost(session.State.CharacterVitals!).RequiredPoints);
            Assert.Equal(9, Boost(session.State.CharacterVitals!).Progress);

            NativeCharacterVitals partial = session.State.CharacterVitals!.AwardGuardianForceAbilityPoints(0, 190);
            Assert.Equal(199, Boost(partial).Progress);
            Assert.False(Boost(partial).Learned);
            NativeCharacterVitals learned = partial.AwardGuardianForceAbilityPoints(0, 1);
            Assert.True(Boost(learned).Learned);

            LiveMenuCatalog menu = LiveMenuCatalog.ReadInstalled(assets);
            string? nativeDescription = menu.AbilityDescription(boost);
            string language = "ru";
            menu.InterfaceLanguage = () => language;
            menu.FindAbilityDescriptionOverride = (id, locale) => host.GuardianRules.Description(id, locale);
            Assert.Equal("Урон призыва может превышать 9999.", menu.AbilityDescription(boost));
            language = "de";
            Assert.Equal("Summon damage can exceed 9,999.", menu.ReadBonusAbilityDescription(boost, false, null));

            File.WriteAllText(payload, """{"schemaVersion":1,"abilities":[{"id":91,"apCost":256}]}""");
            host.Mods.RequestRefresh();
            Assert.False(host.Pump(force: true));
            world.SynchronizeGameplayTuning();
            Assert.Equal(200, Boost(session.State.CharacterVitals!).RequiredPoints);
            Assert.Contains(host.Mods.Diagnostics, diagnostic => diagnostic.Code == "Memoria.InvalidGuardianForceRules");

            Assert.True(host.Mods.SetEnabled(packageId, false).Success);
            Assert.True(host.Pump(force: true));
            world.SynchronizeGameplayTuning();
            Assert.Equal(10, Boost(session.State.CharacterVitals!).RequiredPoints);
            Assert.Equal(9, Boost(session.State.CharacterVitals!).Progress);
            Assert.Equal(nativeDescription, menu.AbilityDescription(boost));
            Assert.True(Boost(GameplaySaveCharacters.Capture(learned).Restore(original)).Learned);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }

        static NativeGuardianForceAbility Boost(NativeCharacterVitals vitals) => Assert.Single(vitals.GuardianForceAbilities(0), ability => ability.Ability == boost);
    }
}
