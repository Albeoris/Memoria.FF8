using Memoria.FF8.Assets;
using Memoria.FF8.BattleEffects;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed partial class ExpandedBattleEffectMatrixTests
{
    [Theory]
    [InlineData(166, 462)]
    [InlineData(213, 284)]
    [InlineData(219, 462)]
    [InlineData(257, 326)]
    [InlineData(258, 190)]
    [InlineData(285, 410)]
    [InlineData(320, 483)]
    public void Installed_enemy_turn_reaches_its_original_late_or_hit_counter_branch(int effectId, int formation)
    {
        string directory = TestResources.GameDirectory;
        using GameAssets game = new(directory);
        using BattleAssets assets = new(directory);
        NativeBattleEffectBinding effect = Assert.Single(game.ReadNativeEffectCatalog(), binding => binding.Effect == effectId);
        EnemyScenarioScriptedAction[] scripts = ScenarioEnemyDiscovery.Discover(assets).ScriptedActions.Where(script => script.NativeEffectId == effectId).ToArray();
        EnemyRouteResult result = RunPreparedEnemyTurn(directory, PrepareField(directory), effect, scripts);
        Assert.True(result.Status is "native" or "logical", result.Detail);
        Assert.Contains($"Formation {formation},", result.Detail);
        if (effectId == 213)
        {
            Assert.Matches("band [12],", result.Detail);
        }
        if (effectId == 258)
        {
            Assert.Contains("genuine Attack setup", result.Detail);
        }
    }

    private static EnemyRouteResult RunPreparedEnemyTurn(string directory, GameState field, NativeBattleEffectBinding effect, EnemyScenarioScriptedAction[] scripts)
    {
        bool elnoyle = effect.Effect == 213;
        bool sacred = effect.Effect == 258;
        bool omega = effect.Effect is 166 or 219;
        bool useAreaEnemyRoster = effect.Effect is 285 or 320;
        // Elnoyle's turn callback increments local 220 only after its RNG guard and uses the action at five.
        // Sacred's hit callback increments battle variable 96 while more than one enemy remains.
        // Formation 189 takes the separate solo branch before the shared-counter test.
        // Bahamut's turn callback can reach Mega Flare from six on its local turn counter.
        // Catoblepas reaches Thunder Summon when its cast counter reaches three.
        // Tiamat spends five turns showing its Dark Flare message before casting.
        (int formation, int model, int pc) = effect.Effect switch
        {
            166 => (462, 114, 115),
            213 => (284, 131, 55),
            219 => (462, 114, 145),
            257 => (326, 67, 284),
            258 => (190, 63, 94),
            285 => (410, 133, 45),
            320 => (483, 132, 85),
            _ => throw new ArgumentOutOfRangeException(nameof(effect))
        };
        field = field with { GameplayPreferences = field.GameplayPreferences with { Tuning = field.GameplayPreferences.Tuning with { MaximumDamage = 4 } } };
        List<string> attempted = [];
        string? firstBlocker = null;
        foreach (byte seed in new byte[] { 0, 5, 9 })
        {
            using GameWorld world = StartBattle(directory, field, checked((ushort)formation), nativeSeed: seed, prepareHigherBand: elnoyle, useAreaEnemyRoster: useAreaEnemyRoster);
            BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
            IEnumerable<BattleCombatant> owners = battle.Combatants.Where(actor => !actor.IsPlayer && actor.ModelId == model);
            BattleCombatant target = useAreaEnemyRoster ? owners.First() : owners.Last();
            int band = Assert.IsType<NativeBattleAiDefinition>(battle.GetCombatantDefinition(target.Id).NativeAi).LevelBand;
            if (elnoyle && band == 0)
            {
                firstBlocker = "enemy-formation-entry";
                attempted.Add($"seed {seed}: {target.Id} resolved to band {band}.");
                continue;
            }
            EnemyScenarioScriptedAction script = Assert.Single(scripts, script => script.ModelId == model && script.Callback == 1 && script.LevelBand == band && script.Offset == pc);
            // Sacred shares the counter; any Omega can reach the authored late phase first.
            Dictionary<string, EnemyScenarioScriptedAction[]> actors = sacred || omega
                ? battle.Combatants.Where(actor => !actor.IsPlayer && actor.ModelId == model).ToDictionary(actor => actor.Id, _ => new[] { script })
                : new() { [target.Id] = [script] };
            EnemyAttackPreparation? preparation = sacred ? new(new(formation, model, 11, 0, band), target, battle.Events.Count) : null;
            world.ApplyBattleInput(new(BattleInputKind.SetMode, Mode: BattleAtbMode.Normal));
            EnemyRouteResult result = ObserveEnemyCast(world, battle, effect, actors, preparation, extendOriginalTurns: true);
            if (result.Status is "native" or "logical")
            {
                return result with { Detail = $"Formation {formation}, seed {seed}, {result.Detail}" };
            }
            firstBlocker = result.FirstBlockerStage;
            attempted.Add($"seed {seed}: {result.Detail}");
        }
        return new("blocked", $"Formation {formation}: {string.Join("; ", attempted)}", firstBlocker);
    }
}
