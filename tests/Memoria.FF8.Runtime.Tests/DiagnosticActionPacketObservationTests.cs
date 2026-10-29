using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class DiagnosticActionPacketObservationTests
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    private static object? Invoke(BattleSession battle, string name, params object?[] arguments)
    {
        try
        {
            return typeof(BattleSession).GetMethod(name, Instance)!.Invoke(battle, arguments);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is { } inner)
        {
            ExceptionDispatchInfo.Capture(inner).Throw();
            throw;
        }
    }

    private static void Set(BattleSession battle, string field, object value) => typeof(BattleSession).GetField(field, Instance)!.SetValue(battle, value);
    private static string RandomOwners(BattleSession battle) => JsonSerializer.Serialize(new[] { "random", "nativeCrtRandom", "nativeGlobalRandom", "nativeBattleUiRandom" }.ToDictionary(name => name, name => typeof(BattleSession).GetField(name, Instance)!.GetValue(battle)));
    private static void Callback(BattleSession battle, long callback) => typeof(BattleSession).GetProperty(nameof(BattleSession.NativeHostCallbacks), Instance)!.SetValue(battle, callback);

    private static (BattleSession Battle, BattleDiagnosticEffectDispatch Dispatch) Fixture()
    {
        // Component-only packet/read-window fixture; it does not establish installed action admission.
        BattleSession battle = new(new BattleDefinition { NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(), Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }] }, BattleAtbMode.TurnBased);
        battle.DiagnosticEffectDispatched += static _ => { };
        NativeBattleActionPacketsState packets = battle.NativeActionPackets;
        (packets, _) = packets.Prepare(new(0, 14, 0, -1, 101, 188, new(0x47E590, 0, "Control")));
        (packets, _) = packets.AppendContact(new(new(3, 0, 0, 0, 0, 40000, 0) { ExtendedDamageAmount = 40000 }, new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        packets = packets.WritePlayerRepeatMode(0, 1).ActivateHeader(0);
        Set(battle, "nativeActionPackets", packets);
        Callback(battle, 10);
        Set(battle, "diagnosticDispatchSerial", 1L);
        Invoke(battle, "ObserveDiagnosticPreparedHeader", 0);
        BattleDiagnosticEffectDispatch dispatch = new(1, 10, "party", BattleAbility.LimitBreak, 101, 188, 188, 14) { NativeActorIndex = 0, NativeTargetMask = 8, NativeHeaderIndex = 0, NativeHeaderGeneration = 1 };
        return (battle, dispatch);
    }

    private static void Publish(BattleSession battle, BattleDiagnosticEffectDispatch dispatch, Action<BattleDiagnosticEffectDispatch> observer) => Invoke(battle, "PublishDiagnosticEffectDispatch", observer, dispatch);

    [Fact]
    public void Packet_observation_is_scoped_to_the_exact_synchronous_dispatch_object()
    {
        var (battle, dispatch) = Fixture();
        Assert.Throws<InvalidOperationException>(() => battle.ObserveDiagnosticActionPackets(dispatch));
        Publish(battle, dispatch, current =>
        {
            Assert.Throws<InvalidOperationException>(() => battle.ObserveDiagnosticActionPackets(current with { }));
            Exception? workerError = null;
            Thread worker = new(() =>
            {
                try
                {
                    battle.ObserveDiagnosticActionPackets(current);
                }
                catch (Exception exception)
                {
                    workerError = exception;
                }
            });
            worker.Start();
            Assert.True(worker.Join(TimeSpan.FromSeconds(5)));
            Assert.IsType<InvalidOperationException>(workerError);
            Assert.Equal((ushort)8, NativeBattleActionGraphics.Normalize(battle.ObserveDiagnosticActionPackets(current), 0).TargetMask);
        });
        Assert.Throws<InvalidOperationException>(() => battle.ObserveDiagnosticActionPackets(dispatch));
    }

    [Fact]
    public void Packet_observation_detaches_all_backing_arrays_records_and_preserves_native_values_without_side_effects()
    {
        var (battle, dispatch) = Fixture();
        NativeBattleActionPacketsState source = battle.NativeActionPackets;
        string before = JsonSerializer.Serialize(source);
        string random = RandomOwners(battle);
        int events = battle.Events.Count;
        NativeBattleActionPacketsState? captured = null;
        Publish(battle, dispatch, current => captured = battle.ObserveDiagnosticActionPackets(current));
        NativeBattleActionPacketsState copy = Assert.IsType<NativeBattleActionPacketsState>(captured);
        Assert.NotSame(source, copy);
        Assert.NotSame(ImmutableCollectionsMarshal.AsArray(source.Headers), ImmutableCollectionsMarshal.AsArray(copy.Headers));
        Assert.NotSame(ImmutableCollectionsMarshal.AsArray(source.Contacts), ImmutableCollectionsMarshal.AsArray(copy.Contacts));
        for (int index = 0; index < NativeBattleActionPacketsState.HeaderCapacity; index++)
        {
            Assert.NotSame(source.Headers[index], copy.Headers[index]);
            Assert.NotSame(ImmutableCollectionsMarshal.AsArray(source.Headers[index].Payload), ImmutableCollectionsMarshal.AsArray(copy.Headers[index].Payload));
            Assert.Equal(source.Headers[index].UnknownByteMask, copy.Headers[index].UnknownByteMask);
        }
        Assert.NotSame(source.Headers[0].Data, copy.Headers[0].Data);
        Assert.NotSame(source.ReadContact(0), copy.ReadContact(0));
        Assert.NotSame(source.ReadContact(0).Primary, copy.ReadContact(0).Primary);
        Assert.NotSame(source.ReadContact(0).Secondary, copy.ReadContact(0).Secondary);
        Assert.Equal(40000, copy.ReadContact(0).Primary.ExtendedDamageAmount);
        Assert.Equal(before, JsonSerializer.Serialize(copy));
        Assert.Throws<NotSupportedException>(() => copy.Headers[0].ReadByte(12));
        ImmutableCollectionsMarshal.AsArray(copy.Headers[0].Payload)![0] = 2;
        ImmutableCollectionsMarshal.AsArray(copy.Contacts)![0] = null;
        Assert.Equal(before, JsonSerializer.Serialize(source));
        Assert.Equal(before, JsonSerializer.Serialize(battle.NativeActionPackets));
        Assert.Equal(random, RandomOwners(battle));
        Assert.Equal(events, battle.Events.Count);
        Assert.Equal(10L, battle.NativeHostCallbacks);
    }

    [Theory]
    [InlineData("serial")]
    [InlineData("callback")]
    [InlineData("header")]
    [InlineData("generation")]
    public void Packet_observation_rejects_stale_dispatch_ownership(string fault)
    {
        var (battle, original) = Fixture();
        BattleDiagnosticEffectDispatch dispatch = fault == "header" ? original with { NativeHeaderIndex = 1 } : original;
        Publish(battle, dispatch, current =>
        {
            if (fault == "serial")
            {
                Set(battle, "diagnosticDispatchSerial", 2L);
            }
            else if (fault == "callback")
            {
                Callback(battle, 11);
            }
            else if (fault == "generation")
            {
                Invoke(battle, "ObserveDiagnosticPreparedHeader", 0);
            }
            Assert.Throws<InvalidOperationException>(() => battle.ObserveDiagnosticActionPackets(current));
        });
    }

    [Theory]
    [InlineData("actor")]
    [InlineData("command")]
    [InlineData("action")]
    [InlineData("effect")]
    [InlineData("fallback")]
    [InlineData("recipient")]
    public void Packet_observation_requires_exact_native_header_source_fields(string fault)
    {
        var (battle, original) = Fixture();
        BattleDiagnosticEffectDispatch dispatch = fault switch
        {
            "actor" => original with { NativeActorIndex = 1 },
            "command" => original with { NativeCommand = 237 },
            "action" => original with { ActionId = 102 },
            "effect" => original with { RequestedEffect = 192 },
            "fallback" => original with { ExecutedEffect = 192 },
            _ => original with { NativeTargetMask = 16 }
        };
        Publish(battle, dispatch, current => Assert.Throws<InvalidDataException>(() => battle.ObserveDiagnosticActionPackets(current)));
    }

    [Fact]
    public void Missing_native_payload_writer_remains_a_failure_and_exception_closes_the_read_window()
    {
        var (battle, dispatch) = Fixture();
        NativeBattleActionPacketsState packets = battle.NativeActionPackets;
        Set(battle, "nativeActionPackets", packets with { Headers = packets.Headers.SetItem(0, packets.Headers[0] with { UnknownByteMask = packets.Headers[0].UnknownByteMask | (1u << 17) }) });
        Publish(battle, dispatch, current => Assert.Throws<NotSupportedException>(() => battle.ObserveDiagnosticActionPackets(current)));
        Assert.Throws<InvalidOperationException>(() => battle.ObserveDiagnosticActionPackets(dispatch));
        Set(battle, "nativeActionPackets", packets);
        Assert.Throws<IOException>(() => Publish(battle, dispatch, _ => throw new IOException("Observer stopped.")));
        Assert.Throws<InvalidOperationException>(() => battle.ObserveDiagnosticActionPackets(dispatch));
    }
}
