using Memoria.FF7.Formats;
using Memoria.FF7.Runtime;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

/// <summary>One mixed combat profile for native action families; visual effect identifiers never select mechanics.</summary>
internal static class CrossoverBattleAbilityRules
{
    internal static byte HostTargetFlags(byte nativeFlags) => (byte)(((nativeFlags & 0x40) != 0 ? 2 : 0) | (NativeBattleLimitRules.CanSelectEitherSide(nativeFlags) ? 0 : 0x10));

    public static string? UnsupportedReason(BattleNativeAction action)
    {
        if (action.PresentationOnly)
        {
            return "This command requires its native invocation process.";
        }
        if (action.HitCount > 1)
        {
            return "The bridge cannot yet publish multiple native contacts for this command.";
        }
        if (action.AttackType is not (1 or 2 or 3 or 8 or 9 or 21 or 22))
        {
            return $"Native attack type {action.AttackType} requires a combat operation that the bridge cannot represent.";
        }
        ushort saved = Ff7GuestBattleStatuses.ToOrigin(Ff7GuestBattleStatuses.ToHostStatuses(action.Statuses0, action.Statuses1));
        uint battle = Ff7GuestBattleStatuses.ToOriginBattle(Ff7GuestBattleStatuses.ToHostStatuses(action.Statuses0, action.Statuses1));
        if (saved != action.Statuses0 || battle != action.Statuses1)
        {
            return "This command changes native statuses or execution flags that have no FFVII counterpart.";
        }
        return null;
    }

    public static Ff7BattleGuestPhysicalHit Describe(CrossoverFf8BattleKit kit, BattleNativeAction action)
    {
        if (UnsupportedReason(action) is { } reason)
        {
            throw new NotSupportedException(reason);
        }
        bool physical = action.AttackType is 1 or 9;
        bool healing = action.AttackType is 3 or 21;
        bool fraction = action.AttackType is 8 or 21;
        uint statuses = Ff7GuestBattleStatuses.ToHostStatuses(action.Statuses0, action.Statuses1);
        byte formula = physical ? (byte)0x11 : fraction ? (byte)(healing ? 0x24 : 0x23) : healing ? (byte)0x25 : (byte)0x22;
        // The shared profile preserves the original opening magic/healing calibration across all source powers.
        int power = fraction ? action.Power * 2 : physical ? CrossoverGunbladeHostAttack.ProjectPhysicalPower(action.Power, 2) : action.Power * (healing ? 5 : 8) / 18;
        byte accuracy = physical ? action.Hit ?? checked((byte)kit.Stats.Hit) : healing ? byte.MaxValue : (byte)100;
        if (!physical && action.Hit is byte hit)
        {
            accuracy = hit == 255 ? byte.MaxValue : (byte)Math.Min(100, (int)hit);
        }
        Ff7BattleAttack? record = statuses == 0 ? null : new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, (byte)((healing ? 0x40 : 0) | (action.StatusAttack > 100 ? 63 : (action.StatusAttack + 3) / 4)), byte.MaxValue, 0, statuses, 0, ushort.MaxValue);
        Ff7BattleDamageActor source = CrossoverGunbladeHostAttack.ProjectPhysicalSource(kit) with { Magic = ProjectMagic(kit.Stats.Magic), CriticalBonus = physical ? (byte)(action.Critical.GetValueOrDefault() * 100 / 256) : (byte)0 };
        Ff7BattleDamageAction mechanics = new(formula, checked((byte)Math.Min(255, power)), accuracy, LongRange: !physical, Healing: healing, UsePhysicalStats: physical, IgnoreDefense: action.AttackType == 22, HasStatusOperation: record is not null, AddsStatuses: record is not null && !healing)
        {
            AlwaysHits = action.Hit == 255,
            SuppressCritical = !physical
        };
        byte element = (byte)(action.Element & -action.Element);
        return new(source, mechanics, healing ? (ushort)0x200 : Ff7GuestBattleElements.ToHost(element)) { StatusRecord = record, ElementPercent = physical ? action.ElementPercent : null };
    }

    internal static byte ProjectMagic(int magic)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(magic);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(magic, 255);
        return checked((byte)(magic <= 6 ? magic * 23 / 6 : 23 + (magic - 6) * (255 - 23) / (255 - 6)));
    }
}
