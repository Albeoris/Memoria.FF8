using Memoria.FF8.Runtime;

namespace Memoria.FF8.GameUi;

public sealed partial class BattleHudController
{
    private string? selectedDrawSourceId;
    public bool SelectingDrawMode { get; private set; }
    public bool SelectingDrawSource => SelectedCommand == BattleAbility.Draw && SelectingTarget && selectedDrawSourceId is null;
    public bool SelectingDrawCastRecipient => SelectedCommand == BattleAbility.Draw && SelectingTarget && selectedDrawSourceId is not null;
    private bool CastSelectedDraw => DrawCast && SelectedAction?.Id < 64;

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
        return known is not null && !known.Contains(id) ? action with { DisplayName = "????" } : action;
    }

    private IReadOnlyList<BattleCombatant> DrawTargets()
    {
        if (SelectingDrawSource)
        {
            return session.Combatants.Where(actor => actor.IsAlive && !actor.IsPlayer && !actor.Untargetable).ToArray();
        }
        if (SelectedAction is not { } action || SelectedActor is not { } caster)
        {
            return [];
        }
        bool fixedSelf = action.NativeAction?.TargetFlags is byte flags && NativeBattleLimitRules.IsFixedAllyTarget(flags);
        return session.Combatants.Where(actor => actor.IsActive && !actor.Untargetable && (actor.IsPlayer == caster.IsPlayer) == action.Allies && BattleSession.HasValidTargetLifeState(action, actor) && (!fixedSelf || actor == caster)).ToArray();
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
        actionIndex = 0;
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
            selectedDrawSourceId = null;
            SelectingTarget = true;
            DrawCast = false;
            Message = "Choose a source to draw from.";
        }
        else if (SelectedAction is null)
        {
            return true;
        }
        else if (!SelectingDrawMode)
        {
            SelectingDrawMode = true;
            DrawCast = false;
            Message = "Stock or Cast?";
        }
        else if (CastSelectedDraw)
        {
            SelectingDrawMode = false;
            SelectingTarget = true;
            selectedTargetId = null;
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
        try
        {
            (execute ?? throw new NotSupportedException("Battle action dispatcher is unavailable."))(new BattleInput(BattleInputKind.Action, ActorId: actor.Id, TargetId: selectedDrawSourceId, Ability: BattleAbility.Draw, ActionId: SelectedAction!.Id));
        }
        catch (Exception error) when (error is InvalidOperationException or NotSupportedException)
        {
            ShowCommandError(error.Message);
            return;
        }
        SelectingDrawMode = false;
        SelectingAction = false;
        SelectingTarget = false;
        session.CloseNativeActionMenu();
        selectedDrawSourceId = null;
        selectedActorId = null;
        commandIndex = 0;
        SoundRequested?.Invoke(MenuSound.Confirm);
    }

    private BattleInput WithDrawTarget(BattleInput input)
    {
        return SelectedCommand != BattleAbility.Draw ? input : input with { TargetId = selectedDrawSourceId ?? input.TargetId, DrawCast = CastSelectedDraw, DrawCastTargetId = selectedDrawSourceId is not null ? input.TargetId : null };
    }
}
