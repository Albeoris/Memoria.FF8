using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Assets;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.BattleEffects.Tests;

public sealed class ScenarioEnemyEffect65RecipeTests
{
    [Fact]
    public void InstalledDefenseIgnoringPhysicalActionDeclaresDefeatHoldBeforeItsFirstInvocation()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory), "Select installed original resources for the native enemy recipe.");
        using IResourceSource source = GameDataSourceFactory.Open(directory);
        ResourceService resources = new(source);
        byte[] kernel = resources.Read(new ResourceKey("main/kernel.bin"));
        NativeCharacterVitals initial = NativeCharacterInitialization.Read(resources.Read(new ResourceKey("main/init.out")), kernel);
        using BattleAssets assets = new(directory, resources);
        BattleEncounter formation = assets.LoadEncounter(510);
        EnemyScenarioModel model = new(124, assets.ReadEnemyName(124), assets.ReadEnemyAi(124, 0));
        EnemyScenarioDiscoveryResult discovery = ScenarioEnemyDiscovery.Discover([formation], [model], assets.NativeActions);
        EnemyScenarioActionOwner owner = discovery.Owners.Single(candidate => candidate.ModelId == 124 && candidate.LevelBand == 2 && candidate.AbilitySlot == 9);
        Assert.Equal((byte)36, owner.NativeAction!.AttackType);
        Assert.Equal((byte)64, owner.NativeAction.Power);
        Assert.Equal((ushort)65, owner.NativeAction.MagicId);
        string template = Path.Combine(Path.GetTempPath(), "enemy-physical-recipe-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            File.WriteAllText(template, """{"fixture":{"characters":[{"row":0,"character_id":0,"stocks":[],"guardian_forces":[0]}]},"driver":{}}""");
            Assert.True(ScenarioDiscovery.TryPrepareEnemyRecipe(assets, initial, NativeBattleExperienceDefinitionReader.Read(kernel), owner, discovery.ScriptedActions, template, out JsonObject? recipe, out _, out _, out string? blocker, discovery.Startups), blocker);
            JsonSerializerOptions json = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
            BattleDiagnosticTerminalHoldDeclaration hold = recipe!["driver"]!["terminal_hold"]!.Deserialize<BattleDiagnosticTerminalHoldDeclaration>(json)!;
            BattleDiagnosticCombatRepair repair = Assert.Single(recipe["driver"]!["repairs"]!.Deserialize<BattleDiagnosticCombatRepair[]>(json)!);
            hold.Validate();
            repair.Validate();
            Assert.True(repair.Revive);
            Assert.Equal("party-0", repair.ActorId);
            Assert.Equal(new[] { repair.ActorId }, hold.PartyActorIds);
        }
        finally
        {
            File.Delete(template);
        }
    }
}
