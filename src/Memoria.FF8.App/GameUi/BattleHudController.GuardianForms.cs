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
    public string GuardianFormCommandLabel => HasGuardianFormCommands ? GuardianFormLabel(SelectedActor!, SelectedGuardianFormCommand) : string.Empty;
    private IReadOnlyList<BattleGuardianFormAction> GuardianFormCommands => SelectedActor is { } actor ? session.GetGuardianFormActions(actor.Id) : [];
    private IReadOnlyList<string> GuardianFormChoiceLabels => GuardianFormCommands.Select(action => GuardianFormLabel(SelectedActor!, action)).ToArray();
    private BattleGuardianFormAction SelectedGuardianFormCommand
    {
        get
        {
            IReadOnlyList<BattleGuardianFormAction> commands = GuardianFormCommands;
            return commands[Math.Clamp(commandIndex, 0, commands.Count - 1)];
        }
    }

    private string GuardianFormLabel(BattleCombatant actor, BattleGuardianFormAction action)
    {
        BattleActionDefinition definition = session.GetGuardianFormActionDefinition(actor.Id, action);
        return action switch
        {
            BattleGuardianFormAction.Attack => SystemText("battle.ui.guardianformcommandlabel.attack", definition.Name),
            BattleGuardianFormAction.HeavyStrike => SystemText("battle.ui.guardianformcommandlabel.heavy.strike", definition.Name),
            BattleGuardianFormAction.HellFire => ChoiceLabel(definition with { Name = definition.NativeAction?.Name ?? "Hell Fire", DisplayName = null }),
            _ => ChoiceLabel(definition)
        };
    }

    public bool CanOpenGuardianFormActions(BattleActionDefinition action)
    {
        return action.Ability == BattleAbility.GuardianForce && SelectedActor is { } actor && session.CanRequestGuardianForm(actor.Id, action.Id);
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
        SetSystemMessage(() => SystemText("battle.ui.openguardianformactions.summon.transform.the.selected.character.into.this", "Summon: transform the selected character into this Guardian Force."));
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
            SetSystemMessage(() => SystemText("battle.ui.cancelguardianformselection.choose.a.guardian.force", "Choose a Guardian Force."));
            SoundRequested?.Invoke(MenuSound.Cancel);
            return true;
        }
        if (HasGuardianFormCommands && SelectingTarget)
        {
            SelectingTarget = false;
            SetSystemMessage(() => SystemText("battle.ui.cancelguardianformselection.choose.a.form.command", "Choose a form command."));
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
                ShowCommandError(SystemText("battle.ui.confirmguardianformcommand.this.guardian.force.cannot.transform.the.selected", "This Guardian Force cannot transform the selected character now."));
                return true;
            }
            if (!SubmitGuardianFormInput(new(BattleInputKind.GuardianFormActivate, ActorId: actor.Id, TargetId: actor.Id, ActionId: action.Id)))
            {
                return true;
            }
            SetSystemMessage(() => FormatSystemText("battle.ui.confirmguardianformcommand.summon", "{0}: Summon", actor.Name));
        }
        else
        {
            if (!SelectingTarget)
            {
                SelectingTarget = true;
                RestoreTargetCursor();
                session.OpenNativeActionMenu();
                SetSystemMessage(() => SystemText("battle.ui.confirmguardianformcommand.select.a.target", "Select a target."));
                SoundRequested?.Invoke(MenuSound.Confirm);
                return true;
            }
            BattleGuardianFormAction action = SelectedGuardianFormCommand;
            string label = GuardianFormLabel(actor, action);
            if (SelectedTarget is not { } target || !SubmitGuardianFormInput(new(BattleInputKind.GuardianFormAction, ActorId: actor.Id, TargetId: target.Id, ActionId: (int)action)))
            {
                return true;
            }
            SetSystemMessage(() => FormatSystemText("battle.ui.confirmguardianformcommand.", "{0}: {1} \u00E2\u2020\u2019 {2}", actor.Name, label, target.Name));
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
