namespace Memoria.FF8.Runtime;

/// <summary>Threshold observations from current saved owners. Null means the required owner or predicate input is unknown.</summary>
public readonly record struct OriginalAchievementProgress
{
    public ushort? GuardianForceMask { get; init; }
    public bool? MaximumSeeDRank { get; init; }
    public bool? MaximumCharacterLevel { get; init; }
    public bool? AllCards { get; init; }
    public bool? MaximumGil { get; init; }
    public bool? WeaponUpgraded { get; init; }
    public bool? AllBlueMagic { get; init; }
    public bool? AllAngeloTricks { get; init; }
    public bool? MagazineCollection { get; init; }
    public long? CardWins { get; init; }
    public bool? Chocobo { get; init; }
    public bool? ObelLake { get; init; }
    public bool? TimberManiacs { get; init; }
}

public static class OriginalAchievementState
{
    public static OriginalAchievementProgress Observe(GameState state, bool? nativeLanguage12 = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        NativeCharacterVitals? vitals = state.CharacterVitals;
        vitals?.Validate();
        state.CardInventory?.Validate();
        state.Economy?.Validate();
        state.SeeD?.Validate();
        state.LimitProgress?.Validate();
        state.SavedWorld?.WorldEventFlags116?.Validate();
        return new OriginalAchievementProgress
        {
            GuardianForceMask = vitals is null ? null : GuardianForceMask(vitals),
            MaximumSeeDRank = state.SeeD is null ? null : state.SeeD.RankPoints >= 3100,
            MaximumCharacterLevel = vitals is null ? null : vitals.Records.Any(character => character.Experience >= 99000),
            AllCards = state.CardInventory is null ? null : AllCards(state.CardInventory),
            MaximumGil = state.Economy is null ? null : state.Economy.Gil >= 99999999,
            WeaponUpgraded = vitals is null ? null : WeaponUpgraded(vitals),
            AllBlueMagic = state.LimitProgress is null ? null : state.LimitProgress.BlueMagicFlags == ushort.MaxValue,
            AllAngeloTricks = state.LimitProgress is null ? null : state.LimitProgress.AngeloFlags == byte.MaxValue,
            MagazineCollection = state.Economy is null ? null : Enumerable.Range(0xB1, 22).All(id => state.Economy.Items.Any(item => item.ItemId == id && item.Count > 0)),
            CardWins = state.CardInventory?.TripleTriadStatistics?.Wins,
            Chocobo = WorldFlag(state, 1UL << 3),
            ObelLake = WorldFlag(state, 3UL << 32),
            TimberManiacs = TimberManiacs(state.EventMemory, nativeLanguage12)
        };
    }

    private static ushort GuardianForceMask(NativeCharacterVitals vitals)
    {
        ushort mask = 0;
        for (int id = 0; id < 16; id++)
        {
            if (vitals.GuardianForces[id].Flags != 0)
            {
                mask |= (ushort)(1 << id);
            }
        }
        return mask;
    }

    private static bool AllCards(NativeCardInventory cards)
    {
        return cards.Cards.Take(77).All(value => value >= 0x81)
            && cards.Cards.Skip(77).All(value => value == 0xF0)
            && cards.ObtainedRareCards.Take(4).All(value => value == byte.MaxValue)
            && (cards.ObtainedRareCards[4] & 1) != 0;
    }

    private static bool WeaponUpgraded(NativeCharacterVitals vitals)
    {
        int[] initialWeapons = [0, 7, 11, 15, 19, 24];
        return Enumerable.Range(0, initialWeapons.Length).Any(row => vitals.Records[row].Weapon > initialWeapons[row]);
    }

    private static bool? WorldFlag(GameState state, ulong mask)
    {
        NativeWorldEventFlags? flags = state.SavedWorld?.WorldEventFlags116;
        return flags is null || (flags.KnownMask & mask) != mask ? null : (flags.Value & mask) == mask;
    }

    private static bool? TimberManiacs(NativeEventMemory memory, bool? nativeLanguage12)
    {
        if (!memory.HasRepresentedExactBytes(304, 2))
        {
            return null;
        }
        int flags = memory.Read(304, 2).Values[0];
        if ((flags & 0x3BFE) == 0x3BFE || (flags & 0x3BFD) == 0x3BFD)
        {
            return true;
        }
        bool alternateComplete = (flags & 0x37FE) == 0x37FE || (flags & 0x37FD) == 0x37FD;
        // The additional native masks depend on the selected original resource language.
        return alternateComplete ? nativeLanguage12 : false;
    }
}
