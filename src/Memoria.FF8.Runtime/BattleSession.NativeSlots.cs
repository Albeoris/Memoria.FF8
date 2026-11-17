namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private readonly Dictionary<string, BattleNativeSlotsRoll> nativeSlotsRolls = new(StringComparer.Ordinal);

    public BattleNativeSlotsRoll? GetNativeSlotsRoll(string actorId)
    {
        _ = GetActor(actorId);
        return nativeSlotsRolls.GetValueOrDefault(actorId);
    }

    public BattleNativeSlotsRoll RollNativeSlots(string actorId)
    {
        RequireNoPendingSlotsDeal();
        BattleCombatant actor = GetActor(actorId);
        byte crisis = GetNativeLimitLevel(actorId);
        if (Outcome != BattleOutcome.Active || !actor.IsPlayer || !actor.IsReady || actor.NativeLimit is not { Command: 16 } rules || crisis == 0)
        {
            throw new InvalidOperationException("Slots roll requires Selphie's ready owned Limit selection.");
        }
        if (rules.SlotsSelectionBytes.IsDefaultOrEmpty || rules.SlotsSpellBytes.IsDefaultOrEmpty)
        {
            throw new NotSupportedException("Native Slots roll requires its actual contiguous kernel26/27 tables.");
        }
        BattleNativeSlotsRoll roll = GenerateNativeSlotsRoll(rules, actor.Level, crisis);
        nativeSlotsRolls[actorId] = roll;
        return roll;
    }

    private BattleNativeSlotsRoll GenerateNativeSlotsRoll(BattleNativeLimitDefinition rules, int level, byte crisis)
    {
        BattleNativeSlotsRoll roll = NativeBattleSlotsRules.Roll(rules, level, crisis, (byte)NextRandomByte(), (byte)NextRandomByte(), (byte)NextRandomByte(), (byte)NextRandomByte());
        _ = FindInstalledNativeAction(16, roll.MagicId);
        return roll;
    }

    private static bool SupportsExpandedSlotsAction(BattleNativeAction? action)
    {
        return action is { Command: 16, KernelSection: 1, AttackType: 2, Power: 0, HitCount: 1, TargetFlags: 0x48, AttackFlags: 0x23 } &&
            action is { Statuses0: 0, Statuses1: 0x10000, StatusAttack: 254 } or { Statuses0: 1, Statuses1: 0, StatusAttack: 255 };
    }

    private bool CanCastExpandedSlots(BattleNativeAction action, BattleNativeSlotsRoll roll)
    {
        return SupportsExpandedSlotsAction(action) && roll is { Repetitions: 1 } && roll.MagicId == action.ActionId && definition.LimitProgress?.SpecialMagicFlags is not null;
    }

    private void QueueExpandedSlots(BattleCombatant actor, BattleNativeAction action, byte sequence, BattleNativeSlotsRoll roll)
    {
        if (!CanCastExpandedSlots(action, roll))
        {
            throw new NotSupportedException("This Slots result is unavailable in expanded battles.");
        }
        BattleCombatant target = enemies.FirstOrDefault(enemy => enemy.IsAlive && !enemy.Untargetable) ?? throw new InvalidOperationException("Slots requires a living targetable enemy.");
        ValidateNativeActionContract(action);
        byte nativeSequence = NativeBattleActionGraphics.ResolvePlayerSequence(16, sequence, action.ActionId, definition.NativeCommandSequences);
        OnNativePlayerCommandSubmitted(actor, 16);
        BattleTargetRequest request = new(new BattleTargetSelection.SideGroup(BattleTargetSide.Enemies), BattleTargetEligibility.Living, false, BattleTargetReplacement.None);
        actions.Enqueue(new(actor.Id, target.Id, request, BattleAbility.LimitBreak, 16 << 16 | action.ActionId)
        {
            NativeQueuedCommand = 16,
            NativeAction = action,
            NativeSlots = roll,
            NativeSequence = nativeSequence
        });
        actor.Atb = 0;
        actor.IsBusy = true;
        nativeLimitLevels.Remove(actor.Id);
        nativeSlotsRolls.Remove(actor.Id);
        Log("Queued", actor.Id, target.Id, 0, $"{actor.Name}: {action.Name} queued.");
    }

    private void ValidateNativeSlotsPreflight(BattleNativeSlotsRoll roll, BattleNativeAction action)
    {
        bool needsPreflight = false;
        if (needsPreflight != (roll.PreflightSelection is NativeBattleTargetRequestAdapter.DeferredOperands) ||
            !needsPreflight && roll.PreflightSelection is not null)
        {
            throw new InvalidDataException("Accepted Slots requires its original single-target preflight selection.");
        }
    }

    private BattleTargetRequest SelectNativeSlotsTargets(BattleNativeAction action, BattleTargetRequest original, BattleTargetSelection? preflight)
    {
        byte flags = action.TargetFlags!.Value;
        if ((flags & 0x30) != 0x10)
        {
            return original;
        }
        bool players = (flags & 0x40) == 0;
        bool healing = players && action.AttackType is 3 or 4 or 21 or 25 or 32;
        bool statuses = action.AttackType is not (3 or 4 or 5 or 6 or 13 or 16 or 21 or 25 or 32) && (action.Statuses0 != 0 || action.Statuses1 != 0);
        bool damaging = !players && action.AttackType is 2 or 8 or 11 or 15 or 20 or 22 or 26 or 33;
        if (!healing && !statuses && !damaging)
        {
            return original;
        }
        IEnumerable<BattleCombatant> candidates = combatants.Where(candidate => candidate.NativeActorIndex is >= 0 and < 7 && (candidate.NativeActorIndex is < 3) == players && candidate.IsAlive && candidate.IsActive && !candidate.Untargetable);
        NativeBattleTargetRequestAdapter.DeferredOperands selected = preflight as NativeBattleTargetRequestAdapter.DeferredOperands ?? throw new InvalidDataException("Accepted Slots has no original target preference.");
        BattleCombatant? preferred = ReadOriginalSelectedActor(selected, activeOnly: true);
        BattleCombatant? target = candidates.OrderBy(candidate => damaging && ModifyNativeElement(candidate, action, 100, false) < 0 ? 1 : 0)
            .ThenBy(candidate => statuses ? NativeSlotsStatusPriority(candidate, action, !players) : 0)
            .ThenBy(candidate => healing ? candidate.CurrentHp : 0)
            .ThenBy(candidate => candidate == preferred ? 0 : 1)
            .ThenBy(candidate => candidate.NativeActorIndex)
            .FirstOrDefault();
        // A selected target must not retain the random-target modifier, which would replace it again.
        return target is null ? original : new(NativeBattleTargetRequestAdapter.SelectOriginalSlot(NativeBattleSlots.Decode(target.RequireNativeActorIndex())), original.Eligibility, original.AllowsKnockedOutAllies, original.Replacement);
    }

    private static int NativeSlotsStatusPriority(BattleCombatant candidate, BattleNativeAction action, bool considerImmunity)
    {
        if (considerImmunity && action.StatusAttack != 255 && !NativeSlotsHasSusceptibleStatus(candidate, action.Statuses0, action.Statuses1))
        {
            return 2;
        }
        ushort missingSaved = (ushort)(action.Statuses0 & ~NativeSavedStatusBits(candidate) & 0x7F);
        uint missingBattle = action.Statuses1 & ~NativeBattleStatusBits(candidate);
        if (missingSaved == 0 && missingBattle == 0)
        {
            return 1;
        }
        if (!considerImmunity || action.StatusAttack == 255)
        {
            return 0;
        }
        return NativeSlotsHasSusceptibleStatus(candidate, missingSaved, missingBattle) ? 0 : 2;
    }

    private static bool NativeSlotsHasSusceptibleStatus(BattleCombatant candidate, ushort saved, uint battle)
    {
        for (int bit = 0; bit < 40; bit++)
        {
            bool included = bit < 7 ? (saved & (1 << bit)) != 0 : bit >= 8 && (battle & (1u << (bit - 8))) != 0;
            if (included && GetNativeStatusResistance(candidate, bit) < 200)
            {
                return true;
            }
        }
        return false;
    }
}
