using Memoria.FF8.Runtime;

namespace Memoria.FF8.GameUi;

public sealed partial class BattleHudController
{
    private readonly Dictionary<(string Actor, bool Form), (BattleAbility Ability, int? NativeCommand)> rememberedCommands = [];
    private readonly Dictionary<(string Actor, bool Form, BattleAbility Command, int? NativeCommand), CursorSelection> rememberedActions = [];
    private string? cursorActorId;
    private bool cursorMemory;

    public bool CursorMemory
    {
        get => cursorMemory;
        set
        {
            if (cursorMemory == value)
            {
                return;
            }
            cursorMemory = value;
            if (!SelectingAction && !SelectingTarget)
            {
                RestoreCommandCursor(SelectedActor);
            }
        }
    }

    private sealed record CursorSelection(string Actor, bool Form, BattleAbility Command, int? NativeCommand, BattleAbility? ActionAbility, int? ActionId, string? Target);

    private void SynchronizeCursorActor(BattleCombatant? actor)
    {
        if (cursorActorId != actor?.Id)
        {
            cursorActorId = actor?.Id;
            RestoreCommandCursor(actor);
        }
    }

    private void RestoreCommandCursor(BattleCombatant? actor)
    {
        commandIndex = 0;
        if (!CursorMemory || actor is null)
        {
            return;
        }
        bool form = session.GetGuardianForm(actor.Id) is { Phase: BattleGuardianFormPhase.Active, RemainingTicks: > 0 };
        if (rememberedCommands.TryGetValue((actor.Id, form), out var command))
        {
            commandIndex = Math.Max(0, PlayerCommandChoices.ToList().FindIndex(choice => choice.Ability == command.Ability && choice.Command == command.NativeCommand));
        }
    }

    private CursorSelection? RememberedAction()
    {
        if (!CursorMemory || SelectedActor is not { } actor)
        {
            return null;
        }
        return rememberedActions.GetValueOrDefault((actor.Id, HasGuardianFormCommands, SelectedCommand, SelectedNativeCommand));
    }

    private void RestoreActionCursor()
    {
        CursorSelection? memory = RememberedAction();
        if (SelectedCommand == BattleAbility.Magic)
        {
            BattleActionDefinition? action = MagicStockChoices().FirstOrDefault(action => action.Id == memory?.ActionId && action.Available);
            magicSection = action is null ? MagicSections.FirstOrDefault() : ClassifyMagic(action);
        }
        actionIndex = memory is null ? 0 : Math.Max(0, ActionChoices.ToList().FindIndex(action => action.Id == memory.ActionId && action.Ability == memory.ActionAbility && action.Available));
    }

    private void RestoreTargetCursor()
    {
        CursorSelection? memory = RememberedAction();
        string? target = memory?.ActionId == SelectedAction?.Id ? memory?.Target : null;
        selectedTargetId = SmartRecoveryTarget() ?? (target is not null && SelectableTargets.Any(candidate => candidate.Id == target) ? target : null);
    }

    private CursorSelection CaptureCursorSelection(BattleCombatant actor)
    {
        BattleAbility command = SelectedCommand;
        BattleActionDefinition? action = command == BattleAbility.Attack || HasGuardianFormCommands ? null : SelectedAction;
        return new(actor.Id, HasGuardianFormCommands, command, SelectedNativeCommand, action?.Ability, action?.Id, selectedTargetId);
    }

    private void RememberCursorSelection(CursorSelection selection)
    {
        if (!CursorMemory)
        {
            return;
        }
        rememberedCommands[(selection.Actor, selection.Form)] = (selection.Command, selection.NativeCommand);
        rememberedActions[(selection.Actor, selection.Form, selection.Command, selection.NativeCommand)] = selection;
    }
}
