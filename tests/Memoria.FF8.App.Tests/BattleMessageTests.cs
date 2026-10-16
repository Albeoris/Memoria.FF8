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
        Assert.Equal(new[] { "First message", "Second message", "Third message" }, battle.Events.Where(item => item.Kind == "NativeText").Select(item => item.Message));
        BattleSession restored = BattleSession.Restore(battle.Capture());
        for (int callback = 0; callback < 3; callback++)
        {
            battle.Tick();
            restored.Tick();
        }
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
        BattleEvent[] messages = battle.Events.Where(item => item.Kind == "NativeText").ToArray();
        Assert.Equal(new[] { "First message", "Second message", "Third message", "Delayed message" }, messages.Select(item => item.Message));
        Assert.Equal(new[] { 30, 32, 30, 30 }, messages.Select(item => item.Amount));
        Assert.Equal(new int?[] { 3, 1, 3, 3 }, messages.Select(item => item.NativeMessageMode));
        Assert.All(messages, item => Assert.Equal(0, item.NativeMessageDelayFrames));
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(BattleSession.Restore(battle.Capture()).Capture()));
    }

    [Fact]
    public void EveryQueuedMessageIsVisibleWhileGameplayTimeAndVitalsStayPaused()
    {
        BattleSession battle = Create([1, 0, 24, 1, 26, 2, 34, 3, 2, 0]);
        BattleHudController hud = new(battle, battle.QueueAttack, () => { }, () => battle.AdvanceTurn());
        BattleSnapshot before = battle.Capture();
        hud.BuildView();
        Assert.Equal("First message", hud.BattleMessage);
        hud.AdvancePresentation(TimeSpan.FromSeconds(1));
        Assert.Equal("Second message", hud.BattleMessage);
        hud.AdvancePresentation(FrameDuration(32));
        Assert.Equal("Third message", hud.BattleMessage);
        hud.AdvancePresentation(TimeSpan.FromSeconds(1));
        Assert.Null(hud.BattleMessage);
        battle.Tick();
        battle.Tick();
        hud.AdvancePresentation(TimeSpan.Zero);
        Assert.Equal("Delayed message", hud.BattleMessage);
        hud.AdvancePresentation(TimeSpan.FromSeconds(1));
        Assert.Null(hud.BattleMessage);
        Assert.Equal(before.Frame, battle.Frame);
        Assert.Equal(JsonSerializer.Serialize(before.Combatants), JsonSerializer.Serialize(battle.Capture().Combatants));
        Assert.Empty(battle.NativeScriptState.Tasks.Links.Where(link => link.Previous != 0 || link.Next != 0));
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

    [Fact]
    public void NativeControlTagsResolveNamesLinesAndColorsWithoutChangingTheSavedMessage()
    {
        BattleSession original = Create([1, 0, 0]);
        BattleDefinition definition = original.Capture().Definition;
        BattleCombatantDefinition enemy = definition.Combatants[1];
        const string encoded = "{Color Red}{Char Squall} and {Name Shiva}{Line}{Color White}ready";
        enemy = enemy with { NativeAi = enemy.NativeAi! with { Texts = [encoded] } };
        BattleSession battle = AdmitInitial(definition with { Combatants = [definition.Combatants[0] with { Name = "Leon" }, enemy] });
        BattleHudController hud = new(battle, battle.QueueAttack, () => { }, () => battle.AdvanceTurn(), guardianForceName: id => id == 1 ? "Frost" : $"GF {id}");

        hud.BuildView();

        Assert.Equal("Leon and Frost\nready", hud.BattleMessage);
        Assert.Equal(Microsoft.Xna.Framework.Color.Red, hud.BattleMessageDocument!.Pages[0].Lines[0].Runs[0].Color);
        Assert.Equal(encoded, Assert.Single(battle.Events, entry => entry.Kind == "NativeText").Message);
    }

    private static TimeSpan FrameDuration(int frames)
    {
        // A native frame is a repeating fraction in TimeSpan ticks; advance to the first representable instant at its boundary.
        return TimeSpan.FromTicks((long)Math.Ceiling(frames * (double)TimeSpan.TicksPerSecond / BattleSession.FramesPerSecond));
    }

    private static BattleSession Create(byte[] script)
    {
        NativeBattleAiDefinition ai = new([script, [0], [0], [0], [0]], Enumerable.Repeat(new NativeBattleAiAbility(0, 0, 0), 48).ToArray(), 0, ["First message", "Second message", "Third message", "Delayed message"]);
        return AdmitInitial(new() { NativeMessageSpeed = 3, Combatants = [new() { Id = "party-0", Name = "Squall", IsPlayer = true, InitialAtb = BattleSession.AtbMaximum }, new() { Id = "enemy", Name = "Enemy", ModelId = 22, NativeAi = ai }] });
    }

    private static BattleSession AdmitInitial(BattleDefinition definition)
    {
        BattleSession battle = new(definition, BattleAtbMode.TurnBased);
        battle.Tick();
        return battle;
    }
}
