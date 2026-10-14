namespace Memoria.FF8.Runtime;

public sealed record NativeJunctionResult(NativeCharacterVitals Vitals, byte[] FieldParty);

public sealed partial record NativeCharacterVitals
{
    /// <summary>Native52CC20 scans all eight physical saved rows using the low identity BYTE.</summary>
    public byte FindPhysicalRow(byte identity)
    {
        Validate();
        int row = Array.FindIndex(Records, record => record.CharacterId == identity);
        return row < 0 ? byte.MaxValue : (byte)row;
    }

    public NativeJunctionResult PrepareJunction(int rawOperand, IReadOnlyList<int> fieldParty, uint guestFlags)
    {
        Validate();
        if (fieldParty.Count != 3 || fieldParty.Any(identity => identity is < 0 or > 255))
        {
            throw new InvalidDataException("Native JUNCTION needs the actual three raw field character bytes.");
        }
        if ((rawOperand & 3) == 3)
        {
            throw new NotSupportedException("Native JUNCTION requests menu22 guest selection; no menu result or mapping is inferred.");
        }
        NativeCharacterVitals next = DeepCopy();
        NativeCharacterVital[] records = next.Records.ToArray();
        if ((rawOperand & 1) == 0)
        {
            // Native4ACAE0 clears the dream flag and resets every identity;
            // native51EFB0 then restores raw bytes188..193 without a rebuild.
            for (int row = 0; row < records.Length; row++)
            {
                records[row] = records[row] with { CharacterId = (byte)row };
            }
            next = next with { DreamEnabled = false, Records = records, CombatParty = JunctionPartyBackup.Skip(3).ToArray() };
            next.Validate();
            return new NativeJunctionResult(next.DeepCopy(), JunctionPartyBackup.Take(3).ToArray());
        }
        records[0] = records[0] with { CharacterId = 8 };
        byte[] backup = [.. fieldParty.Select(identity => (byte)identity), .. CombatParty];
        byte[] combat = CombatParty.ToArray();
        byte[] party = [8, 255, 255];
        byte identity = 9;
        int fieldSlot = 1;
        for (int slot = 0; slot < combat.Length; slot++)
        {
            byte row = combat[slot];
            if (row is 0 or 255)
            {
                continue;
            }
            if ((records[row].MemberFlags & 8) == 0)
            {
                combat[slot] = 255;
                continue;
            }
            if (identity > 10)
            {
                throw new NotSupportedException("Native JUNCTION would consume identity11 outside the eleven authenticated character definitions.");
            }
            records[row] = records[row] with { CharacterId = identity };
            if ((guestFlags & (1u << (identity - 8))) != 0)
            {
                party[fieldSlot++] = identity;
            }
            else
            {
                combat[slot] = 255;
            }
            identity++;
        }
        ReorderNativeDreamCombatParty(combat);
        next = (next with { DreamEnabled = true, Records = records, CombatParty = combat, JunctionPartyBackup = backup }).RebuildParty();
        // Native51EFB0 resets remaining normal identities by PHYSICAL row.
        for (int row = 0; row < next.Records.Length; row++)
        {
            if (next.Records[row].CharacterId < 8)
            {
                next = next.ResetGuardianForcesAtRow(row);
            }
        }
        return new NativeJunctionResult(next.DeepCopy(), party);
    }

    private static void ReorderNativeDreamCombatParty(byte[] combat)
    {
        // Native4ACEE0(-1) returns immediately on the first FF. With a full
        // party it swaps the first two nonzero rows, preserving the row0 slot.
        if (combat.Contains((byte)255))
        {
            return;
        }
        int first = Array.FindIndex(combat, row => row != 0);
        int second = Enumerable.Range(0, combat.Length).FirstOrDefault(slot => slot != first && combat[slot] != 0, -1);
        if (first < 0 || second < 0)
        {
            throw new NotSupportedException("Native JUNCTION reorder would access a negative combat slot outside the current saved-party owner.");
        }
        (combat[first], combat[second]) = (combat[second], combat[first]);
    }

    public int ReadJunctionPartyBackup(int address, int width)
    {
        Validate();
        if (width is not (1 or 2 or 4) || address < 188 || address + width > 194)
        {
            throw new NotSupportedException("Native JUNCTION backup projection cannot consume surrounding unowned bytes.");
        }
        uint value = 0;
        for (int index = 0; index < width; index++)
        {
            value |= (uint)JunctionPartyBackup[address - 188 + index] << (8 * index);
        }
        return unchecked((int)value);
    }

    public NativeCharacterVitals WriteJunctionPartyBackup(int address, int width, int rawValue)
    {
        _ = ReadJunctionPartyBackup(address, width);
        byte[] backup = JunctionPartyBackup.ToArray();
        for (int index = 0; index < width; index++)
        {
            backup[address - 188 + index] = unchecked((byte)((uint)rawValue >> (8 * index)));
        }
        return this with { JunctionPartyBackup = backup };
    }
}
