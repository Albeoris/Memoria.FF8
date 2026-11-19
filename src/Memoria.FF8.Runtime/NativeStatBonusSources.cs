using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeStatBonusGroup(string Source, int HitPoints, ImmutableArray<int> Stats);

/// <summary>Permanent growth by origin. Unknown source identifiers remain ordinary permanent growth.</summary>
public sealed record NativeStatBonusSources(ImmutableArray<NativeStatBonusGroup> Groups)
{
    public const string LevelUp = "level-up";
    public const string Items = "items";
    public const string Devour = "devour";
    public const string Initial = "initial";

    public void Validate()
    {
        if (Groups.IsDefault || Groups.Any(group => group is null || string.IsNullOrWhiteSpace(group.Source) || group.HitPoints is < 0 or > 9999 || group.Stats.IsDefault || group.Stats.Length != 6 || group.Stats.Any(value => value is < 0 or > 255)) || Groups.Select(group => group.Source).Distinct(StringComparer.Ordinal).Count() != Groups.Length)
        {
            throw new InvalidDataException("Stat bonus sources require distinct identifiers and bounded HP and six statistics.");
        }
    }

    public static NativeStatBonusSources FromInitial(ushort hp, IReadOnlyList<byte> stats)
    {
        return new([new(Initial, hp, stats.Select(value => (int)value).ToImmutableArray())]);
    }

    public static NativeStatBonusSources FromLegacy(ushort hp, IReadOnlyList<byte> stats, bool hpBonus = true, IReadOnlyList<bool>? statBonus = null)
    {
        int levelHp = hpBonus ? Math.Min((int)hp, 2970) : 0;
        ImmutableArray<int> levelStats = Enumerable.Range(0, 6).Select(index => (statBonus?[index] ?? index < 4) ? Math.Min((int)stats[index], 99) : 0).ToImmutableArray();
        return new([new(LevelUp, levelHp, levelStats), new(Items, hp - levelHp, Enumerable.Range(0, 6).Select(index => stats[index] - levelStats[index]).ToImmutableArray())]);
    }

    public BattleNativeCharacterGrowth Total(bool excludeLevelUp = false)
    {
        Validate();
        NativeStatBonusGroup[] selected = Groups.Where(group => !excludeLevelUp || group.Source != LevelUp).ToArray();
        return new((ushort)Math.Min(9999L, selected.Sum(group => (long)group.HitPoints)), Enumerable.Range(0, 6).Select(index => (byte)Math.Min(255L, selected.Sum(group => (long)group.Stats[index]))).ToImmutableArray());
    }

    public NativeStatBonusSources Add(string source, int hp, IReadOnlyList<int> stats, bool levelBased)
    {
        Validate();
        if (hp < 0 || stats.Count != 6 || stats.Any(value => value < 0))
        {
            throw new ArgumentOutOfRangeException(nameof(stats));
        }
        NativeStatBonusGroup current = Groups.FirstOrDefault(group => group.Source == source) ?? new(source, 0, [0, 0, 0, 0, 0, 0]);
        // Hidden level growth must never consume room available to item or Devour growth.
        BattleNativeCharacterGrowth used = levelBased && source == LevelUp ? new((ushort)current.HitPoints, current.Stats.Select(value => (byte)value).ToImmutableArray()) : Total(levelBased);
        NativeStatBonusGroup updated = current with
        {
            HitPoints = Math.Min(9999, current.HitPoints + Math.Min(hp, 9999 - used.HitPointBonus)),
            Stats = Enumerable.Range(0, 6).Select(index => Math.Min(255, current.Stats[index] + Math.Min(stats[index], 255 - used.StatBonuses[index]))).ToImmutableArray()
        };
        return new(Groups.Where(group => group.Source != source).Append(updated).ToImmutableArray());
    }

    public bool EquivalentTo(NativeStatBonusSources other)
    {
        return Groups.Length == other.Groups.Length && Groups.All(group => other.Groups.Any(candidate => group.Source == candidate.Source && group.HitPoints == candidate.HitPoints && group.Stats.AsSpan().SequenceEqual(candidate.Stats.AsSpan())));
    }
}

public sealed partial record NativeCharacterVitals
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool LevelBasedStatBonuses { get; init; }

    public BattleNativeCharacterGrowth EffectiveGrowth(NativeCharacterVital character)
    {
        int level = CharacterLevel(character);
        return character.Growth.Effective(LevelBasedStatBonuses, level, character.Junction.Abilities);
    }
}
