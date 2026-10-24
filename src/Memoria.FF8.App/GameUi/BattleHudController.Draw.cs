using Memoria.FF8.Runtime;

namespace Memoria.FF8.GameUi;

public sealed partial class BattleHudController
{
    private string? selectedDrawSourceId;
    private int? selectedDrawActionId;
    public bool DrawAvailable => session.Combatants.Any(IsDrawSource);
    public bool SelectingDrawMode { get; private set; }
    public bool SelectingDrawSource => SelectedCommand == BattleAbility.Draw && SelectingTarget && selectedDrawSourceId is null;
    public bool SelectingDrawCastRecipient => SelectedCommand == BattleAbility.Draw && SelectingTarget && selectedDrawSourceId is not null;
    private bool CastSelectedDraw => DrawCast && SelectedAction?.Id < 64;

    private static bool IsDrawSource(BattleCombatant actor)
    {
        return actor.IsActive && actor.IsAlive && !actor.IsPlayer && !actor.Untargetable && actor.NativeDrawSlots.Any(slot => slot.Id != 0);
    }

    private bool RefreshDrawSelection()
    {
        if (SelectedCommand != BattleAbility.Draw || !SelectingAction && !SelectingTarget)
        {
            selectedDrawActionId = null;
            return false;
        }
        BattleCombatant? source = session.Combatants.FirstOrDefault(actor => actor.Id == selectedDrawSourceId);
        bool validSource = source is not null && IsDrawSource(source);
        bool staleAction = (SelectingDrawMode || SelectingDrawCastRecipient) && selectedDrawActionId is int id && source?.NativeDrawSlots.Any(slot => slot.Id == id) != true;
        if (validSource && !staleAction || selectedDrawSourceId is null && DrawAvailable)
        {
            if ((SelectingDrawMode || SelectingDrawCastRecipient) && selectedDrawActionId is int selectedId)
            {
                actionIndex = DrawActionChoices().ToList().FindIndex(action => action.Id == selectedId);
            }
            return false;
        }
        SelectingDrawMode = false;
        DrawCast = false;
        selectedDrawActionId = null;
        selectedTargetId = null;
        if (validSource)
        {
            SelectingTarget = false;
            SelectingAction = true;
            RestoreActionCursor();
            Message = "Choose magic to draw.";
        }
        else
        {
            selectedDrawSourceId = null;
            SelectingAction = false;
            SelectingTarget = DrawAvailable;
            session.CloseNativeActionMenu();
            if (SelectingTarget)
            {
                RestoreTargetCursor(drawSource: true);
            }
            Message = SelectingTarget ? "Choose a source to draw from." : "No drawable entries remain.";
        }
        return true;
    }

    private IReadOnlyList<BattleActionDefinition> DrawActionChoices()
    {
        int[] selections = session.Combatants.Where(actor => actor.Id == selectedDrawSourceId).SelectMany(actor => actor.NativeDrawSlots).Select(slot => (int)slot.Id).Where(id => id != 0).Distinct().ToArray();
        return selections.Select(id => id >= 64 ? new BattleActionDefinition(BattleAbility.Draw, id, guardianForceName(id - 64), BattleActionEffect.Native, 0, false) : DrawMagicChoice(id)).ToArray();
    }

    private BattleActionDefinition DrawMagicChoice(int id)
    {
        BattleActionDefinition action = session.ActionDefinitions.Single(action => action.Ability == BattleAbility.Magic && action.Id == id);
        NativeKnownMagic? known = session.KnownMagic;
        if (known is null && action.NativeAction is not null)
        {
            throw new NotSupportedException("Native Draw menu requires its saved known magic bank; migrate the unknown owner explicitly.");
        }
        return action with
        {
            DisplayName = known is not null && !known.Contains(id) ? "????" : action.DisplayName,
            Available = action.Available && DrawStockCount(id) < 100
        };
    }

    private int DrawStockCount(int id)
    {
        return SelectedActor?.MagicStocks.FirstOrDefault(stock => stock.Id == id)?.Count ?? 0;
    }

    private IReadOnlyList<BattleCombatant> DrawTargets()
    {
        if (SelectingDrawSource)
        {
            return session.Combatants.Where(IsDrawSource).ToArray();
        }
        if (SelectedAction is not { } action || SelectedActor is not { } caster)
        {
            return [];
        }
        IReadOnlyList<BattleCombatant> candidates = SelectableTargets;
        bool players = candidates.FirstOrDefault(target => target.Id == selectedTargetId)?.IsPlayer ?? ((caster.IsPlayer == action.Allies));
        if (!candidates.Any(target => target.IsPlayer == players) && candidates.Count > 0)
        {
            players = candidates[0].IsPlayer;
        }
        return candidates.Where(target => target.IsPlayer == players).ToArray();
    }

    private bool CollectDrawTarget(BattleCombatant target)
    {
        if (!SelectingDrawSource)
        {
            return true;
        }
        selectedDrawSourceId = target.Id;
        selectedTargetId = null;
        SelectingTarget = false;
        SelectingAction = true;
        RestoreActionCursor();
        session.OpenNativeActionMenu();
        Message = "Choose magic to draw.";
        SoundRequested?.Invoke(MenuSound.Confirm);
        return false;
    }

    private bool ConfirmDrawSelection()
    {
        if (SelectedCommand != BattleAbility.Draw || SelectingTarget)
        {
            return false;
        }
        if (!SelectingAction)
        {
            if (!DrawAvailable)
            {
                ShowCommandError("No drawable entries remain.");
                SoundRequested?.Invoke(MenuSound.Error);
                return true;
            }
            selectedDrawSourceId = null;
            SelectingTarget = true;
            DrawCast = false;
            RestoreTargetCursor(drawSource: true);
            Message = "Choose a source to draw from.";
        }
        else if (SelectedAction is null)
        {
            return true;
        }
        else if (SelectedAction is { Available: false } && (!SelectingDrawMode || !CastSelectedDraw))
        {
            SoundRequested?.Invoke(MenuSound.Error);
            return true;
        }
        else if (!SelectingDrawMode)
        {
            selectedDrawActionId = SelectedAction.Id;
            SelectingDrawMode = true;
            RestoreDrawModeCursor();
            Message = "Stock or Cast?";
        }
        else if (CastSelectedDraw)
        {
            SelectingDrawMode = false;
            SelectingTarget = true;
            RestoreTargetCursor();
            Message = "Choose the spell recipient.";
        }
        else
        {
            SubmitDrawStock();
            return true;
        }
        SoundRequested?.Invoke(MenuSound.Confirm);
        return true;
    }

    private void SubmitDrawStock()
    {
        BattleCombatant actor = SelectedActor!;
        CursorSelection cursor = CaptureCursorSelection(actor);
        try
        {
            (execute ?? throw new NotSupportedException("Battle action dispatcher is unavailable."))(new BattleInput(BattleInputKind.Action, ActorId: actor.Id, TargetId: selectedDrawSourceId, Ability: BattleAbility.Draw, ActionId: SelectedAction!.Id));
        }
        catch (Exception error) when (error is InvalidOperationException or NotSupportedException)
        {
            ShowCommandError(error.Message);
            return;
        }
        RememberCursorSelection(cursor);
        SelectingDrawMode = false;
        SelectingAction = false;
        SelectingTarget = false;
        session.CloseNativeActionMenu();
        selectedDrawSourceId = null;
        selectedActorId = null;
        RestoreCommandCursor(SelectedActor);
        SoundRequested?.Invoke(MenuSound.Confirm);
    }

    private BattleInput WithDrawTarget(BattleInput input)
    {
        return SelectedCommand != BattleAbility.Draw ? input : input with { TargetId = selectedDrawSourceId ?? input.TargetId, DrawCast = CastSelectedDraw, DrawCastTargetId = selectedDrawSourceId is not null ? input.TargetId : null };
    }
}
