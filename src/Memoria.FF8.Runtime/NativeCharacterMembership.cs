namespace Memoria.FF8.Runtime;

public sealed partial record NativeCharacterVitals
{
    public NativeCharacterVitals WithMemberFlags(int rawCharacter, bool enabled)
    {
        Validate();
        NativeCharacterVitals state = enabled ? this : ResetGuardianForces(rawCharacter);
        if (rawCharacter is 8 or 9 or 10)
        {
            // Native51E670/51E710 own only the event DWORD104 guest bits here.
            // SUBMEMBER still executes52CC20/4ACE50 before that event-bit write.
            return state.DeepCopy();
        }
        int row = NativeMembershipRow(rawCharacter);
        NativeCharacterVital[] records = state.Records.ToArray();
        ushort flags = records[row].MemberFlags;
        if (enabled)
        {
            int mask = rawCharacter == 0 ? 0x0D : rawCharacter is 4 or 6 or 7 ? 1 : 9;
            flags = unchecked((ushort)(flags | mask));
        }
        else
        {
            flags = (ushort)(flags & 0xFFF6);
        }
        records[row] = records[row] with { MemberFlags = flags };
        if (!enabled && rawCharacter == 6)
        {
            // Native56DB20 copies all64 magic bytes from physical row6 to5,
            // then47E410 clears four16-byte blocks in the source row.
            records[5] = records[5] with { MagicInventory = records[6].MagicInventory.ToArray() };
            records[6] = records[6] with { MagicInventory = EmptyNativeMagicInventory() };
        }
        else if (!enabled && rawCharacter == 7)
        {
            RedistributeNativeSeiferMagic(records);
        }
        // Native membership/inventory writes do not rebuild after the reset.
        return (state with { Records = records }).DeepCopy();
    }

    public NativeCharacterVitals WithPartyMemberFlags(int rawCharacter)
    {
        Validate();
        if (rawCharacter >= 8)
        {
            // Native51E160 writes event DWORD104 on its guest branch.
            return DeepCopy();
        }
        int row = NativeMembershipRow(rawCharacter);
        int mask = rawCharacter == 0 ? 0x0D : rawCharacter == 4 ? 1 : 9;
        NativeCharacterVital[] records = Records.ToArray();
        records[row] = records[row] with { MemberFlags = unchecked((ushort)(records[row].MemberFlags | mask)) };
        return (this with { Records = records }).DeepCopy();
    }

    private static int NativeMembershipRow(int rawCharacter)
    {
        uint offset = unchecked((uint)rawCharacter * 152u);
        if (offset > 7u * 152u || offset % 152u != 0)
        {
            throw new NotSupportedException("Native membership destination is outside the eight owned physical saved rows.");
        }
        return (int)(offset / 152u);
    }

    private static NativeMagicStock[] EmptyNativeMagicInventory() => Enumerable.Range(0, 32).Select(_ => new NativeMagicStock(0, 0)).ToArray();

    private static void RedistributeNativeSeiferMagic(NativeCharacterVital[] records)
    {
        NativeMagicStock[][] inventories = records.Select(record => record.MagicInventory.ToArray()).ToArray();
        foreach (NativeMagicStock stock in records[7].MagicInventory)
        {
            if (stock.SpellId == 0)
            {
                continue;
            }
            int remaining = stock.Count;
            bool consumed = false;
            for (int row = 0; row < records.Length; row++)
            {
                if ((records[row].MemberFlags & 1) == 0)
                {
                    continue;
                }
                int entry = Array.FindIndex(inventories[row], candidate => candidate.SpellId == stock.SpellId);
                if (entry < 0)
                {
                    continue;
                }
                int combined = remaining + inventories[row][entry].Count;
                if (combined <= 100)
                {
                    inventories[row][entry] = inventories[row][entry] with { Count = unchecked((byte)combined) };
                    consumed = true;
                    break;
                }
                inventories[row][entry] = inventories[row][entry] with { Count = 100 };
                remaining = combined - 100;
            }
            if (consumed)
            {
                continue;
            }
            for (int row = 0; row < records.Length; row++)
            {
                if ((records[row].MemberFlags & 1) == 0 || inventories[row].Any(candidate => candidate.SpellId == stock.SpellId))
                {
                    continue;
                }
                int empty = Array.FindIndex(inventories[row], candidate => candidate.SpellId == 0);
                if (empty >= 0)
                {
                    inventories[row][empty] = new NativeMagicStock(stock.SpellId, unchecked((byte)remaining));
                    break;
                }
            }
        }
        for (int row = 0; row < records.Length; row++)
        {
            records[row] = records[row] with { MagicInventory = row == 7 ? EmptyNativeMagicInventory() : inventories[row] };
        }
    }
}
