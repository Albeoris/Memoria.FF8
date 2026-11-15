namespace Memoria.FF8.Runtime;

internal static class NativeBattlePartyReplacementSelection
{
    internal static byte? Select(NativeCharacterVitals vitals, IEnumerable<int> occupiedRows)
    {
        ArgumentNullException.ThrowIfNull(vitals);
        ArgumentNullException.ThrowIfNull(occupiedRows);

        HashSet<int> occupied = [];
        foreach (int row in occupiedRows)
        {
            if ((uint)row >= vitals.Records.Length || !occupied.Add(row))
            {
                throw new InvalidDataException("Occupied party seats must refer to distinct saved character rows.");
            }
        }

        int candidateCount = vitals.ExtendedRosterEnabled && !vitals.DreamEnabled ? vitals.Records.Length : NativeCharacterVitals.NativeRecordCount;
        byte? selected = null;
        uint minimumExperience = uint.MaxValue;
        for (int row = 0; row < candidateCount; row++)
        {
            if (occupied.Contains(row) || !vitals.IsRosterMember(row))
            {
                continue;
            }
            NativeCharacterVital record = vitals.Records[row];
            if ((record.Status & 1) == 0 && (selected is null || record.Experience < minimumExperience))
            {
                selected = checked((byte)row);
                minimumExperience = record.Experience;
            }
        }
        return selected;
    }
}
