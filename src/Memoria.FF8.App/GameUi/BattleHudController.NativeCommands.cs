using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed partial class BattleHudController
{
    private string? nativeLimitSelectionActorId;
    private bool nativeLimitCommandSelected;
    private float limitCommandReveal;
    private ushort nativeCommandHeldButtons;
    private int nativeSlotsChoice;

    public bool SelectingNativeSlots => SelectingAction && !SelectingTarget && SelectedCommand == BattleAbility.LimitBreak && SelectedActor?.NativeLimit?.Command == 16;
    public string? NativeSlotsLabel => SelectingNativeSlots && SelectedAction is { } action ? ChoiceLabel(action) : null;

    private bool NavigateNativeSlots(MenuInput input)
    {
        if (!SelectingNativeSlots)
        {
            return false;
        }
        if (input is MenuInput.Up or MenuInput.Down)
        {
            nativeSlotsChoice = 1 - nativeSlotsChoice;
            SoundRequested?.Invoke(MenuSound.Move);
            return true;
        }
        if (input is MenuInput.PageLeft or MenuInput.PageRight)
        {
            RerollNativeSlots();
            return true;
        }
        return false;
    }

    private bool ConfirmNativeSlots()
    {
        if (!SelectingNativeSlots || nativeSlotsChoice != 0)
        {
            return false;
        }
        RerollNativeSlots();
        return true;
    }

    public bool CommandMenuHidden => session.Outcome == BattleOutcome.Active && (session.NativeGraphics?.UiServices is { } ui ? (ui.Flags942 & 0x841) != 0x40 : (nativeCommandHeldButtons & 0x100) != 0);

    public bool NativeLimitAvailable => !HasGuardianFormCommands && SelectedActor is { } actor && session.GetNativeLimitLevel(actor.Id) > 0;
    private bool NativeLimitDirectTarget => SelectedCommand == BattleAbility.LimitBreak && SelectedActor?.NativeLimit?.Command is 5 or 11;

    public void SetNativeCommandButtons(ushort buttons)
    {
        if (CardSlotsOwnsInput)
        {
            return;
        }
        nativeCommandHeldButtons = buttons;
        RefreshNativeLimitSelection();
        if (!CommandMenuHidden && !SelectingAction && !SelectingTarget)
        {
            SelectNativeLimit((buttons & 0x2000) != 0 && (buttons & 0x5000) == 0);
        }
    }

    private void SelectNativeLimit(bool selected)
    {
        bool next = selected && commandIndex == 0 && NativeLimitAvailable;
        if (next != nativeLimitCommandSelected)
        {
            nativeLimitCommandSelected = next;
            SoundRequested?.Invoke(MenuSound.Move);
        }
    }

    internal string CommandLabel(BattleAbility command)
    {
        if (command == BattleAbility.LimitBreak)
        {
            return LimitCommandLabel();
        }
        IReadOnlyList<BattleCommandPresentation> commands = PlayerCommandChoices;
        BattleCommandPresentation? selected = commands.ElementAtOrDefault(Math.Clamp(commandIndex, 0, Math.Max(0, commands.Count - 1)));
        return CommandLabel(selected?.Ability == command ? selected : commands.FirstOrDefault(choice => choice.Ability == command) ?? BattleCommandPresentation.Default(command));
    }

    private string CommandLabel(BattleCommandPresentation command)
    {
        string original = command.Ability == BattleAbility.LimitBreak && SelectedActor is { } actor ? session.GetNativeLimitCommandName(actor.Id) : command.Name;
        string formatted = FormatBattleText(localizedCommandName?.Invoke(command.Command, original) ?? original);
        if (command.Ability == BattleAbility.Draw && !DrawStockAvailable && DrawAvailable)
        {
            return formatted + ": " + SystemText("battle.ui.choicelabels.cast", "Cast");
        }
        return formatted;
    }

    private BattleCommandPresentation LimitCommand(BattleCombatant actor)
    {
        return new(BattleAbility.LimitBreak, actor.NativeLimit!.Command, actor.NativeLimit.InvocationChoices.FirstOrDefault()?.Name ?? string.Empty);
    }

    private IReadOnlyList<BattleCommandPresentation> PlayerCommandChoices => GetPlayerCommandPresentations();
    private int? SelectedNativeCommand
    {
        get
        {
            IReadOnlyList<BattleCommandPresentation> commands = PlayerCommandChoices;
            return commands.ElementAtOrDefault(Math.Clamp(commandIndex, 0, Math.Max(0, commands.Count - 1)))?.Command;
        }
    }

    private string LimitCommandLabel()
    {
        if (SelectedActor is { } actor)
        {
            return CommandLabel(LimitCommand(actor));
        }
        return string.Empty;
    }

    private BattleCombatant? NativeShotActor => session.Combatants.FirstOrDefault(actor => session.GetShotUi(actor.Id) is { OwnsInput: true });
    private BattleCombatant? NativeDuelActor => session.Combatants.FirstOrDefault(actor => session.GetDuelUi(actor.Id) is { OwnsInput: true });
    public BattleNativeDuelState? NativeDuelState => NativeDuelActor is { } actor ? session.GetNativeDuel(actor.Id) : null;
    internal BattleTimedInputUi? DuelUi => NativeDuelActor is { } actor ? session.GetDuelUi(actor.Id) : null;
    public IReadOnlyList<BattleNativeDuelChoice> NativeDuelChoices => NativeDuelActor is { } actor ? session.GetNativeDuelChoices(actor.Id).Where(choice => choice.Learned).ToArray() : [];
    private BattleCombatant? NativeRenzokukenActor => session.Combatants.FirstOrDefault(actor => session.GetNativeRenzokuken(actor.Id) is not null);
    private bool NativeTimedLimitActive => NativeShotActor is not null || NativeDuelActor is not null || NativeRenzokukenActor is not null;
    public Func<ushort, string> NativeButtonBindingHint { get; set; } = new KeyboardInput().NativeButtonBindingHint;
    private IReadOnlyList<string>? NativeDuelChoiceLabels => NativeDuelState is not null ? NativeDuelChoices.Select(choice => FormatBattleText(choice.Name) + "  " + string.Join(" ", choice.Buttons.Select(button => ButtonPromptText.Mark(NativeButtonBindingHint(button))))).ToArray() : null;

    private void PresentNativeShot()
    {
        if (CommandMenuHidden && session.NativeGuardianBoost is { Enabled: true } boost)
        {
            string window = boost.Phase == 4 ? boost.Window == 0 ? SystemText("battle.ui.boost.stop", "STOP") : FormatSystemText("battle.ui.boost.press", "{0}: PRESS", ButtonPromptText.Mark(NativeButtonBindingHint(0x80))) : SystemText("battle.ui.boost.wait", "WAIT");
            SetSystemMessage(() => FormatSystemText("battle.ui.presentnativeshot.gf.boost", "GF Boost {0}% \u00B7 {1}", boost.DisplayPercent, window));
        }
        else if (NativeShotActor is { } actor && session.GetNativeShot(actor.Id) is { } shot)
        {
            SetSystemMessage(() => shot.Phase == BattleNativeShotPhase.Ready ? FormatSystemText("battle.ui.presentnativeshot.shot.fire.finish.ammo", "Shot \u00B7 {0}: Fire \u00B7 {1}: Finish \u00B7 {2} ammo", GunbladeBindingHint, ButtonPromptText.Mark(NativeButtonBindingHint(0x10)), shot.RemainingAmmo) : SystemText("battle.ui.presentnativeshot.shot", "Shot"));
        }
        else if (NativeDuelActor is { } duelist)
        {
            SetSystemMessage(() => session.GetNativeDuel(duelist.Id)!.Phase == BattleNativeDuelPhase.Ready ? SystemText("battle.ui.presentnativeshot.duel.enter.a.button.sequence", "Duel · Enter a button sequence") : SystemText("battle.ui.presentnativeshot.duel", "Duel"));
        }
        else if (NativeRenzokukenActor is { } gunblade)
        {
            BattleNativeRenzokukenState state = session.GetNativeRenzokuken(gunblade.Id)!;
            SetSystemMessage(() => session.NativeRenzokukenBarVisible && session.CanTriggerNativeRenzokuken ? FormatSystemText("battle.ui.presentnativeshot.renzokuken.trigger.hit", "Renzokuken \u00B7 {0}: Trigger \u00B7 Hit {1}", GunbladeBindingHint, state.Input.ContactIndex + 1) : SystemText("battle.ui.presentnativeshot.renzokuken", "Renzokuken"));
        }
    }

    private void PrepareNativeLimitChoices()
    {
        if (SelectedCommand == BattleAbility.LimitBreak && SelectedActor is { NativeLimit.Command: 16 } actor && session.GetNativeSlotsRoll(actor.Id) is null)
        {
            session.RollNativeSlots(actor.Id);
        }
        nativeSlotsChoice = 0;
    }

    public void RerollNativeSlots()
    {
        if (CardSlotsOwnsInput)
        {
            return;
        }
        if (!SelectingAction || SelectingTarget || SelectedCommand != BattleAbility.LimitBreak || SelectedActor is not { NativeLimit.Command: 16 } actor)
        {
            throw new InvalidOperationException("Do over requires the active Slots choice menu.");
        }
        session.RollNativeSlots(actor.Id);
        SetSystemMessage(() => SystemText("battle.ui.rerollnativeslots.do.over.to.change.magic.or.cast", "Do over to change magic, or Cast to accept."));
        SoundRequested?.Invoke(MenuSound.Confirm);
    }

    private IReadOnlyList<BattleCommandPresentation> GetPlayerCommandPresentations()
    {
        if (NativeTimedLimitActive)
        {
            return [];
        }
        if (SelectedActor is not { } actor)
        {
            return [];
        }
        if (HasGuardianFormCommands)
        {
            return [BattleCommandPresentation.Default(BattleAbility.Attack), BattleCommandPresentation.Default(BattleAbility.Magic), BattleCommandPresentation.Default(BattleAbility.GuardianForce)];
        }
        if (nativeLimitCommandSelected && NativeLimitAvailable && actor.Abilities.Count > 0)
        {
            BattleCommandPresentation[] commands = BattleCommandPresentation.Read(actor).ToArray();
            commands[0] = LimitCommand(actor);
            return commands;
        }
        return BattleCommandPresentation.Read(actor).Select(choice => choice.Ability == BattleAbility.LimitBreak ? LimitCommand(actor) : choice).ToArray();
    }

    private void RefreshNativeLimitSelection()
    {
        if (CardSlotsOwnsInput)
        {
            return;
        }
        if (session.Outcome != BattleOutcome.Active || NativeTimedLimitActive || CommandMenuHidden)
        {
            return;
        }
        BattleCombatant? actor = SelectedActor;
        if (actor?.Id == nativeLimitSelectionActorId)
        {
            return;
        }
        bool ownedSelection = actor is not null && session.HasNativeLimitSelection(actor.Id);
        if (nativeLimitSelectionActorId is not null)
        {
            SelectingAction = false;
            SelectingTarget = false;
            selectedDrawSourceId = null;
            selectedTargetId = null;
            selectedMagicTargets.Clear();
            magicCastCount = 1;
            magicCastCountSelected = false;
            session.CloseNativeActionMenu();
        }
        nativeLimitSelectionActorId = actor?.Id;
        nativeLimitCommandSelected = false;
        limitCommandReveal = 0;
        RestoreCommandCursor(actor);
        if (actor?.NativeLimit is not null && !ownedSelection)
        {
            session.SelectPlayerCommandActor(actor.Id);
        }
    }

    private void AdvanceCommandRowTransition(TimeSpan elapsed)
    {
        float progress = (float)(elapsed.TotalSeconds / 0.12);
        limitCommandReveal = nativeLimitCommandSelected ? Math.Min(1, limitCommandReveal + progress) : Math.Max(0, limitCommandReveal - progress);
    }

    internal float LimitCommandReveal => limitCommandReveal;
    internal string OrdinaryFirstCommandLabel => SelectedActor is { Abilities.Count: > 0 } actor ? CommandLabel(BattleCommandPresentation.Read(actor)[0]) : string.Empty;
}
