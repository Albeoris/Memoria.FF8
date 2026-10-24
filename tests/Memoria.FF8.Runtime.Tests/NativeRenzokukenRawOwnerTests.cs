using System.Collections.Immutable;
using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeRenzokukenRawOwnerTests
{
    [Fact]
    public void Raw_widget_mirrors_input_and_elapsed_projection_cannot_advance_it_again()
    {
        BattleSession battle = Fixture();
        Dictionary<string, BattleNativeRenzokukenState> processes = Field<Dictionary<string, BattleNativeRenzokukenState>>(battle, "nativeRenzokukenProcesses");
        processes.Add("party", new(160, 8, null, new()));
        BattleNativeRenzokukenBlock block = new(1, [10, 10, 10], NativeBattleRenzokukenRules.Timeline([10, 10, 10]));
        BattleNativeRenzokukenInputState input = new() { Phase = 4, PoseClock = 16, SubframeClock = 2 };
        NativeBattleTriggerWidget widget = new(0, block, input, true, false, 0, 0, 4, -1) { SourceMagicId = 160 };
        Invoke(battle, "MirrorNativeRenzokukenWidget", widget);
        BattleNativeRenzokukenState before = processes["party"];
        Assert.Equal(input, before.Input);
        Assert.Equal(4, before.PoseFrames);

        battle.AdvanceNativeRenzokukenPresentation(TimeSpan.FromSeconds(1));
        battle.AdvanceNativeRenzokukenUi(true, true);

        Assert.Equal(before, processes["party"]);
        Assert.Equal(0, battle.Frame);
        Assert.Empty(battle.Events);
    }

    [Fact]
    public void Closing_physical_widget_does_not_own_a_new_Renzokuken_process_or_its_input()
    {
        BattleSession battle = Fixture();
        Dictionary<string, BattleNativeRenzokukenState> processes = Field<Dictionary<string, BattleNativeRenzokukenState>>(battle, "nativeRenzokukenProcesses");
        BattleNativeRenzokukenState process = new(159, 8, null, new());
        processes.Add("party", process);
        BattleNativeRenzokukenBlock block = new(1, [10, 10], NativeBattleRenzokukenRules.Timeline([10, 10]));
        NativeBattleTriggerWidget widget = new(0, block, new() { Phase = 8, ContactIndex = 1 }, true, false, 0, 0, 4, -1);
        typeof(BattleSession).GetProperty(nameof(BattleSession.NativeGraphics))!.SetValue(battle, battle.NativeGraphics! with { TriggerWidget = widget });

        Invoke(battle, "MirrorNativeRenzokukenWidget", widget);

        Assert.Equal(process, processes["party"]);
        Assert.False(battle.CanTriggerNativeRenzokuken);
        Assert.False(battle.NativeRenzokukenBarVisible);
        Assert.False(battle.NativeRenzokukenTriggerWindowOpen);
        Assert.False(battle.TriggerNativeRenzokuken());
        Assert.Equal(widget, battle.NativeGraphics!.TriggerWidget);
    }

    [Fact]
    public void Raw_pending_contact_allows_exactly_one_input_ahead_of_committed_receipts()
    {
        BattleSession battle = Fixture();
        Dictionary<string, BattleNativeRenzokukenState> processes = Field<Dictionary<string, BattleNativeRenzokukenState>>(battle, "nativeRenzokukenProcesses");
        processes.Add("party", new(160, 8, null, new() { ContactIndex = 1, Phase = 4 }));
        Field<Dictionary<string, byte>>(battle, "nativeLimitLevels").Add("party", 1);
        BattleActionPresentation command = new("party", "enemy", BattleActionPhase.Windup, 1, BattleAbility.LimitBreak, 65533) { NativeQueuedCommand = 5, NativeTargetMask = 8, NativeRenzokuken = new(1) };
        typeof(BattleSession).GetField("nativeTimedAction", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(battle, new NativeBattleTimedAction(command, 0));
        NativeBattleGraphicsState graphics = battle.NativeGraphics! with { PendingTrigger = new(0, 1, 2, 25, 32) };
        typeof(BattleSession).GetProperty(nameof(BattleSession.NativeGraphics))!.SetValue(battle, graphics);
        Invoke(battle, "ValidateNativeRenzokukenOwners");

        typeof(BattleSession).GetProperty(nameof(BattleSession.NativeGraphics))!.SetValue(battle, graphics with { PendingTrigger = null });
        TargetInvocationException missing = Assert.Throws<TargetInvocationException>(() => Invoke(battle, "ValidateNativeRenzokukenOwners"));
        Assert.IsType<InvalidDataException>(missing.InnerException);
    }

    private static BattleSession Fixture()
    {
        BattleSession battle = new(new BattleDefinition { Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }] }, BattleAtbMode.Normal);
        NativeBattleGraphicsState graphics = new(Enumerable.Range(0, 7).Select(index => new NativeBattleGraphicsModel(index, 0)).ToImmutableArray(), ["party", null, null, "enemy", null, null, null], 0, 0);
        typeof(BattleSession).GetProperty(nameof(BattleSession.NativeGraphics))!.SetValue(battle, graphics);
        return battle;
    }

    private static T Field<T>(BattleSession battle, string name) => (T)typeof(BattleSession).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(battle)!;
    private static object? Invoke(BattleSession battle, string name, params object[] arguments) => typeof(BattleSession).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(battle, arguments);
}
