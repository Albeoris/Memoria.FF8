using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class DiagnosticCounterObservationTests
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;

    private static object? Invoke(BattleSession battle, string method, params object?[] arguments)
    {
        try
        {
            return typeof(BattleSession).GetMethod(method, Instance)!.Invoke(battle, arguments);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is { } inner)
        {
            ExceptionDispatchInfo.Capture(inner).Throw();
            throw;
        }
    }

    private static void Set(BattleSession battle, string field, object value) => typeof(BattleSession).GetField(field, Instance)!.SetValue(battle, value);
    private static BattleSession Fixture() => new(new BattleDefinition { NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(), Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }] }, BattleAtbMode.TurnBased);
    private static string State(BattleSession battle) => JsonSerializer.Serialize(battle.Capture());

    [Fact]
    public void Callback_observations_are_owned_by_physical_bank_slot_epoch_not_decoded_callback_identity()
    {
        BattleSession battle = Fixture();
        List<BattleDiagnosticCounterCallbackObservation> observations = [];
        battle.DiagnosticCounterCallbackObserved += observations.Add;
        BattleCombatant actor = battle.Combatants[1];
        HashSet<NativeBattleCommandLocation> locations = [];
        bool reused = false;
        for (int index = 0; index < 12; index++)
        {
            Invoke(battle, "QueueNativeCallback", actor, (ushort)2, (byte)0);
            var queued = observations[^1];
            Assert.Equal(BattleDiagnosticCounterCallbackPhase.Queued, queued.Phase);
            reused |= !locations.Add(queued.Queue.Location);
            NativeBattleCommandPacket source = battle.NativeCommandBanks.Banks[queued.Queue.Location.Bank].Packets[2 * queued.Queue.Location.Slot];
            string before = State(battle);
            Invoke(battle, "ObserveDiagnosticCounterCallbackTaken", queued.Queue.Location, source);
            Assert.Equal(before, State(battle));
            Assert.Equal(BattleDiagnosticCounterCallbackPhase.Taken, observations[^1].Phase);
            Assert.Equal(queued.Queue.Epoch, observations[^1].Queue.Epoch);
            // The native dispatcher constructs a fresh callback from this packet; no logical object identity is needed.
            BattleNativeCallback decoded = new(source.ReadByte(0), source.ReadWord(4));
            Assert.Equal(queued.Queue.ActorIndex, decoded.ActorIndex);
            Assert.Equal(queued.Queue.Stage, decoded.Stage);
            Set(battle, "nativeDispatchCommandLocation", queued.Queue.Location);
            Invoke(battle, "ReleaseNativeDispatchedCommand");
        }
        Assert.True(reused);
        Assert.Equal(Enumerable.Range(1, 12).Select(value => (long)value), observations.Where(value => value.Phase == BattleDiagnosticCounterCallbackPhase.Queued).Select(value => value.Queue.Epoch));
    }

    [Fact]
    public void Callback_epoch_rejects_changed_physical_payload_and_is_not_erased_by_action_header_reset()
    {
        BattleSession battle = Fixture();
        List<BattleDiagnosticCounterCallbackObservation> observations = [];
        battle.DiagnosticCounterCallbackObserved += observations.Add;
        Invoke(battle, "QueueNativeCallback", battle.Combatants[1], (ushort)2, (byte)0);
        BattleDiagnosticCounterCallback queue = observations.Single().Queue;
        NativeBattleCommandPacket packet = battle.NativeCommandBanks.Banks[queue.Location.Bank].Packets[2 * queue.Location.Slot];
        Invoke(battle, "ResetDiagnosticReflectionHeaders");
        Assert.Throws<InvalidDataException>(() => Invoke(battle, "ObserveDiagnosticCounterCallbackTaken", queue.Location, packet with { Bytes = packet.Bytes.SetItem(4, 3) }));
    }

    [Fact]
    public void Normal_header_reset_preserves_the_actual_queued_callback_epoch()
    {
        BattleSession battle = Fixture();
        List<BattleDiagnosticCounterCallbackObservation> observations = [];
        battle.DiagnosticCounterCallbackObserved += observations.Add;
        Invoke(battle, "QueueNativeCallback", battle.Combatants[1], (ushort)2, (byte)0);
        BattleDiagnosticCounterCallback queue = observations.Single().Queue;
        NativeBattleCommandPacket packet = battle.NativeCommandBanks.Banks[queue.Location.Bank].Packets[2 * queue.Location.Slot];
        Invoke(battle, "ResetDiagnosticReflectionHeaders");
        Invoke(battle, "ObserveDiagnosticCounterCallbackTaken", queue.Location, packet);
        Assert.Equal(queue.Epoch, observations[^1].Queue.Epoch);
        Assert.Equal(BattleDiagnosticCounterCallbackPhase.Taken, observations[^1].Phase);
    }

    [Fact]
    public void Contact_publication_waits_for_repeat_writer_and_detaches_native_packet_storage()
    {
        // Component-only source publication fixture; it does not admit any installed counter action.
        BattleSession battle = Fixture();
        List<BattleDiagnosticCounterContact> receipts = [];
        battle.DiagnosticCounterContactPublished += receipts.Add;
        NativeBattleActionPacketsState packets = battle.NativeActionPackets;
        (packets, _) = packets.Prepare(new(0, 1, 4, -1, 0, 0, new(0x47E590, 0, "Control")));
        Set(battle, "nativeActionPackets", packets);
        Invoke(battle, "ObserveDiagnosticPreparedHeader", 0);
        NativeBattleContactPacket packet = new(new(3, 4, 0, 0, 0, 10, 0), new(255, 0, 0, 0, 0, 0, 0));
        (packets, int contactIndex) = packets.AppendContact(packet);
        Set(battle, "nativeActionPackets", packets);
        BattleDiagnosticCounterActor recipient = new("enemy", 3, 84, 90, true, 0, new(0, 0, 1, 0, 0, 0), new(2, 0), [4, 0, 0, 0, 0, 0, 0, 0]);
        BattleDiagnosticCounterPrimary primary = new(1, 10, "party", 0, "enemy", 3, 1, 0, 1, 100, 90, 0, 1, recipient, false);
        object sidecar = typeof(BattleSession).GetField("diagnosticCounterContacts", Instance)!.GetValue(battle)!;
        sidecar.GetType().GetMethod("Add")!.Invoke(sidecar, [packet, primary]);
        Invoke(battle, "ObserveDiagnosticCounterAppend", packet, contactIndex, false);
        Assert.Empty(receipts);
        (packets, _) = packets.Publish(1);
        packets = packets.WritePlayerRepeatMode(0, 1);
        Set(battle, "nativeActionPackets", packets);
        string before = State(battle);
        Invoke(battle, "ObserveDiagnosticCounterPublishedHeaders", 0);
        Assert.Equal(before, State(battle));
        BattleDiagnosticCounterContact receipt = Assert.Single(receipts);
        Assert.Equal((byte)0, receipt.Packets.Headers[0].ReadByte(17));
        Assert.Equal(contactIndex, receipt.ContactIndex);
        Assert.False(receipt.Primary.Preliminary);
        Assert.Equal(packet, receipt.Packets.ReadContact(contactIndex));
        Assert.NotSame(packets.Headers[0], receipt.Packets.Headers[0]);
        Assert.NotSame(packet, receipt.Packets.ReadContact(contactIndex));
        Assert.Equal(packets.Headers[0].UnknownByteMask, receipt.Packets.Headers[0].UnknownByteMask);
        Assert.Equal(packets.Headers[0].Payload.ToArray(), receipt.Packets.Headers[0].Payload.ToArray());
        Invoke(battle, "ObserveDiagnosticCounterPublishedHeaders", 0);
        Assert.Single(receipts);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Actual_resolver_parameter_classifies_identical_zero_damage_internal253_without_native_changes(bool preliminary)
    {
        // Declared component contacts exercise the actual resolver; no original-resource admission is claimed.
        BattleSession Resolve(bool observed, out NativeBattleContactPacket packet, out BattleDiagnosticCounterPrimary? primary)
        {
            BattleSession battle = new(new BattleDefinition
            {
                NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
                Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true, ModelId = 0, WeaponId = 0, Attack = 20 }, new() { Id = "enemy", Name = "Enemy", ModelId = 84, MaximumHp = 1000, CurrentHp = 1000, Defense = 5, NativeStatus0 = 4, NativeAi = new(Enumerable.Range(0, 5).Select(_ => new byte[] { 0 }).ToArray(), Enumerable.Repeat(new NativeBattleAiAbility(0, 0, 0), 48).ToArray(), 0, []) }]
            }, BattleAtbMode.TurnBased);
            if (observed)
            {
                battle.DiagnosticCounterContactPublished += _ => { };
            }
            BattleCombatant actor = battle.Combatants[0];
            BattleCombatant target = battle.Combatants[1];
            // The single-contact component declares the same BD9 count that its native hit plan initializes.
            typeof(BattleCombatant).GetProperty("NativeRemainingHits", Instance)!.SetValue(target, (byte)1);
            BattleNativeAction native = new(253, 0, -1, -1) { MagicId = 0, AttackType = 10, Power = 20, HitCount = 1, AttackFlags = 0, TargetFlags = 0, PowerModifier = 0, LevelModifier = 0, DamagePresentation = 4, Element = 0, ElementPercent = 0, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, Hit = 255, Critical = 0, RawRecord = [], SupplementalRecord = [] };
            BattleActionPresentation presentation = new(actor.Id, target.Id, BattleActionPhase.Windup, 0, BattleAbility.Attack, 0) { NativeAction = native, NativeQueuedCommand = 1, NativeHeaderIndex = 0, GunbladeTriggerTiming = 1 };
            BattleActionDefinition action = new(BattleAbility.Attack, 0, "Component contact", BattleActionEffect.Native, 20, false) { NativeAction = native };
            (NativeBattleActionPacketsState prepared, _) = battle.NativeActionPackets.Prepare(new(0, 1, 13, -1, 0, 0, null));
            Set(battle, "nativeActionPackets", prepared);
            Invoke(battle, "ObserveDiagnosticPreparedHeader", 0);
            Invoke(battle, "SetNativeActionEvent", actor, (byte)2);
            NativeBattleActionScratch scratch = (NativeBattleActionScratch)typeof(BattleSession).GetField("nativeActionScratch", Instance)!.GetValue(battle)!;
            Set(battle, "nativeActionScratch", scratch with { ActorIndex = 0, Command = 253, Action = 0, DeferredEffectsE00 = false });
            Set(battle, "resolvingNativeAction", presentation);
            Set(battle, "diagnosticCounterProducingHeader", 0);
            Set(battle, "nativeReactionOrder", (byte)0);
            Set(battle, "nativeActionCommitDepth", 1);
            packet = (NativeBattleContactPacket)Invoke(battle, "ResolveInstalledNativeHitWithContact", actor, target, presentation, action, native, (byte)255, preliminary)!;
            var table = (System.Runtime.CompilerServices.ConditionalWeakTable<NativeBattleContactPacket, BattleDiagnosticCounterPrimary>)typeof(BattleSession).GetField("diagnosticCounterContacts", Instance)!.GetValue(battle)!;
            table.TryGetValue(packet, out primary);
            Set(battle, "nativeActionCommitDepth", 0);
            typeof(BattleSession).GetField("resolvingNativeAction", Instance)!.SetValue(battle, null);
            return battle;
        }
        BattleSession control = Resolve(false, out NativeBattleContactPacket original, out _);
        BattleSession observed = Resolve(true, out NativeBattleContactPacket packet, out BattleDiagnosticCounterPrimary? primary);
        Assert.NotNull(primary);
        Assert.Equal(preliminary, primary.Preliminary);
        Assert.Equal((byte)253, primary.InternalCommand);
        Assert.Equal(primary.BeforeHp, primary.AfterHp);
        Assert.Equal((ushort)0, packet.Primary.Amount6);
        Assert.Equal(preliminary ? (byte)3 : (byte)0, primary.Recipient.LastAction!.AttackType);
        Assert.Equal(original, packet);
        Assert.Equal(State(control), State(observed));
    }

    [Fact]
    public void Observer_subscription_does_not_change_queued_native_packets_or_rng()
    {
        BattleSession control = Fixture();
        BattleSession observed = Fixture();
        observed.DiagnosticCounterCallbackObserved += _ => { };
        Invoke(control, "QueueNativeCallback", control.Combatants[1], (ushort)2, (byte)0);
        Invoke(observed, "QueueNativeCallback", observed.Combatants[1], (ushort)2, (byte)0);
        Assert.Equal(State(control), State(observed));
    }
}
