using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed partial class BattleHudController
{
    private string? nativeLimitSelectionActorId;
    private bool nativeLimitCommandSelected;
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
        // TODO(TD-009): Project the native eight-unit command-row slide without its pixel animation.
        bool next = selected && commandIndex == 0 && NativeLimitAvailable;
        if (next != nativeLimitCommandSelected)
        {
            nativeLimitCommandSelected = next;
            SoundRequested?.Invoke(MenuSound.Move);
        }
    }

    internal string CommandLabel(BattleAbility command)
    {
        if (command == BattleAbility.LimitBreak && SelectedActor is { } actor)
        {
            string original = session.GetNativeLimitCommandName(actor.Id);
            return FormatBattleText(localizedCommandName?.Invoke(command, actor, original) ?? original);
        }
        string label = command == BattleAbility.GuardianForce ? "GF" : command.ToString();
        return FormatBattleText(localizedCommandName?.Invoke(command, SelectedActor, label) ?? label);
    }

    private BattleCombatant? NativeShotActor => session.Combatants.FirstOrDefault(actor => session.GetNativeShot(actor.Id) is not null);
    private BattleCombatant? NativeDuelActor => session.Combatants.FirstOrDefault(actor => session.GetNativeDuel(actor.Id) is not null);
    public BattleNativeDuelState? NativeDuelState => NativeDuelActor is { } actor ? session.GetNativeDuel(actor.Id) : null;
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

    private IReadOnlyList<BattleAbility> GetPlayerCommandChoices()
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
            return [BattleAbility.Attack, BattleAbility.Magic, BattleAbility.GuardianForce];
        }
        if (nativeLimitCommandSelected && NativeLimitAvailable && actor.Abilities.Count > 0)
        {
            BattleAbility[] commands = actor.Abilities.ToArray();
            commands[0] = BattleAbility.LimitBreak;
            return commands;
        }
        return actor.Abilities;
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
            SelectingDrawMode = false;
            selectedDrawSourceId = null;
            selectedTargetId = null;
            selectedMagicTargets.Clear();
            magicCastCount = 1;
            magicCastCountSelected = false;
            session.CloseNativeActionMenu();
        }
        nativeLimitSelectionActorId = actor?.Id;
        nativeLimitCommandSelected = false;
        RestoreCommandCursor(actor);
        if (actor?.NativeLimit is not null && !ownedSelection)
        {
            session.SelectPlayerCommandActor(actor.Id);
        }
    }
}
