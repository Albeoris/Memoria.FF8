namespace Memoria.FF8.Runtime;

public static class ExpandedBattlePartyRows
{
    public static byte[]? Select(NativeCharacterVitals vitals)
    {
        if (vitals.DreamEnabled || vitals.CombatParty.Contains(byte.MaxValue))
        {
            return null;
        }
        for (byte row = 0; row < vitals.Records.Length; row++)
        {
            if (!vitals.CombatParty.Contains(row) && vitals.IsRosterMember(row) && vitals.Records[row].HitPoints is { Minimum: > 0 } hp && hp.Minimum == hp.Maximum)
            {
                return [.. vitals.CombatParty, row];
            }
        }
        return null;
    }
}
