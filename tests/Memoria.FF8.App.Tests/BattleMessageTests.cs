using System.Text.Json;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class BattleMessageTests
{
    [Theory]
    [InlineData("actor")]
    [InlineData("enemy")]
    [InlineData("heap")]
    public void Actor_dialogue_is_shown_by_default_and_quiet_policy_prunes_active_and_queued_speech(string provenance)
    {
        BattleSession original = Create();
        BattleEvent speech = new(original.Frame, "NativeText", "party-0", null, 30, "Actor dialogue")
        {
            NativeTextIdentity = provenance == "enemy" ? BattleTextIdentity.Enemy(22, 0) : null,
            NativeAiHeapMessage = provenance == "heap" ? new(0, 0, null, []) : null
        };
        BattleEvent global = new(original.Frame, "NativeText", null, null, 30, "Alert") { NativeTextIdentity = BattleTextIdentity.Global(0) };
        BattleSession battle = BattleSession.Restore(original.Capture() with { Events = [speech, speech with { Message = "Queued dialogue" }, global] });
        string before = JsonSerializer.Serialize(battle.Capture());
        BattleHudController hud = new(battle, battle.QueueAttack, () => { }, () => { }, readPresentationUi: BattleHudTestPresentation.Visible);
        hud.BuildView();
        Assert.True(hud.ShowActorDialogue);
        Assert.Equal("Actor dialogue", hud.BattleMessage);

        hud.ShowActorDialogue = false;
        Assert.Equal("Alert", hud.BattleMessage);
        hud.AdvancePresentation(TimeSpan.FromSeconds(3));
        Assert.Null(hud.BattleMessage);
        hud.ShowActorDialogue = true;
        hud.BuildView();
        Assert.Null(hud.BattleMessage);
        Assert.Equal(before, JsonSerializer.Serialize(battle.Capture()));
    }

    [Fact]
    public void Quiet_actor_dialogue_keeps_action_titles_alerts_and_command_errors_and_consumes_speech_once()
    {
        BattleSession original = Create();
        BattleEvent speech = new(original.Frame, "NativeText", "party-0", null, 30, "Speech");
        BattleEvent title = new(original.Frame, "NativeText", "enemy", null, 30, "Fire") { IsEnemyActionTitle = true };
        BattleEvent partyTitle = new(original.Frame, "NativeText", "party-0", null, 30, "Cure") { NativeTextIdentity = new("kernel_magicspells_name", 0) };
        BattleEvent global = new(original.Frame, "NativeText", null, null, 30, "Alert") { NativeTextIdentity = BattleTextIdentity.Global(0) };
        BattleSession battle = BattleSession.Restore(original.Capture() with { Events = [speech, title, partyTitle, global] });
        BattleEvent[] before = battle.Events.ToArray();
        BattleHudController hud = new(battle, battle.QueueAttack, () => { }, () => { }, readPresentationUi: BattleHudTestPresentation.Visible) { ShowActorDialogue = false, ShowEnemySpellNames = true };
        hud.BuildView();
        Assert.Equal("Fire", hud.BattleMessage);
        hud.AdvancePresentation(FrameDuration(30));
        Assert.Equal("Cure", hud.BattleMessage);
        hud.AdvancePresentation(FrameDuration(30));
        Assert.Equal("Alert", hud.BattleMessage);
        hud.AdvancePresentation(FrameDuration(30));
        Assert.Null(hud.BattleMessage);
        typeof(BattleHudController).GetMethod("ShowCommandError", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(hud, ["Unavailable command"]);
        Assert.Equal("Unavailable command", hud.BattleMessage);
        hud.AdvancePresentation(TimeSpan.FromSeconds(6));
        hud.ShowActorDialogue = true;
        hud.BuildView();
        Assert.Null(hud.BattleMessage);
        Assert.Equal(before, battle.Events);
    }

    [Fact]
    public void Disabling_enemy_titles_removes_active_and_queued_titles_without_delaying_scripted_text()
    {
        BattleSession original = Create();
        BattleSession battle = BattleSession.Restore(original.Capture() with
        {
            Events =
            [
                new(original.Frame, "NativeText", "enemy", null, 30, "Fire") { IsEnemyActionTitle = true },
                new(original.Frame, "NativeText", "enemy", null, 30, "Blizzard") { IsEnemyActionTitle = true },
                new(original.Frame, "NativeText", "enemy", null, 30, "Dialogue")
            ]
        });
        BattleHudController hud = new(battle, battle.QueueAttack, () => { }, () => { }, readPresentationUi: BattleHudTestPresentation.Visible) { ShowEnemySpellNames = true };
        hud.BuildView();
        Assert.Equal("Fire", hud.BattleMessage);
        hud.ShowEnemySpellNames = false;
        Assert.Equal("Dialogue", hud.BattleMessage);
        hud.AdvancePresentation(FrameDuration(30));
        Assert.Null(hud.BattleMessage);
        hud.ShowEnemySpellNames = true;
        Assert.Null(hud.BattleMessage);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Enemy_titles_are_optional_without_hiding_scripted_messages_or_player_titles(bool enabled)
    {
        BattleSession original = Create();
        BattleSnapshot snapshot = original.Capture() with
        {
            Events =
            [
                new(original.Frame, "NativeText", "enemy", null, 30, "Fire") { NativeMessageMode = 1, IsEnemyActionTitle = true },
                new(original.Frame, "NativeText", "enemy", null, 30, "You shall not pass!") { NativeMessageMode = 1 },
                new(original.Frame, "NativeText", "party-0", null, 30, "Cure") { NativeMessageMode = 1 }
            ]
        };
        BattleSession battle = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(snapshot))!);
        BattleHudController hud = new(battle, battle.QueueAttack, () => { }, () => { }, readPresentationUi: BattleHudTestPresentation.Visible) { ShowEnemySpellNames = enabled };
        hud.BuildView();
        if (enabled)
        {
            Assert.Equal("Fire", hud.BattleMessage);
            hud.AdvancePresentation(FrameDuration(30));
        }
        Assert.Equal("You shall not pass!", hud.BattleMessage);
        hud.AdvancePresentation(FrameDuration(30));
        Assert.Equal("Cure", hud.BattleMessage);
        hud.AdvancePresentation(FrameDuration(30));
        Assert.Null(hud.BattleMessage);
        Assert.Equal(JsonSerializer.Serialize(snapshot), JsonSerializer.Serialize(battle.Capture()));
    }

    [Theory]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(120)]
    public void Native_message_duration_and_guardian_seconds_use_fixed_callback_units(int displayRate)
    {
        BattleSession battle = Create("First message");
        BattleHudController hud = new(battle, battle.QueueAttack, () => { }, () => { }, readPresentationUi: BattleHudTestPresentation.Visible);
        hud.BuildView();
        Assert.Equal(15.5, hud.BattleCallbackRate);
        Assert.Equal(2, hud.GuardianFormSeconds(31));
        Assert.Equal(3, hud.GuardianFormSeconds(32));
        long previous = 0;
        for (int frame = 1; frame <= displayRate * 2; frame++)
        {
            long timestamp = (long)frame * TimeSpan.TicksPerSecond / displayRate;
            hud.AdvancePresentation(TimeSpan.FromTicks(timestamp - previous));
            previous = timestamp;
            if (timestamp <= TimeSpan.TicksPerSecond)
            {
                Assert.Equal("First message", hud.BattleMessage);
            }
        }
        Assert.Null(hud.BattleMessage);
    }

    [Fact]
    public void Open_battle_message_updates_when_its_live_input_bindings_change()
    {
        BattleSession battle = Create("First message");
        BattleSnapshot snapshot = battle.Capture();
        BattleDefinition definition = snapshot.Definition;
        battle = CreateMessages(definition, "{Key Triangle}/{Key Cross}");
        KeyboardInput input = new();
        BattleHudController hud = new(battle, battle.QueueAttack, () => { }, () => battle.AdvanceTurn(), readPresentationUi: BattleHudTestPresentation.Visible) { NativeButtonBindingHint = input.NativeButtonBindingHint };
        hud.BuildView();
        Assert.Equal("C/X", hud.BattleMessage);
        input.Profile = input.Profile.WithBinding(ControlAction.Cancel, ControlDevice.Keyboard, new([Microsoft.Xna.Framework.Input.Keys.K], []));
        Assert.Equal("K/X", hud.BattleMessage);
    }

    [Fact]
    public void Hud_displays_each_committed_message_without_advancing_gameplay()
    {
        BattleSession battle = Create("First message", "Second message", "Third message", "Delayed message");
        BattleHudController hud = new(battle, battle.QueueAttack, () => { }, () => battle.AdvanceTurn(), readPresentationUi: BattleHudTestPresentation.Visible);
        BattleSnapshot before = battle.Capture();
        hud.BuildView();
        Assert.Equal("First message", hud.BattleMessage);
        hud.AdvancePresentation(FrameDuration(30));
        Assert.Equal("Second message", hud.BattleMessage);
        hud.AdvancePresentation(FrameDuration(32));
        Assert.Equal("Third message", hud.BattleMessage);
        hud.AdvancePresentation(FrameDuration(30));
        Assert.Equal("Delayed message", hud.BattleMessage);
        hud.AdvancePresentation(FrameDuration(30));
        Assert.Null(hud.BattleMessage);
        Assert.Equal(before.Frame, battle.Frame);
        Assert.Equal(JsonSerializer.Serialize(before.Combatants), JsonSerializer.Serialize(battle.Capture().Combatants));
        Assert.Empty(battle.NativeScriptState.Tasks.Links.Where(link => link.Previous != 0 || link.Next != 0));
    }

    [Fact]
    public void LoadingAfterAnOldMessageDoesNotReplayItsHistory()
    {
        BattleSession battle = Create("First message");
        BattleSession restored = BattleSession.Restore(battle.Capture() with { Frame = 10 });
        BattleHudController hud = new(restored, restored.QueueAttack, () => { }, () => restored.AdvanceTurn(), readPresentationUi: BattleHudTestPresentation.Visible);
        hud.BuildView();
        Assert.Null(hud.BattleMessage);
    }

    [Fact]
    public void NativeControlTagsResolveNamesLinesAndColorsWithoutChangingTheSavedMessage()
    {
        BattleSession original = Create("First message");
        BattleDefinition definition = original.Capture().Definition;
        BattleCombatantDefinition enemy = definition.Combatants[1];
        const string encoded = "{Color Red}{Char Squall} and {Name Shiva}{Line}{Color White}ready";
        BattleSession battle = CreateMessages(definition with { Combatants = [definition.Combatants[0] with { Name = "Leon" }, enemy] }, encoded);
        BattleHudController hud = new(battle, battle.QueueAttack, () => { }, () => battle.AdvanceTurn(), guardianForceName: id => id == 1 ? "Frost" : $"GF {id}", readPresentationUi: BattleHudTestPresentation.Visible);

        hud.BuildView();

        Assert.Equal("Leon and Frost\nready", hud.BattleMessage);
        Assert.Equal(Microsoft.Xna.Framework.Color.Red, hud.BattleMessageDocument!.Pages[0].Lines[0].Runs[0].Color);
        Assert.Equal(encoded, Assert.Single(battle.Events, entry => entry.Kind == "NativeText").Message);
    }

    private static TimeSpan FrameDuration(int frames)
    {
        // A native frame is a repeating fraction in TimeSpan ticks; advance to the first representable instant at its boundary.
        return TimeSpan.FromTicks((long)Math.Ceiling(frames * (double)TimeSpan.TicksPerSecond / BattlePresentationTiming.CallbacksPerSecond));
    }

    private static BattleSession Create(params string[] messages)
    {
        BattleDefinition definition = new() { Combatants = [new() { Id = "party-0", Name = "Squall", IsPlayer = true, InitialAtb = BattleSession.AtbMaximum }, new() { Id = "enemy", Name = "Enemy", ModelId = 22 }] };
        return CreateMessages(definition, messages);
    }

    private static BattleSession CreateMessages(BattleDefinition definition, params string[] messages)
    {
        BattleSession battle = new(definition, BattleAtbMode.TurnBased);
        BattleEvent[] receipts = messages.Select((message, index) => new BattleEvent(battle.Frame, "NativeText", "enemy", null, index == 1 ? 32 : 30, message) { NativeMessageMode = index == 1 ? 1 : 3 }).ToArray();
        return BattleSession.Restore(battle.Capture() with { Events = receipts });
    }
}
