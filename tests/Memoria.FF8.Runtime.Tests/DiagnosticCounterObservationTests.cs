using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class DiagnosticCounterObservationTests
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;

    private static object? Invoke(object owner, string method, params object?[] arguments)
    {
        try
        {
            return owner.GetType().GetMethod(method, Instance)!.Invoke(owner, arguments);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is { } inner)
        {
            ExceptionDispatchInfo.Capture(inner).Throw();
            throw;
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Current_action_publication_retains_typed_attacker_and_detaches_committed_packets(bool preliminary)
    {
        BattleSession battle = Fixture();
        BattleModelOwner source = battle.ReadPresentationModelOwner("party");
        BattleModelOwner target = battle.ReadPresentationModelOwner("enemy");
        BattleModelRoster roster = new([source, target]);
        NativeBattleModelAliases aliases = new(roster, [(NativeBattleModelSlot.Decode(0), source), (NativeBattleModelSlot.Decode(3), target)]);
        BattlePresentationProcess process = NativeBattleHostedTestFixture.SeedProcess(battle, NativeBattleHostedTestFixture.Graphics(roster, aliases, _ => new(3)));
        List<BattleDiagnosticCounterContact> receipts = [];
        battle.DiagnosticCounterContactPublished += receipts.Add;
        var (control, _, original) = Resolve(observed: false);
        (BattleSession candidate, BattleActionPresentation action, NativeBattleContactPacket packet) = Resolve(observed: true);
        Assert.Equal(original, packet);
        Assert.Equal(State(control), State(candidate));
        NativeBattleActionPacketsState packets = process.ActionPackets;
        NativeBattleActionHeaderPreparation header = new(source, 1, 13, -1, 0, 0, null);
        (packets, BattleActionHeaderReference reference) = packets.Prepare(header);
        (packets, BattleContactOccurrence occurrence) = packets.AppendContact(packet);
        NativeBattleHostedTestFixture.SetField(process, "nativeActionPackets", packets);
        Invoke(battle, "PublishDiagnosticCounterContacts", candidate, process, ImmutableArray<BattleActionHeaderReference>.Empty, null);
        Assert.Empty(receipts);

        (packets, _) = packets.Publish(1);
        packets = packets.WritePlayerRepeatMode(reference, 1);
        NativeBattleHostedTestFixture.SetField(process, "nativeActionPackets", packets);
        Invoke(process, "ObserveDiagnosticCommittedHostedHeaders", ImmutableArray.Create(reference));
        BattleSession.ExpandedHostedInvocation invocation = new(action, header, [new("enemy", packet)], null, BattleSession.ExpandedHostedContactDisposition.RequiredImpacts) { HeaderReference = reference };
        Set(battle, "expandedOperationCursor", new BattleSession.ExpandedOperationCursor([invocation], Current: 0));
        string before = State(battle);
        ImmutableArray<BattleActionHeaderReference> appended = preliminary ? [reference] : [];
        Invoke(battle, "PublishDiagnosticCounterContacts", candidate, process, appended, preliminary ? null : reference);
        Assert.Equal(before, State(battle));
        BattleDiagnosticCounterContact receipt = Assert.Single(receipts);
        Assert.Equal(occurrence.Ordinal, receipt.ContactIndex);
        Assert.Equal(preliminary, receipt.Primary.Preliminary);
        Assert.Equal(!preliminary, receipt.TriggerRecomputed);
        Assert.Equal(preliminary ? (byte)1 : (byte)253, receipt.Primary.InternalCommand);
        Assert.Equal(preliminary ? (byte)3 : (byte)0, receipt.Primary.Recipient.LastAction!.AttackType);
        Assert.Equal("party", receipt.Primary.Recipient.LastAttackerId);
        Assert.Null(receipt.Primary.Recipient.LastAction.AttackerIndex);
        Assert.Equal(receipt.Primary.BeforeHp, receipt.Primary.AfterHp);
        NativeBattleRetainedActionPackets retained = packets.ExportRetained();
        Assert.NotSame(retained.Headers[0], receipt.Packets.Headers[0]);
        Assert.Equal((byte)0, receipt.Packets.Headers[0].ReadOriginalByte(17));
        NativeBattleRawContactPacket raw = Assert.IsType<NativeBattleRawContactPacket>(retained.Contacts[occurrence.Ordinal]);
        NativeBattleRawContactPacket detached = Assert.IsType<NativeBattleRawContactPacket>(receipt.Packets.Contacts[receipt.ContactIndex]);
        Assert.NotSame(raw, detached);
        Assert.Equal(NativeBattleContactPacketCodec.Encode(raw), NativeBattleContactPacketCodec.Encode(detached));
        Assert.Equal(retained.Headers[0].UnknownByteMask, receipt.Packets.Headers[0].UnknownByteMask);
        Assert.Equal(retained.Headers[0].Payload.ToArray(), receipt.Packets.Headers[0].Payload.ToArray());

        (BattleSession Session, BattleActionPresentation Command, NativeBattleContactPacket Packet) Resolve(bool observed)
        {
            BattleSession owner = new(new BattleDefinition
            {
                NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
                Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true, ModelId = 0, WeaponId = 0, Attack = 20 }, new() { Id = "enemy", Name = "Enemy", ModelId = 84, MaximumHp = 1000, CurrentHp = 1000, Defense = 5, NativeStatus0 = 4, NativeAi = new(Enumerable.Range(0, 5).Select(_ => new byte[] { 0 }).ToArray(), Enumerable.Repeat(new NativeBattleAiAbility(0, 0, 0), 48).ToArray(), 0, []) }]
            }, BattleAtbMode.TurnBased);
            Set(owner, "diagnosticCounterTracksContacts", observed);
            BattleCombatant actor = owner.Combatants[0];
            BattleCombatant recipient = owner.Combatants[1];
            recipient.NativeRemainingHits = 1;
            byte command = preliminary ? (byte)1 : (byte)253;
            BattleNativeAction native = new(command, 0, -1, -1) { MagicId = 0, AttackType = 10, Power = 20, HitCount = 1, AttackFlags = 0, TargetFlags = 0, PowerModifier = 0, LevelModifier = 0, DamagePresentation = 4, Element = 0, ElementPercent = 0, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, Hit = 255, Critical = 0, RawRecord = [], SupplementalRecord = [] };
            BattleActionPresentation action = new(actor.Id, recipient.Id, BattleActionPhase.Windup, new BattleTargetRequest(new BattleTargetSelection.Explicit([new BattleEnemyActorReference(recipient.Id)]), BattleTargetEligibility.SavedStatus, false, BattleTargetReplacement.None), BattleAbility.Attack, 0) { NativeAction = native, NativeQueuedCommand = 1, GunbladeTriggerTiming = 1 };
            BattleActionDefinition definition = new(BattleAbility.Attack, 0, "Component contact", BattleActionEffect.Native, 20, false) { NativeAction = native };
            Invoke(owner, "SetNativeActionEvent", actor, (byte)2);
            NativeBattleActionScratch scratch = (NativeBattleActionScratch)typeof(BattleSession).GetField("nativeActionScratch", Instance)!.GetValue(owner)!;
            Set(owner, "nativeActionScratch", scratch with { ActorIndex = 0, Command = command, Action = 0, DeferredEffectsE00 = false });
            Set(owner, "resolvingNativeAction", action);
            Set(owner, "nativeReactionOrder", (byte)0);
            Set(owner, "nativeActionCommitDepth", 1);
            var resolved = (NativeBattleContactPacket)Invoke(owner, "ResolveInstalledNativeHitWithContact", actor, recipient, action, definition, native, null!, preliminary)!;
            Set(owner, "nativeActionCommitDepth", 0);
            typeof(BattleSession).GetField("resolvingNativeAction", Instance)!.SetValue(owner, null);
            return (owner, action, resolved);
        }
    }
    private static void Set(BattleSession battle, string field, object value) => typeof(BattleSession).GetField(field, Instance)!.SetValue(battle, value);
    private static BattleSession Fixture() => new(new BattleDefinition { NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(), Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }] }, BattleAtbMode.TurnBased);
    private static string State(BattleSession battle) => JsonSerializer.Serialize(battle.Capture());

    [Fact]
    public void Typed_callback_observations_keep_actor_slot_and_distinct_repeated_epochs()
    {
        BattleSession battle = Fixture();
        List<BattleDiagnosticCounterCallbackObservation> observations = [];
        battle.DiagnosticCounterCallbackObserved += observations.Add;
        BattleExpandedAiCallbackRequest request = new(new BattleEnemyActorReference("enemy"), 2);
        string before = State(battle);
        for (int index = 1; index <= 12; index++)
        {
            Observe(BattleParticipantCallbackPhase.Queued);
            Observe(BattleParticipantCallbackPhase.Started);
            Observe(BattleParticipantCallbackPhase.Completed);
            BattleDiagnosticCounterCallbackObservation[] current = observations.TakeLast(3).ToArray();
            Assert.Equal(new[] { BattleDiagnosticCounterCallbackPhase.Queued, BattleDiagnosticCounterCallbackPhase.Started, BattleDiagnosticCounterCallbackPhase.Completed }, current.Select(item => item.Phase));
            Assert.All(current, item =>
            {
                Assert.Equal(index, item.Queue.Epoch);
                Assert.Equal("enemy", item.Queue.ActorId);
                Assert.Equal(BattleRosterSlot.Enemy(0), item.Queue.Slot);
                Assert.Equal((byte)3, item.Queue.ActorIndex);
                Assert.Equal((ushort)2, item.Queue.Stage);
            });
        }
        Assert.Equal(before, State(battle));

        void Observe(BattleParticipantCallbackPhase phase)
        {
            Invoke(battle, "ObserveDiagnosticParticipantCallback", new BattleParticipantCallbackReceipt(request, phase, []));
        }
    }
}
