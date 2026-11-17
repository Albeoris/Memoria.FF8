using static Memoria.FF8.Tests.NativeBattleTypedFixture;
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
        Assert.Throws<InvalidOperationException>(() => battle.Presentation!.ObserveDiagnosticEffectInvocation(dispatch));
        BattleDiagnosticEffectInvocation? observed = null;
        Publish(battle, dispatch, entered, current =>
        {
            Assert.Same(published, battle.Presentation!.Graphics.OriginalEffect);
            Assert.Equal((byte)2, battle.Presentation!.Graphics.OriginalEffect!.Parameters!.Parameter80);
            observed = battle.Presentation!.ObserveDiagnosticEffectInvocation(current);
            Assert.Equal((byte)8, observed.Parameter80);
            Assert.Equal((byte)0, observed.Parameter82);
            Assert.Equal((ushort)274, observed.RequestedEffect);
            Assert.Equal(0, observed.HeaderIndex);
            Assert.Throws<InvalidDataException>(() => battle.Presentation!.ObserveDiagnosticActionPackets(current));
        });
        Assert.Equal((byte)8, observed!.Parameter80);
        Assert.Same(published, battle.Presentation!.Graphics.OriginalEffect);
        Assert.Throws<InvalidOperationException>(() => battle.Presentation!.ObserveDiagnosticEffectInvocation(dispatch));
    }

    [Fact]
    public void Invocation_observation_rejects_a_copied_dispatch_another_thread_and_unwinds_after_failure()
    {
        var (battle, dispatch, entered, _) = Fixture();
        Assert.Throws<InvalidDataException>(() => Publish(battle, dispatch, entered, current =>
        {
            Assert.Throws<InvalidOperationException>(() => battle.Presentation!.ObserveDiagnosticEffectInvocation(current with { }));
            Exception? failure = null;
            Thread worker = new(() =>
            {
                try
                {
                    battle.Presentation!.ObserveDiagnosticEffectInvocation(current);
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
        Assert.Throws<InvalidOperationException>(() => battle.Presentation!.ObserveDiagnosticEffectInvocation(dispatch));
        Publish(battle, dispatch, entered, current => Assert.Equal((byte)8, battle.Presentation!.ObserveDiagnosticEffectInvocation(current).Parameter80));
    }

    private static (BattleSession Battle, BattleDiagnosticEffectDispatch Dispatch, NativeBattleOriginalEffectState Entered, NativeBattleOriginalEffectState Published) Fixture()
    {
        BattleSession battle = new(new BattleDefinition { NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(), Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true, ModelId = 1 }, new() { Id = "enemy", Name = "Enemy" }] }, BattleAtbMode.Normal);
        battle.DiagnosticEffectDispatched += _ =>
        {
        };
        NativeBattleActionPacketsState packets = battle.Presentation!.NativeActionPackets;
        (packets, _) = packets.Prepare(new(OriginalOwner(0), 241, 0, -1, 65532, 0, null));
        (packets, _) = packets.AppendContact(new(new(OriginalOwner(3), 0, 0, 0, 0, 0, 0), null));
        (packets, _) = packets.Publish(1);
        packets = packets.WritePlayerRepeatMode(new BattleActionHeaderReference(0), 1).ActivateHeader(new BattleActionHeaderReference(0));
        typeof(BattleSession).GetField("nativeActionPackets", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(battle, packets);
        typeof(BattleSession).GetProperty(nameof(BattleSession.NativeHostCallbacks))!.SetValue(battle, 485L);
        typeof(BattleSession).GetField("diagnosticDispatchSerial", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(battle, 5L);
        Invoke(battle, "ObserveDiagnosticPreparedHeader", 0);
        NativeBattleOriginalEffectState entered = new(274, 273, new(274, 0x6897E0, 0x6897F0), new(OriginalOwner(0), 0, BattleModelTargets.From([OriginalOwner(3)]), new BattleActionHeaderReference(0), 0, 0) { Parameter80 = 8, Parameter82 = 0 }, 0);
        NativeBattleOriginalEffectState published = entered with { Parameters = entered.Parameters! with { Parameter80 = 2, Parameter81 = 1 } };
        NativeBattleGraphicsState graphics = OriginalUnboundGraphics(_ => new(3)) with { OriginalEffect = published };
        typeof(BattlePresentationProcess).GetProperty(nameof(BattlePresentationProcess.NativeGraphics))!.SetValue(battle.Presentation!, graphics);
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
