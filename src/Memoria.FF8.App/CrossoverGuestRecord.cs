using Memoria.FF7.Runtime;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal static class CrossoverGuestRecord
{
    public static int FindOriginalRow(NativeCharacterVital[] records, bool extendedRosterEnabled, int character)
    {
        if ((uint)character >= records.Length || NativeCharacterVitals.IsExtendedRosterCharacter(character) && !extendedRosterEnabled)
        {
            return -1;
        }
        return character;
    }

    public static int FindOriginalRow(NativeCharacterVitals vitals, int character)
        => FindOriginalRow(vitals.Records, vitals.ExtendedRosterEnabled, character);

    public static CharacterAppearanceMapping[] PresentationMappings(CharacterAppearanceMapping[] mappings, GameplaySaveCharacters? characters)
    {
        return characters is { ExtendedRosterEnabled: false } ? CrossoverSaveAdapter.WithoutExtendedRosterMappings(mappings) : mappings;
    }

    public static bool OwnsBattleRow(NativeCharacterVitals vitals, int row, int character)
    {
        if ((uint)row >= vitals.Records.Length || (uint)character >= NativeCharacterVitals.ExtendedRecordCount)
        {
            return false;
        }
        if (row == character)
        {
            return row < NativeCharacterVitals.NativeRecordCount || vitals.ExtendedRosterEnabled;
        }
        // Older suspended encounters admitted the temporary dream identity at its native row.
        return vitals.Records.Length == NativeCharacterVitals.NativeRecordCount && character >= NativeCharacterVitals.NativeRecordCount && vitals.Records[row].CharacterId == character && vitals.Records.Count(record => record.CharacterId == character) == 1;
    }

    public static NativeCharacterVitals ProjectIdentity(NativeCharacterVitals vitals, int row, int character)
    {
        if (vitals.Records[row].CharacterId == character)
        {
            return vitals;
        }
        NativeCharacterVital[] records = vitals.Records.ToArray();
        records[row] = records[row] with { CharacterId = checked((byte)character) };
        return vitals with { Records = records };
    }

    public static ushort MaximumHitPoints(NativeCharacterVitals vitals, int row, int character)
        => ProjectIdentity(vitals, row, character).GetMagicMaximumHitPoints(row);
}
