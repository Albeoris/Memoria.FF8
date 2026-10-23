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

    private string CommandLabel(BattleAbility command)
    {
        if (command == BattleAbility.LimitBreak && SelectedActor is { } actor)
        {
            return session.GetNativeLimitCommandName(actor.Id);
        }
        return command == BattleAbility.GuardianForce ? "GF" : command.ToString();
    }

    private BattleCombatant? NativeShotActor => session.Combatants.FirstOrDefault(actor => session.GetNativeShot(actor.Id) is not null);
    private BattleCombatant? NativeDuelActor => session.Combatants.FirstOrDefault(actor => session.GetNativeDuel(actor.Id) is not null);
    private BattleCombatant? NativeRenzokukenActor => session.Combatants.FirstOrDefault(actor => session.GetNativeRenzokuken(actor.Id) is not null);
    private bool NativeTimedLimitActive => NativeShotActor is not null || NativeDuelActor is not null || NativeRenzokukenActor is not null;
    public Func<ushort, string> NativeButtonBindingHint { get; set; } = new KeyboardInput().NativeButtonBindingHint;
    private IReadOnlyList<string>? NativeDuelChoiceLabels => NativeDuelActor is { } actor ? session.GetNativeDuelChoices(actor.Id).Where(choice => choice.Learned).Select(choice => choice.Name + "  " + string.Join(" ", choice.Buttons.Select(button => ButtonPromptText.Mark(NativeButtonBindingHint(button))))).ToArray() : null;

    private void PresentNativeShot()
    {
        if (CommandMenuHidden && session.NativeGuardianBoost is { Enabled: true } boost)
        {
            string window = boost.Phase == 4 ? boost.Window == 0 ? "STOP" : $"{ButtonPromptText.Mark(NativeButtonBindingHint(0x80))}: PRESS" : "WAIT";
            Message = $"GF Boost {boost.DisplayPercent}% · {window}";
        }
        else if (NativeShotActor is { } actor && session.GetNativeShot(actor.Id) is { } shot)
        {
            Message = shot.Phase == BattleNativeShotPhase.Ready ? $"Shot · {GunbladeBindingHint}: Fire · {ButtonPromptText.Mark(NativeButtonBindingHint(0x10))}: Finish · {shot.RemainingAmmo} ammo" : "Shot";
        }
        else if (NativeDuelActor is { } duelist)
        {
            Message = session.GetNativeDuel(duelist.Id)!.Phase == BattleNativeDuelPhase.Ready ? "Duel · Enter a button sequence" : "Duel";
        }
        else if (NativeRenzokukenActor is { } gunblade)
        {
            BattleNativeRenzokukenState state = session.GetNativeRenzokuken(gunblade.Id)!;
            Message = session.NativeRenzokukenBarVisible && session.CanTriggerNativeRenzokuken ? $"Renzokuken · {GunbladeBindingHint}: Trigger · Hit {state.Input.ContactIndex + 1}" : "Renzokuken";
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
        if (!SelectingAction || SelectingTarget || SelectedCommand != BattleAbility.LimitBreak || SelectedActor is not { NativeLimit.Command: 16 } actor)
        {
            throw new InvalidOperationException("Do over requires the active Slots choice menu.");
        }
        session.RollNativeSlots(actor.Id);
        Message = "Do over to change magic, or Cast to accept.";
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
