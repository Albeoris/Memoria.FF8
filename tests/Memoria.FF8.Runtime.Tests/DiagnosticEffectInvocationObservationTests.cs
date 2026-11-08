using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace Memoria.FF8.Runtime.Tests;

public sealed class DiagnosticEffectInvocationObservationTests
{
    [Fact]
    public void Actual_entered_parameters_are_observed_while_the_published_graphics_view_is_stale()
    {
        var (battle, dispatch, entered, published) = Fixture();
        Assert.Throws<InvalidOperationException>(() => battle.ObserveDiagnosticEffectInvocation(dispatch));
        BattleDiagnosticEffectInvocation? observed = null;
        Publish(battle, dispatch, entered, current =>
        {
            Assert.Same(published, battle.NativeGraphics!.OriginalEffect);
            Assert.Equal((byte)2, battle.NativeGraphics.OriginalEffect!.Parameters!.Parameter80);
            observed = battle.ObserveDiagnosticEffectInvocation(current);
            Assert.Equal((byte)8, observed.Parameter80);
            Assert.Equal((byte)0, observed.Parameter82);
            Assert.Equal((ushort)274, observed.RequestedEffect);
            Assert.Equal(0, observed.HeaderIndex);
            Assert.Throws<InvalidDataException>(() => battle.ObserveDiagnosticActionPackets(current));
        });
        Assert.Equal((byte)8, observed!.Parameter80);
        Assert.Same(published, battle.NativeGraphics!.OriginalEffect);
        Assert.Throws<InvalidOperationException>(() => battle.ObserveDiagnosticEffectInvocation(dispatch));
    }

    [Fact]
    public void Invocation_observation_rejects_a_copied_dispatch_another_thread_and_unwinds_after_failure()
    {
        var (battle, dispatch, entered, _) = Fixture();
        Assert.Throws<InvalidDataException>(() => Publish(battle, dispatch, entered, current =>
        {
            Assert.Throws<InvalidOperationException>(() => battle.ObserveDiagnosticEffectInvocation(current with { }));
            Exception? failure = null;
            Thread worker = new(() =>
            {
                try
                {
                    battle.ObserveDiagnosticEffectInvocation(current);
                }
                catch (Exception error)
                {
                    failure = error;
                }
            });
            worker.Start();
            Assert.True(worker.Join(TimeSpan.FromSeconds(5)));
            Assert.IsType<InvalidOperationException>(failure);
            throw new InvalidDataException("Bounded observer failure.");
        }));
        Assert.Throws<InvalidOperationException>(() => battle.ObserveDiagnosticEffectInvocation(dispatch));
        Publish(battle, dispatch, entered, current => Assert.Equal((byte)8, battle.ObserveDiagnosticEffectInvocation(current).Parameter80));
    }

    private static (BattleSession Battle, BattleDiagnosticEffectDispatch Dispatch, NativeBattleOriginalEffectState Entered, NativeBattleOriginalEffectState Published) Fixture()
    {
        BattleSession battle = new(new BattleDefinition { NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(), Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true, ModelId = 1 }, new() { Id = "enemy", Name = "Enemy" }] }, BattleAtbMode.Normal);
        battle.DiagnosticEffectDispatched += _ =>
        {
        };
        NativeBattleActionPacketsState packets = battle.NativeActionPackets;
        (packets, _) = packets.Prepare(new(0, 241, 0, -1, 65532, 0, null));
        (packets, _) = packets.AppendContact(new(new(3, 0, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        packets = packets.WritePlayerRepeatMode(0, 1).ActivateHeader(0);
        typeof(BattleSession).GetField("nativeActionPackets", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(battle, packets);
        typeof(BattleSession).GetProperty(nameof(BattleSession.NativeHostCallbacks))!.SetValue(battle, 485L);
        typeof(BattleSession).GetField("diagnosticDispatchSerial", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(battle, 5L);
        Invoke(battle, "ObserveDiagnosticPreparedHeader", 0);
        NativeBattleOriginalEffectState entered = new(274, 273, new(274, 0x6897E0, 0x6897F0), new(0, 0, 8, 0, 0, 0) { Parameter80 = 8, Parameter82 = 0 }, 0);
        NativeBattleOriginalEffectState published = entered with { Parameters = entered.Parameters! with { Parameter80 = 2, Parameter81 = 1 } };
        NativeBattleGraphicsState graphics = new(Enumerable.Range(0, 7).Select(index => new NativeBattleGraphicsModel(index, 3)).ToImmutableArray(), Enumerable.Repeat<string?>(null, 7).ToImmutableArray(), 0, 0) { OriginalEffect = published };
        typeof(BattleSession).GetProperty(nameof(BattleSession.NativeGraphics))!.SetValue(battle, graphics);
        BattleDiagnosticEffectDispatch dispatch = new(5, 485, "party", BattleAbility.NativeCommand, 65532, 274, 274, 241) { NativeActorIndex = 0, NativeTargetMask = 8, NativeHeaderIndex = 0, NativeHeaderGeneration = 1 };
        return (battle, dispatch, entered, published);
    }

    private static void Publish(BattleSession battle, BattleDiagnosticEffectDispatch dispatch, NativeBattleOriginalEffectState entered, Action<BattleDiagnosticEffectDispatch> observer) => Invoke(battle, "PublishDiagnosticEnteredEffect", observer, dispatch, entered);

    private static void Invoke(BattleSession battle, string name, params object?[] arguments)
    {
        try
        {
            typeof(BattleSession).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(battle, arguments);
        }
        catch (TargetInvocationException error) when (error.InnerException is { } inner)
        {
            ExceptionDispatchInfo.Capture(inner).Throw();
        }
    }
}
