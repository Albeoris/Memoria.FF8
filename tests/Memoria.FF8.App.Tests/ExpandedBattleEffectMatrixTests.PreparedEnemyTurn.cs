using Memoria.FF8.Assets;
using Memoria.FF8.BattleEffects;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed partial class ExpandedBattleEffectMatrixTests
{
    [Theory]
    [InlineData(19, 62)]
    [InlineData(47, 215)]
    [InlineData(49, 147)]
    [InlineData(60, 810)]
    [InlineData(126, 439)]
    [InlineData(129, 398)]
    [InlineData(135, 227)]
    [InlineData(155, 163)]
    [InlineData(166, 462)]
    [InlineData(172, 124)]
    [InlineData(213, 284)]
    [InlineData(219, 462)]
    [InlineData(235, 615)]
    [InlineData(257, 326)]
    [InlineData(258, 190)]
    [InlineData(285, 410)]
    [InlineData(313, 161)]
    [InlineData(320, 483)]
    [InlineData(323, 810)]
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
        bool demiSetup = effect.Effect is 135 or 155 or 235;
        bool elnoyle = effect.Effect == 213;
        bool sacred = effect.Effect == 258;
        bool omega = effect.Effect is 166 or 219;
        bool vysage = effect.Effect == 47;
        bool iguion = effect.Effect == 49;
        bool soleEnemy = effect.Effect is 126 or 129;
        bool gim52a = effect.Effect == 313;
        // Granaldo uses its physical Raldo slot checks specifically in formation 62.
        // The high-band Vysage branch checks for both living arms.
        // Iguion checks the shared reaction counter after the party draws Carbuncle.
        // Cockatrice and Grendel have separate branches when one enemy remains.
        // Grendel uses Breath in the middle or high band when it is the sole living enemy.
        // Elastoid's hit callback sets local 220 below three tenths of its maximum HP.
        // Elnoyle's turn callback increments local 220 only after its RNG guard and uses the action at five.
        // Sacred's hit callback increments battle variable 96 while more than one enemy remains.
        // Formation 189 takes the separate solo branch before the shared-counter test.
        // Bahamut's turn callback can reach Mega Flare from six on its local turn counter.
        // Catoblepas reaches Thunder Summon when its cast counter reaches three.
        // A living GIM47N prevents GIM52A from overwriting the hit counter with one.
        // Tiamat spends five turns showing its Dark Flare message before casting.
        (int formation, int model, int pc) = effect.Effect switch
        {
            19 => (62, 95, 54),
            47 => (215, 54, 46),
            49 => (147, 93, 61),
            60 => (810, 137, 215),
            126 => (439, 16, 76),
            129 => (398, 53, 61),
            135 => (227, 26, 114),
            155 => (163, 20, 71),
            166 => (462, 114, 115),
            172 => (124, 29, 65),
            213 => (284, 131, 55),
            219 => (462, 114, 145),
            235 => (615, 41, 73),
            257 => (326, 67, 284),
            258 => (190, 63, 94),
            285 => (410, 133, 45),
            313 => (161, 1, 99),
            320 => (483, 132, 85),
            323 => (810, 136, 114),
            _ => throw new ArgumentOutOfRangeException(nameof(effect))
        };
        if (effect.Effect is not (135 or 155))
        {
            field = field with { GameplayPreferences = field.GameplayPreferences with { Tuning = field.GameplayPreferences.Tuning with { MaximumDamage = 4 } } };
        }
        BattleActionDefinition? demi = null;
        Action<NativeCharacterVital[]>? prepareParty = null;
        if (demiSetup)
        {
            using BattleAssets assets = new(directory);
            demi = Assert.Single(assets.ActionDefinitions, action => action.Ability == BattleAbility.Magic && action.Id == DemiSpellId);
            prepareParty = PrepareDemiStock;
        }
        else if (iguion)
        {
            int draw = field.CharacterVitals!.MenuDefinitions.CommandAbilityCommands.IndexOf(6);
            Assert.True(draw >= 0);
            prepareParty = records => PrepareDrawCommand(records, checked((uint)(20 + draw)));
        }
        List<string> attempted = [];
        string? firstBlocker = null;
        foreach (byte seed in new byte[] { 0, 5, 9 })
        {
            using GameWorld world = StartBattle(directory, field, checked((ushort)formation), demi, nativeSeed: seed, prepareHigherBand: elnoyle || vysage || soleEnemy || gim52a, prepareParty: prepareParty);
            BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
            IEnumerable<BattleCombatant> owners = battle.Combatants.Where(actor => !actor.IsPlayer && actor.ModelId == model);
            BattleCombatant target = owners.First();
            int band = Assert.IsType<NativeBattleAiDefinition>(battle.GetCombatantDefinition(target.Id).NativeAi).LevelBand;
            if ((elnoyle || soleEnemy) && band == 0)
            {
                firstBlocker = "enemy-formation-entry";
                attempted.Add($"seed {seed}: {target.Id} resolved to band {band}.");
                continue;
            }
            EnemyScenarioScriptedAction[] matching = scripts.Where(script => script.ModelId == model && script.Callback == 1 && script.LevelBand == band && (effect.Effect is 19 or 172 or 235 || script.Offset == pc)).ToArray();
            Assert.NotEmpty(matching);
            if (soleEnemy)
            {
                PrepareSoleEnemyByLethalAttacks(world, battle, target);
            }
            if (iguion)
            {
                PrepareIguionDrawAndPartners(world, battle, target);
            }
            // Sacred shares the counter; any Omega can reach the authored late phase first.
            Dictionary<string, EnemyScenarioScriptedAction[]> actors = sacred || omega
                ? battle.Combatants.Where(actor => !actor.IsPlayer && actor.ModelId == model).ToDictionary(actor => actor.Id, _ => matching)
                : new() { [target.Id] = matching };
            EnemyAttackPreparation? preparation = effect.Effect switch
            {
                49 => new(new(formation, model, 6, 0, band), target, battle.Events.Count),
                135 or 155 => new(new(formation, model, 0, effect.Effect == 135 ? 3 : 5, band, UsesDemi: true), target, battle.Events.Count, demi),
                172 => new(new(formation, model, 3, 0, band), target, battle.Events.Count),
                235 => new(new(formation, model, 3, 0, band, UsesDemi: true), target, battle.Events.Count, demi),
                258 => new(new(formation, model, 11, 0, band), target, battle.Events.Count),
                313 => new(new(formation, model, 5, 0, band), target, battle.Events.Count),
                _ => null
            };
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
