using Memoria.FF8.Runtime;

namespace Memoria.FF8.GameUi;

public sealed partial class BattleHudController
{
    private readonly Dictionary<(string Actor, bool Form), BattleAbility> rememberedCommands = [];
    private readonly Dictionary<(string Actor, bool Form, BattleAbility Command), CursorSelection> rememberedActions = [];
    private readonly Dictionary<(string Actor, int Spell), bool> rememberedDrawModes = [];
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

    private sealed record CursorSelection(string Actor, bool Form, BattleAbility Command, BattleAbility? ActionAbility, int? ActionId, bool Cast, string? Source, string? Target);

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
        if (rememberedCommands.TryGetValue((actor.Id, form), out BattleAbility command))
        {
            IReadOnlyList<BattleAbility> commands = form ? [BattleAbility.Attack, BattleAbility.Magic, BattleAbility.GuardianForce] : actor.Abilities;
            commandIndex = Math.Max(0, commands.ToList().IndexOf(command));
        }
    }

    private CursorSelection? RememberedAction()
    {
        if (!CursorMemory || SelectedActor is not { } actor)
        {
            return null;
        }
        return rememberedActions.GetValueOrDefault((actor.Id, HasGuardianFormCommands, SelectedCommand));
    }

    private void RestoreActionCursor()
    {
        CursorSelection? memory = RememberedAction();
        actionIndex = memory is null ? 0 : Math.Max(0, ActionChoices.ToList().FindIndex(action => action.Id == memory.ActionId && action.Ability == memory.ActionAbility && action.Available));
    }

    private void RestoreTargetCursor(bool drawSource = false)
    {
        CursorSelection? memory = RememberedAction();
        string? target = drawSource ? memory?.Source : memory?.ActionId == SelectedAction?.Id ? memory?.Target : null;
        selectedTargetId = SmartRecoveryTarget(drawSource) ?? (target is not null && SelectableTargets.Any(candidate => candidate.Id == target) ? target : null);
    }

    private void RestoreDrawModeCursor()
    {
        DrawCast = CursorMemory && SelectedActor is { } actor && SelectedAction is { Id: < 64 } action && rememberedDrawModes.GetValueOrDefault((actor.Id, action.Id));
    }

    private CursorSelection CaptureCursorSelection(BattleCombatant actor)
    {
        BattleAbility command = SelectedCommand;
        BattleActionDefinition? action = command == BattleAbility.Attack || HasGuardianFormCommands ? null : SelectedAction;
        return new(actor.Id, HasGuardianFormCommands, command, action?.Ability, action?.Id, CastSelectedDraw, selectedDrawSourceId, selectedTargetId);
    }

    private void RememberCursorSelection(CursorSelection selection)
    {
        if (!CursorMemory)
        {
            return;
        }
        rememberedCommands[(selection.Actor, selection.Form)] = selection.Command;
        rememberedActions[(selection.Actor, selection.Form, selection.Command)] = selection;
        if (selection.Command == BattleAbility.Draw && selection.ActionId is int spell && spell < 64)
        {
            rememberedDrawModes[(selection.Actor, spell)] = selection.Cast;
        }
    }
}
