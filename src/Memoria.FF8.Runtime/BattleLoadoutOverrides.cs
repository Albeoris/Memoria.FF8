using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record BattleCharacterLoadoutOverride
{
    public int? WeaponId { get; init; }
    public BattleLimitFamily[]? LimitFamilies { get; init; }

    public BattleCharacterLoadoutOverride DeepCopy()
    {
        return this with { LimitFamilies = LimitFamilies?.ToArray() };
    }
}

/// <summary>Durable logical assignments, independent of native physical character rows.</summary>
public sealed record BattleLoadoutOverrides
{
    public const int CurrentVersion = 1;
    public int Version { get; init; } = CurrentVersion;
    public ImmutableSortedDictionary<int, BattleCharacterLoadoutOverride> Characters { get; init; } = ImmutableSortedDictionary<int, BattleCharacterLoadoutOverride>.Empty;

    public BattleCharacterLoadoutOverride? Get(int characterId)
    {
        return Characters.GetValueOrDefault(characterId)?.DeepCopy();
    }

    public BattleLoadoutOverrides DeepCopy()
    {
        return this with { Characters = Characters.ToImmutableSortedDictionary(pair => pair.Key, pair => pair.Value.DeepCopy()) };
    }

    public void Validate()
    {
        if (Version != CurrentVersion || Characters is null)
        {
            throw new InvalidDataException("Unsupported battle loadout schema.");
        }
        foreach ((int character, BattleCharacterLoadoutOverride entry) in Characters)
        {
            if (character is < 0 or > 10 || entry is null || entry.WeaponId is < 0 || entry.LimitFamilies is { } families && (families.Any(family => !Enum.IsDefined(family)) || families.Distinct().Count() != families.Length))
            {
                throw new InvalidDataException($"Invalid battle loadout for character {character}.");
            }
        }
    }
}

public sealed record BattleLoadoutDefaults(int CharacterId, int WeaponId, BattleLimitFamily[] LimitFamilies);

/// <summary>Detached editor transaction. All exchanges stage both participants before publication.</summary>
public sealed class BattleLoadoutDraft
{
    private BattleLoadoutOverrides overrides;
    private readonly BattleLoadoutOverrides original;
    private readonly Dictionary<int, BattleLoadoutDefaults> defaults;
    internal GameSession? Owner { get; set; }
    internal string Baseline { get; set; } = "";
    internal string ResourceRevision { get; }
    public IReadOnlyList<int> ChangedCharacters => defaults.Keys.Where(character => !Equivalent(original.Get(character), overrides.Get(character))).Order().ToArray();

    private static bool Equivalent(BattleCharacterLoadoutOverride? first, BattleCharacterLoadoutOverride? second)
    {
        if (first is null || second is null)
        {
            return first is null && second is null;
        }
        return first.WeaponId == second.WeaponId && (first.LimitFamilies is null || second.LimitFamilies is null ? first.LimitFamilies is null && second.LimitFamilies is null : first.LimitFamilies.SequenceEqual(second.LimitFamilies));
    }

    public IReadOnlyList<int> Characters => defaults.Keys.Order().ToArray();
    public BattleLoadoutOverrides Overrides => overrides.DeepCopy();

    public BattleLoadoutDraft(BattleLoadoutOverrides? source, IEnumerable<BattleLoadoutDefaults> defaults, string resourceRevision = "")
    {
        overrides = source?.DeepCopy() ?? new();
        overrides.Validate();
        original = overrides.DeepCopy();
        this.defaults = defaults.ToDictionary(entry => entry.CharacterId, entry => entry with { LimitFamilies = entry.LimitFamilies.ToArray() });
        ResourceRevision = resourceRevision;
        foreach (BattleLoadoutDefaults entry in this.defaults.Values)
        {
            if (entry.CharacterId is < 0 or > 10 || entry.WeaponId < 0 || entry.LimitFamilies.Any(family => !Enum.IsDefined(family)) || entry.LimitFamilies.Distinct().Count() != entry.LimitFamilies.Length)
            {
                throw new InvalidDataException("Invalid native loadout defaults.");
            }
        }
    }

    private BattleCharacterLoadoutOverride Entry(int character)
    {
        RequireCharacter(character);
        return overrides.Get(character) ?? new();
    }

    private void RequireCharacter(int character)
    {
        if (!defaults.ContainsKey(character))
        {
            throw new InvalidOperationException($"Character {character} is not available in this editor.");
        }
    }

    private void Set(int character, BattleCharacterLoadoutOverride entry)
    {
        RequireCharacter(character);
        BattleLoadoutOverrides next = overrides with { Characters = overrides.Characters.SetItem(character, entry.DeepCopy()) };
        next.Validate();
        overrides = next;
    }

    public int ResolveWeapon(int character)
    {
        return Entry(character).WeaponId ?? defaults[character].WeaponId;
    }

    public BattleLimitFamily[] ResolveFamilies(int character)
    {
        return (Entry(character).LimitFamilies ?? defaults[character].LimitFamilies).ToArray();
    }

    public void AssignWeapon(int character, int weaponId)
    {
        Set(character, Entry(character) with { WeaponId = weaponId });
    }

    public void SetLimits(int character, IEnumerable<BattleLimitFamily> families)
    {
        Set(character, Entry(character) with { LimitFamilies = families.ToArray() });
    }

    public void AddLimit(int character, BattleLimitFamily family)
    {
        BattleLimitFamily[] families = ResolveFamilies(character);
        if (!families.Contains(family))
        {
            SetLimits(character, families.Append(family));
        }
    }

    public void RemoveLimit(int character, BattleLimitFamily family)
    {
        BattleLimitFamily[] families = ResolveFamilies(character);
        if (families.Contains(family))
        {
            SetLimits(character, families.Where(existing => existing != family));
        }
    }

    public void RemoveAllLimits(int character)
    {
        SetLimits(character, []);
    }

    public void ReorderLimit(int character, BattleLimitFamily family, int direction)
    {
        BattleLimitFamily[] families = ResolveFamilies(character);
        int index = Array.IndexOf(families, family);
        int target = index + Math.Sign(direction);
        if (index < 0 || target < 0 || target >= families.Length)
        {
            return;
        }
        (families[index], families[target]) = (families[target], families[index]);
        SetLimits(character, families);
    }

    public void SwapWeapons(int first, int second)
    {
        RequireCharacter(first);
        RequireCharacter(second);
        if (first == second)
        {
            return;
        }
        int firstWeapon = ResolveWeapon(first);
        int secondWeapon = ResolveWeapon(second);
        BattleCharacterLoadoutOverride firstEntry = Entry(first) with { WeaponId = secondWeapon };
        BattleCharacterLoadoutOverride secondEntry = Entry(second) with { WeaponId = firstWeapon };
        overrides = overrides with { Characters = overrides.Characters.SetItem(first, firstEntry).SetItem(second, secondEntry) };
    }

    public void MoveLimit(int source, int destination, BattleLimitFamily family)
    {
        RequireCharacter(source);
        RequireCharacter(destination);
        if (source == destination || !ResolveFamilies(source).Contains(family))
        {
            return;
        }
        BattleCharacterLoadoutOverride sourceEntry = Entry(source) with { LimitFamilies = ResolveFamilies(source).Where(existing => existing != family).ToArray() };
        BattleCharacterLoadoutOverride destinationEntry = Entry(destination) with { LimitFamilies = ResolveFamilies(destination).Append(family).Distinct().ToArray() };
        overrides = overrides with { Characters = overrides.Characters.SetItem(source, sourceEntry).SetItem(destination, destinationEntry) };
    }

    public void SwapLimitSets(int first, int second)
    {
        RequireCharacter(first);
        RequireCharacter(second);
        if (first == second)
        {
            return;
        }
        BattleLimitFamily[] firstFamilies = ResolveFamilies(first);
        BattleLimitFamily[] secondFamilies = ResolveFamilies(second);
        overrides = overrides with { Characters = overrides.Characters.SetItem(first, Entry(first) with { LimitFamilies = secondFamilies }).SetItem(second, Entry(second) with { LimitFamilies = firstFamilies }) };
    }

    public void ResetCharacter(int character)
    {
        RequireCharacter(character);
        overrides = overrides with { Characters = overrides.Characters.Remove(character) };
    }
}
