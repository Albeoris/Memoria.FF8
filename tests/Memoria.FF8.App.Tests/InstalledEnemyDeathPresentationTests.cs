using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class InstalledEnemyDeathPresentationTests
{
    [Fact]
    public void LethalPhysicalAttackRemovesStoppedGrendelWhileTheOtherEnemyRemains()
    {
        const uint stop = 0x8;
        string directory = TestResources.GameDirectory;
        GameSession session = new(ExpandedBattleEffectMatrixTests.PrepareField(directory));
        using GameWorld world = new(session, directory);
        world.EnsureField();
        world.ConfigureGameplayPreferences(session.State.GameplayPreferences with { AdditionalEnemies = 1 });
        BattleSession battle = world.PrepareBattleEntry(700, 0, 41, BattleAtbMode.TurnBased).Battle;
        BattleCombatant attacker = battle.Combatants.Single(actor => actor.Id == "party-0");
        BattleCombatant[] enemies = battle.Combatants.Where(actor => !actor.IsPlayer && actor.IsActive).ToArray();
        Assert.Equal(2, enemies.Length);
        BattleCombatant target = Assert.Single(enemies, actor => actor.ModelId == 53);
        BattleCombatant survivor = Assert.Single(enemies, actor => actor != target);
        BattleModelOwner model = battle.ReadPresentationModelOwner(target.Id);

        AdvanceUntil(() => battle.NativeEntryReady && battle.ActiveAction is null && battle.QueuedActions.Count == 0);
        target.NativeStatus1 |= stop;
        target.NativeStatusTimers[3] = 3000;
        target.CurrentHp = target.MaximumHp;
        Attack();
        Assert.True(target.IsAlive);
        Assert.Equal(stop, target.NativeStatus1 & stop);
        Assert.NotEqual(0u, (ReadModel().Flags8 ?? 0) & 0x8);

        target.CurrentHp = 1;
        Attack();
        Assert.Equal(0, target.CurrentHp);
        Assert.Equal(0u, target.NativeStatus1 & stop);
        Assert.Equal(-1111, target.NativeStatusTimers[3]);
        AdvanceUntil(() => !target.IsActive && !IsModelDrawable());
        Assert.True(survivor.IsAlive);
        Assert.Equal(BattleOutcome.Active, battle.Outcome);
        Assert.Single(battle.Events, item => item.Kind == "Defeated" && item.TargetId == target.Id);

        NativeBattleGraphicsModel ReadModel() => battle.Presentation!.NativeGraphics!.ActorModels.Get(model);

        bool IsModelDrawable()
        {
            return battle.Presentation!.NativeGraphics!.ActorModels.TryGet(model, out NativeBattleGraphicsModel? current) && (current.Flags0 & 2) != 0;
        }

        void Attack()
        {
            int firstEvent = battle.Events.Count;
            attacker.Atb = BattleSession.AtbMaximum;
            battle.QueueAttack(attacker.Id, target.Id);
            AdvanceUntil(() => battle.Events.Skip(firstEvent).Any(item => item.Kind == "ActionComplete" && item.ActorId == attacker.Id));
        }

        void AdvanceUntil(Func<bool> finished)
        {
            for (int callback = 0; callback < 1200 && !finished(); callback++)
            {
                foreach (BattleCombatant actor in battle.Combatants)
                {
                    if (actor != attacker)
                    {
                        actor.Atb = 0;
                    }
                }
                battle.Tick();
            }
            Assert.True(finished(), $"Battle reached frame {battle.Frame}, outcome {battle.Outcome}.");
        }
    }
}
