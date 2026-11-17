namespace Memoria.FF8.Runtime;

public static partial class BattleResultProjection
{
    private static (byte[] Participating, byte[] Active) ReadPartyOccupancy(BattleResult result, NativeCharacterVitals vitals, BattleDefinition? admitted, byte[] entry)
    {
        if (result.PartyOccupancyChanges.IsEmpty)
        {
            return (entry, entry);
        }
        if (admitted is null)
        {
            throw new InvalidDataException("Party replacements require their admitted battle and installed character resources.");
        }

        byte?[] slots = entry.Select(row => (byte?)row).ToArray();
        List<byte> participating = [.. entry];
        foreach (BattlePartyOccupancyChange change in result.PartyOccupancyChanges)
        {
            int position = change.Slot.Position;
            if (position >= slots.Length || slots[position] != change.OutgoingRow)
            {
                throw new InvalidDataException("A party occupancy receipt does not match its outgoing row or follows a previously retired slot.");
            }
            if (change.IncomingRow is byte incoming)
            {
                NativeCharacterVital saved = vitals.Records[incoming];
                if (participating.Contains(incoming) || (saved.MemberFlags & 1) == 0 || (saved.Status & 1) != 0 ||
                    !admitted.PartyReplacementCandidates.TryGetValue(incoming, out BattleCombatantDefinition? candidate) ||
                    !candidate.IsPlayer || candidate.MagicRecordId != incoming || candidate.ModelId != saved.CharacterId)
                {
                    throw new InvalidDataException("A party occupancy receipt requires an installed, eligible and previously unallocated reserve.");
                }
                participating.Add(incoming);
            }
            slots[position] = change.IncomingRow;
        }
        return (participating.ToArray(), slots.OfType<byte>().ToArray());
    }
}
