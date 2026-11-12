using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

public readonly record struct Ff7BattleActionVitals(uint Hp, uint MaxHp, ushort Mp, ushort MaxMp, uint Statuses, uint StatusImmunities = 0);

public readonly record struct Ff7BattleActionReceipt(Ff7BattleActionVitals Source, Ff7BattleActionVitals Target, Ff7BattleDamageResult Damage, uint AddedStatuses, uint RemovedStatuses)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public sbyte? NativeResultMarker { get; init; }
}

/// <summary>Resolves the native target transaction. The action owner publishes the receipt and spends costs once.</summary>
public static class Ff7NativeBattleActionResolver
{
    public const uint Death = 1;
    public const uint Sleep = 4;
    public const uint Poison = 8;
    public const uint Slow = 0x200;
    public const uint Darkness = 0x04000000;

    private const uint PhysicalWakeStatuses = Sleep | 0x40 | 0x00400000;
    private const uint SupportedRecordStatuses = Death | Sleep | Poison | Slow | Darkness | Ff7NativeBattleStatuses.SupportedRecordStatuses;
    private const uint Petrify = 0x4000;
    private const uint Peerless = 0x01000000;
    private const uint DeathForce = 0x10000000;
    private const uint Resist = 0x20000000;

    public static Ff7BattleAttack ReadItem(ushort itemId, IReadOnlyList<KernelSection> kernel)
    {
        return TryReadItem(itemId, kernel) ?? throw new NotSupportedException($"FFVII item {itemId} has no supported in-battle action.");
    }

    public static Ff7BattleAttack? TryReadItem(ushort itemId, IReadOnlyList<KernelSection> kernel)
    {
        ArgumentNullException.ThrowIfNull(kernel);
        ReadOnlySpan<byte> items = kernel.Single(section => section.Type == 4).Data.Span;
        if (items.Length != 128 * Ff7BattleAttack.RecordSize)
        {
            throw new InvalidDataException("FFVII battle item requires an original consumable record.");
        }
        if (itemId >= 128)
        {
            return null;
        }
        ReadOnlySpan<byte> item = items.Slice(itemId * Ff7BattleAttack.RecordSize, Ff7BattleAttack.RecordSize);
        Ff7BattleAttack record = Ff7BattleAttack.Read(item);
        if ((item[10] & 1) != 0 || record.DamageCalculation is not (0x08 or 0x24 or 0x26 or 0x37 or 0x47) || (ReadStatusMask(record) & ~SupportedRecordStatuses) != 0 || record.AdditionalEffect != byte.MaxValue)
        {
            return null;
        }
        return record with { Accuracy = byte.MaxValue, CastingCost = 0 };
    }

    public static bool CanTarget(Ff7BattleActionVitals target, Ff7BattleAttack? record)
    {
        bool dead = (target.Statuses & Death) != 0;
        return !dead || record is { SpecialFlags: var flags } && (flags & 0x900) != 0x900;
    }

    public static bool TrySpendCastingCost(Ff7BattleActionVitals source, Ff7BattleAttack record, out Ff7BattleActionVitals remaining)
    {
        ArgumentNullException.ThrowIfNull(record);
        remaining = source;
        if (record.CastingCost > source.Mp || (record.CastingCost != 0 && (source.Statuses & 0x80) != 0))
        {
            return false;
        }
        remaining = source with { Mp = (ushort)(source.Mp - record.CastingCost) };
        return true;
    }

    public static bool TrySpendItem(IReadOnlyList<Ff7InventorySlot> inventory, ushort itemId, out Ff7InventorySlot[] remaining)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        remaining = inventory.ToArray();
        int index = Array.FindIndex(remaining, slot => slot.ItemId == itemId);
        if (index < 0 || remaining[index].Quantity <= 0)
        {
            return false;
        }
        if (remaining[index].Quantity == 1)
        {
            remaining = [.. remaining.AsSpan(0, index), .. remaining.AsSpan(index + 1)];
        }
        else
        {
            remaining[index] = remaining[index] with { Quantity = remaining[index].Quantity - 1 };
        }
        return true;
    }

    public static Ff7BattleActionReceipt Resolve(Ff7BattleDamageActor sourceStats, Ff7BattleActionVitals source, Ff7BattleDamageActor targetStats, Ff7BattleActionVitals target, Ff7BattleDamageAction action, Ff7BattleAttack? record, Ff7BattleRng random, bool sameActor = false)
        => ResolveCore(sourceStats, source, targetStats, target, action, record, random, sameActor, false, null);

    public static Ff7BattleActionReceipt ResolveGuestContact(Ff7BattleDamageActor sourceStats, Ff7BattleActionVitals source, Ff7BattleDamageActor targetStats, Ff7BattleActionVitals target, Ff7BattleDamageAction action, Ff7BattleAttack? record, Ff7BattleRng random, bool sameActor = false, IReadOnlyList<byte>? originStatusResistances = null)
        => ResolveCore(sourceStats, source, targetStats, target, action, record, random, sameActor, true, originStatusResistances);

    private static Ff7BattleActionReceipt ResolveCore(Ff7BattleDamageActor sourceStats, Ff7BattleActionVitals source, Ff7BattleDamageActor targetStats, Ff7BattleActionVitals target, Ff7BattleDamageAction action, Ff7BattleAttack? record, Ff7BattleRng random, bool sameActor, bool guestContact, IReadOnlyList<byte>? originStatusResistances)
    {
        ArgumentNullException.ThrowIfNull(random);
        if (action.RequiresKnockedOutTarget && target.Hp != 0 && (target.Statuses & Death) == 0 || action.RequiredTargetLevelMultiple != 0 && targetStats.Level % action.RequiredTargetLevelMultiple != 0)
        {
            return new(source, target, new(true, false, 0), 0, 0);
        }
        int mode = Ff7NativeBattleStatuses.ReadRecordMode(record);
        uint mask = ReadStatusMask(record);
        uint supportedStatuses = guestContact ? Ff7GuestBattleStatuses.SupportedHost | Ff7NativeBattleStatuses.SupportedRecordStatuses : SupportedRecordStatuses;
        if ((mask & ~supportedStatuses) != 0 || record is { AdditionalEffect: not byte.MaxValue })
        {
            throw new NotSupportedException($"FFVII attack has unsupported additional effect or status mask 0x{mask:X8}.");
        }
        bool admittedStatus = mask != 0 && RollStatus(record!, action, random);
        if (!admittedStatus && action.Power == 0 && action.BaseDamage is null && action.FixedDamage is null && action.FractionDenominator == 0 && action.Formula is not (0x00 or 0x50))
        {
            return new(source, target, new(true, false, 0), 0, 0);
        }
        if (action.Formula == 0x08)
        {
            if (mask != 0)
            {
                throw new NotSupportedException("FFVII full recovery with a separate status operation has no admitted native contract.");
            }
            if (!CanTarget(target, record))
            {
                return new(source, target, new(true, false, 0), 0, 0);
            }
            if ((action.Affinity & Ff7BattleDamageAffinity.Absorb) != 0)
            {
                if ((target.Statuses & Death) != 0)
                {
                    return new(source, target, new(true, false, 0), 0, 0);
                }
                Ff7BattleActionVitals killed = target with { Hp = 0, Statuses = target.Statuses | Death };
                return new(sameActor ? killed : source, killed, new(false, false, 0), Death, 0) { NativeResultMarker = -2 };
            }
            Ff7BattleActionVitals recovered = target with { Hp = target.MaxHp, Mp = target.MaxMp };
            return new(sameActor ? recovered : source, recovered, new(false, false, 0), 0, 0) { NativeResultMarker = -3 };
        }

        Ff7BattleDamageActor projectedTarget = targetStats with { Statuses = target.Statuses, CurrentHitPoints = target.Hp, MaximumHitPoints = target.MaxHp, CurrentMagicPoints = target.Mp, MaximumMagicPoints = target.MaxMp };
        Ff7BattleDamageAction projectedAction = action with { HasStatusOperation = admittedStatus, AddsStatuses = mode == 0 && mask != 0 };
        Ff7BattleDamageResult damage = Ff7NativeBattleDamage.Resolve(sourceStats with { Statuses = source.Statuses }, projectedTarget, projectedAction, random);
        if (damage.Missed || !CanTarget(target, record))
        {
            return new(source, target, new(true, false, 0), 0, 0);
        }

        uint admittedMask = admittedStatus ? mask : 0;
        if (admittedMask != 0 && mode is 0 or 2 && originStatusResistances is not null)
        {
            admittedMask = Ff7GuestBattleStatuses.ResistIncoming(admittedMask, target.Statuses, originStatusResistances, random);
        }
        uint statuses = ApplyStatuses(target, targetStats.IsEnemy, targetStats.NativeConditions, record, action.Formula, admittedMask, mode, guestContact, action.IsItem);
        if (!action.RequiresKnockedOutTarget && (target.Statuses & (Petrify | Peerless)) != 0)
        {
            damage = damage with { Damage = 0 };
        }
        Ff7BattleActionVitals nextTarget = ApplyDamage(target with { Hp = action.RequiresKnockedOutTarget ? 0 : target.Hp, Statuses = statuses }, damage.Damage, action.TargetsMagicPoints);
        if (guestContact && (target.Statuses & Death) == 0 && (nextTarget.Statuses & Death) != 0)
        {
            nextTarget = nextTarget with { Hp = 0 };
        }
        Ff7BattleActionVitals nextSource = sameActor ? nextTarget : source;
        if (record is { SpecialFlags: var drainFlags } && (drainFlags & 0x10) == 0 && damage.Damage != 0)
        {
            uint oldPool = action.TargetsMagicPoints ? target.Mp : target.Hp;
            int drain = (int)Math.Min(Math.Abs((long)damage.Damage), oldPool);
            if (damage.Damage < 0)
            {
                drain = -drain;
            }
            nextSource = ApplyDamage(nextSource, -drain, action.TargetsMagicPoints);
            if (sameActor)
            {
                nextTarget = nextSource;
            }
        }
        return new(nextSource, nextTarget, damage, nextTarget.Statuses & ~target.Statuses, target.Statuses & ~nextTarget.Statuses);
    }

    private static uint ReadStatusMask(Ff7BattleAttack? record) => Ff7NativeBattleStatuses.ReadRecordMask(record);

    public static Ff7BattleActionReceipt ApplyPreparedHpDamage(Ff7BattleActionVitals source, Ff7BattleActionVitals target, Ff7BattleDamageResult damage)
    {
        if (damage.Missed)
        {
            return new(source, target, damage, 0, 0);
        }
        Ff7BattleActionVitals next = ApplyDamage(target, damage.Damage, false);
        return new(source, next, damage, next.Statuses & ~target.Statuses, target.Statuses & ~next.Statuses);
    }

    internal static Ff7BattleActionReceipt ApplyPreparedGuestContact(Ff7BattleActionVitals source, Ff7BattleActionVitals target, Ff7BattleActionReceipt prepared, Ff7BattleDamageAction action, bool sameActor)
    {
        if (prepared.Damage.Missed)
        {
            return new(source, target, prepared.Damage, 0, 0);
        }
        uint statuses = (target.Statuses & ~prepared.RemovedStatuses) | prepared.AddedStatuses;
        Ff7BattleActionVitals next = ApplyDamage(target with { Hp = action.RequiresKnockedOutTarget ? 0 : target.Hp, Statuses = statuses }, prepared.Damage.Damage, action.TargetsMagicPoints);
        if ((target.Statuses & Death) == 0 && (next.Statuses & Death) != 0)
        {
            next = next with { Hp = 0 };
        }
        return new(sameActor ? next : source, next, prepared.Damage, next.Statuses & ~target.Statuses, target.Statuses & ~next.Statuses);
    }

    internal static Ff7BattleActionReceipt ApplyGuestItem(Ff7BattleActionVitals source, Ff7BattleActionVitals target, Ff7BattleGuestItemEffect effect, Ff7BattleDamageResult? prepared = null, bool nullifyRecovery = false)
    {
        if (effect.ReviveDivisor is not (0 or 4 or 8) || effect.MaximumHitPointFraction > 16 || effect.MaximumHitPointFraction != 0 && effect.ReviveDivisor != 0 || (effect.RemoveStatuses & ~Ff7GuestBattleStatuses.SupportedHost) != 0 || (effect.RemoveStatuses & Death) != 0)
        {
            throw new NotSupportedException("Guest recovery requires an admitted original item effect.");
        }
        bool revive = effect.ReviveDivisor != 0;
        bool dead = target.Hp == 0 || (target.Statuses & Death) != 0;
        int recovery = effect.MaximumHitPointFraction != 0 ? (int)Math.Min(int.MaxValue, (long)target.MaxHp * effect.MaximumHitPointFraction / 16) : effect.HitPoints;
        Ff7BattleDamageResult damage = prepared ?? new(dead != revive, false, -(revive ? Math.Max(1, (int)target.MaxHp / effect.ReviveDivisor) : recovery));
        if (nullifyRecovery)
        {
            damage = damage with { Damage = 0 };
        }
        if (damage.Missed || dead != revive)
        {
            return new(source, target, new(true, false, 0), 0, 0);
        }
        uint statuses = target.Statuses & ~(effect.RemoveStatuses | (revive ? Death : 0u));
        Ff7BattleActionVitals next = ApplyDamage(target with { Statuses = statuses }, damage.Damage, false);
        return new(source, next, damage, next.Statuses & ~target.Statuses, target.Statuses & ~next.Statuses);
    }

    private static bool RollStatus(Ff7BattleAttack record, Ff7BattleDamageAction action, Ff7BattleRng random)
    {
        if (Ff7NativeBattleStatuses.ReadImprisonedType(record) is not null)
        {
            return true;
        }
        int threshold = (record.StatusChange & 0x3F) * 4;
        if (threshold >= 252)
        {
            return true;
        }
        if (action.Split)
        {
            threshold = threshold * 2 / 3;
        }
        int roll = (random.NextByte() * 100 >> 8) + 1;
        return roll < threshold;
    }

    private static uint ApplyStatuses(Ff7BattleActionVitals target, bool enemy, uint conditions, Ff7BattleAttack? record, byte formula, uint mask, int mode, bool guestContact, bool item)
    {
        uint immunity = target.StatusImmunities;
        if (!item && (target.Statuses & Resist) != 0)
        {
            immunity |= 0x5FFFFFFF;
        }
        if (!item && (target.Statuses & DeathForce) != 0)
        {
            immunity |= Death;
        }
        if ((target.Statuses & Peerless) != 0)
        {
            immunity |= 0x7FFFFFFF;
        }
        if ((immunity & 0x300) != 0)
        {
            immunity |= 0x300;
        }
        if (!enemy && mode == 1 && (mask & Death) != 0)
        {
            immunity &= ~Death;
        }
        if ((conditions & 0x1000) != 0)
        {
            immunity |= Death;
        }
        if (record is { SpecialFlags: var flags } && (flags & 0x80) == 0)
        {
            immunity = 0;
        }

        uint add = mode == 0 && (target.Statuses & Petrify) == 0 ? mask : 0;
        uint remove = mode == 1 ? mask : 0;
        if ((formula >> 4) is 1 or 6 or 10 or 11)
        {
            remove |= target.Statuses & PhysicalWakeStatuses;
        }
        if ((add & Slow & ~immunity) != 0 || !guestContact && (add & Slow) != 0)
        {
            remove |= 0x100;
        }
        if (guestContact && (add & 0x100 & ~immunity) != 0)
        {
            remove |= Slow;
        }
        uint toggle = mode == 2 ? mask : 0;
        return ((target.Statuses | (add & ~immunity)) & ~(remove & ~immunity)) ^ (toggle & ~immunity);
    }

    private static Ff7BattleActionVitals ApplyDamage(Ff7BattleActionVitals target, int damage, bool magicPoints)
    {
        if (magicPoints)
        {
            return target with { Mp = (ushort)Math.Clamp((long)target.Mp - damage, 0, target.MaxMp) };
        }
        uint hp = (uint)Math.Clamp((long)target.Hp - damage, 0, target.MaxHp);
        return target with { Hp = hp, Statuses = hp == 0 ? target.Statuses | Death : target.Statuses };
    }
}
