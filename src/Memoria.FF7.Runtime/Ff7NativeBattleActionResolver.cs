using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

public readonly record struct Ff7BattleActionVitals(uint Hp, uint MaxHp, ushort Mp, ushort MaxMp, uint Statuses, uint StatusImmunities = 0)
{
    public ushort OriginSavedStatuses { get; init; }
}

public readonly record struct Ff7BattleActionReceipt(Ff7BattleActionVitals Source, Ff7BattleActionVitals Target, Ff7BattleDamageResult Damage, uint AddedStatuses, uint RemovedStatuses)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public sbyte? NativeResultMarker { get; init; }
    public uint AddedOriginBattleStatuses { get; init; }
    public uint RemovedOriginBattleStatuses { get; init; }
    public ushort AddedOriginSavedStatuses { get; init; }
    public ushort RemovedOriginSavedStatuses { get; init; }
    public int SourceHitPointDamage { get; init; }
    public Ff7BattleParameterOperation? AddedParameterStatus { get; init; }
    public uint RemovedParameterStatus { get; init; }
}

/// <summary>Resolves the native target transaction. The action owner publishes the receipt and spends costs once.</summary>
public static class Ff7NativeBattleActionResolver
{
    public const uint Death = 1;
    public const uint Sleep = 4;
    public const uint Poison = 8;
    public const uint Slow = 0x200;
    public const uint Darkness = 0x04000000;
    private const uint Stop = 0x400;

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

    public static Ff7BattleActionReceipt ResolveGuestContact(Ff7BattleDamageActor sourceStats, Ff7BattleActionVitals source, Ff7BattleDamageActor targetStats, Ff7BattleActionVitals target, Ff7BattleDamageAction action, Ff7BattleAttack? record, Ff7BattleRng random, bool sameActor = false, IReadOnlyList<byte>? originStatusResistances = null, uint originBattleStatuses = 0, ushort originSourceStatuses = 0, ushort originTargetStatuses = 0)
    {
        action.ParameterOperation?.Validate();
        if ((action.GuestOriginBattleStatuses & ~Ff7GuestBattleStatuses.OriginMulticast) != 0 || (action.GuestOriginSavedStatuses & ~Ff7OriginSavedStatusRules.Supported) != 0)
        {
            throw new NotSupportedException("The guest contact contains an unsupported source status operation.");
        }
        sourceStats = sourceStats with { OriginSavedStatuses = (ushort)(sourceStats.OriginSavedStatuses | originSourceStatuses) };
        targetStats = targetStats with { OriginSavedStatuses = (ushort)(targetStats.OriginSavedStatuses | originTargetStatuses) };
        Ff7BattleActionReceipt receipt = ResolveCore(sourceStats, source, targetStats, target, action, record, random, sameActor, true, originStatusResistances);
        if (receipt.Damage.Missed)
        {
            return receipt;
        }
        uint mask = action.GuestOriginBattleStatuses;
        uint changed = 0;
        foreach (uint bit in new uint[] { 0x20000, 0x40000 })
        {
            if ((mask & bit) == 0 || (originBattleStatuses & bit) != 0 && !action.Healing)
            {
                continue;
            }
            if (action.Healing || action.GuestOriginStatusAttack == 255)
            {
                changed |= bit;
                continue;
            }
            int resistance = originStatusResistances?[bit == 0x20000 ? 25 : 26] ?? 100;
            int effective = action.GuestOriginStatusAttack + sourceStats.Magic / 4 - targetStats.MagicDefense / 4 - resistance;
            if (resistance < 200 && effective > 0 && (action.GuestOriginStatusAttack >= 250 || random.NextByte() <= 255 * effective / 100))
            {
                changed |= bit;
            }
        }
        int percent = Math.Max(0, action.GuestDrainStatusAttack - (originStatusResistances?[23] ?? 100));
        int sourceDamage = action.GuestDrainStatusAttack == 0 ? 0 : checked((int)Math.Clamp(-(long)receipt.Damage.Damage * percent / 100, -9999, 9999));
        if (((sourceStats.OriginSavedStatuses ^ targetStats.OriginSavedStatuses) & Ff7OriginSavedStatusRules.Zombie) != 0)
        {
            sourceDamage = -sourceDamage;
        }
        Ff7BattleActionVitals nextSource = ApplyDamage(receipt.Source, sourceDamage, false);
        ushort savedChanged = 0;
        bool zombiePresent = Ff7OriginSavedStatusRules.IsZombie(targetStats.OriginSavedStatuses);
        if ((action.GuestOriginSavedStatuses & Ff7OriginSavedStatusRules.Zombie) != 0)
        {
            if (action.Healing)
            {
                savedChanged = (ushort)(targetStats.OriginSavedStatuses & Ff7OriginSavedStatusRules.Zombie);
            }
            else if (!zombiePresent && (receipt.Target.Statuses & (Death | Petrify | Peerless | Resist)) == 0)
            {
                int resistance = originStatusResistances?[6] ?? 100;
                int attack = action.UsePhysicalStats ? sourceStats.Attack : sourceStats.Magic;
                int defense = action.UsePhysicalStats ? targetStats.Defense : targetStats.MagicDefense;
                int effective = action.GuestOriginStatusAttack + attack / 4 - defense / 4 - resistance;
                if (action.GuestOriginStatusAttack == 255 || resistance < 200 && effective > 0 && (action.GuestOriginStatusAttack >= 250 || random.NextByte() <= 255 * effective / 100))
                {
                    savedChanged = Ff7OriginSavedStatusRules.Zombie;
                }
            }
        }
        ushort addedSaved = action.Healing ? (ushort)0 : savedChanged;
        ushort removedSaved = action.Healing ? savedChanged : (ushort)0;
        if ((receipt.Target.Statuses & Death) != 0)
        {
            addedSaved = 0;
            removedSaved |= (ushort)(targetStats.OriginSavedStatuses & Ff7OriginSavedStatusRules.Zombie);
        }
        if (addedSaved != 0 && (receipt.Target.Statuses & Ff7NativeBattleStatuses.DeathSentence) != 0)
        {
            receipt = receipt with { Target = receipt.Target with { Statuses = receipt.Target.Statuses & ~Ff7NativeBattleStatuses.DeathSentence }, RemovedStatuses = receipt.RemovedStatuses | Ff7NativeBattleStatuses.DeathSentence, AddedStatuses = receipt.AddedStatuses & ~Ff7NativeBattleStatuses.DeathSentence };
        }
        Ff7BattleParameterOperation? addedParameter = null;
        uint removedParameter = 0;
        if (action.ParameterOperation is { } parameter)
        {
            if (!action.Healing && targetStats.ParameterStatus is { } existing && existing.SourceStatusMask != parameter.SourceStatusMask)
            {
                throw new NotSupportedException("Concurrent defensive parameter statuses require independent owners.");
            }
            if (action.Healing)
            {
                removedParameter = targetStats.ParameterStatus?.SourceStatusMask == parameter.SourceStatusMask ? parameter.SourceStatusMask : 0;
            }
            else if (targetStats.ParameterStatus is null && (target.Statuses & (Petrify | Peerless | Resist)) == 0)
            {
                int resistance = originStatusResistances?[parameter.ResistanceIndex] ?? 100;
                int attack = action.UsePhysicalStats ? sourceStats.Attack : sourceStats.Magic;
                int defense = action.UsePhysicalStats ? targetStats.Defense : targetStats.MagicDefense;
                int effective = action.GuestOriginStatusAttack + attack / 4 - defense / 4 - resistance;
                if (action.GuestOriginStatusAttack == 255 || resistance < 200 && effective > 0 && (action.GuestOriginStatusAttack >= 250 || random.NextByte() <= 255 * effective / 100))
                {
                    addedParameter = parameter;
                }
            }
        }
        return receipt with
        {
            Source = nextSource,
            Target = sameActor ? nextSource : receipt.Target,
            AddedOriginBattleStatuses = action.Healing ? 0 : changed,
            RemovedOriginBattleStatuses = action.Healing ? changed & originBattleStatuses : 0,
            AddedOriginSavedStatuses = addedSaved,
            RemovedOriginSavedStatuses = removedSaved,
            SourceHitPointDamage = sourceDamage,
            AddedParameterStatus = addedParameter,
            RemovedParameterStatus = removedParameter
        };
    }

    private static Ff7BattleActionReceipt ResolveCore(Ff7BattleDamageActor sourceStats, Ff7BattleActionVitals source, Ff7BattleDamageActor targetStats, Ff7BattleActionVitals target, Ff7BattleDamageAction action, Ff7BattleAttack? record, Ff7BattleRng random, bool sameActor, bool guestContact, IReadOnlyList<byte>? originStatusResistances)
    {
        ArgumentNullException.ThrowIfNull(random);
        source = source with { OriginSavedStatuses = sourceStats.OriginSavedStatuses };
        target = target with { OriginSavedStatuses = targetStats.OriginSavedStatuses };
        bool zombie = Ff7OriginSavedStatusRules.IsZombie(targetStats.OriginSavedStatuses);
        bool revival = action.RequiresKnockedOutTarget || (ReadStatusMask(record) & Death) != 0 && Ff7NativeBattleStatuses.ReadRecordMode(record) == 1;
        bool offensiveRevival = zombie && revival && target.Hp != 0 && (target.Statuses & Death) == 0;
        if (offensiveRevival)
        {
            action = action with { RequiresKnockedOutTarget = false, Healing = false, Formula = 0x22, Power = 0, BaseDamage = null, FixedDamage = null, FractionNumerator = 0, FractionDenominator = 0, MinimumDamage = 0 };
            record = record is null ? throw new InvalidDataException("Offensive revival requires its status transaction.") : record with { StatusMask = Death, StatusChange = 63 };
        }
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
        bool originOperation = action.GuestOriginBattleStatuses != 0 || action.ParameterOperation is not null || action.GuestOriginSavedStatuses != 0;
        if (!admittedStatus && !originOperation && action.Power == 0 && action.BaseDamage is null && action.FixedDamage is null && action.FractionDenominator == 0 && action.Formula is not (0x00 or 0x50))
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
        Ff7BattleDamageAction projectedAction = action with { HasStatusOperation = admittedStatus || originOperation, AddsStatuses = mode == 0 && (mask != 0 || originOperation) };
        Ff7BattleDamageResult damage = Ff7NativeBattleDamage.Resolve(sourceStats with { Statuses = source.Statuses }, projectedTarget, projectedAction, random);
        if (damage.Missed || !CanTarget(target, record))
        {
            return new(source, target, new(true, false, 0), 0, 0);
        }

        uint admittedMask = admittedStatus ? mask : 0;
        if (zombie && mode == 0)
        {
            admittedMask &= ~(Ff7NativeBattleStatuses.DeathSentence | (offensiveRevival ? 0u : Death));
        }
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
        return new(nextSource, nextTarget, damage, nextTarget.Statuses & ~target.Statuses, target.Statuses & ~nextTarget.Statuses)
        {
            RemovedOriginSavedStatuses = (ushort)(target.OriginSavedStatuses & ~nextTarget.OriginSavedStatuses & Ff7OriginSavedStatusRules.Supported)
        };
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
        bool offensiveRevival = action.RequiresKnockedOutTarget && Ff7OriginSavedStatusRules.IsZombie(prepared.Target.OriginSavedStatuses) && (prepared.AddedStatuses & Death) != 0;
        ushort originSaved = (ushort)((target.OriginSavedStatuses | prepared.AddedOriginSavedStatuses) & ~prepared.RemovedOriginSavedStatuses);
        Ff7BattleActionVitals next = ApplyDamage(target with { Hp = action.RequiresKnockedOutTarget && !offensiveRevival ? 0 : target.Hp, Statuses = statuses, OriginSavedStatuses = originSaved }, prepared.Damage.Damage, action.TargetsMagicPoints);
        if ((target.Statuses & Death) == 0 && (next.Statuses & Death) != 0)
        {
            next = next with { Hp = 0 };
        }
        Ff7BattleActionVitals nextSource = ApplyDamage(sameActor ? next : source, prepared.SourceHitPointDamage, false);
        return new(nextSource, sameActor ? nextSource : next, prepared.Damage, next.Statuses & ~target.Statuses, target.Statuses & ~next.Statuses)
        {
            AddedOriginBattleStatuses = prepared.AddedOriginBattleStatuses,
            RemovedOriginBattleStatuses = prepared.RemovedOriginBattleStatuses,
            AddedOriginSavedStatuses = prepared.AddedOriginSavedStatuses,
            RemovedOriginSavedStatuses = prepared.RemovedOriginSavedStatuses,
            SourceHitPointDamage = prepared.SourceHitPointDamage,
            AddedParameterStatus = prepared.AddedParameterStatus,
            RemovedParameterStatus = prepared.RemovedParameterStatus
        };
    }

    internal static Ff7BattleActionReceipt ApplyGuestItem(Ff7BattleActionVitals source, Ff7BattleActionVitals target, Ff7BattleGuestItemEffect effect, Ff7BattleDamageResult? prepared = null, bool nullifyRecovery = false)
    {
        if (effect.ReviveDivisor is not (0 or 4 or 8) || effect.FractionRecoveryLimit is 0 or > 60000 || effect.RecoveryMultiplier is not (1 or 2) || effect.MaximumHitPointFraction == 0 && effect.RecoveryMultiplier != 1 || effect.MaximumHitPointFraction > 16 || effect.MaximumHitPointFraction != 0 && effect.ReviveDivisor != 0 || (effect.RemoveStatuses & ~Ff7GuestBattleStatuses.SupportedHost) != 0 || (effect.RemoveStatuses & Death) != 0)
        {
            throw new NotSupportedException("Guest recovery requires an admitted original item effect.");
        }
        bool revive = effect.ReviveDivisor != 0;
        bool dead = target.Hp == 0 || (target.Statuses & Death) != 0;
        int recovery = effect.MaximumHitPointFraction != 0 ? (int)Math.Min(effect.FractionRecoveryLimit, (long)target.MaxHp * effect.MaximumHitPointFraction / 16 * effect.RecoveryMultiplier) : effect.HitPoints;
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
        return new(source, next, damage, next.Statuses & ~target.Statuses, target.Statuses & ~next.Statuses) { RemovedParameterStatus = effect.RemoveParameterStatuses };
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
        uint statuses = hp == 0 ? target.Statuses | Death : target.Statuses;
        if ((statuses & Death) != 0 && (target.StatusImmunities & Stop) == 0)
        {
            statuses &= ~Stop;
        }
        ushort originSaved = (statuses & Death) != 0 ? (ushort)(target.OriginSavedStatuses & ~Ff7OriginSavedStatusRules.Zombie) : target.OriginSavedStatuses;
        return target with { Hp = hp, Statuses = statuses, OriginSavedStatuses = originSaved };
    }
}
