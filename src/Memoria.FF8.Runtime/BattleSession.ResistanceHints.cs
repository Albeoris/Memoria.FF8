using System.Numerics;

namespace Memoria.FF8.Runtime;

public enum BattleResistanceHintRating
{
    Neutral,
    Favorable,
    Ineffective
}

/// <summary>The resistance channels used by an action, independent of the target and party Scan knowledge.</summary>
public readonly record struct BattleResistanceHintProfile
{
    public int? ElementIndex { get; internal init; }
    public ulong StatusMask { get; internal init; }
    public bool IsEmpty => ElementIndex is null && StatusMask == 0;
    public bool CanRate => NativeContact is not null;
    internal BattleNativeAction? NativeContact { get; init; }
    internal int ElementPercent { get; init; }
    internal bool DealsDamage { get; init; }
    internal bool HasUnclassifiedStatus { get; init; }

    public static BattleResistanceHintRating Classify(int percent, bool status)
    {
        if (percent >= 100)
        {
            return BattleResistanceHintRating.Ineffective;
        }
        return (status ? percent <= 50 : percent < 0) ? BattleResistanceHintRating.Favorable : BattleResistanceHintRating.Neutral;
    }
}

public sealed partial class BattleSession
{
    private static readonly ulong namedStatusMask = Enum.GetValues<NativeStatusId>().Aggregate(0UL, (mask, status) => mask | (1UL << (int)status));

    public BattleResistanceHintProfile GetResistanceHintProfile(BattleCombatant actor, BattleAbility ability, BattleActionDefinition? action = null)
    {
        if (!actor.IsPlayer || action is { Allies: true } || action is not null && action.Ability != ability)
        {
            return default;
        }
        BattleNativeAction? native = ability switch
        {
            BattleAbility.Attack => action?.NativeAction ?? actor.NativeAttack,
            BattleAbility.Magic or BattleAbility.GuardianForce or BattleAbility.EnemyAbility => action?.NativeAction,
            _ => null
        };
        if (native is null || native.PresentationOnly || native.HitCount == 0 || !native.UsesPhysicalDamageFormula && !native.UsesMagicDamageFormula && !native.UsesFixedDamageFormula)
        {
            return default;
        }

        bool dealsDamage = native.AttackType switch
        {
            18 or 20 or 28 or 29 or 35 => true,
            27 => 100 * native.Power > (native.Hit ?? 255),
            34 => false, // This formula uses the kill count only against player characters.
            _ => native.Power > 0
        };
        if (native.UsesFixedDamageFormula)
        {
            return dealsDamage ? new() { NativeContact = native, DealsDamage = true } : default;
        }
        int elementPercent = native.UsesPhysicalDamageFormula ? native.ElementPercent.GetValueOrDefault() : 100;
        int? elementIndex = dealsDamage && native.Element != 0 && elementPercent > 0 ? BitOperations.TrailingZeroCount((uint)native.Element) : null;
        ulong applied = (native.Statuses0 & 0x7FUL) | ((ulong)native.AppliedBattleStatuses << 16);
        if (native.DrainsHitPoints && dealsDamage && native.StatusAttack > 0)
        {
            applied |= 1UL << (int)NativeStatusId.Drain;
        }
        if (!dealsDamage && applied == 0)
        {
            return default;
        }
        return new()
        {
            NativeContact = native,
            ElementIndex = elementIndex,
            ElementPercent = elementPercent,
            StatusMask = applied & namedStatusMask,
            DealsDamage = dealsDamage,
            HasUnclassifiedStatus = (applied & ~namedStatusMask) != 0
        };
    }

    public bool IsPartyScanKnown(BattleCombatant target)
    {
        if (target.IsPlayer || (target.NativeCombatantFlags & 0x2000) != 0 || !byId.TryGetValue(target.Id, out BattleCombatant? current) || current != target)
        {
            return false;
        }
        if ((uint)target.ModelId < 160 && nativeScannedModels?.Contains(target.ModelId) == true)
        {
            return true;
        }
        foreach (BattleScanObservation observation in ScanKnowledge.Observations)
        {
            if (observation.Observer == BattleObserverSide.Party && observation.TargetId == target.Id)
            {
                return true;
            }
        }
        return false;
    }

    public int? GetKnownElementResistance(BattleCombatant target, int elementIndex)
    {
        return (uint)elementIndex < 8 && IsPartyScanKnown(target) ? EffectiveNativeElementDefense(target, elementIndex) - 800 : null;
    }

    public int? GetKnownStatusResistance(BattleCombatant target, NativeStatusId status)
    {
        return Enum.IsDefined(status) && IsPartyScanKnown(target) ? StatusResistancePercent(target, status) : null;
    }

    private static int? StatusResistancePercent(BattleCombatant target, NativeStatusId status)
    {
        int bit = (int)status;
        int index = bit < 16 ? bit : bit - 8;
        return target.NativeStatusResistances.Count == 40 ? GetNativeStatusResistance(target, index) - 100 : null;
    }

    public BattleResistanceHintRating RateResistanceHints(BattleCombatant target, BattleResistanceHintProfile profile)
    {
        if (profile.NativeContact is not { } native || !IsPartyScanKnown(target))
        {
            return BattleResistanceHintRating.Neutral;
        }
        if (NativeOffensiveContactBlocked(native, NativeSavedStatusBits(target), NativeBattleStatusBits(target)))
        {
            return BattleResistanceHintRating.Ineffective;
        }
        bool primaryDamageGetsThrough = profile.DealsDamage && !(native.UsesPercentageDamageFormula && BlocksPercentageDamage(target));
        if (primaryDamageGetsThrough && profile.ElementIndex is int element)
        {
            int percent = EffectiveNativeElementDefense(target, element) - 800;
            if (BattleResistanceHintProfile.Classify(percent, status: false) == BattleResistanceHintRating.Favorable)
            {
                return BattleResistanceHintRating.Favorable;
            }
            // Physical junctions can leave damage even when the target is immune to the element.
            primaryDamageGetsThrough = percent * profile.ElementPercent < 10_000;
        }
        bool effective = primaryDamageGetsThrough || profile.HasUnclassifiedStatus;
        for (ulong mask = profile.StatusMask; mask != 0; mask &= mask - 1)
        {
            NativeStatusId status = (NativeStatusId)BitOperations.TrailingZeroCount(mask);
            int? percent = StatusResistancePercent(target, status);
            if (status == NativeStatusId.Drain && (!primaryDamageGetsThrough || percent >= native.StatusAttack - 100))
            {
                continue;
            }
            BattleResistanceHintRating rating = percent is int value ? BattleResistanceHintProfile.Classify(value, status: true) : BattleResistanceHintRating.Neutral;
            if (rating == BattleResistanceHintRating.Favorable)
            {
                return rating;
            }
            effective |= rating != BattleResistanceHintRating.Ineffective || native.StatusAttack == byte.MaxValue;
        }
        return effective ? BattleResistanceHintRating.Neutral : BattleResistanceHintRating.Ineffective;
    }
}
