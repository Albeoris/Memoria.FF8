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
        BattleDiagnosticCounterPrimary primary = new(1, 10, "party", 0, "enemy", 3, 1, 0, 253, 100, 90, 0, 1, recipient);
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
        Assert.Equal(packet, receipt.Packets.ReadContact(contactIndex));
        Assert.NotSame(packets.Headers[0], receipt.Packets.Headers[0]);
        Assert.NotSame(packet, receipt.Packets.ReadContact(contactIndex));
        Assert.Equal(packets.Headers[0].UnknownByteMask, receipt.Packets.Headers[0].UnknownByteMask);
        Assert.Equal(packets.Headers[0].Payload.ToArray(), receipt.Packets.Headers[0].Payload.ToArray());
        Invoke(battle, "ObserveDiagnosticCounterPublishedHeaders", 0);
        Assert.Single(receipts);
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
