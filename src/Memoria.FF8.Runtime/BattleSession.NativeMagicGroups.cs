using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    public static bool CanSelectEitherTargetSide(BattleActionDefinition action)
    {
        return action.NativeAction?.TargetFlags is byte flags ? NativeBattleLimitRules.CanSelectEitherSide(flags) : !action.AllTargets;
    }

    public static bool IsValidSelectedActionTarget(BattleCombatant actor, BattleActionDefinition action, BattleCombatant target)
    {
        if (!target.IsActive || target.Untargetable || !HasValidTargetLifeState(action, target))
        {
            return false;
        }
        if (action.NativeAction?.TargetFlags is byte flags && NativeBattleLimitRules.IsFixedAllyTarget(flags))
        {
            return target.Id == actor.Id;
        }
        return CanSelectEitherTargetSide(action) || (target.IsPlayer == actor.IsPlayer) == action.Allies;
    }

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
        int stock = GetAvailableMagicCasts(actorId, spellId);
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
        //4FDD90 collects the status-selected targets;485610 stops casting when486A10 exhausts stock.
        if ((statuses & 0x20000) != 0)
        {
            counts.Add(2);
        }
        if ((statuses & 0x40000) != 0)
        {
            counts.Add(3);
        }
        return counts;
    }

    public IReadOnlyList<int> GetPlayerMagicCastCounts(string actorId, int spellId)
    {
        BattleCombatant actor = GetActor(actorId);
        int available = GetAvailableMagicCasts(actorId, spellId);
        return GetNativeMagicCastCounts(actorId, spellId).Select(mode => MagicCost(actor, mode) == 1 ? mode : Math.Min(mode, available)).Distinct().Order().ToArray();
    }

    private ImmutableArray<ushort> PrepareNativeMagicTargets(BattleCombatant actor, BattleActionDefinition action, string firstTarget, IReadOnlyList<string>? targets)
    {
        if (targets is null)
        {
            return [];
        }
        if (action.Ability != BattleAbility.Magic || action.NativeAction is not { Command: 2, TargetFlags: byte flags } || targets.Count is < 1 or > 3 || targets[0] != firstTarget || !GetNativeMagicCastCounts(actor.Id, action.Id).Contains(targets.Count) && !(actor.IsPlayer && GetPlayerMagicCastCounts(actor.Id, action.Id).Contains(targets.Count)))
        {
            throw new InvalidOperationException("Magic target groups require the current native Double/Triple status, sufficient stock and one installed spell.");
        }
        ImmutableArray<ushort>.Builder masks = ImmutableArray.CreateBuilder<ushort>(targets.Count);
        foreach (string targetId in targets)
        {
            BattleCombatant target = GetActor(targetId);
            if (!IsValidSelectedActionTarget(actor, action, target))
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
        uint statuses = EncounterMulticastStatusBits(actor);
        int mode = command.NativeMagicCastMode == 0 ? command.NativeMagicTargets.Length : command.NativeMagicCastMode;
        int count = mode >= 3 && (statuses & 0x40000) != 0 ? command.NativeMagicTargets.Length : command.NativeMagicTargets.Length >= 2 && (statuses & 0x20000) != 0 ? 2 : 1;
        if (!actor.IsPlayer && HasEncounterPolicy(actor))
        {
            count = Math.Min(command.NativeMagicTargets.Length, (statuses & 0x40000) != 0 ? 3 : (statuses & 0x20000) != 0 ? 2 : 1);
        }
        uint flags = count > 1 && actor.IsPlayer ? actor.NativePlayerStatRules?.NativeStatusPassiveFlags ?? throw new NotSupportedException("Native repeated magic requires its equipped passive flags.") : 0;
        int costMode = command.NativeMagicCastMode != 0 && (statuses & 0x40000) != 0 ? command.NativeMagicCastMode : count;
        bool singleCost = (magicVitals?.MagicSystem.InfiniteMagic != true || magicVitals?.MagicSystem.UnlimitedMagic == true) && (count > 1 && costMode == 2 && (flags & 0x20) != 0 || count > 1 && costMode == 3 && (flags & 0x40) != 0);
        int completed = 0;
        for (int index = 0; index < count; index++)
        {
            if (command.MagicReservationId is not null && !singleCost && GetAvailableMagicCasts(actor.Id, command.ActionId, command.MagicReservationId) == 0)
            {
                break;
            }
            statuses = EncounterMulticastStatusBits(actor);
            if (index > 0 && ((NativeSavedStatusBits(actor) & 0x35) != 0 || (statuses & 0x4009) != 0 || index == 1 && (statuses & 0x60000) == 0 || index == 2 && (statuses & 0x40000) == 0))
            {
                break;
            }
            ushort mask = command.NativeMagicTargets[index];
            BattleCombatant target = combatants.FirstOrDefault(candidate => candidate.IsActive && candidate.NativeIndex >= 0 && (mask & (1 << candidate.NativeIndex)) != 0) ?? byId[command.TargetId];
            if (!target.IsAlive && (command.SmartAttack is not null || !combatants.Any(candidate => candidate.IsActive && candidate.IsAlive && !candidate.Untargetable && candidate.IsPlayer == target.IsPlayer)))
            {
                break;
            }
            BattleActionPresentation cast = command with { TargetId = target.Id, NativeTargetMask = mask, NativeMagicTargets = [], NativeMagicCastMode = 0, NativeAction = action.NativeAction };
            nativeAffinityAction = null;
            //485610 submits one104 for the group; the effect consumes the following headers itself.
            if (!ResolveInstalledNativeAction(actor, target, cast, action, queueGraphics: index == 0))
            {
                break;
            }
            completed++;
            //486A10 returns FF after clearing the last stock as well as when no stock exists.
            //485610 exits before the next cast and this cast's affinity update in either case.
            if (command.MagicReservationId is not null)
            {
                CommitMagicReservation(command.MagicReservationId, singleCost ? 1 : completed);
                CompleteGuardianForceAffinity(actor, cast);
                continue;
            }
            if (actor.IsPlayer && !singleCost && !UnlimitedMagic && (!actor.TryConsumeMagic(command.ActionId) || !actor.NativeMagicInventory.Any(stock => stock.SpellId == command.ActionId && stock.Count > 0)))
            {
                break;
            }
            CompleteGuardianForceAffinity(actor, cast);
        }
        ReleaseMagicReservation(command.MagicReservationId);
        if (actor.IsPlayer && command.MagicReservationId is null && singleCost && completed != 0 && !UnlimitedMagic)
        {
            actor.TryConsumeMagic(command.ActionId);
        }
    }

    private void ValidateNativeMagicTargets(BattleAbility ability, int actionId, ImmutableArray<ushort> targets, int mode = 0)
    {
        if (mode != 0 && (mode != 3 || targets.Length != 2 || ability != BattleAbility.Magic))
        {
            throw new InvalidDataException("An abbreviated Triple command requires exactly two selected magic targets.");
        }
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
