using Memoria.FF8.Runtime;

namespace Memoria.FF8.BattleEffects;

public static partial class ScenarioDiscovery
{
    public static bool TrySelectShootParty(IReadOnlyList<NativeCharacterVital> records, IReadOnlyList<byte> currentParty, out byte[]? party, out string? blocker)
    {
        party = null;
        blocker = null;
        if (records.Count != 8 || currentParty.Count != 3 || !Eligible(0))
        {
            blocker = "SHOOT_PARTY_PRECONDITION: Shoot requires original living character records including the normal first party owner.";
            return false;
        }
        List<byte> selected = [0];
        HashSet<byte> identities = [records[0].CharacterId];
        foreach (int row in currentParty.Select(value => (int)value).Concat(Enumerable.Range(0, records.Count)))
        {
            if (row < records.Count && Eligible(row) && !selected.Contains((byte)row) && identities.Add(records[row].CharacterId))
            {
                selected.Add((byte)row);
                if (selected.Count == 3)
                {
                    party = selected.ToArray();
                    return true;
                }
            }
        }
        blocker = "SHOOT_PARTY_PRECONDITION: Shoot's fixed physical party-slot choreography requires three distinct living original saved character records before battle allocation.";
        return false;

        bool Eligible(int row) => records[row] is { CharacterId: <= 10, HitPoints.IsPositive: true } record && (record.Status & 3) == 0;
    }
}
