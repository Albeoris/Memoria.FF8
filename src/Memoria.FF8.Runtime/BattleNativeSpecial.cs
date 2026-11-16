using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record BattleNativeEnemySpecial(byte CommonCard, byte RareCard, ImmutableArray<byte> DevourEffects, byte DevourOutcome);
public sealed record BattleNativeDevourEffect(byte Effect, byte Quantity, ushort SavedStatuses, uint BattleStatuses, byte StatFlags, byte HitPointBonus, string? Description);
public sealed record BattleNativeSpecialContext(byte? Card, int? DevourEffect, byte? DevourOutcome, bool Failed);
public sealed record BattleNativeCharacterGrowth(ushort HitPointBonus, ImmutableArray<byte> StatBonuses)
{
    public NativeStatBonusSources? BonusSources { get; init; }
    [System.Text.Json.Serialization.JsonIgnore]
    public NativeStatBonusSources Sources => BonusSources ?? NativeStatBonusSources.FromLegacy(HitPointBonus, StatBonuses);

    public BattleNativeCharacterGrowth Add(string source, int hp, IReadOnlyList<int> stats, bool levelBased)
    {
        if (hp == 0 && stats.All(value => value == 0))
        {
            return this;
        }
        NativeStatBonusSources sources = Sources.Add(source, hp, stats, levelBased);
        return sources.Total() with { BonusSources = sources };
    }

    public bool SameSources(BattleNativeCharacterGrowth other) => Sources.EquivalentTo(other.Sources);

    public BattleNativeCharacterGrowth Effective(bool levelBased, int level, uint abilities)
    {
        if (!levelBased)
        {
            return BonusSources?.Total() ?? this;
        }
        BattleNativeCharacterGrowth permanent = Sources.Total(true);
        bool Equipped(int id) => Enumerable.Range(0, 4).Any(slot => (byte)(abilities >> (8 * slot)) == id);
        return new((ushort)Math.Min(9999, permanent.HitPointBonus + (Equipped(65) ? LevelBonus(65, level) : 0)), Enumerable.Range(0, 6).Select(index => (byte)Math.Min(255, permanent.StatBonuses[index] + (index < 4 && Equipped(66 + index) ? LevelBonus(66 + index, level) : 0))).ToImmutableArray());
    }

    public static int LevelBonusPerLevel(int ability) => ability switch
    {
        65 => 30,
        >= 66 and <= 69 => 1,
        _ => 0
    };

    public static int LevelBonus(int ability, int level) => Math.Clamp(level - 1, 0, 99) * LevelBonusPerLevel(ability);

    public void Validate()
    {
        if (HitPointBonus > 9999 || StatBonuses.IsDefault || StatBonuses.Length != 6)
        {
            throw new InvalidDataException("Native character growth requires its saved HP WORD and six statistic bonus BYTEs.");
        }
        if (BonusSources is { } sources)
        {
            BattleNativeCharacterGrowth total = sources.Total();
            if (total.HitPointBonus != HitPointBonus || !total.StatBonuses.AsSpan().SequenceEqual(StatBonuses.AsSpan()))
            {
                throw new InvalidDataException("Stat bonus source totals disagree with their native projection.");
            }
        }
    }
}
public sealed record BattleNativeGrowthChange(byte Row, BattleNativeCharacterGrowth Before, BattleNativeCharacterGrowth After);
public sealed record BattleNativeCardChange(NativeCardInventory Before, NativeCardInventory After);
