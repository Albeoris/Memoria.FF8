using System.Reflection;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class ExpandedBattleNativeGraphicTests
{
    [Theory]
    [InlineData(29, 110)]
    [InlineData(164, 248)]
    public void Installed_original_ai_graphic_completes_on_the_attached_process(ushort formation, ushort effectId)
    {
        string directory = TestResources.GameDirectory;
        GameSession session = new(ExpandedBattleEffectMatrixTests.PrepareField(directory));
        session.SetExamTimer(session.ExamTimer!.SetRemaining(600));
        using GameWorld world = new(session, directory);
        world.EnsureField();
        Assert.True(world.Apply(new(PlayerActionKind.Wait, Frames: 4)).Accepted);
        ExpandedBattleEntryAdmissionTests.SelectFourAllies(world, session);
        string source = world.Events.Capture().GetProperty("ProgramHash").GetString()!;
        session.SetRandomEncounters(new(true, NativeRandomEncounterState.FromColdProcess(), new(SceneKind.Field, 133, world.Events.Frame, formation, source)));
        world.ApplyBattleInput(new(BattleInputKind.StartEncounter, Mode: BattleAtbMode.TurnBased, NativeSeed: 4));

        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        ExpandedBattleEntryAdmissionTests.ReachReady(world, battle);
        ExpandedBattlePresentationProcess process = Assert.IsType<ExpandedBattlePresentationProcess>(battle.ExpandedPresentationProcess);
        BattleSession presentation = Assert.IsType<BattleSession>(typeof(ExpandedBattlePresentationProcess).GetField("presentation", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(process));
        Assert.Equal(4, battle.Combatants.Count(actor => actor.IsPlayer));
        BattleCombatant attacker = battle.Combatants.Single(actor => actor.Id == "party-3");
        BattleCombatant trigger = battle.Combatants.Single(actor => !actor.IsPlayer && actor.ModelId == (effectId == 110 ? 74 : 101));
        if (effectId == 110)
        {
            BattleCombatant wedge = battle.Combatants.Single(actor => !actor.IsPlayer && actor.ModelId == 73);
            Assert.True(wedge.Suspended);
            Strike(trigger, 1);
            for (int callback = 0; callback < 1200 && (wedge.Suspended || battle.ActiveAction is not null || battle.QueuedActions.Count != 0); callback++)
            {
                Advance();
            }
            Assert.True(trigger.IsAlive);
            Assert.False(wedge.Suspended);
        }

        Strike(trigger, effectId == 110 ? 1 : trigger.MaximumHp / 10 + 1);
        long? actionToken = null;
        string? casterId = null;
        int contextCount = 0;
        bool drewEffect = false;
        for (int callback = 0; callback < 1600 && (actionToken is null || !process.IsActionComplete(actionToken.Value)); callback++)
        {
            Advance();
            if (process.Graphics.OriginalEffect is { Parameters: { } parameters } effect && effect.Binding.Effect == effectId && effect.ReturnedDescriptor is > 0)
            {
                actionToken ??= Assert.IsType<long>(process.LastStepActionToken);
                Assert.Equal(actionToken, process.LastStepActionToken);
                NativeBattleActionHeaderSlot header = presentation.NativeActionPackets.ReadHeader(parameters.Header);
                Assert.Equal((byte)252, header.Command);
                Assert.Equal((ushort)1, header.Action);
                Assert.Equal(effectId, header.Effect);
                casterId = Assert.IsType<BattleEnemyActorModel>(header.Source).Actor.ActorId;
                contextCount = header.ReadContactCount();
                Assert.True(contextCount > 0);
                if (effectId == 248)
                {
                    Assert.Equal(71, battle.Combatants.Single(actor => actor.Id == casterId).ModelId);
                    NativeBattleContactPacket packet = Assert.Single(presentation.NativeActionPackets.ReadHeaderContacts(parameters.Header));
                    Assert.Equal(header.Source, packet.Primary.Recipient);
                }
                Assert.Equal(contextCount, battle.Events.Count(entry => entry.Kind == "EffectContact" && entry.NativeGraphicMagicId == effectId));
                Assert.Empty(process.LastFrameServices!.ReadOrderedReceipts().OfType<NativeBattlePresentationContactReceived>());
                drewEffect |= !effect.PresentationPackets.IsEmpty;
            }
        }
        Assert.True(process.IsActionComplete(Assert.IsType<long>(actionToken)));
        Assert.NotNull(casterId);
        Assert.True(drewEffect);
        Assert.Equal(contextCount, battle.Events.Count(entry => entry.Kind == "EffectContact" && entry.NativeGraphicMagicId == effectId));
        Assert.DoesNotContain(battle.Events, entry => entry.Kind == "Damage" && entry.NativeGraphicMagicId == effectId);

        BattleCombatant nextTarget = battle.Combatants.First(actor => !actor.IsPlayer && actor.IsAlive && actor.SceneSlot >= 8);
        int nextEvent = battle.Events.Count;
        Strike(nextTarget, nextTarget.CurrentHp);
        for (int callback = 0; callback < 1600 && !CompletedNextAttack(); callback++)
        {
            Advance();
        }
        Assert.True(CompletedNextAttack());

        bool CompletedNextAttack() => battle.Events.Skip(nextEvent).Any(entry => entry.Kind == "ActionComplete" && entry.ActorId == attacker.Id && entry.TargetId == nextTarget.Id);

        void Strike(BattleCombatant target, int hp)
        {
            target.CurrentHp = hp;
            attacker.Atb = BattleSession.AtbMaximum;
            world.ApplyBattleInput(new(BattleInputKind.Attack, ActorId: attacker.Id, TargetId: target.Id));
        }

        void Advance()
        {
            world.ApplyBattleInput(new(BattleInputKind.Tick));
            Assert.Same(process, battle.ExpandedPresentationProcess);
        }
    }
}
