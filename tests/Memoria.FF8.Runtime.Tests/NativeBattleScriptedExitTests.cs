using System.Reflection;
using System.Text.Json;
using static Memoria.FF8.Tests.NativeBattleTypedFixture;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleScriptedExitTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(0x200, true)]
    public void NativeScriptedExitWaitsForItsOwnersAndPublishesKindThreeBeforeFinalizing(ushort mode, bool canReturn)
    {
        const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        FieldInfo scriptState = typeof(BattleSession).GetField("nativeScriptState", privateInstance)!;
        PropertyInfo graphicsState = typeof(BattlePresentationProcess).GetProperty(nameof(BattlePresentationProcess.NativeGraphics))!;
        PropertyInfo activeAction = typeof(BattleSession).GetProperty(nameof(BattleSession.ActiveAction))!;
        MethodInfo considerOutcome = typeof(BattleSession).GetMethod("CompleteBattleIfNeeded", privateInstance)!;
        MethodInfo dispatch = typeof(BattleSession).GetMethod("DispatchFrameCore", privateInstance)!;
        BattleSession battle = new(new()
        {
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            NativeFieldBattleMode = mode,
            Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true, NativeCombatantFlags = 1 }, new() { Id = "enemy", Name = "Enemy" }]
        }, BattleAtbMode.Normal);
        NativeBattleGraphicsState graphics = OriginalGraphics([
            (0, battle.ReadPresentationModelOwner("party"), new(0)),
            (3, battle.ReadPresentationModelOwner("enemy"), new(0))
        ]) with { EntryReady = true, EntryLifecycle = 4 };
        graphicsState.SetValue(battle.Presentation!, graphics);

        considerOutcome.Invoke(battle, null);
        Assert.Null(battle.Presentation!.Graphics.NativeOutcome);
        scriptState.SetValue(battle, battle.NativeScriptState with { ScriptedExitRequested = true, AllocationControl = new(0, true, false) });
        considerOutcome.Invoke(battle, null);
        Assert.Null(battle.Presentation!.Graphics.NativeOutcome);
        scriptState.SetValue(battle, battle.NativeScriptState with { AllocationControl = new(0, false, false) });
        activeAction.SetValue(battle, new BattleActionPresentation("party", "enemy", BattleActionPhase.Recovery, BattleTargetRequest.None));
        considerOutcome.Invoke(battle, null);
        Assert.Null(battle.Presentation!.Graphics.NativeOutcome);
        Assert.Empty(battle.Presentation!.Graphics.Descriptors.Queue);

        activeAction.SetValue(battle, null);
        considerOutcome.Invoke(battle, null);
        NativeBattleGraphicsState pending = battle.Presentation!.Graphics;
        NativeBattleOutcomeState outcome = Assert.IsType<NativeBattleOutcomeState>(pending.NativeOutcome);
        Assert.Equal((byte)1, outcome.ResultByte6E7);
        Assert.Equal((byte)3, outcome.FadeStyleE01);
        Assert.Null(outcome.FadeDurationB0C);
        Assert.Equal(BattleOutcome.Active, battle.Outcome);
        Assert.Equal(new (ushort, uint)[] { (112, 0), (10, 0x47DF60), (10, 0x47DFC0) }, pending.Descriptors.Queue.Select(slot => (pending.Descriptors.Packets[slot].Command, pending.Descriptors.Packets[slot].Argument)));
        considerOutcome.Invoke(battle, null);
        Assert.Same(pending, battle.Presentation?.Graphics);

        // The installed field test drives the real descriptor and fade callbacks. Here, publish only their final byte.
        graphicsState.SetValue(battle.Presentation!, pending with { NativeOutcome = outcome with { FadeDurationB0C = 0 } });
        dispatch.Invoke(battle, [false, false]);
        Assert.Equal(BattleOutcome.ScriptedExit, battle.Outcome);
        Assert.Equal((byte)1, battle.NativeResult);
        Assert.Equal(canReturn, battle.CanReturnToField);
        Assert.Empty(battle.EnemyDefeats);
        Assert.Single(battle.Events, entry => entry.Kind == "Outcome");
    }

    [Fact]
    public void HitCallbackExitWaitsForTheCurrentActionAndRestoresItsPendingRequest()
    {
        NativeBattleAiDefinition ai = new([[0], [0], [0], [0], [57, 0]], Enumerable.Repeat(new NativeBattleAiAbility(0, 0, 0), 48).ToArray(), 0, []);
        BattleSession battle = new(new() { NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(), NativeFieldBattleMode = 0x200, Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true, InitialAtb = BattleSession.AtbMaximum }, new() { Id = "enemy", Name = "Enemy", NativeAi = ai, NativeCombatantFlags = 0x10 }] }, BattleAtbMode.Normal);
        battle.QueueAttack("party", "enemy");
        for (int frame = 0; frame < 100 && !battle.NativeScriptState.ScriptedExitRequested; frame++)
        {
            battle.Tick();
        }
        Assert.True(battle.NativeScriptState.ScriptedExitRequested);
        Assert.Equal(BattleOutcome.Active, battle.Outcome);
        Assert.Equal(BattleActionPhase.Recovery, battle.ActiveAction!.Phase);
        BattleSession restored = BattleSession.Restore(battle.Capture());
        for (int frame = 0; frame < 100 && battle.Outcome == BattleOutcome.Active; frame++)
        {
            battle.Tick();
            restored.Tick();
        }
        Assert.Equal(BattleOutcome.ScriptedExit, battle.Outcome);
        Assert.Null(battle.ActiveAction);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(0x200, true)]
    public void ScriptedExitKeepsNativeResultOneAndDoesNotBecomeVictory(ushort mode, bool canReturn)
    {
        NativeBattleAiDefinition ai = new([[57, 0], [0], [0], [0], [0]], Enumerable.Repeat(new NativeBattleAiAbility(0, 0, 0), 48).ToArray(), 0, []);
        BattleSession battle = new(new() { NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(), NativeFieldBattleMode = mode, Combatants = [new() { Id = "party-0", Name = "Party", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy", NativeAi = ai }] }, BattleAtbMode.Normal);
        battle.Tick();
        Assert.Equal(BattleOutcome.ScriptedExit, battle.Outcome);
        Assert.Equal((byte)1, battle.NativeResult);
        Assert.Equal(canReturn, battle.CanReturnToField);
        Assert.Empty(battle.EnemyDefeats);
        Assert.Empty(battle.QueuedActions);
        Assert.True(battle.Combatants.All(actor => actor.IsAlive));
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(BattleSession.Restore(battle.Capture()).Capture()));
    }
}
