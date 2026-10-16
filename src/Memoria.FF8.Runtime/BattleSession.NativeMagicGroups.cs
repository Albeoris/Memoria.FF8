using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    public static bool HasValidTargetLifeState(BattleActionDefinition action, BattleCombatant target)
    {
        if (!target.IsActive)
        {
            return false;
        }
        if (!IsNativeRevive(action))
        {
            return target.IsAlive;
        }
        //491940/4935A0 route Life/Full-life against Zombie through491AD0 damage.
        return !target.IsAlive || action.NativeAction?.AttackType is 5 or 6 && (NativeSavedStatusBits(target) & 0x40) != 0;
    }

    public IReadOnlyList<int> GetNativeMagicCastCounts(string actorId, int spellId)
    {
        BattleCombatant actor = GetActor(actorId);
        int stock = actor.MagicStocks.FirstOrDefault(entry => entry.Id == spellId)?.Count ?? 0;
        if (stock == 0)
        {
            return [];
        }
        List<int> counts = [1];
        uint statuses = NativeBattleStatusBits(actor);
        if ((statuses & 0x60000) == 0)
        {
            return counts;
        }
        uint flags = actor.NativePlayerStatRules?.NativeStatusPassiveFlags ?? throw new NotSupportedException("Double/Triple magic requires the original equipped character-ability flags, including Double/Triple Single.");
        if ((statuses & 0x20000) != 0 && (stock >= 2 || (flags & 0x20) != 0))
        {
            counts.Add(2);
        }
        if ((statuses & 0x40000) != 0 && (stock >= 3 || (flags & 0x40) != 0))
        {
            counts.Add(3);
        }
        return counts;
    }

    private ImmutableArray<ushort> PrepareNativeMagicTargets(BattleCombatant actor, BattleActionDefinition action, string firstTarget, IReadOnlyList<string>? targets)
    {
        if (targets is null)
        {
            return [];
        }
        if (action.Ability != BattleAbility.Magic || action.NativeAction is not { Command: 2, TargetFlags: byte flags } || targets.Count is < 1 or > 3 || targets[0] != firstTarget || !GetNativeMagicCastCounts(actor.Id, action.Id).Contains(targets.Count))
        {
            throw new InvalidOperationException("Magic target groups require the current native Double/Triple status, sufficient stock and one installed spell.");
        }
        ImmutableArray<ushort>.Builder masks = ImmutableArray.CreateBuilder<ushort>(targets.Count);
        foreach (string targetId in targets)
        {
            BattleCombatant target = GetActor(targetId);
            if (!target.IsActive || target.Untargetable || (target.IsPlayer == actor.IsPlayer) != action.Allies || !HasValidTargetLifeState(action, target) || NativeBattleLimitRules.IsFixedAllyTarget(flags) && target.Id != actor.Id)
            {
                throw new InvalidOperationException("Every selected magic target must satisfy the original spell's target contract.");
            }
            masks.Add(NativeBattleLimitRules.PlayerTargetMask(flags, target.NativeIndex));
        }
        return masks.MoveToImmutable();
    }

    private void ResolveNativeMagicGroup(BattleCombatant actor, BattleActionPresentation command)
    {
        BattleActionDefinition action = FindAction(BattleAbility.Magic, command.ActionId);
        uint statuses = NativeBattleStatusBits(actor);
        int count = command.NativeMagicTargets.Length >= 3 && (statuses & 0x40000) != 0 ? 3 : command.NativeMagicTargets.Length >= 2 && (statuses & 0x20000) != 0 ? 2 : 1;
        uint flags = count > 1 ? actor.NativePlayerStatRules?.NativeStatusPassiveFlags ?? throw new NotSupportedException("Native repeated magic requires its equipped passive flags.") : 0;
        bool singleCost = count == 2 && (flags & 0x20) != 0 || count == 3 && (flags & 0x40) != 0;
        int completed = 0;
        for (int index = 0; index < count; index++)
        {
            statuses = NativeBattleStatusBits(actor);
            if (index > 0 && ((NativeSavedStatusBits(actor) & 0x35) != 0 || (statuses & 0x4009) != 0 || index == 1 && (statuses & 0x60000) == 0 || index == 2 && (statuses & 0x40000) == 0))
            {
                break;
            }
            ushort mask = command.NativeMagicTargets[index];
            BattleCombatant target = combatants.FirstOrDefault(candidate => candidate.IsActive && candidate.NativeIndex >= 0 && (mask & (1 << candidate.NativeIndex)) != 0) ?? byId[command.TargetId];
            BattleActionPresentation cast = command with { TargetId = target.Id, NativeTargetMask = mask, NativeMagicTargets = [], NativeAction = action.NativeAction };
            nativeAffinityAction = null;
            if (!ResolveInstalledNativeAction(actor, target, cast, action))
            {
                break;
            }
            completed++;
            //486A10 returns FF after clearing the last stock as well as when no stock exists.
            //485610 exits before the next cast and this cast's affinity update in either case.
            if (!singleCost && (!actor.TryConsumeMagic(command.ActionId) || !actor.NativeMagicInventory.Any(stock => stock.SpellId == command.ActionId && stock.Count > 0)))
            {
                break;
            }
            CompleteGuardianForceAffinity(actor, cast);
        }
        if (singleCost && completed != 0)
        {
            actor.TryConsumeMagic(command.ActionId);
        }
    }

    private void ValidateNativeMagicTargets(BattleAbility ability, int actionId, ImmutableArray<ushort> targets)
    {
        if (targets.IsDefault || targets.Length > 3 || targets.Length != 0 && ability != BattleAbility.Magic || targets.Any(mask => (mask & 0x1FFF) == 0 || (mask & 0x1F80) != 0))
        {
            throw new InvalidDataException("Native magic groups require one to three original physical target masks.");
        }
        if (targets.Length != 0 && FindAction(BattleAbility.Magic, actionId).NativeAction is not { Command: 2 })
        {
            throw new InvalidDataException("Native repeated magic requires its original installed command2 effect.");
        }
    }
}
