using Memoria.FF7.Formats;
using Memoria.FF7.Runtime;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

/// <summary>One mixed combat profile for native action families; visual effect identifiers never select mechanics.</summary>
internal static class CrossoverBattleAbilityRules
{
    internal static byte HostTargetFlags(byte nativeFlags)
    {
        if ((nativeFlags & 0x30) == 0x20)
        {
            return 4;
        }
        byte group = (nativeFlags & 0x30) switch
        {
            0 => 4,
            0x10 => 0,
            _ => 0
        };
        return (byte)(group | ((nativeFlags & 0x40) != 0 ? 2 : 0) | (NativeBattleLimitRules.CanSelectEitherSide(nativeFlags) ? 0 : 0x10));
    }

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
        if (action.AttackType is not (1 or 2 or 3 or 4 or 5 or 6 or 8 or 9 or 11 or 15 or 20 or 21 or 22 or 26 or 27 or 29 or 32 or 35 or 36))
        {
            return $"Native attack type {action.AttackType} requires a combat operation that the bridge cannot represent.";
        }
        bool healing = IsHealing(action.AttackType);
        if (!healing && !IgnoresStatuses(action.AttackType) && (action.Statuses1 & 8) != 0)
        {
            return "Stop requires a native timer operation that the mixed battle host does not support.";
        }
        ushort saved = Ff7GuestBattleStatuses.ToOrigin(Ff7GuestBattleStatuses.ToHostStatuses(action.Statuses0, action.Statuses1));
        uint battle = Ff7GuestBattleStatuses.ToOriginBattle(Ff7GuestBattleStatuses.ToHostStatuses(action.Statuses0, action.Statuses1));
        if (!healing && !IgnoresStatuses(action.AttackType) && (saved != action.Statuses0 || battle != action.Statuses1))
        {
            return "This command changes native statuses or execution flags that have no FFVII counterpart.";
        }
        return null;
    }

    public static Ff7BattleGuestPhysicalHit Describe(CrossoverFf8BattleKit kit, BattleNativeAction action, int? abilityLevel = null, int damagePercent = 0)
    {
        if (UnsupportedReason(action) is { } reason)
        {
            throw new NotSupportedException(reason);
        }
        bool physical = action.AttackType is 1 or 9 or 36;
        bool healing = IsHealing(action.AttackType);
        bool revive = action.AttackType is 5 or 6;
        bool fraction = action.AttackType is 5 or 6 or 8 or 20 or 21 or 32;
        bool fixedDamage = action.AttackType is 4 or 27 or 29 or 35;
        bool guardian = action.AttackType is 11 or 15;
        int level = abilityLevel ?? kit.Level;
        ArgumentOutOfRangeException.ThrowIfLessThan(level, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(level, 100);
        ArgumentOutOfRangeException.ThrowIfNegative(damagePercent);
        if (action.AttackType == 26 && action.Hit == 0)
        {
            throw new InvalidDataException("A level-conditioned attack requires a nonzero target level divisor.");
        }
        uint statuses = revive ? Ff7NativeBattleActionResolver.Death : IgnoresStatuses(action.AttackType) ? 0 : Ff7GuestBattleStatuses.ToHostStatuses(action.Statuses0, action.Statuses1);
        byte formula = physical ? (byte)0x11 : fixedDamage ? (byte)0x26 : fraction ? (byte)(action.AttackType == 8 ? 0x23 : 0x24) : healing ? (byte)0x25 : (byte)0x22;
        // The shared profile preserves the original opening magic/healing calibration across all source powers.
        int power = fraction ? action.Power * 2 : physical ? CrossoverGunbladeHostAttack.ProjectPhysicalPower(action.Power, 2) : action.Power * (healing ? 5 : 8) / 18;
        if (revive)
        {
            power = action.AttackType == 5 ? 4 : 32;
        }
        byte accuracy = physical ? action.Hit ?? checked((byte)kit.Stats.Hit) : healing ? byte.MaxValue : (byte)100;
        if (!physical && !revive && !IgnoresStatuses(action.AttackType) && action.AttackType != 26 && action.Hit is byte hit)
        {
            accuracy = hit == 255 ? byte.MaxValue : (byte)Math.Min(100, (int)hit);
        }
        Ff7BattleAttack? record = statuses == 0 ? null : new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, (byte)((healing ? 0x40 : 0) | (revive || action.StatusAttack > 100 ? 63 : (action.StatusAttack + 3) / 4)), byte.MaxValue, 0, statuses, 0, revive ? (ushort)(ushort.MaxValue & ~0x900) : ushort.MaxValue);
        Ff7BattleDamageActor source = CrossoverGunbladeHostAttack.ProjectPhysicalSource(kit) with { Magic = ProjectMagic(kit.Stats.Magic), CriticalBonus = physical ? (byte)(action.Critical.GetValueOrDefault() * 100 / 256) : (byte)0 };
        Ff7BattleDamageAction mechanics = new(formula, checked((byte)Math.Min(255, power)), accuracy, LongRange: !physical, Healing: healing, UsePhysicalStats: physical, IgnoreDefense: action.AttackType is 15 or 22 or 36, HasStatusOperation: record is not null, AddsStatuses: record is not null && !healing)
        {
            AlwaysHits = revive || IgnoresStatuses(action.AttackType) || action.Hit == 255,
            SuppressCritical = !physical,
            RequiresKnockedOutTarget = revive,
            MinimumDamage = action.AttackType == 5 ? 1 : 0,
            RequiredTargetLevelMultiple = action.AttackType == 26 ? action.Hit ?? byte.MaxValue : (byte)0,
            BaseDamage = guardian && action.Power != 0 ? ProjectGuardianForceBase(action, level, damagePercent) : null,
            FixedDamage = action.AttackType switch
            {
                4 => action.Power * 50,
                27 => Math.Max(0, action.Power * 100 - (action.Hit ?? 255)),
                29 => 1000 * (action.Power * level / 1000 + 1),
                35 => 1,
                _ => null
            },
            FractionNumerator = action.AttackType == 20 ? level : 0,
            FractionDenominator = action.AttackType == 20 ? (action.PowerModifier ?? 0) - (action.LevelModifier ?? 0) + 100 : 0
        };
        byte element = (byte)(action.Element & -action.Element);
        return new(source, mechanics, healing ? (ushort)0x200 : Ff7GuestBattleElements.ToHost(element)) { StatusRecord = record, ElementPercent = physical ? action.ElementPercent : null };
    }

    private static bool IsHealing(byte type) => type is 3 or 4 or 5 or 6 or 21 or 32;

    private static bool IgnoresStatuses(byte type) => type is 27 or 29 or 35;

    private static int ProjectGuardianForceBase(BattleNativeAction action, int level, int damagePercent)
    {
        // Preserve the GF's own growth and learned bonuses; the host supplies defense, variance and the damage cap.
        int basis = (action.LevelModifier ?? 0) * level / 10 + action.Power + (action.PowerModifier ?? 0);
        return (damagePercent + 100) * (action.Power * basis / 8) / 100;
    }

    internal static byte ProjectMagic(int magic)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(magic);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(magic, 255);
        return checked((byte)(magic <= 6 ? magic * 23 / 6 : 23 + (magic - 6) * (255 - 23) / (255 - 6)));
    }
}
