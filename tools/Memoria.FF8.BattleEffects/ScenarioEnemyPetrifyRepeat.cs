using Memoria.FF8.Runtime;

namespace Memoria.FF8.BattleEffects;

public static partial class ScenarioDiscovery
{
    public static bool TrySelectEnemyPetrifyRepeatParty(BattleNativeAction action, byte selector, IReadOnlyList<NativeCharacterVital> records, IReadOnlyList<byte> currentParty, out byte[]? party, out string? blocker)
    {
        party = null;
        blocker = null;
        if ((action.Statuses0 & 4) == 0 || (action.Statuses1 & 0x10000) != 0 || selector is not (0 or 201))
        {
            return true;
        }
        IReadOnlyList<byte> preferred = currentParty.Count == 3 && currentParty.Count(row => row != 255) == 1 ? new byte[] { 0, 1, 3 } : currentParty;
        return TrySelectThreeLivingPartyRecords(records, preferred, "PETRIFY_REPEAT_PARTY_PRECONDITION", out party, out blocker);
    }
}
