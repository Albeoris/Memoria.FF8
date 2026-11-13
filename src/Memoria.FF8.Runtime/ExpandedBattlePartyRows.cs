namespace Memoria.FF8.Runtime;

public static class ExpandedBattlePartyRows
{
    public static byte[]? Select(NativeCharacterVitals vitals, int count = 4)
    {
        if (count == NativeCharacterVitals.ExtendedRecordCount)
        {
            if (vitals.DreamEnabled || vitals.Records.Length != count || vitals.Records.Select(record => record.CharacterId).Distinct().Count() != count || vitals.Records.Any(record => record.HitPoints.Minimum != record.HitPoints.Maximum))
            {
                return null;
            }
            List<byte> full = vitals.CombatParty.Where(row => row != byte.MaxValue).ToList();
            full.AddRange(Enumerable.Range(0, count).Select(row => (byte)row).Except(full).ToArray());
            return full.ToArray();
        }
        if (count is not (4 or 8) || vitals.DreamEnabled || vitals.CombatParty.Contains(byte.MaxValue))
        {
            return null;
        }
        List<byte> selected = [.. vitals.CombatParty];
        for (byte row = 0; row < vitals.Records.Length; row++)
        {
            if (!vitals.CombatParty.Contains(row) && vitals.IsRosterMember(row) && vitals.Records[row].HitPoints is { Minimum: > 0 } hp && hp.Minimum == hp.Maximum)
            {
                selected.Add(row);
                if (selected.Count == count)
                {
                    return selected.ToArray();
                }
            }
        }
        return null;
    }
}
