using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed partial class BattleHudController
{
    private string? selectedDrawSourceId;
    private int? selectedDrawActionId;

    public bool ShowingDrawList => SelectedCommand == BattleAbility.Draw && SelectingAction && !SelectingTarget;
    public bool SelectingDrawCastRecipient => SelectedCommand == BattleAbility.Draw && SelectingTarget;
    public bool DrawStockAvailable => DrawChoices(false).Count > 0;
    public bool DrawCastAvailable => DrawChoices(true).Count > 0;
    public bool DrawAvailable => MagicAvailable && (DrawStockAvailable || DrawCastAvailable);
    private bool CastSelectedDraw => DrawCast && SelectedAction?.Id < 64;

    internal bool IsDrawGuardianForce(BattleActionDefinition action) => SelectedCommand == BattleAbility.Draw && action.Id >= 64;

    private static bool IsDrawSource(BattleCombatant actor)
    {
        return actor.IsActive && actor.IsAlive && !actor.IsPlayer && !actor.Untargetable && actor.NativeDrawSlots.Any(slot => slot.Id != 0);
    }

    private int DrawStockCapacity(int id)
    {
        if (SelectedActor is not { } actor)
        {
            return 0;
        }
        if (session.MagicVitals is { } vitals && actor.MagicRecordId is int row)
        {
            return MagicInventoryResolver.Resolve(vitals, row).Capacity(id);
        }
        IReadOnlyList<NativeMagicStock> inventory = actor.NativeMagicSlots;
        NativeMagicStock? entry = inventory.FirstOrDefault(stock => stock.SpellId == id);
        if (entry is not null)
        {
            return Math.Max(0, 100 - entry.Count);
        }
        return inventory.Any(stock => stock.SpellId == 0) ? 100 : 0;
    }

    private int DrawSourceScore(BattleCombatant source, int id, bool cast)
    {
        if (id >= 64)
        {
            return session.GuardianForces.Any(gf => gf.Id == id - 64 && !gf.Acquired) ? 1 : 0;
        }
        if (SelectedActor is not { } actor || session.ActionDefinitions.FirstOrDefault(action => action.Ability == BattleAbility.Magic && action.Id == id) is not { Available: true } spell)
        {
            return 0;
        }
        int capacity = cast ? 100 : DrawStockCapacity(id);
        if (capacity == 0 || cast && !session.Combatants.Any(target => BattleSession.IsValidSelectedActionTarget(actor, spell, target)))
        {
            return 0;
        }
        int score = 0;
        for (int random = 0; random < 32; random++)
        {
            int quantity = session.PreviewDrawQuantity(actor, source, id, random);
            if (quantity > 0)
            {
                score += cast ? 1 : Math.Min(capacity, quantity);
            }
        }
        return score;
    }

    private BattleCombatant? BestDrawSource(int id, bool cast)
    {
        return session.Combatants.Where(IsDrawSource).Where(source => source.NativeDrawSlots.Any(slot => slot.Id == id)).Select(source => (Source: source, Score: DrawSourceScore(source, id, cast))).Where(candidate => candidate.Score > 0).OrderByDescending(candidate => candidate.Score).Select(candidate => candidate.Source).FirstOrDefault();
    }

    private IReadOnlyList<BattleActionDefinition> DrawChoices(bool cast)
    {
        int[] selections = session.Combatants.Where(IsDrawSource).SelectMany(actor => actor.NativeDrawSlots).Select(slot => (int)slot.Id).Where(id => id != 0 && (!cast || id < 64)).Distinct().Order().ToArray();
        return selections.Where(id => BestDrawSource(id, cast) is not null).Select(id => id >= 64 ? new BattleActionDefinition(BattleAbility.Draw, id, "????", BattleActionEffect.Native, 0, false) : DrawMagicChoice(id)).ToArray();
    }

    private IReadOnlyList<BattleActionDefinition> DrawActionChoices() => DrawChoices(DrawCast);

    private BattleActionDefinition DrawMagicChoice(int id)
    {
        BattleActionDefinition action = session.ActionDefinitions.Single(action => action.Ability == BattleAbility.Magic && action.Id == id);
        NativeKnownMagic? known = session.KnownMagic;
        if (known is null && action.NativeAction is not null)
        {
            throw new NotSupportedException("Native Draw menu requires its saved known magic bank; migrate the unknown owner explicitly.");
        }
        return action with { DisplayName = known is not null && !known.Contains(id) ? "????" : action.DisplayName };
    }

    private int DrawStockCount(int id)
    {
        return SelectedActor?.MagicStocks.FirstOrDefault(stock => stock.Id == id)?.Count ?? 0;
    }

    internal void HoverDrawChoice(int index)
    {
        if (ShowingDrawList && index >= 0 && index < ActionChoices.Count && actionIndex != index)
        {
            actionIndex = index;
            selectedDrawActionId = SelectedAction?.Id;
            SoundRequested?.Invoke(MenuSound.Move);
        }
    }

    public void ChangeDrawPage(bool cast)
    {
        if (!ShowingDrawList || cast == DrawCast || (cast ? !DrawCastAvailable : !DrawStockAvailable))
        {
            return;
        }
        int? id = SelectedAction?.Id;
        DrawCast = cast;
        actionIndex = Math.Max(0, ActionChoices.ToList().FindIndex(action => action.Id == id));
        selectedDrawActionId = SelectedAction?.Id;
        SoundRequested?.Invoke(MenuSound.Move);
    }

    private bool NavigateDrawList(MenuInput input)
    {
        if (!ShowingDrawList || input is not (MenuInput.Up or MenuInput.Down or MenuInput.Left or MenuInput.Right or MenuInput.PageLeft or MenuInput.PageRight))
        {
            return false;
        }
        if (input is MenuInput.PageLeft or MenuInput.PageRight)
        {
            ChangeDrawPage(input == MenuInput.PageRight);
            return true;
        }
        BattleDrawLayout layout = BattleDrawLayout.Calculate(ActionChoices.Count);
        (int column, int row) = layout.GetCell(ChoiceIndex);
        int next = actionIndex;
        if (input is MenuInput.Up or MenuInput.Down && layout.ColumnRows(column) > 0)
        {
            next = layout.GetIndex(column, (row + (input == MenuInput.Up ? -1 : 1) + layout.ColumnRows(column)) % layout.ColumnRows(column));
        }
        else if (input == MenuInput.Left && column == 0)
        {
            ChangeDrawPage(false);
            return true;
        }
        else if (input == MenuInput.Right && column == layout.Columns - 1)
        {
            ChangeDrawPage(true);
            return true;
        }
        else if (input is MenuInput.Left or MenuInput.Right)
        {
            column += input == MenuInput.Left ? -1 : 1;
            next = layout.GetIndex(column, Math.Min(row, layout.ColumnRows(column) - 1));
        }
        if (next >= 0 && next != actionIndex)
        {
            actionIndex = next;
            selectedDrawActionId = SelectedAction?.Id;
            SoundRequested?.Invoke(MenuSound.Move);
        }
        return true;
    }

    private bool RefreshDrawSelection()
    {
        if (SelectedCommand != BattleAbility.Draw || !SelectingAction)
        {
            selectedDrawActionId = null;
            return false;
        }
        int? previous = selectedDrawActionId;
        bool pageEmpty = ActionChoices.Count == 0;
        if (pageEmpty && (DrawCast ? DrawStockAvailable : DrawCastAvailable))
        {
            DrawCast = !DrawCast;
        }
        IReadOnlyList<BattleActionDefinition> choices = ActionChoices;
        int index = previous is int id ? choices.ToList().FindIndex(action => action.Id == id) : -1;
        bool stale = previous is not null && index < 0;
        if (index >= 0)
        {
            actionIndex = index;
        }
        else
        {
            actionIndex = Math.Clamp(actionIndex, 0, Math.Max(0, choices.Count - 1));
        }
        selectedDrawActionId = SelectedAction?.Id;
        if (choices.Count == 0)
        {
            SelectingAction = false;
            SelectingTarget = false;
            selectedDrawSourceId = null;
            selectedTargetId = null;
            session.CloseNativeActionMenu();
            SetSystemMessage(() => SystemText("battle.ui.refreshdrawselection.no.drawable.entries.remain", "No drawable entries remain."));
            return true;
        }
        if (stale || pageEmpty)
        {
            SelectingTarget = false;
            selectedDrawSourceId = null;
            selectedTargetId = null;
            return true;
        }
        if (SelectingDrawCastRecipient)
        {
            selectedDrawSourceId = BestDrawSource(SelectedAction!.Id, true)?.Id;
        }
        return false;
    }

    private IReadOnlyList<BattleCombatant> DrawTargets()
    {
        if (ShowingDrawList)
        {
            return SelectedAction is { } selected && BestDrawSource(selected.Id, DrawCast) is { } source ? [source] : [];
        }
        if (SelectedAction is not { } action || SelectedActor is not { } caster)
        {
            return [];
        }
        IReadOnlyList<BattleCombatant> candidates = SelectableTargets;
        bool players = candidates.FirstOrDefault(target => target.Id == selectedTargetId)?.IsPlayer ?? (caster.IsPlayer == action.Allies);
        if (!candidates.Any(target => target.IsPlayer == players) && candidates.Count > 0)
        {
            players = candidates[0].IsPlayer;
        }
        return candidates.Where(target => target.IsPlayer == players).ToArray();
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
                ShowCommandError(SystemText("battle.ui.confirmdrawselection.no.drawable.entries.remain", "No drawable entries remain."));
                SoundRequested?.Invoke(MenuSound.Error);
                return true;
            }
            DrawCast = !DrawStockAvailable;
            SelectingAction = true;
            selectedTargetId = null;
            RestoreActionCursor();
            selectedDrawActionId = SelectedAction?.Id;
            session.OpenNativeActionMenu();
            SetSystemMessage(() => SystemText("battle.ui.collectdrawtarget.choose.magic.to.draw", "Choose magic to draw."));
        }
        else if (SelectedAction is { Available: true } action && BestDrawSource(action.Id, DrawCast) is { } source)
        {
            selectedDrawActionId = action.Id;
            selectedDrawSourceId = source.Id;
            if (!DrawCast)
            {
                SubmitDrawStock();
                return true;
            }
            SelectingTarget = true;
            RestoreTargetCursor();
            SetSystemMessage(() => SystemText("battle.ui.confirmdrawselection.choose.the.spell.recipient", "Choose the spell recipient."));
        }
        else
        {
            SoundRequested?.Invoke(MenuSound.Error);
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
