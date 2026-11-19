using System.Collections.Immutable;
using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeDuelInterruptionLifecycleTests
{
    [Theory]
    [InlineData(BattleNativeDuelPhase.Opening, true, true)]
    [InlineData(BattleNativeDuelPhase.Ready, true, false)]
    [InlineData(BattleNativeDuelPhase.AwaitingEffect, true, true)]
    [InlineData(BattleNativeDuelPhase.Opening, false, false)]
    [InlineData(BattleNativeDuelPhase.Ready, false, true)]
    [InlineData(BattleNativeDuelPhase.AwaitingEffect, false, false)]
    public void Interrupted_foreign_Duel_keeps_escape_and_terminal_interlocks_until_gameplay_and_widget_both_retire(BattleNativeDuelPhase phase, bool knockedOut, bool gameplayFirst)
    {
        BattleNativeLimitDefinition readiness = new(9, 18, 0, 1, new byte[32].ToImmutableArray());
        BattleNativeLimitDefinition duel = readiness with { CharacterId = 1, Command = 11 };
        BattleSession battle = new(new BattleDefinition
        {
            LimitProgress = new(new string('0', 64), 0, 0, 0, 0, new byte[8].ToImmutableArray()) { Options978 = 0 },
            Combatants =
            [
                new() { Id = "actor", Name = "Actor", IsPlayer = true, ModelId = 9, NativeLimit = readiness, NativeLimitFamilies = [new(BattleLimitFamily.Duel, duel)] },
                new() { Id = "survivor", Name = "Survivor", IsPlayer = true },
                new() { Id = "enemy", Name = "Enemy" }
            ]
        }, BattleAtbMode.TurnBased);
        BattleCombatant actor = battle.Combatants.Single(candidate => candidate.Id == "actor");
        actor.IsBusy = true;
        BattleTargetRequest target = new(new BattleTargetSelection.Explicit([new BattleEnemyActorReference("enemy")]), BattleTargetEligibility.Living, false, BattleTargetReplacement.None);
        Dictionary<string, BattleNativeDuelState> owners = (Dictionary<string, BattleNativeDuelState>)Field("nativeDuelProcesses").GetValue(battle)!;
        Dictionary<string, byte> crisis = (Dictionary<string, byte>)Field("nativeLimitLevels").GetValue(battle)!;
        owners.Add(actor.Id, new(0, target, 240, 200, phase) { WidgetPhase = phase == BattleNativeDuelPhase.Opening ? (byte)0 : (byte)4, Selection1D28DEF = 254 });
        crisis.Add(actor.Id, 2);
        if (knockedOut)
        {
            actor.CurrentHp = 0;
            SetKnockedOut(actor);
        }
        else
        {
            actor.Suspended = true;
        }
        BattleCombatant enemy = battle.Combatants.Single(candidate => candidate.Id == "enemy");
        enemy.CurrentHp = 0;
        SetKnockedOut(enemy);
        Invoke(battle, "ReconcileExpandedDuel");
        Assert.Equal(BattleNativeDuelPhase.Finisher, owners[actor.Id].Phase);
        Assert.Equal((byte)255, owners[actor.Id].Selection1D28DEF);
        Assert.Equal((byte)11, owners[actor.Id].WidgetPhase);
        Assert.True(Interlocked(battle));
        battle.PublishNativeEscapeButtons(3);
        Assert.True(battle.Escape.Held);
        Assert.False(battle.Escape.Completed);
        Assert.Equal(BattleOutcome.Active, Invoke(battle, "DetermineOutcome"));
        if (gameplayFirst)
        {
            Invoke(battle, "EndNativeDuel", actor.Id);
            Assert.True(owners[actor.Id].GameplayFinished);
            Assert.True(actor.IsBusy);
            Assert.True(Interlocked(battle));
        }
        int callbacks = 0;
        while (owners.TryGetValue(actor.Id, out BattleNativeDuelState? state) && !state.WidgetClosed && callbacks++ < 40)
        {
            object facts = Invoke(battle, "ReadNativeDuelWidgetFacts")!;
            BattleNativeDuelState next = NativeBattleDuelRules.AdvanceWidget(state with { PendingPulse = 0 }, true, 0, false, [], () => throw new InvalidOperationException("Closing must not draw UI randomness."));
            Type updateType = typeof(BattleSession).Assembly.GetType("Memoria.FF8.Runtime.NativeBattleDuelWidgetUpdated")!;
            Invoke(battle, "ApplyNativeDuelWidgetUpdate", Activator.CreateInstance(updateType, facts, next)!);
        }
        Assert.InRange(callbacks, 1, 40);
        if (!gameplayFirst)
        {
            Assert.True(owners[actor.Id].WidgetClosed);
            Assert.True(actor.IsBusy);
            Assert.True(Interlocked(battle));
            Assert.Equal(BattleOutcome.Active, Invoke(battle, "DetermineOutcome"));
            Invoke(battle, "EndNativeDuel", actor.Id);
        }
        Assert.Empty(owners);
        Assert.Empty(crisis);
        Assert.False(actor.IsBusy);
        Assert.False(Interlocked(battle));
        Assert.Null(battle.GetNativeDuel(actor.Id));
        Assert.Equal(BattleOutcome.Victory, Invoke(battle, "DetermineOutcome"));
        Invoke(battle, "CompleteBattleIfNeeded");
        Assert.Equal(BattleOutcome.Victory, battle.Outcome);
        Assert.Empty(battle.Capture().NativeLimits);
        Assert.Empty(BattleSession.Restore(battle.Capture()).Capture().NativeLimits);
        Assert.Equal(9, actor.ModelId);
        Assert.True(battle.HasNativeLimitFamily(actor.Id, BattleLimitFamily.Duel));
    }

    private static FieldInfo Field(string name) => typeof(BattleSession).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static void SetKnockedOut(BattleCombatant actor) => typeof(BattleCombatant).GetProperty("NativeStatus0", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(actor, (ushort)1);

    private static object? Invoke(BattleSession battle, string name, params object[] arguments) => typeof(BattleSession).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(battle, arguments);

    private static bool Interlocked(BattleSession battle) => (bool)typeof(BattleSession).GetProperty("HasNativeEscapeInterlock", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(battle)!;
}
