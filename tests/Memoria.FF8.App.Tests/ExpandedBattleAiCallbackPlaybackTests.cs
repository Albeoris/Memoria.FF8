using System.Reflection;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class ExpandedBattleAiCallbackPlaybackTests
{
    private const byte Demi = 13;
    private const ushort DemiEffect = 107;
    private const string AddedAlly = "party-3";

    [Fact]
    public void Synthetic_turn_commits_two_installed_Demi_actions_and_one_completion_on_the_attached_process()
    {
        string directory = TestResources.GameDirectory;
        using GameAssets game = new(directory);
        GameSession field = new(ExpandedBattleEffectMatrixTests.PrepareField(directory) with { GlobalRandom = new(917) });
        field.SetExamTimer(field.ExamTimer!.SetRemaining(600));
        using GameWorld world = new(field, directory, sharedAssets: game);
        ExpandedBattleInstalledEntry.Prepare(world, field, session => ExpandedBattleEntryAdmissionTests.SelectFourAllies(world, session));
        GameWorld.PreparedBattleEntry entry = world.PrepareBattleEntry(ExpandedBattleInstalledEntry.Formation, flags: 0, seed: ExpandedBattleInstalledEntry.Seed, BattleAtbMode.TurnBased);
        BattleDefinition definition = entry.Battle.ReadDefinition();
        BattleNativeAction demi = Assert.Single(definition.NativeActions, action => action.Command == 2 && action.ActionId == Demi && action.CrisisLevel is null);
        Assert.Equal("Demi", demi.Name);
        Assert.True(demi is { MagicId: DemiEffect, AttackType: 8, HitCount: 1 });

        BattleCombatantDefinition owner = Assert.Single(definition.Combatants, actor => !actor.IsPlayer && actor.SceneSlot is >= 0 and < ExpandedBattleFormation.OriginalEnemyPrototypeCount);
        // Target the fourth party member. The second cast is reached only after actual HP falls below 90%.
        byte[] turn = [4, 3, 3, Demi, 6, 2, 0, 3, 1, 9, 0, 3, 0, 3, Demi, 6, 0];
        BattleCombatantDefinition[] actors = definition.Combatants.Select(actor => actor.IsPlayer ? actor : actor with
        {
            NativeAi = (actor.NativeAi ?? throw new InvalidDataException("The installed test enemy is missing its original AI records.")) with { Scripts = [[0], actor.Id == owner.Id ? turn : [0], [0], [0], [0]] }
        }).ToArray();
        BattleSession battle = GameWorld.CreateLiveBattleSession(definition with { Combatants = actors }, field.State.GameplayPreferences with { AtbMode = BattleAtbMode.TurnBased }, game, world.BattleResources);
        world.CommitPreparedBattleEntry(entry with { Battle = battle });
        BattlePresentationProcess process = Assert.IsType<BattlePresentationProcess>(battle.Presentation);
        for (int callback = 0; callback < 1200 && (!process.EntryReady || battle.QueuedActions.Count > 0 || battle.ActiveAction is not null); callback++)
        {
            foreach (BattleCombatant actor in battle.Combatants)
            {
                actor.Atb = 0;
            }
            battle.Tick();
        }
        Assert.True(process.EntryReady);
        Assert.Equal(4, battle.Combatants.Count(actor => actor.IsPlayer));
        Assert.Equal(5, battle.Combatants.Count(actor => !actor.IsPlayer));
        Assert.Empty(battle.QueuedActions);
        Assert.Null(battle.ActiveAction);

        BattleSession presentation = Assert.IsType<BattleSession>(typeof(BattlePresentationProcess).GetField("presentation", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(process));
        BattleCombatant enemy = battle.Combatants.Single(actor => actor.Id == owner.Id);
        BattleCombatant target = battle.Combatants.Single(actor => actor.Id == AddedAlly);
        BattleModelOwner enemyOwner = battle.ReadPresentationModelOwner(enemy.Id);
        BattleModelOwner targetOwner = battle.ReadPresentationModelOwner(target.Id);
        Assert.False(presentation.NativeActionPackets.ModelAliases.TryEncodeExact(targetOwner, out _));
        Assert.Equal(0u, target.NativeCombatantFlags & 0x10000);
        Assert.Equal((byte)0, enemy.AiTurnCount);
        int initialHp = target.CurrentHp;
        Assert.True(initialHp >= target.MaximumHp / 10 * 9);
        int firstDamage = demi.Power * initialHp / 16;
        int secondDamage = demi.Power * (initialHp - firstDamage) / 16;
        Assert.True(firstDamage > 0 && secondDamage > 0 && initialHp - firstDamage < target.MaximumHp / 10 * 9);
        var initialStock = target.MagicStocks.Select(stock => (stock.Id, stock.Count)).ToArray();
        var enemyHealth = battle.Combatants.Where(actor => !actor.IsPlayer).Select(actor => actor.CurrentHp).ToArray();
        int events = battle.Events.Count;
        enemy.Atb = BattleSession.AtbMaximum;

        BattleContactOccurrence[]? expected = null;
        double? eventGauge = null;
        byte? reactionOrder = null;
        HashSet<long> tokens = [];
        List<NativeBattleContactPacket> delivered = [];
        List<NativeBattlePresentationCompletionRequested> completions = [];
        for (int callback = 0; callback < 1800 && !Completed(battle, events, enemy.Id); callback++)
        {
            battle.Tick();
            Assert.Same(process, battle.Presentation);
            if (process.LastFrameServices is { } services)
            {
                if (process.LastStepActionToken is long token)
                {
                    tokens.Add(token);
                    delivered.AddRange(services.ReadOrderedReceipts().OfType<NativeBattlePresentationContactReceived>().Select(contact => contact.Packet));
                }
                completions.AddRange(services.ReadOrderedReceipts().OfType<NativeBattlePresentationCompletionRequested>().Where(receipt => receipt.ActorId == enemy.Id));
            }
            if (battle.ActiveAction?.ExpandedAiCallback is { Stage: 1 } request && request.Actor.ActorId == enemy.Id)
            {
                BattleSnapshot snapshot = battle.Capture();
                if (expected is null)
                {
                    Assert.Equal(initialHp - firstDamage - secondDamage, target.CurrentHp);
                    NativeBattleActionPacketsState packets = presentation.NativeActionPackets;
                    var headers = packets.PublishedHeaders().Where(entry => entry.Slot.Source == enemyOwner).ToArray();
                    Assert.Equal(2, headers.Length);
                    Assert.All(headers, entry =>
                    {
                        Assert.Equal((ushort)Demi, entry.Slot.Action);
                        Assert.Equal(DemiEffect, entry.Slot.Effect);
                        Assert.Equal(1, entry.Slot.ReadContactCount());
                        Assert.Equal((byte)0, entry.Slot.ReadRepeatModeMinusOne());
                    });
                    expected = headers.Select(entry => packets.ResolveContact(entry.Reference, 0)).ToArray();
                    Assert.NotEqual(expected[0], expected[1]);
                    eventGauge = enemy.Atb;
                    reactionOrder = snapshot.NativeReactionOrder;
                    Assert.NotNull(reactionOrder);
                }
                Assert.Equal(1u, snapshot.NativeClock?.ActionPause);
                Assert.Equal(eventGauge, enemy.Atb);
                Assert.Equal(reactionOrder, snapshot.NativeReactionOrder);
                Assert.True(enemy.IsBusy);
                Assert.DoesNotContain(battle.Events.Skip(events), entry => entry.Kind == "ActionComplete");
            }
        }

        Assert.NotNull(expected);
        Assert.Equal(expected.Select(occurrence => (BattleContactOccurrence?)occurrence), delivered.Select(packet => packet.Occurrence));
        Assert.All(delivered, packet => Assert.Equal(targetOwner, packet.Primary.Recipient));
        Assert.Equal(new[] { firstDamage, secondDamage }, delivered.Select(packet => packet.Primary.PresentedAmount));
        Assert.Equal(2, tokens.Count);
        Assert.All(tokens, token => Assert.True(process.IsActionComplete(token)));
        NativeBattlePresentationCompletionRequested complete = Assert.Single(completions);
        Assert.Equal(0x47DE70u, complete.Callback);
        Assert.Equal(enemyOwner, complete.Owner);
        Assert.True(process.IsCompletionAcknowledged(complete.Token));
        Assert.Single(battle.Events.Skip(events), entry => entry.Kind == "ActionComplete" && entry.ActorId == enemy.Id);
        Assert.Equal((byte)1, enemy.AiTurnCount);
        Assert.False(enemy.IsBusy);
        Assert.Equal(0d, enemy.Atb);
        Assert.Equal(0u, battle.Capture().NativeClock?.ActionPause);
        Assert.Equal(initialHp - firstDamage - secondDamage, target.CurrentHp);
        Assert.Equal(initialStock, target.MagicStocks.Select(stock => (stock.Id, stock.Count)));
        Assert.Equal(enemyHealth, battle.Combatants.Where(actor => !actor.IsPlayer).Select(actor => actor.CurrentHp));

        target.Atb = BattleSession.AtbMaximum;
        int next = battle.Events.Count;
        battle.QueueAttack(target.Id, enemy.Id);
        for (int callback = 0; callback < 1200 && !Completed(battle, next, target.Id); callback++)
        {
            battle.Tick();
            Assert.Same(process, battle.Presentation);
        }
        Assert.Equal(enemy.Id, Assert.Single(battle.Events.Skip(next), entry => entry.Kind == "EffectContact" && entry.ActorId == target.Id).TargetId);
        Assert.Single(battle.Events.Skip(next), entry => entry.Kind == "ActionComplete" && entry.ActorId == target.Id);
        Assert.All(battle.Combatants.Where(actor => !actor.IsPlayer), actor => Assert.True(actor.IsAlive));
    }

    private static bool Completed(BattleSession battle, int start, string actorId)
    {
        return battle.Events.Skip(start).Any(entry => entry.Kind == "ActionComplete" && entry.ActorId == actorId);
    }
}
