using Memoria.FF8.Runtime;

namespace Memoria.FF8.GameUi;

public sealed partial class BattleHudController
{
    private string? selectedDrawSourceId;
    public bool SelectingDrawCastRecipient => SelectedCommand == BattleAbility.Draw && selectedDrawSourceId is not null;
    private bool CastSelectedDraw => DrawCast && SelectedAction?.Id < 64;

    private IReadOnlyList<BattleActionDefinition> DrawActionChoices()
    {
        int[] selections = session.Combatants.Where(actor => actor.IsAlive && !actor.IsPlayer && !actor.Untargetable).SelectMany(actor => actor.NativeDrawSlots).Select(slot => (int)slot.Id).Where(id => id != 0).Distinct().ToArray();
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
        if (!SelectingDrawCastRecipient)
        {
            return session.Combatants.Where(actor => actor.IsAlive && !actor.IsPlayer && !actor.Untargetable && actor.NativeDrawSlots.Any(slot => slot.Id == SelectedAction?.Id)).ToArray();
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
        if (SelectedCommand != BattleAbility.Draw || !CastSelectedDraw || selectedDrawSourceId is not null)
        {
            return true;
        }
        selectedDrawSourceId = target.Id;
        selectedTargetId = null;
        Message = "Draw Cast: select the spell recipient. Cancel: return to source.";
        SoundRequested?.Invoke(MenuSound.Confirm);
        return false;
    }

    private BattleInput WithDrawTarget(BattleInput input)
    {
        return SelectedCommand != BattleAbility.Draw ? input : input with { TargetId = selectedDrawSourceId ?? input.TargetId, DrawCast = CastSelectedDraw, DrawCastTargetId = selectedDrawSourceId is not null ? input.TargetId : null };
    }
}
