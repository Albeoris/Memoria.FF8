using System.Collections.Immutable;
using System.Numerics;
using System.Text.Json.Serialization;
using Memoria.Battle;

namespace Memoria.FF8.Runtime;

public enum NativeStatusId
{
    Ko = 0,
    Poison = 1,
    Petrify = 2,
    Blind = 3,
    Silence = 4,
    Berserk = 5,
    Zombie = 6,
    Sleep = 16,
    Haste = 17,
    Slow = 18,
    Stop = 19,
    Regen = 20,
    Protect = 21,
    Shell = 22,
    Reflect = 23,
    Aura = 24,
    Curse = 25,
    Doom = 26,
    Invincible = 27,
    Petrifying = 28,
    Float = 29,
    Confuse = 30,
    Drain = 31,
    Double = 33,
    Triple = 34,
    Vit0 = 40,
    AngelWing = 41
}

public enum BattleStatId
{
    Strength,
    Vitality,
    Spirit
}

public enum StatusStatOperation
{
    ReducePercent
}

public readonly record struct StatusStatModifier(
    [property: JsonRequired] NativeStatusId Status,
    [property: JsonRequired] BattleStatId Stat,
    [property: JsonRequired] StatusStatOperation Operation,
    [property: JsonRequired] int Value);

public static class StatusStatRules
{
    public const int MinimumPercent = StatusStatScaling.MinimumPercent;
    public const int MaximumPercent = StatusStatScaling.MaximumPercent;

    public static ImmutableArray<StatusStatModifier> Native { get; } =
    [
        new(NativeStatusId.Vit0, BattleStatId.Vitality, StatusStatOperation.ReducePercent, MaximumPercent),
        new(NativeStatusId.Vit0, BattleStatId.Spirit, StatusStatOperation.ReducePercent, MaximumPercent)
    ];

    public static bool TryGetStatusId(bool saved, uint mask, out NativeStatusId status)
    {
        status = (NativeStatusId)(BitOperations.TrailingZeroCount(mask) + (saved ? 0 : 16));
        return BitOperations.IsPow2(mask) && (!saved || mask <= ushort.MaxValue) && Enum.IsDefined(status);
    }

    public static ImmutableArray<StatusStatModifier> Resolve(ImmutableArray<StatusStatModifier> overrides)
    {
        if (overrides.IsDefaultOrEmpty || overrides == Native)
        {
            return Native;
        }

        Dictionary<(NativeStatusId, BattleStatId), StatusStatModifier> rules = [];
        foreach (StatusStatModifier modifier in overrides)
        {
            if (!Enum.IsDefined(modifier.Status) || !Enum.IsDefined(modifier.Stat) || modifier.Operation != StatusStatOperation.ReducePercent ||
                modifier.Value is < MinimumPercent or > MaximumPercent)
            {
                throw new InvalidDataException("Status stat rules require a supported status, stat and operation with a percentage from zero to one hundred.");
            }
            if (!rules.TryAdd((modifier.Status, modifier.Stat), modifier))
            {
                throw new InvalidDataException($"Duplicate status stat rule for {modifier.Status}/{modifier.Stat}.");
            }
        }
        foreach (StatusStatModifier modifier in Native)
        {
            rules.TryAdd((modifier.Status, modifier.Stat), modifier);
        }
        return rules.Values.OrderBy(modifier => modifier.Status).ThenBy(modifier => modifier.Stat).ToImmutableArray();
    }

    public static int Apply(ImmutableArray<StatusStatModifier> rules, BattleStatId stat, int baseValue, ushort savedStatuses, uint battleStatuses)
    {
        if ((savedStatuses | battleStatuses) == 0)
        {
            return baseValue;
        }
        if (rules.IsDefaultOrEmpty)
        {
            rules = Native;
        }
        foreach (StatusStatModifier modifier in rules)
        {
            if (modifier.Stat == stat && IsActive(modifier.Status, savedStatuses, battleStatuses))
            {
                baseValue = modifier.Operation switch
                {
                    StatusStatOperation.ReducePercent => StatusStatScaling.ReducePercent(baseValue, modifier.Value),
                    _ => throw new InvalidDataException($"Unsupported status stat operation {modifier.Operation}.")
                };
            }
        }
        return baseValue;
    }

    private static bool IsActive(NativeStatusId status, ushort savedStatuses, uint battleStatuses)
    {
        int bit = (int)status;
        return bit < 16 ? (savedStatuses & (1u << bit)) != 0 : (battleStatuses & (1u << (bit - 16))) != 0;
    }
}
