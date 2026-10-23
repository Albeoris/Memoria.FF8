using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed partial class BattleHudController
{
    private string? guardianFormMenuActorId;
    private string? guardianFormSelectionActorId;
    private bool guardianFormWasActive;

    public bool SelectingGuardianFormAction { get; private set; }
    public bool HasGuardianFormCommands => SelectedActor is { } actor && session.GetGuardianForm(actor.Id) is { Phase: BattleGuardianFormPhase.Active, RemainingTicks: > 0 };
    public string GuardianFormCommandLabel => commandIndex switch { 1 => "Fire", 2 => "Hell Fire", _ => "Attack" };
    private BattleGuardianFormAction SelectedGuardianFormCommand => (BattleGuardianFormAction)Math.Clamp(commandIndex, 0, 2);

    public bool CanOpenGuardianFormActions(BattleActionDefinition action)
    {
        return session.GuardianFormsEnabled && action.Ability == BattleAbility.GuardianForce && BattleGuardianFormProfiles.Find(action.Id) is not null && SelectedActor is { } actor && session.GetGuardianForm(actor.Id) is null;
    }

    public void OpenGuardianFormActions(int index)
    {
        if (CardSlotsOwnsInput)
        {
            return;
        }
        if (!SelectingAction || SelectingTarget || SelectingGuardianFormAction || SelectedCommand != BattleAbility.GuardianForce || ActionChoices.ElementAtOrDefault(index) is not { } action || !CanOpenGuardianFormActions(action))
        {
            return;
        }
        actionIndex = index;
        guardianFormMenuActorId = SelectedActor!.Id;
        SelectingGuardianFormAction = true;
        Message = "Summon: transform the selected character into this Guardian Force.";
        SoundRequested?.Invoke(MenuSound.Move);
    }

    private bool NavigateGuardianFormActions(MenuInput input)
    {
        if (SelectingGuardianFormAction)
        {
            if (input is MenuInput.Cancel or MenuInput.Left)
            {
                CancelGuardianFormSelection();
            }
            else if (input == MenuInput.Confirm)
            {
                Attack();
            }
            return true;
        }
        if (input == MenuInput.Right && SelectingAction && !SelectingTarget && SelectedCommand == BattleAbility.GuardianForce && SelectedAction is { } action && CanOpenGuardianFormActions(action))
        {
            OpenGuardianFormActions(actionIndex);
            return true;
        }
        return false;
    }

    private bool CancelGuardianFormSelection()
    {
        if (SelectingGuardianFormAction)
        {
            SelectingGuardianFormAction = false;
            guardianFormMenuActorId = null;
            Message = "Choose a Guardian Force.";
            SoundRequested?.Invoke(MenuSound.Cancel);
            return true;
        }
        if (HasGuardianFormCommands && SelectingTarget)
        {
            SelectingTarget = false;
            Message = "Choose a form command.";
            SoundRequested?.Invoke(MenuSound.Cancel);
            return true;
        }
        return false;
    }

    private bool ConfirmGuardianFormCommand(BattleCombatant actor)
    {
        if (!SelectingGuardianFormAction && !HasGuardianFormCommands)
        {
            return false;
        }
        if (SelectingGuardianFormAction)
        {
            if (actor.Id != guardianFormMenuActorId || SelectedAction is not { } action || !session.CanRequestGuardianForm(actor.Id, action.Id))
            {
                ShowCommandError("This Guardian Force cannot transform the selected character now.");
                return true;
            }
            if (!SubmitGuardianFormInput(new(BattleInputKind.GuardianFormActivate, ActorId: actor.Id, TargetId: actor.Id, ActionId: action.Id)))
            {
                return true;
            }
            Message = $"{actor.Name}: Summon";
        }
        else
        {
            if (!SelectingTarget)
            {
                SelectingTarget = true;
                RestoreTargetCursor();
                session.OpenNativeActionMenu();
                Message = "Select a target.";
                SoundRequested?.Invoke(MenuSound.Confirm);
                return true;
            }
            if (SelectedTarget is not { } target || !SubmitGuardianFormInput(new(BattleInputKind.GuardianFormAction, ActorId: actor.Id, TargetId: target.Id, ActionId: (int)SelectedGuardianFormCommand)))
            {
                return true;
            }
            Message = $"{actor.Name}: {GuardianFormCommandLabel} â†’ {target.Name}";
        }
        SelectingGuardianFormAction = false;
        guardianFormMenuActorId = null;
        SelectingAction = false;
        SelectingTarget = false;
        selectedTargetId = null;
        session.CloseNativeActionMenu();
        SoundRequested?.Invoke(MenuSound.Confirm);
        return true;
    }

    private bool SubmitGuardianFormInput(BattleInput input)
    {
        CursorSelection cursor = CaptureCursorSelection(SelectedActor!);
        try
        {
            (execute ?? throw new NotSupportedException("Battle action dispatcher is unavailable."))(input);
            RememberCursorSelection(cursor);
            return true;
        }
        catch (Exception error) when (error is InvalidOperationException or NotSupportedException)
        {
            ShowCommandError(error.Message);
            return false;
        }
    }

    private IReadOnlyList<BattleCombatant> GuardianFormTargets()
    {
        BattleCombatant actor = SelectedActor!;
        BattleActionDefinition action = session.GetGuardianFormActionDefinition(actor.Id, SelectedGuardianFormCommand);
        bool fixedSelf = action.NativeAction?.TargetFlags is byte flags && NativeBattleLimitRules.IsFixedAllyTarget(flags);
        return session.Combatants.Where(target => target.IsActive && !target.Untargetable && target.IsPlayer == action.Allies && (!fixedSelf || target.Id == actor.Id) && BattleSession.HasValidTargetLifeState(action, target)).ToArray();
    }

    private void RefreshGuardianFormSelection()
    {
        string? actorId = SelectedActor?.Id;
        bool active = HasGuardianFormCommands;
        if (guardianFormSelectionActorId == actorId && guardianFormWasActive != active || SelectingGuardianFormAction && (guardianFormMenuActorId != actorId || session.GetGuardianForm(actorId!) is not null))
        {
            SelectingGuardianFormAction = false;
            guardianFormMenuActorId = null;
            SelectingAction = false;
            SelectingTarget = false;
            selectedTargetId = null;
            RestoreCommandCursor(SelectedActor);
            session.CloseNativeActionMenu();
        }
        guardianFormSelectionActorId = actorId;
        guardianFormWasActive = active;
    }
}
