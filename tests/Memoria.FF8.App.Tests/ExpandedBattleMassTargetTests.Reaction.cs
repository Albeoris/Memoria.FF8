using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed partial class ExpandedBattleMassTargetTests
{
    [Fact]
    public void InstalledOilboyleStageTwoCountersTheActualExpandedAttacker()
    {
        using GameWorld world = StartBattle(79, null, false, out GameSession session, 11);
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        BattleCombatant attacker = battle.Combatants.Last(actor => actor.IsPlayer);
        BattleCombatant enemy = battle.Combatants.Last(actor => !actor.IsPlayer);
        Assert.Equal(10, attacker.Slot!.Value.Position);
        Assert.Equal(10, enemy.Slot!.Value.Position);
        Assert.Equal(99, enemy.ModelId);
        NativeBattleAiDefinition program = Assert.IsType<NativeBattleAiDefinition>(enemy.NativeAi);
        Assert.Equal(new byte[] { 2, 0, 200, 1, 5, 0, 18, 0 }, program.Scripts[2][..8]);
        Assert.Equal(new byte[] { 4, 203, 12, 2 }, program.Scripts[2][16..20]);
        NativeBattleAiAbility ability = program.Abilities[16 * program.LevelBand + 2];
        Assert.Equal((byte)8, ability.Command);
        Assert.Equal((ushort)181, ability.Id);

        BattleActionPresentation? reaction = null;
        for (int attempt = 0; attempt < 24 && reaction is null; attempt++)
        {
            Assert.True(enemy.IsAlive);
            enemy.CurrentHp = enemy.MaximumHp / 3;
            attacker.Atb = BattleSession.AtbMaximum;
            world.ApplyBattleInput(new(BattleInputKind.Action) { ActorId = attacker.Id, TargetId = enemy.Id, Ability = BattleAbility.Attack });
            Assert.True(attacker.IsBusy);
            for (int frame = 0; frame < 160; frame++)
            {
                world.ApplyBattleInput(new(BattleInputKind.Tick));
                if (battle.ActiveAction is { NativeAiCallbackStage: 2 } current)
                {
                    reaction = current;
                    break;
                }
                if (!attacker.IsBusy && !enemy.IsBusy && battle.ActiveAction is null)
                {
                    break;
                }
            }
        }

        Assert.NotNull(reaction);
        Assert.Equal(enemy.Id, reaction.ActorId);
        Assert.Equal(attacker.Id, reaction.TargetId);
        Assert.Equal(new[] { attacker.Id }, Assert.IsType<BattleAiRecipients>(reaction.AiRecipients).ActorIds);
        Assert.Equal(ability.Command, reaction.NativeQueuedCommand);
        Assert.Equal(ability.Id, reaction.ActionId);
        Assert.Equal(ability.Sequence, reaction.NativeSequence);

        string path = Path.Combine(Path.GetTempPath(), $"memoria-expanded-reaction-{Guid.NewGuid():N}.json");
        try
        {
            session.Save(path, compact: true);
            using GameWorld restoredWorld = new(new(GameSession.Load(path)), TestResources.GameDirectory);
            restoredWorld.EnsureField();
            BattleSession restored = Assert.IsType<BattleSession>(restoredWorld.Battle);
            Assert.Equal(attacker.Id, Assert.IsType<BattleActionPresentation>(restored.ActiveAction).TargetId);
            BattleEvent contact = FinishReaction(world, battle);
            Assert.Equal(contact, FinishReaction(restoredWorld, restored));
        }
        finally
        {
            File.Delete(path);
        }

        BattleEvent FinishReaction(GameWorld owner, BattleSession active)
        {
            int previousEvents = active.Events.Count;
            for (int frame = 0; frame < 160 && active.ActiveAction is not null; frame++)
            {
                owner.ApplyBattleInput(new(BattleInputKind.Tick));
            }
            Assert.Null(active.ActiveAction);
            BattleEvent contact = Assert.Single(active.Events.Skip(previousEvents), entry => entry.Kind == "EffectContact" && entry.ActorId == enemy.Id);
            Assert.Equal(attacker.Id, contact.TargetId);
            Assert.Equal(ability.Sequence, contact.NativeSequence);
            return contact;
        }
    }
}
