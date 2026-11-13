using Memoria.FF8.Assets;
using Memoria.FF8.BattleEffects;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed partial class ExpandedBattleEffectMatrixTests
{
    [Theory]
    [InlineData(29, 368, 0)]
    [InlineData(152, 137, HigherEnemyBand)]
    [InlineData(239, 221, 0)]
    public void Installed_enemy_turn_follows_genuine_party_attack_setup(int effectId, int formation, int band)
    {
        string directory = TestResources.GameDirectory;
        using GameAssets game = new(directory);
        using BattleAssets assets = new(directory);
        NativeBattleEffectBinding effect = Assert.Single(game.ReadNativeEffectCatalog(), binding => binding.Effect == effectId);
        EnemyScenarioScriptedAction[] scripts = ScenarioEnemyDiscovery.Discover(assets).ScriptedActions.Where(script => script.NativeEffectId == effectId).ToArray();
        EnemyRouteResult result = RunDiscoveredEnemyCast(directory, PrepareField(directory), assets, effect, scripts, probeEnemies: true);
        Assert.True(result.Status is "native" or "logical", result.Detail);
        Assert.Contains($"Formation {formation},", result.Detail);
        Assert.Contains($"band {band},", result.Detail);
        Assert.Contains("genuine Attack setup", result.Detail);
    }

    private static EnemyAttackSetup? AttackSetup(int effect) => effect switch
    {
        // Each prerequisite is read by the selected original callback 1.
        29 => new(368, 3, 0, 3),
        // Formation 137 resolves the original HP curve at the party's level instead of fixing the enemy at level 4.
        152 => new(137, 18, 0, 5, HigherEnemyBand),
        239 => new(221, 45, 5, 0),
        _ => HpTurnSetup(effect)
    };

    private sealed record EnemyAttackSetup(int Formation, int Model, int RequiredHits, int HpTenths, int Band = 0, bool UsesDemi = false, bool QuarterHp = false);

    private sealed class EnemyAttackPreparation(EnemyAttackSetup setup, BattleCombatant target, int firstEvent, BattleActionDefinition? demi = null)
    {
        private const int MaximumAttacks = 24;
        private int submitted;
        private int scannedEvents = firstEvent;
        private int hits;

        public bool HasHit => hits > 0;
        private bool ReachedHpThreshold => setup.QuarterHp ? target.CurrentHp < (target.MaximumHp >> 2) : setup.HpTenths == 0 || target.CurrentHp < setup.HpTenths * (target.MaximumHp / 10);
        private BattleAbility SetupAbility => demi?.Ability ?? BattleAbility.Attack;
        public bool Ready => hits > 0 && hits >= setup.RequiredHits && ReachedHpThreshold;
        public string Describe() => $"{target.Id}, {hits} damaging hit(s), {target.CurrentHp}/{target.MaximumHp} HP after genuine {demi?.Name ?? "Attack"} setup";

        public void SubmitIfReady(GameWorld world, BattleSession battle)
        {
            if (Ready || submitted >= MaximumAttacks || !target.IsAlive || !target.IsActive || target.Untargetable ||
                battle.ActiveAction is { } active && active.Ability == SetupAbility && active.TargetId == target.Id ||
                battle.QueuedActions.Any(action => action.Ability == SetupAbility && action.TargetId == target.Id))
            {
                return;
            }
            if (demi is not null)
            {
                SubmitDemi(world, battle, demi);
                return;
            }
            BattleCombatant? attacker = battle.Combatants.Where(actor => actor.IsPlayer && actor.IsReady && actor.Abilities.Contains(BattleAbility.Attack))
                .OrderBy(actor => actor.Attack).ThenBy(actor => actor.Id, StringComparer.Ordinal).FirstOrDefault();
            if (attacker is null)
            {
                return;
            }
            world.ApplyBattleInput(new(BattleInputKind.Attack) { ActorId = attacker.Id, TargetId = target.Id });
            Assert.Contains(battle.QueuedActions, action => action.ActorId == attacker.Id && action.TargetId == target.Id && action.Ability == BattleAbility.Attack);
            submitted++;
        }

        public void Observe(BattleSession battle)
        {
            IReadOnlyList<BattleEvent> events = battle.Events;
            for (; scannedEvents < events.Count; scannedEvents++)
            {
                BattleEvent entry = events[scannedEvents];
                if (entry is { Kind: "Damage", Amount: > 0 } && entry.Ability == SetupAbility && entry.TargetId == target.Id &&
                    battle.Combatants.Any(actor => actor.IsPlayer && actor.Id == entry.ActorId))
                {
                    hits++;
                }
            }
        }

        private void SubmitDemi(GameWorld world, BattleSession battle, BattleActionDefinition action)
        {
            BattleCombatant? caster = battle.Combatants.FirstOrDefault(actor => actor.IsPlayer && battle.CanChoosePlayerCommand(actor.Id) && actor.MagicStocks.Any(stock => stock.Id == action.Id && stock.Count > 0));
            if (caster is null || submitted >= 5)
            {
                return;
            }
            BattleHudController hud = new(battle, (_, _) => { }, () => { }, () => { }, execute: input => world.ApplyBattleInput(input));
            hud.SelectParty(caster.Id);
            int commandIndex = hud.Commands.ToList().IndexOf(action.Ability);
            Assert.InRange(commandIndex, 0, hud.Commands.Count - 1);
            hud.Choose(commandIndex);
            int actionIndex = hud.ActionChoices.ToList().FindIndex(candidate => candidate.Id == action.Id);
            Assert.InRange(actionIndex, 0, hud.ActionChoices.Count - 1);
            hud.Choose(actionIndex);
            Assert.True(hud.SelectingTarget);
            hud.SelectTarget(target.Id, false);
            Assert.Equal(target.Id, hud.SelectedTarget?.Id);
            hud.Attack();
            Assert.Contains(battle.QueuedActions, queued => queued.ActorId == caster.Id && queued.TargetId == target.Id && queued.Ability == action.Ability && queued.ActionId == action.Id);
            submitted++;
        }
    }
}
