namespace Memoria.FF8.Runtime;

public sealed partial record NativeCharacterVitals
{
    /// <summary>Validates persisted logical banks and ownership at publication boundaries, after transactional reconciliation.</summary>
    public void ValidateMagicSystemConsistency()
    {
        MagicSystem.Validate();
        if (!MagicSystem.RequiresMemoriaFormat)
        {
            return;
        }
        for (int row = 0; row < 8; row++)
        {
            HashSet<int> ids = [];
            foreach (NativeMagicStock stock in Records[row].MagicInventory.Concat(row == 0 ? MagicSystem.RecordZeroExtension : Array.Empty<NativeMagicStock>()))
            {
                if (stock.SpellId != 0 && stock.Count != 0 && (stock.SpellId > 56 || stock.Count > 100 || !ids.Add(stock.SpellId)))
                {
                    throw new InvalidDataException("Saved logical magic bank has invalid or duplicate positive stock.");
                }
            }
        }
        foreach (IGrouping<(int BankId, int SpellId, MagicCastContext Context), MagicCastReservation> group in MagicSystem.Reservations.GroupBy(r => (r.BankId, r.SpellId, r.Context)))
        {
            int mask = 0;
            foreach (MagicCastReservation reservation in group)
            {
                if ((mask & reservation.TicketMask) != 0 || MagicSystem.UnlimitedMagic && reservation.TicketMask != 0 || MagicSystem.InfiniteMagic && !MagicSystem.UnlimitedMagic && System.Numerics.BitOperations.PopCount((uint)reservation.TicketMask) != reservation.Count - reservation.CommittedCount || (reservation.BankId == -1) != MagicSystem.SharedMagicStock || MagicSystem.CommittedActionIds.Contains(reservation.ActionId, StringComparer.Ordinal))
                {
                    throw new InvalidDataException("Saved magic reservations overlap or contradict their active storage rules.");
                }
                mask |= reservation.TicketMask;
            }
        }
        if (MagicSystem.SharedMagicStock)
        {
            HashSet<int> owners = [];
            for (int row = 0; row < 8; row++)
            {
                foreach (int slot in Enumerable.Range(0, 19).Where(slot => Records[row].Junction.MagicJunctions[slot] != 0))
                {
                    byte spell = Records[row].Junction.MagicJunctions[slot];
                    if (GetMagicCount(row, spell) == 0 || !JunctionMagicSlotAvailable(row, slot) || !owners.Add(spell))
                    {
                        throw new InvalidDataException("Saved magic has invalid, depleted or duplicate active Junction ownership.");
                    }
                }
            }
        }
    }
}
