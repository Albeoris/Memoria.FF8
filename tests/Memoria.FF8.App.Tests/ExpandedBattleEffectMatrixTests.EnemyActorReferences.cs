using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.BattleEffects;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed partial class ExpandedBattleEffectMatrixTests
{
    [Theory]
    [InlineData(13, 21)]
    [InlineData(120, 41)]
    [InlineData(235, 615)]
    public void Installed_enemy_turn_preserves_logical_last_attacker(int effectId, int formation)
    {
        string directory = TestResources.GameDirectory;
        using GameAssets game = new(directory);
        using BattleAssets assets = new(directory);
        NativeBattleEffectBinding effect = Assert.Single(game.ReadNativeEffectCatalog(), binding => binding.Effect == effectId);
        EnemyScenarioScriptedAction[] scripts = ScenarioEnemyDiscovery.Discover(assets).ScriptedActions.Where(script => script.NativeEffectId == effectId).ToArray();
        EnemyRouteResult result = RunDiscoveredEnemyCast(directory, PrepareField(directory), assets, effect, scripts, probeEnemies: true, verifyActorReference: true);
        Assert.True(result.Status is "native" or "logical", result.Detail);
        Assert.Contains($"Formation {formation},", result.Detail);
        Assert.Contains("retaliated against", result.Detail);
    }

    private static EnemyActorReferenceSetup? ActorReferenceSetup(int effect) => effect switch
    {
        13 => new(new(21, 14, 1, 5, HigherEnemyBand), 0, 8, 11, 15),
        120 => new(new(41, 6, 0, 0, HigherEnemyBand), 1, 2, 41, 11, UsesFire: true),
        235 => new(new(615, 41, 3, 0), 0, 8, 93, 13),
        _ => null
    };

    private sealed record EnemyActorReferenceSetup(EnemyAttackSetup Attacks, int VariableIndex, int Command, int ActionId, int Sequence, bool UsesFire = false);

    private sealed class EnemyActorReferencePreparation(EnemyActorReferenceSetup setup, BattleCombatant owner, EnemyAttackPreparation attacks, BattleActionDefinition? fire)
    {
        private bool fireSubmitted;
        private string? storedAttacker;
        private bool retaliationQueued;
        private bool retaliationContact;
        private int eventCursor;

        public bool RetaliationContact => retaliationContact;

        public string Describe()
        {
            string source = fire is null ? attacks.Describe() : $"Fire from {CasterId}";
            string observation = retaliationContact ? $"retaliated against {storedAttacker}" : $"stored attacker {storedAttacker ?? "unobserved"}, retaliation pending";
            return $"{source}; {observation}";
        }

        public void SubmitIfReady(GameWorld world, BattleSession battle)
        {
            if (fire is null)
            {
                // Blue Dragon can take a different branch at three hits. Let the first stored reference be consumed first.
                if (!attacks.HasHit || retaliationContact)
                {
                    attacks.SubmitIfReady(world, battle);
                }
                return;
            }

            BattleCombatant caster = Assert.Single(battle.Combatants, actor => actor.Id == CasterId);
            if (fireSubmitted || !caster.IsReady || !owner.IsAlive || !owner.IsActive || owner.Untargetable)
            {
                return;
            }
            BattleHudController hud = new(battle, (_, _) => { }, () => { }, () => { }, execute: input => world.ApplyBattleInput(input));
            hud.SelectParty(caster.Id);
            int command = hud.Commands.ToList().IndexOf(BattleAbility.Magic);
            Assert.InRange(command, 0, hud.Commands.Count - 1);
            hud.Choose(command);
            int action = hud.ActionChoices.ToList().FindIndex(candidate => candidate.Id == fire.Id);
            Assert.InRange(action, 0, hud.ActionChoices.Count - 1);
            hud.Choose(action);
            Assert.True(hud.SelectingTarget);
            hud.SelectTarget(owner.Id, false);
            hud.Attack();
            Assert.Contains(battle.QueuedActions, queued => queued.ActorId == caster.Id && queued.TargetId == owner.Id && queued.Ability == BattleAbility.Magic && queued.ActionId == fire.Id);
            fireSubmitted = true;
        }

        public void Observe(BattleSession battle)
        {
            attacks.Observe(battle);
            if (retaliationContact || !fireSubmitted && !attacks.HasHit)
            {
                return;
            }

            if (storedAttacker is null)
            {
                if (battle.ExpandedEffect is not null)
                {
                    return;
                }
                BattleSnapshot snapshot = battle.Capture();
                NativeBattleParticipantSnapshot? participant = snapshot.Combatants.Single(actor => actor.Id == owner.Id).NativeParticipant;
                if (participant?.ActorReferences is not { } actorReferences || !actorReferences.TryGetValue(setup.VariableIndex, out string? attacker))
                {
                    return;
                }
                Assert.NotNull(attacker);
                Assert.Equal(attacker, participant.LastAttackerId);
                Assert.Equal<byte?>(fire is null ? (byte)0 : (byte)1, participant.LastAction?.AttackType);
                Assert.Contains(battle.Combatants, actor => actor.Id == attacker && actor.IsPlayer);
                if (fire is not null)
                {
                    Assert.Equal(CasterId, attacker);
                }
                string json = JsonSerializer.Serialize(snapshot);
                BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(json)!);
                Assert.Equal(attacker, restored.Capture().Combatants.Single(actor => actor.Id == owner.Id).NativeParticipant!.ActorReferences![setup.VariableIndex]);
                storedAttacker = attacker;
                eventCursor = battle.Events.Count;
            }

            if (battle.ActiveAction is { ActorId: var actorId, Ability: BattleAbility.EnemyAbility, NativeAiCallbackStage: 1 } active && actorId == owner.Id && active.NativeQueuedCommand == setup.Command && active.ActionId == setup.ActionId && active.NativeSequence == setup.Sequence)
            {
                Assert.NotNull(active.AiRecipients);
                Assert.Equal(storedAttacker, Assert.Single(active.AiRecipients.ActorIds));
                retaliationQueued = true;
            }

            IReadOnlyList<BattleEvent> events = battle.Events;
            for (; eventCursor < events.Count; eventCursor++)
            {
                BattleEvent entry = events[eventCursor];
                if (retaliationQueued && entry.ActorId == owner.Id && entry is { Kind: "EffectContact", Ability: BattleAbility.EnemyAbility })
                {
                    Assert.Equal(storedAttacker, entry.TargetId);
                    retaliationContact = true;
                }
                else if (entry.ActorId == owner.Id && entry.Kind == "ActionComplete")
                {
                    retaliationQueued = false;
                }
            }
        }
    }
}
