using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

public readonly record struct Ff7BattleDamageActor(byte Level, byte Attack, byte Magic, byte Dexterity, byte Luck, byte Evade, byte MagicEvade, ushort Defense, ushort MagicDefense, byte CriticalBonus = 0, uint Statuses = 0, bool IsEnemy = false, bool BackRow = false, bool Defending = false, sbyte AttackModifier = 0, sbyte MagicModifier = 0, sbyte DefenseModifier = 0, sbyte MagicDefenseModifier = 0, sbyte EvadeModifier = 0, sbyte DexterityModifier = 0, uint? CurrentHitPoints = null, uint? MaximumHitPoints = null, ushort? CurrentMagicPoints = null, ushort? MaximumMagicPoints = null)
{
    public uint NativeConditions { get; init; }
}

[Flags]
public enum Ff7BattleDamageAffinity : byte
{
    Normal = 0,
    Double = 4,
    Half = 16,
    Nullify = 32,
    Absorb = 64
}

public readonly record struct Ff7BattleDamageAction(byte Formula, byte Power, byte Accuracy, bool LongRange = false, bool BackAttack = false, byte BackAttackMultiplier = 16, bool Split = false, bool ForceCritical = false, bool Healing = false, bool CanBeBlinded = false, Ff7BattleDamageAffinity Affinity = Ff7BattleDamageAffinity.Normal, bool UsePhysicalStats = false, bool IgnoreDefense = false, bool HasStatusOperation = false, bool AddsStatuses = false, bool TargetsMagicPoints = false)
{
    public bool SuppressCritical { get; init; }
    public ushort NativeElements { get; init; }
    public bool IsItem { get; init; }
    public bool IgnoreNativeAffinity { get; init; }
    public bool? NativePhysicalCategory { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool AlwaysHits { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public int? BaseDamage { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public int? FixedDamage { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public int MinimumDamage { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public int FractionNumerator { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public int FractionDenominator { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public byte RequiredTargetLevelMultiple { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool RequiresKnockedOutTarget { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    internal byte? GuestElementPercent { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    internal int GuestElementAdjustment { get; init; }

    public static Ff7BattleDamageAction FromAttack(Ff7BattleAttack attack)
    {
        ArgumentNullException.ThrowIfNull(attack);
        int statusOperation = attack.StatusChange >> 6;
        bool hasStatus = statusOperation < 3 && attack.StatusMask != 0;
        bool physicalFamily = (attack.DamageCalculation >> 4) is 1 or 3 or 6 or 10 or 11;
        return new(attack.DamageCalculation, attack.Power, attack.Accuracy, LongRange: (attack.TargetFlags & 0x20) == 0, ForceCritical: (attack.SpecialFlags & 0x2000) == 0, Healing: attack.Elements != ushort.MaxValue && (attack.Elements & 0x200) != 0, CanBeBlinded: (attack.SpecialFlags & 4) == 0, UsePhysicalStats: physicalFamily || (attack.SpecialFlags & 4) == 0, IgnoreDefense: (attack.SpecialFlags & 0x400) == 0, HasStatusOperation: hasStatus, AddsStatuses: hasStatus && statusOperation == 0, TargetsMagicPoints: (attack.SpecialFlags & 1) == 0) { NativePhysicalCategory = physicalFamily || (attack.SpecialFlags & 4) == 0, NativeElements = attack.Elements == ushort.MaxValue ? (ushort)0 : attack.Elements, IgnoreNativeAffinity = (attack.SpecialFlags & 0x80) == 0 };
    }
}

/// <summary>A negative damage value restores the targeted HP or MP pool.</summary>
public readonly record struct Ff7BattleDamageResult(bool Missed, bool Critical, int Damage);

/// <summary>Resolves the supported native per-target damage calculations against the battle random stream.</summary>
public static class Ff7NativeBattleDamage
{
    private const uint Sadness = 0x10;
    private const uint Fury = 0x20;
    private const uint Frog = 0x800;
    private const uint Small = 0x1000;
    private const uint Barrier = 0x10000;
    private const uint MagicBarrier = 0x20000;
    private const uint Berserk = 0x800000;
    private const uint Darkness = 0x4000000;
    private const uint LuckyGirl = 0x40000000;
    private const uint GuaranteedPhysicalHit = 0x02404445;
    private const uint GuaranteedStatusMagicHit = 0x02004445;

    public static Ff7BattleDamageActor FromEnemy(SceneBattleEnemy enemy)
    {
        ArgumentNullException.ThrowIfNull(enemy);
        return new(enemy.Level, enemy.Strength, enemy.Magic, enemy.Speed, enemy.Luck, enemy.Evasion, 0, (ushort)(enemy.Defense * 2), (ushort)(enemy.MagicDefense * 2), IsEnemy: true);
    }

    public static Ff7BattleDamageAffinity ResolveEnemyAffinity(SceneBattleEnemy target, ushort elements)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (elements == ushort.MaxValue)
        {
            return Ff7BattleDamageAffinity.Normal;
        }
        Ff7BattleDamageAffinity result = Ff7BattleDamageAffinity.Normal;
        foreach (SceneEnemyAffinity affinity in target.Affinities)
        {
            if (affinity.Type >= 16 || (elements & (1 << affinity.Type)) == 0)
            {
                continue;
            }
            result |= affinity.Rate switch
            {
                2 => Ff7BattleDamageAffinity.Double,
                4 => Ff7BattleDamageAffinity.Half,
                5 => Ff7BattleDamageAffinity.Nullify,
                6 => Ff7BattleDamageAffinity.Absorb,
                _ => throw new NotSupportedException($"FFVII enemy {target.Placement.EnemyId} has unsupported elemental affinity rate {affinity.Rate}.")
            };
        }
        return result;
    }

    public static Ff7BattleDamageResult Resolve(Ff7BattleDamageActor attacker, Ff7BattleDamageActor target, Ff7BattleDamageAction action, Ff7BattleRng random)
    {
        ArgumentNullException.ThrowIfNull(random);
        const Ff7BattleDamageAffinity supportedAffinities = Ff7BattleDamageAffinity.Double | Ff7BattleDamageAffinity.Half | Ff7BattleDamageAffinity.Nullify | Ff7BattleDamageAffinity.Absorb;
        if (action.Formula is not (0x00 or 0x04 or 0x11 or 0x22 or 0x23 or 0x24 or 0x25 or 0x26 or 0x37 or 0x47 or 0x50 or 0xB1) || (action.Affinity & ~supportedAffinities) != 0)
        {
            throw new NotSupportedException($"FFVII battle HP damage formula 0x{action.Formula:X2} or affinity 0x{(byte)action.Affinity:X2} is unsupported.");
        }
        if (action.Formula is 0x00 or 0x50)
        {
            return new(false, false, 0);
        }
        if (action.BaseDamage is < 0 || action.FixedDamage is < 0 || action.MinimumDamage < 0 || action.FractionDenominator < 0 || action.FractionNumerator < 0 || action.FractionDenominator == 0 && action.FractionNumerator != 0)
        {
            throw new InvalidDataException("Battle damage operands must describe nonnegative damage and a valid fraction.");
        }
        if (action.RequiredTargetLevelMultiple != 0 && target.Level % action.RequiredTargetLevelMultiple != 0)
        {
            return new(true, false, 0);
        }
        bool hasDamage = action.Power != 0 || action.BaseDamage is not null || action.FixedDamage is not null || action.FractionDenominator != 0;
        if (!hasDamage && !action.HasStatusOperation)
        {
            return new(true, false, 0);
        }
        uint? fractionBasis = action.Formula switch
        {
            0x23 => action.TargetsMagicPoints ? target.CurrentMagicPoints : target.CurrentHitPoints,
            0x04 or 0x24 => action.TargetsMagicPoints ? target.MaximumMagicPoints : target.MaximumHitPoints,
            _ => 0
        };
        if (fractionBasis is not <= int.MaxValue)
        {
            throw new InvalidDataException("FFVII fractional damage requires the target's native vitals.");
        }

        int accuracy = action.Accuracy;
        if (action.CanBeBlinded && (attacker.Statuses & Darkness) != 0)
        {
            accuracy /= 2;
        }

        bool physical = action.Formula is 0x11 or 0xB1;
        bool hit = physical ? PhysicalHit(attacker, target, action, accuracy, random) : (action.Formula >> 4) != 2 || MagicHit(attacker, target, action, accuracy, random);
        if (!hasDamage)
        {
            return new(!hit, false, 0);
        }

        bool critical = false;
        int damage;
        if (physical)
        {
            critical = action.Formula == 0x11 && hit && !action.SuppressCritical && CriticalHit(attacker, target, random);
            critical |= action.ForceCritical;
            damage = PhysicalDamage(attacker, target, action, critical);
        }
        else
        {
            damage = action.Formula is 0x04 or 0x23 or 0x24 ? action.FractionDenominator == 0 ? unchecked((int)fractionBasis.Value * action.Power) / 32 : checked((int)((long)fractionBasis.Value * action.FractionNumerator / action.FractionDenominator)) : MagicDamage(attacker, target, action);
        }

        if (action.MinimumDamage != 0)
        {
            damage = Math.Max(action.MinimumDamage, damage);
        }
        if (action.Formula is not (0x04 or 0x23 or 0x24 or 0x26))
        {
            damage = Math.Max(1, damage * (3841 + random.NextByte()) >> 12);
        }
        if (!hit)
        {
            return new(true, false, 0);
        }
        action = action with { Affinity = action.IgnoreNativeAffinity ? Ff7BattleDamageAffinity.Normal : Ff7NativeBattleStatuses.ShieldAffinity(target.Statuses, action.NativeElements, action.IsItem, action.Affinity) };
        bool immune = (target.NativeConditions & ((action.NativePhysicalCategory ?? action.UsePhysicalStats) ? 0x100U : 0x200U)) != 0;
        int elementalDamage = immune ? 0 : ApplyAffinity(damage, action.Affinity);
        if (!immune && !action.IgnoreNativeAffinity && action.GuestElementPercent is byte percent)
        {
            elementalDamage = damage + (elementalDamage - damage) * percent / 100;
        }
        if (!immune && !action.IgnoreNativeAffinity && action.GuestElementAdjustment != 0)
        {
            elementalDamage = elementalDamage * (100 + action.GuestElementAdjustment) / 100;
        }
        int limit = action.TargetsMagicPoints ? 999 : 9999;
        damage = Math.Clamp(elementalDamage, -limit, limit);
        bool nullified = immune || (action.Affinity & Ff7BattleDamageAffinity.Nullify) != 0 && action.GuestElementPercent is null or 100;
        return new(false, critical && !nullified, action.Healing ? -damage : damage);
    }

    private static int ApplyAffinity(int damage, Ff7BattleDamageAffinity affinity)
    {
        if ((affinity & Ff7BattleDamageAffinity.Nullify) != 0)
        {
            return 0;
        }
        if ((affinity & Ff7BattleDamageAffinity.Absorb) != 0)
        {
            return -damage;
        }
        if ((affinity & Ff7BattleDamageAffinity.Double) != 0)
        {
            damage *= 2;
        }
        if ((affinity & Ff7BattleDamageAffinity.Half) != 0)
        {
            damage = (damage + 1) >> 1;
        }
        return damage;
    }

    private static bool PhysicalHit(Ff7BattleDamageActor attacker, Ff7BattleDamageActor target, Ff7BattleDamageAction action, int accuracy, Ff7BattleRng random)
    {
        int hitRate = 255;
        if (!action.AlwaysHits && !action.BackAttack && !GuaranteesHit(action) && (target.Statuses & GuaranteedPhysicalHit) == 0)
        {
            int dexterity = ApplyModifier(attacker.Dexterity, attacker.DexterityModifier);
            int attackerEvade = ApplyModifier(attacker.Evade + (attacker.IsEnemy ? 0 : attacker.Dexterity / 4), attacker.EvadeModifier);
            int targetEvade = ApplyModifier(target.Evade + (target.IsEnemy ? 0 : target.Dexterity / 4), target.EvadeModifier);
            hitRate = ApplyFury(accuracy + dexterity / 4 + attackerEvade - targetEvade, attacker.Statuses);
        }

        hitRate = Math.Max(hitRate, 1);
        int luckRoll = ScaleByteToPercent(random.NextByte());
        if (luckRoll < attacker.Luck / 4)
        {
            hitRate = 255;
        }
        else if (attacker.IsEnemy && !target.IsEnemy && luckRoll < target.Luck / 4)
        {
            hitRate = 0;
        }
        return RollPercent(random) < hitRate;
    }

    private static bool CriticalHit(Ff7BattleDamageActor attacker, Ff7BattleDamageActor target, Ff7BattleRng random)
    {
        int criticalRate = (attacker.Luck + attacker.Level - target.Level) / 4;
        if (!attacker.IsEnemy)
        {
            criticalRate += attacker.CriticalBonus;
        }
        return RollPercent(random) <= ((attacker.Statuses & LuckyGirl) != 0 ? 255 : criticalRate);
    }

    private static bool MagicHit(Ff7BattleDamageActor attacker, Ff7BattleDamageActor target, Ff7BattleDamageAction action, int accuracy, Ff7BattleRng random)
    {
        int evadeRoll = ScaleByteToPercent(random.NextByte());
        int hitRoll = ScaleByteToPercent(random.NextByte()) + 1;
        if (action.AlwaysHits || accuracy >= 255 || GuaranteesHit(action) || (!action.AddsStatuses && (target.Statuses & GuaranteedStatusMagicHit) != 0))
        {
            return true;
        }
        return evadeRoll >= target.MagicEvade && hitRoll < ApplyFury(accuracy, attacker.Statuses) + attacker.Level - target.Level / 2;
    }

    private static int PhysicalDamage(Ff7BattleDamageActor attacker, Ff7BattleDamageActor target, Ff7BattleDamageAction action, bool critical)
    {
        int attack = ApplyModifier(attacker.Attack, attacker.AttackModifier);
        int defense = action.IgnoreDefense ? 0 : Math.Min(512, ApplyModifier(target.Defense, target.DefenseModifier));
        int level = attacker.Level;
        int basis = attack + (attack * level / 32) * ((attack + level) / 32);
        int damage = basis * (512 - defense) * action.Power / 8192;
        if (critical)
        {
            damage *= 2;
        }
        if ((attacker.Statuses & Berserk) != 0)
        {
            damage = damage * 3 >> 1;
        }
        if (!action.LongRange && (attacker.BackRow || target.BackRow))
        {
            damage /= 2;
        }
        if (target.Defending)
        {
            damage /= 2;
        }
        if (action.BackAttack)
        {
            damage = damage * action.BackAttackMultiplier >> 3;
        }
        if ((attacker.Statuses & Frog) != 0)
        {
            damage >>= 2;
        }
        damage = ApplyDamageModifiers(damage, target.Statuses, Barrier, action.Split);
        return (attacker.Statuses & Small) == 0 ? damage : 0;
    }

    private static int MagicDamage(Ff7BattleDamageActor attacker, Ff7BattleDamageActor target, Ff7BattleDamageAction action)
    {
        if (action.Formula == 0x26)
        {
            return action.FixedDamage ?? action.Power * 20;
        }
        int stat = action.UsePhysicalStats ? ApplyModifier(attacker.Attack, attacker.AttackModifier) : ApplyModifier(attacker.Magic, attacker.MagicModifier);
        int basis = 6 * (stat + attacker.Level);
        uint barrier = action.UsePhysicalStats ? Barrier : MagicBarrier;
        if (action.Formula == 0x25)
        {
            return ApplyDamageModifiers(basis + action.Power * 22, target.Statuses & ~Sadness, barrier, action.Split);
        }
        int defense = action.UsePhysicalStats ? ApplyModifier(target.Defense, target.DefenseModifier) : ApplyModifier(target.MagicDefense, target.MagicDefenseModifier);
        defense = action.IgnoreDefense ? 0 : Math.Min(512, defense);
        if (action.Formula is 0x37 or 0x47)
        {
            return (512 - defense) * action.Power / 32;
        }
        int damage = action.BaseDamage is int supplied ? checked((int)((long)supplied * (512 - defense) / 512)) : basis * (512 - defense) * action.Power / 8192;
        return ApplyDamageModifiers(damage, target.Statuses, barrier, action.Split);
    }

    private static int ApplyDamageModifiers(int damage, uint statuses, uint barrier, bool split)
    {
        if ((statuses & Sadness) != 0)
        {
            damage -= damage * 3 / 10;
        }
        if (split)
        {
            damage = damage * 2 / 3;
        }
        if ((statuses & barrier) != 0)
        {
            damage /= 2;
        }
        return damage;
    }

    private static int ApplyModifier(int value, int modifier) => value + value * modifier / 100;

    private static int ApplyFury(int value, uint statuses) => value < 255 && (statuses & Fury) != 0 ? value - value * 3 / 10 : value;

    private static bool GuaranteesHit(Ff7BattleDamageAction action)
        => action.GuestElementPercent is null or 100 && (action.Affinity & (Ff7BattleDamageAffinity.Absorb | Ff7BattleDamageAffinity.Nullify)) != 0;

    private static int RollPercent(Ff7BattleRng random) => 1 + random.NextWord() * 99 / 65535;

    private static int ScaleByteToPercent(byte value) => value * 100 >> 8;
}
