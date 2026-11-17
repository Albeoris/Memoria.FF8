using System.Reflection;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class ExpandedBattlePresentationProcessClockTests
{
    [Theory]
    [InlineData(4, 5, false)]
    [InlineData(11, 14, true)]
    public void Attached_clock_admits_entry_idle_and_the_actual_action_menu(int allies, int enemies, bool wait)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        var (definition, selected) = ExpandedBattlePresentationProcessEntryTests.CreateDefinitions(game, assets, allies, enemies, 4, expanded: true, nativeClock: true);
        uint options = (definition.NativeSavedOptions!.Value & ~1u) | (wait ? 1u : 0u);
        definition = definition with { NativeSavedOptions = options };
        BattleSession battle = new(definition, BattleAtbMode.Normal);
        battle.AttachPresentation(new(selected));
        BattlePresentationProcess process = Assert.IsType<BattlePresentationProcess>(battle.Presentation);
        BattleCombatant polling = battle.Combatants.First(actor => actor.IsPlayer);
        BattleCombatant ready = battle.Combatants.Last(actor => actor.IsPlayer);
        polling.Atb = 0;
        byte speed = polling.NativeAtbSpeed;
        int gain = definition.NativeClockRules!.AtbSpeedMultiplier * (speed + 30) / 10;
        while (process.Graphics.Callback < 1200 && process.Graphics.EntryLifecycle != 3)
        {
            battle.Tick();
            Assert.Equal(0, polling.NativeAtb!.Current);
        }
        Assert.Equal((byte)3, process.Graphics.EntryLifecycle);
        Assert.Equal((byte)0, battle.Capture().NativeClock!.Active);
        battle.Tick();
        Assert.True(battle.NativeEntryReady);
        Assert.Equal((byte)1, battle.Capture().NativeClock!.Active);
        Assert.Equal(gain, polling.NativeAtb!.Current);
        Assert.False(battle.ReadBattlePresentationPacketBindings().Aliases.TryEncodeExact(battle.ReadPresentationModelOwner(ready.Id), out _));
        Assert.True(battle.CanChoosePlayerCommand(ready.Id));
        Assert.Equal(0u, process.Graphics.UiServices!.ReadyMask);

        battle.Tick();
        Assert.Equal(5 * gain, polling.NativeAtb!.Current);
        battle.OpenNativeActionMenu();
        Assert.Equal((sbyte)2, process.Graphics.UiServices!.Menu957);
        Assert.NotEqual(0, process.Graphics.UiServices.Flags942 & 0x10);
        battle.Tick();
        Assert.Equal((wait ? 5 : 9) * gain, polling.NativeAtb!.Current);
        Assert.Equal(wait ? (byte)0 : (byte)1, battle.Capture().NativeClock!.AtbAdmitted);
        battle.CloseNativeActionMenu();
        Assert.Equal((sbyte)4, process.Graphics.UiServices!.Menu957);
        Assert.Equal(0, process.Graphics.UiServices.Flags942 & 0x10);
        int before = polling.NativeAtb!.Current;
        battle.Tick();
        Assert.Equal(before + 4 * gain, polling.NativeAtb!.Current);

        PropertyInfo graphics = typeof(BattlePresentationProcess).GetProperty(nameof(BattlePresentationProcess.NativeGraphics))!;
        int presentedBefore = battle.ReadDisplayedVital(polling.Id).HitPoints;
        NativeBattleContactHalf damage = new(battle.ReadPresentationModelOwner(polling.Id), 4, 0, 0, polling.NativeStatus0, 200, polling.NativeStatus1);
        typeof(BattleSession).GetMethod("PublishExpandedPartyPresentedContact", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(battle, [battle.ReadBattlePresentationRoster(), damage]);
        graphics.SetValue(process, process.Graphics with { UiServices = process.Graphics.UiServices!.SetFlags(2) });
        battle.Tick();
        Assert.True(process.Graphics.UiServices!.PauseDE9);
        int pausedHp = battle.ReadDisplayedVital(polling.Id).HitPoints;
        Assert.InRange(pausedHp, battle.ReadPresentedVital(polling.Id).HitPoints + 1, presentedBefore - 1);
        long pausedFrame = battle.Frame;
        long pausedGraphics = process.Graphics.Callback;
        uint pausedUi = process.Graphics.UiServices.Counter;
        int pausedGauge = polling.NativeAtb.Current;
        battle.Tick();
        Assert.Equal(pausedFrame, battle.Frame);
        Assert.Equal(pausedGraphics, process.Graphics.Callback);
        Assert.Equal(pausedUi + 4, process.Graphics.UiServices!.Counter);
        Assert.Equal(pausedGauge, polling.NativeAtb.Current);
        Assert.Equal(pausedHp, battle.ReadDisplayedVital(polling.Id).HitPoints);
        graphics.SetValue(process, process.Graphics with { UiServices = process.Graphics.UiServices.SetFlags(0, 2) });
        battle.Tick();
        Assert.False(process.Graphics.UiServices!.PauseDE9);
        Assert.Equal(pausedGraphics + 1, process.Graphics.Callback);
        Assert.Equal(pausedGauge + 4 * gain, polling.NativeAtb.Current);
        Assert.True(battle.ReadDisplayedVital(polling.Id).HitPoints < pausedHp);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Attached_action_pause_holds_raw_time_until_native_completion_or_cancellation(bool gunblade, bool cancel)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        var (definition, selected) = ExpandedBattlePresentationProcessEntryTests.CreateDefinitions(game, assets, 4, 5, 4, expanded: true, nativeClock: true);
        string source = gunblade ? "party-0" : "party-1";
        NativeCharacterVitals vitals = game.ReadNewGameCharacterVitals().WithGuestBattleRecords();
        definition = definition with
        {
            NativeSavedOptions = definition.NativeSavedOptions!.Value & ~1u,
            NativeCommandSequences = selected.NativeCommandSequences,
            NativeRetainedMemory = selected.NativeRetainedMemory,
            NativeFieldBattleRules = 0,
            Combatants = definition.Combatants.Select(actor => actor.Id == source ? actor with
            {
                NativeAttack = assets.ReadPlayerNativeAttackData(vitals, Array.FindIndex(vitals.Records, row => row.CharacterId == actor.ModelId))
            } : actor.NativeTemplate is { } template ? actor with
            {
                MaximumHp = template.Levels[actor.Level].MaximumHp,
                CurrentHp = template.Levels[actor.Level].MaximumHp
            } : actor with { MaximumHp = 6000, CurrentHp = 6000, NativeStatusResistances = new byte[40] }).ToArray()
        };
        selected = selected with
        {
            LimitProgress = selected.LimitProgress! with { Options978 = (ushort)(selected.LimitProgress!.Options978!.Value & ~1) },
            NativeActionCameraRules = game.ReadNativeActionCameraRules()
        };
        BattleSession battle = new(definition, BattleAtbMode.Normal);
        battle.AttachPresentation(new(selected));
        BattlePresentationProcess process = Assert.IsType<BattlePresentationProcess>(battle.Presentation);
        while (!battle.NativeEntryReady && process.Graphics.Callback < 1200)
        {
            battle.Tick();
        }
        Assert.True(battle.NativeEntryReady);
        BattleCombatant polling = battle.Combatants.Single(actor => actor.Id == "party-3");
        BattleCombatant caster = battle.Combatants.Single(actor => actor.Id == source);
        polling.Atb = 0;
        polling.NativeStatusTimers[1] = 100;
        const string target = "enemy-8";
        Assert.Contains(battle.ReadPresentationModelOwner(target), process.Graphics.ActorModels.Owners);
        battle.QueueAttack(source, target);
        battle.Tick();
        Assert.Equal(BattleActionPhase.Windup, battle.ActiveAction!.Phase);
        Assert.Equal(1u, battle.Capture().NativeClock!.ActionPause);
        int admittedGauge = caster.NativeAtb!.Current;
        Assert.True(admittedGauge > 0);
        int beforeGauge = polling.NativeAtb!.Current;
        short beforeStatus = polling.NativeStatusTimers[1];
        if (cancel)
        {
            caster.CurrentHp = 0;
            caster.NativeStatus0 |= 1;
        }
        long? token = null;
        for (int frame = 0; frame < 1500 && battle.ActiveAction is not null; frame++)
        {
            battle.Tick();
            token ??= process.LastStepActionToken;
            if (battle.ActiveAction is not null)
            {
                Assert.Equal(1u, battle.Capture().NativeClock!.ActionPause);
                Assert.Equal(beforeGauge, polling.NativeAtb.Current);
                Assert.Equal(beforeStatus, polling.NativeStatusTimers[1]);
            }
        }
        Assert.Null(battle.ActiveAction);
        Assert.Equal(0u, battle.Capture().NativeClock!.ActionPause);
        if (cancel)
        {
            Assert.Null(token);
            Assert.Equal(admittedGauge, caster.NativeAtb.Current);
        }
        else
        {
            Assert.True(process.IsActionComplete(Assert.IsType<long>(token)));
            int postPoll = definition.NativeClockRules!.AtbSpeedMultiplier * (caster.NativeAtbSpeed + 30) / 10;
            Assert.Equal(postPoll, caster.NativeAtb.Current);
        }
        int resumedGauge = polling.NativeAtb.Current;
        battle.Tick();
        Assert.True(polling.NativeAtb.Current > resumedGauge);
        Assert.True(polling.NativeStatusTimers[1] < beforeStatus);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Turn_based_progression_preserves_the_timer_and_does_not_run_extra_graphics(bool explicitAdvance)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        var (definition, selected) = ExpandedBattlePresentationProcessEntryTests.CreateDefinitions(game, assets, 4, 5, 4, expanded: true, nativeClock: true);
        NativeExamTimerState timer = NativeExamTimerState.FromColdProcess().SetRemaining(10000);
        definition = definition with
        {
            NativeExamTimerAtEntry = timer,
            Combatants = definition.Combatants.Select(actor => actor with { InitialAtb = 0 }).ToArray()
        };
        BattleSession battle = new(definition, BattleAtbMode.TurnBased);
        battle.AttachPresentation(new(selected));
        BattlePresentationProcess process = Assert.IsType<BattlePresentationProcess>(battle.Presentation);
        while (!battle.NativeEntryReady && process.Graphics.Callback < 1200)
        {
            battle.Tick();
        }
        Assert.True(battle.NativeEntryReady);

        BattleCombatant first = battle.Combatants.First(actor => actor.IsPlayer);
        BattleCombatant polling = battle.Combatants.Last(actor => actor.IsPlayer);
        NativeBattleAtbState gauge = polling.NativeAtb!;
        int gain = definition.NativeClockRules!.AtbSpeedMultiplier * (polling.NativeAtbSpeed + 30) / 10;
        first.NativeAtb = first.NativeAtb! with { Current = first.NativeAtb.Maximum - 1 };
        long callbacks = battle.NativeHostCallbacks!.Value;
        long graphics = process.Graphics.Callback;
        NativeExamTimerState before = battle.NativeExamTimer!;

        if (explicitAdvance)
        {
            Assert.Equal(1, battle.AdvanceTurn());
            Assert.Equal(graphics, process.Graphics.Callback);
            Assert.Equal(callbacks + 1, battle.NativeHostCallbacks);
        }
        else
        {
            battle.Tick();
            Assert.Equal(graphics + 1, process.Graphics.Callback);
            Assert.Equal(callbacks + 2, battle.NativeHostCallbacks);
            Assert.True(battle.NativeExamTimer!.HasSameClock(process.Timer!));
        }
        Assert.True(battle.CanChoosePlayerCommand(first.Id));
        Assert.Equal(gauge.Current + gain, polling.NativeAtb!.Current);
        Assert.True(battle.NativeExamTimer!.HasSameClock(before.AdvanceCallbacks(NativeTimerCallbackKind.Battle, explicitAdvance ? 1 : 2)));
    }
}
