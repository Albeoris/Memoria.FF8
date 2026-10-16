using System.Text.Json;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class BattleMessageTests
{
    [Fact]
    public void OriginalTextCommandsQueueMessagesWithTheirNativeDurationAndMode()
    {
        BattleSession battle = Create([1, 0, 24, 1, 26, 2, 34, 3, 2, 0]);
        BattleEvent[] messages = battle.Events.Where(item => item.Kind == "NativeText").ToArray();
        Assert.Equal(new[] { "First message", "Second message", "Third message", "Delayed message" }, messages.Select(item => item.Message));
        Assert.Equal(new[] { 30, 32, 30, 30 }, messages.Select(item => item.Amount));
        Assert.Equal(new int?[] { 3, 1, 3, 3 }, messages.Select(item => item.NativeMessageMode));
        Assert.Equal(new[] { 0, 0, 0, 2 }, messages.Select(item => item.NativeMessageDelayFrames));
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(BattleSession.Restore(battle.Capture()).Capture()));
    }

    [Fact]
    public void EveryQueuedMessageIsVisibleWhileGameplayTimeAndStateStayPaused()
    {
        BattleSession battle = Create([1, 0, 24, 1, 26, 2, 34, 3, 2, 0]);
        BattleHudController hud = new(battle, battle.QueueAttack, () => { }, () => battle.AdvanceTurn());
        string before = JsonSerializer.Serialize(battle.Capture());
        hud.BuildView();
        Assert.Equal("First message", hud.BattleMessage);
        hud.AdvancePresentation(TimeSpan.FromSeconds(1));
        Assert.Equal("Second message", hud.BattleMessage);
        hud.AdvancePresentation(FrameDuration(32));
        Assert.Equal("Third message", hud.BattleMessage);
        hud.AdvancePresentation(TimeSpan.FromSeconds(1));
        Assert.Null(hud.BattleMessage);
        hud.AdvancePresentation(FrameDuration(2));
        Assert.Equal("Delayed message", hud.BattleMessage);
        hud.AdvancePresentation(TimeSpan.FromSeconds(1));
        Assert.Null(hud.BattleMessage);
        Assert.Equal(before, JsonSerializer.Serialize(battle.Capture()));
    }

    [Fact]
    public void LoadingAfterAnOldMessageDoesNotReplayItsHistory()
    {
        BattleSession battle = Create([1, 0, 0]);
        BattleSession restored = BattleSession.Restore(battle.Capture() with { Frame = 10 });
        BattleHudController hud = new(restored, restored.QueueAttack, () => { }, () => restored.AdvanceTurn());
        hud.BuildView();
        Assert.Null(hud.BattleMessage);
    }

    private static TimeSpan FrameDuration(int frames)
    {
        // A native frame is a repeating fraction in TimeSpan ticks; advance to the first representable instant at its boundary.
        return TimeSpan.FromTicks((long)Math.Ceiling(frames * (double)TimeSpan.TicksPerSecond / BattleSession.FramesPerSecond));
    }

    private static BattleSession Create(byte[] script)
    {
        NativeBattleAiDefinition ai = new([script, [0], [0], [0], [0]], Enumerable.Repeat(new NativeBattleAiAbility(0, 0, 0), 48).ToArray(), 0, ["First message", "Second message", "Third message", "Delayed message"]);
        return new(new() { NativeMessageSpeed = 3, Combatants = [new() { Id = "party-0", Name = "Squall", IsPlayer = true, InitialAtb = BattleSession.AtbMaximum }, new() { Id = "enemy", Name = "Enemy", ModelId = 22, NativeAi = ai }] }, BattleAtbMode.TurnBased);
    }
}
