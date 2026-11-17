namespace Memoria.FF8.Runtime;

public enum BattlePartySelectionRestriction
{
    None,
    FixedCountSelection,
    Dream,
    CastleSplit,
    StorySquad
}

public static class BattlePartySelectionPolicy
{
    private const uint CastleSplitFlag = 0x800;
    private const int StoryProgressAddress = 256;

    public static BattlePartySelectionRestriction GetRestriction(NativeCharacterVitals vitals, NativeWorldControlState? controls, int? storyProgress, bool fixedCountSelection = false)
    {
        return GetRestriction(vitals.DreamEnabled, controls?.EventFlags104 ?? 0, storyProgress, fixedCountSelection);
    }

    private static BattlePartySelectionRestriction GetRestriction(bool dreamEnabled, uint eventFlags, int? storyProgress, bool fixedCountSelection)
    {
        if (fixedCountSelection)
        {
            return BattlePartySelectionRestriction.FixedCountSelection;
        }
        if (dreamEnabled)
        {
            return BattlePartySelectionRestriction.Dream;
        }
        if ((eventFlags & CastleSplitFlag) != 0)
        {
            return BattlePartySelectionRestriction.CastleSplit;
        }

        // NG-097: The Missile Base and Garden emergency use authored squads.
        bool missileBaseSquads = storyProgress is >= 450 and < 750;
        // NG-092: The Garden battle uses authored squads through the reunion.
        bool gardenBattleSquads = storyProgress is >= 850 and < 894;
        return missileBaseSquads || gardenBattleSquads ? BattlePartySelectionRestriction.StorySquad : BattlePartySelectionRestriction.None;
    }

    public static int ResolveCapacity(NativeCharacterVitals vitals, int configuredCapacity, NativeWorldControlState? controls, int? storyProgress, bool fixedCountSelection = false)
    {
        ValidateCapacity(configuredCapacity);
        return GetRestriction(vitals, controls, storyProgress, fixedCountSelection) == BattlePartySelectionRestriction.None ? configuredCapacity : NativeBattleSlots.AllyCount;
    }

    public static int ResolveCapacity(GameState state, int configuredCapacity, bool fixedCountSelection = false)
    {
        ValidateCapacity(configuredCapacity);
        return GetRestriction(state, fixedCountSelection) == BattlePartySelectionRestriction.None ? configuredCapacity : NativeBattleSlots.AllyCount;
    }

    public static BattlePartySelectionRestriction GetRestriction(GameState state, bool fixedCountSelection = false)
    {
        if (state.CharacterVitals is not { } vitals)
        {
            return BattlePartySelectionRestriction.None;
        }
        int? storyProgress = state.Variables.TryGetValue(StoryProgressAddress, out int value) ? value : null;
        return GetRestriction(vitals, state.WorldControls, storyProgress, fixedCountSelection);
    }

    internal static int ResolveSavedCapacity(bool dreamEnabled, int savedCapacity, uint eventFlags, IReadOnlyDictionary<int, int> variables)
    {
        int capacity = savedCapacity == 0 ? NativeBattleSlots.AllyCount : savedCapacity;
        ValidateCapacity(capacity);
        int? storyProgress = variables.TryGetValue(StoryProgressAddress, out int value) ? value : null;
        return GetRestriction(dreamEnabled, eventFlags, storyProgress, fixedCountSelection: false) == BattlePartySelectionRestriction.None ? capacity : NativeBattleSlots.AllyCount;
    }

    private static void ValidateCapacity(int configuredCapacity)
    {
        if (configuredCapacity is < NativeBattleSlots.AllyCount or > NativeCharacterVitals.ExtendedRecordCount)
        {
            throw new ArgumentOutOfRangeException(nameof(configuredCapacity));
        }
    }
}
