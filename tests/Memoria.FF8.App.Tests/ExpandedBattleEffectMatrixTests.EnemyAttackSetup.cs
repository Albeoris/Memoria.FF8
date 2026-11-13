using Memoria.FF8.Assets;
using Memoria.FF8.BattleEffects;
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

    private sealed record EnemyAttackSetup(int Formation, int Model, int RequiredHits, int HpTenths, int Band = 0);

    private sealed class EnemyAttackPreparation(EnemyAttackSetup setup, BattleCombatant target, int firstEvent)
    {
        private const int MaximumAttacks = 24;
        private int submitted;
        private int scannedEvents = firstEvent;
        private int hits;

        public bool HasHit => hits > 0;
        public bool Ready => hits > 0 && hits >= setup.RequiredHits && (setup.HpTenths == 0 || target.CurrentHp < setup.HpTenths * (target.MaximumHp / 10));
        public string Describe() => $"{target.Id}, {hits} damaging hit(s), {target.CurrentHp}/{target.MaximumHp} HP after genuine Attack setup";

        public void SubmitIfReady(GameWorld world, BattleSession battle)
        {
            if (Ready || submitted >= MaximumAttacks || !target.IsAlive || !target.IsActive || target.Untargetable ||
                battle.ActiveAction is { Ability: BattleAbility.Attack, TargetId: var activeTarget } && activeTarget == target.Id ||
                battle.QueuedActions.Any(action => action.Ability == BattleAbility.Attack && action.TargetId == target.Id))
            {
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
                if (entry is { Kind: "Damage", Ability: BattleAbility.Attack, Amount: > 0 } && entry.TargetId == target.Id &&
                    battle.Combatants.Any(actor => actor.IsPlayer && actor.Id == entry.ActorId))
                {
                    hits++;
                }
            }
        }
    }
}
