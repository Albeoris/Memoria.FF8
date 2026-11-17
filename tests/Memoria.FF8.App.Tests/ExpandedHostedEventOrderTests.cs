using System.Collections.Immutable;
using System.Reflection;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class ExpandedHostedEventOrderTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Installed_routed_attack_keeps_native_completion_before_the_counter_and_appends_poison_after_it(bool poisoned)
    {
        string directory = TestResources.GameDirectory;
        using GameAssets game = new(directory);
        GameSession field = new(ExpandedBattleEffectMatrixTests.PrepareField(directory) with { GlobalRandom = new(917) });
        field.SetExamTimer(field.ExamTimer!.SetRemaining(600));
        using GameWorld world = new(field, directory, sharedAssets: game);
        ExpandedBattleInstalledEntry.Prepare(world, field, session => ExpandedBattleEntryAdmissionTests.SelectFourAllies(world, session));
        GameWorld.PreparedBattleEntry entry = world.PrepareBattleEntry(ExpandedBattleInstalledEntry.Formation, flags: 0, seed: ExpandedBattleInstalledEntry.Seed, BattleAtbMode.TurnBased);
        BattleDefinition definition = entry.Battle.ReadDefinition();
        BattleNativeAction demi = Assert.Single(definition.NativeActions, action => action.Command == 2 && action.ActionId == 13 && action.CrisisLevel is null);
        Assert.Equal("Demi", demi.Name);
        BattleCombatantDefinition original = Assert.Single(definition.Combatants, actor => !actor.IsPlayer && actor.SceneSlot is >= 0 and < ExpandedBattleFormation.OriginalEnemyPrototypeCount);
        BattleCombatantDefinition support = definition.Combatants.Where(actor => !actor.IsPlayer && actor.SceneSlot >= 8).MaxBy(actor => actor.MaximumHp)!;
        EncounterCompiledDefinition route = new("fixture.encounter", "Classic", "fixture-generation", original.Id, 11,
            ImmutableDictionary<string, EncounterCompiledPolicy>.Empty, ImmutableDictionary<string, string>.Empty, ImmutableDictionary<string, EncounterCompiledTemplate>.Empty, ImmutableDictionary<string, ImmutableArray<string>>.Empty, ImmutableDictionary<string, EncounterCompiledPresentation>.Empty,
            ImmutableDictionary<string, EncounterCompiledDamageRoute>.Empty.Add("share", new("share", "original", [support.Id])), ImmutableDictionary<string, EncounterCompiledEventGate>.Empty, ImmutableDictionary<string, EncounterCompiledPlacement>.Empty, ImmutableDictionary<string, double>.Empty);
        BattleCombatantDefinition[] actors = definition.Combatants.Select(actor => actor.IsPlayer ? actor : actor with
        {
            NativeAi = (actor.NativeAi ?? throw new InvalidDataException("The installed fixture is missing an enemy's original AI records.")) with
            {
                Scripts = [[0], [0], [0], [0], actor.Id == support.Id ? [4, 203, 3, 13, 6, 0] : [0]]
            }
        }).ToArray();
        BattleSession battle = GameWorld.CreateLiveBattleSession(definition with { Combatants = actors, EncounterMod = route }, field.State.GameplayPreferences with { AtbMode = BattleAtbMode.TurnBased }, game, world.BattleResources);
        world.CommitPreparedBattleEntry(entry with { Battle = battle });
        BattlePresentationProcess process = Assert.IsType<BattlePresentationProcess>(battle.Presentation);
        BattleSession presentation = ReadPresentation(process);
        DrainEntry(battle, process);

        BattleCombatant caster = battle.Combatants.Single(actor => actor.Id == "party-3");
        BattleCombatant recipient = battle.Combatants.Single(actor => actor.Id == original.Id);
        BattleCombatant counter = battle.Combatants.Single(actor => actor.Id == support.Id);
        BattleModelOwner casterOwner = battle.ReadPresentationModelOwner(caster.Id);
        BattleModelOwner counterOwner = battle.ReadPresentationModelOwner(counter.Id);
        int casterHp = caster.CurrentHp;
        int originalHp = recipient.CurrentHp;
        int counterHp = counter.CurrentHp;
        int demiDamage = demi.Power * casterHp / 16;
        var stock = caster.MagicStocks.Select(item => (item.Id, item.Count)).ToArray();
        if (poisoned)
        {
            caster.NativeStatus0 |= 2;
        }
        caster.Atb = BattleSession.AtbMaximum;
        int firstEvent = battle.Events.Count;
        battle.QueueAttack(caster.Id, recipient.Id);
        var order = new List<string>();
        var selfDamage = new List<int>();
        var completions = new List<NativeBattlePresentationCompletionRequested>();
        BattleActionHeaderReference? counterHeader = null;
        NativeBattleRandomState? producedRandom = null;
        for (int frame = 0; frame < 2400 && (!BattleCompletedAction(battle, firstEvent, caster.Id) || battle.ActiveAction is not null); frame++)
        {
            battle.Tick();
            Assert.Same(process, battle.Presentation);
            if (counterHeader is null && presentation.NativeActionPackets.PublishedHeaders().FirstOrDefault(header => header.Slot.Source == counterOwner && header.Slot.Action == demi.ActionId) is { Slot: not null } produced)
            {
                counterHeader = produced.Reference;
                producedRandom = Assert.IsType<NativeBattleRandomState>(battle.Capture().Random);
                Assert.Equal(casterHp - demiDamage, caster.CurrentHp);
                Assert.Equal(originalHp, recipient.CurrentHp);
                Assert.InRange(counter.CurrentHp, 1, counterHp - 1);
            }
            foreach (NativeBattlePresentationReceipt receipt in process.LastFrameServices!.ReadOrderedReceipts())
            {
                switch (receipt)
                {
                    case NativeBattlePresentationContactReceived contact:
                        if (contact.Packet.Primary.Recipient == casterOwner)
                        {
                            order.Add("self");
                            selfDamage.Add(contact.Packet.Primary.PresentedAmount);
                        }
                        else
                        {
                            order.Add("attack");
                        }
                        break;
                    case NativeBattlePresentationCompletionRequested completion when completion.Owner == casterOwner:
                        completions.Add(completion);
                        order.Add($"{completion.Callback:X}");
                        if (completion.Callback == 0x47DE70)
                        {
                            BattleActionHeaderReference retained = Assert.IsType<BattleActionHeaderReference>(counterHeader);
                            Assert.Equal(demi.ActionId, presentation.NativeActionPackets.ReadHeader(retained).Action);
                            if (poisoned)
                            {
                                Assert.Equal(new ushort[] { demi.ActionId, 2 }, presentation.NativeActionPackets.PublishedHeaders().Skip(1).Select(header => header.Slot.Action));
                                Assert.DoesNotContain(battle.Events.Skip(firstEvent), item => item.Kind == "ActionComplete" && item.ActorId == caster.Id);
                            }
                            else
                            {
                                Assert.Equal(0, presentation.NativeActionPackets.ActionCount);
                                Assert.False(presentation.NativeActionPackets.HasPublishedHeader(retained));
                                Assert.True(BattleCompletedAction(battle, firstEvent, caster.Id));
                                Assert.NotNull(battle.ActiveAction);
                            }
                        }
                        break;
                }
            }
        }

        Assert.Equal(poisoned ? new[] { "attack", "47DE70", "self", "self", "47E120" } : new[] { "attack", "47DE70", "self" }, order);
        Assert.Equal(poisoned ? 2 : 1, completions.Count);
        Assert.All(completions, completion => Assert.True(process.IsCompletionAcknowledged(completion.Token)));
        Assert.Single(battle.Events.Skip(firstEvent), item => item.Kind == "ActionComplete" && item.ActorId == caster.Id);
        Assert.Null(battle.ActiveAction);
        Assert.Same(caster, battle.Combatants.Single(actor => actor.Id == caster.Id));
        Assert.False(caster.IsBusy);
        Assert.Equal(0d, caster.Atb);
        Assert.Equal(stock, caster.MagicStocks.Select(item => (item.Id, item.Count)));
        NativeBattleRandomState expected = Assert.IsType<NativeBattleRandomState>(producedRandom);
        int poisonDamage = 0;
        if (poisoned)
        {
            expected = expected.Draw(out byte storedRoll);
            poisonDamage = Math.Min((storedRoll % 5 + 5) * caster.MaximumHp / 100, definition.Tuning.MaximumDamage);
        }
        Assert.Equal(poisoned ? new[] { demiDamage, poisonDamage } : new[] { demiDamage }, selfDamage);
        Assert.Equal(casterHp - demiDamage - poisonDamage, caster.CurrentHp);
        Assert.Equal(expected.Indexes.ToArray(), Assert.IsType<NativeBattleRandomState>(battle.Capture().Random).Indexes.ToArray());

        BattleCombatant next = battle.Combatants.First(actor => !actor.IsPlayer && actor.IsAlive && !actor.Untargetable && actor.Id != original.Id && actor.Id != support.Id);
        caster.NativeStatus0 &= 0xFFFD;
        caster.Atb = BattleSession.AtbMaximum;
        Assert.True(caster.IsReady);
        int nextEvent = battle.Events.Count;
        battle.QueueAttack(caster.Id, next.Id);
        for (int frame = 0; frame < 1200 && !BattleCompletedAction(battle, nextEvent, caster.Id); frame++)
        {
            battle.Tick();
            Assert.Same(process, battle.Presentation);
        }
        Assert.Equal(next.Id, Assert.Single(battle.Events.Skip(nextEvent), item => item.Kind == "EffectContact" && item.ActorId == caster.Id).TargetId);
        Assert.True(BattleCompletedAction(battle, nextEvent, caster.Id));
    }

    [Fact]
    public void Installed_expanded_tower_plays_the_first_entrance_before_activation_and_wedge_init()
    {
        string directory = TestResources.GameDirectory;
        using GameAssets game = new(directory);
        GameSession field = new(ExpandedBattleEffectMatrixTests.PrepareField(directory));
        field.SetExamTimer(field.ExamTimer!.SetRemaining(600));
        using GameWorld world = new(field, directory, sharedAssets: game);
        ExpandedBattleInstalledEntry.Prepare(world, field, session => ExpandedBattleEntryAdmissionTests.SelectFourAllies(world, session));
        string source = world.Events.Capture().GetProperty("ProgramHash").GetString()!;
        field.SetRandomEncounters(new(true, NativeRandomEncounterState.FromColdProcess(), new(SceneKind.Field, 133, world.Events.Frame, 29, source)));
        BattleSession battle = ExpandedBattleInstalledEntry.Start(world);
        BattlePresentationProcess process = Assert.IsType<BattlePresentationProcess>(battle.Presentation);
        BattleSession presentation = ReadPresentation(process);
        DrainEntry(battle, process);
        Assert.Equal(4, battle.Combatants.Count(actor => actor.IsPlayer));
        BattleCombatant biggs = Assert.Single(battle.Combatants, actor => !actor.IsPlayer && actor.ModelId == 74);
        BattleCombatant wedge = Assert.Single(battle.Combatants, actor => !actor.IsPlayer && actor.ModelId == 73);
        BattleCombatant caster = battle.Combatants.Single(actor => actor.Id == "party-3");
        BattleModelOwner biggsOwner = battle.ReadPresentationModelOwner(biggs.Id);
        BattleModelOwner wedgeOwner = battle.ReadPresentationModelOwner(wedge.Id);
        Assert.True(wedge.Suspended);
        int start = battle.Events.Count;
        bool entranceObserved = false;
        bool initQueued = false;
        int activations = 0;
        biggs.CurrentHp = 1;
        caster.Atb = BattleSession.AtbMaximum;
        battle.QueueAttack(caster.Id, biggs.Id);
        for (int frame = 0; frame < 1600 && !BattleCompletedAction(battle, start, wedge.Id); frame++)
        {
            battle.Tick();
            Assert.Same(process, battle.Presentation);
            if (!entranceObserved && presentation.NativeActionPackets.PublishedHeaders().FirstOrDefault(header => header.Slot.Command == 0 && header.Slot.Action == 3) is { Slot: not null } entrance)
            {
                entranceObserved = true;
                Assert.Equal(biggsOwner, entrance.Slot.Source);
                Assert.Equal((byte)0, entrance.Slot.Sequence);
                NativeBattleContactHalf contact = Assert.Single(presentation.NativeActionPackets.ReadHeaderContacts(entrance.Reference)).Primary;
                Assert.Equal(wedgeOwner, contact.Recipient);
                Assert.Equal((byte)0, contact.Sequence);
                Assert.Equal((byte)1, contact.Flags2);
                Assert.Equal((ushort)0, contact.Amount6);
                Assert.True(wedge.Suspended);
                Assert.DoesNotContain(battle.Events.Skip(start), item => item.Kind == "Activated" && item.ActorId == wedge.Id);
            }
            foreach (NativeBattleModelParticipantActivated receipt in process.LastFrameServices!.ReadOrderedReceipts().OfType<NativeBattleModelParticipantActivated>().Where(receipt => receipt.Owner == wedgeOwner))
            {
                activations++;
                Assert.True(entranceObserved);
                Assert.Equal(wedge.Id, receipt.ActorId);
                Assert.False(wedge.Suspended);
                Assert.Contains(battle.QueuedActions, action => action.ExpandedAiCallback is { Stage: 0 } request && request.Actor.ActorId == wedge.Id);
                initQueued = true;
            }
        }
        Assert.True(entranceObserved);
        Assert.True(initQueued);
        Assert.Equal(1, activations);
        Assert.Single(battle.Events.Skip(start), item => item.Kind == "Activated" && item.ActorId == wedge.Id);
        Assert.Single(battle.Events.Skip(start), item => item.Kind == "ActionComplete" && item.ActorId == wedge.Id);
        Assert.True(biggs.IsAlive);
        Assert.True(wedge.IsAlive);
    }

    private static void DrainEntry(BattleSession battle, BattlePresentationProcess process)
    {
        for (int frame = 0; frame < 1200 && (!process.EntryReady || battle.ActiveAction is not null || battle.QueuedActions.Count != 0); frame++)
        {
            foreach (BattleCombatant actor in battle.Combatants)
            {
                actor.Atb = 0;
            }
            battle.Tick();
        }
        Assert.True(process.EntryReady);
        Assert.Empty(battle.QueuedActions);
        Assert.Null(battle.ActiveAction);
    }

    private static BattleSession ReadPresentation(BattlePresentationProcess process)
    {
        return Assert.IsType<BattleSession>(typeof(BattlePresentationProcess).GetField("presentation", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(process));
    }

    private static bool BattleCompletedAction(BattleSession battle, int start, string actorId)
    {
        return battle.Events.Skip(start).Any(item => item.Kind == "ActionComplete" && item.ActorId == actorId);
    }
}
