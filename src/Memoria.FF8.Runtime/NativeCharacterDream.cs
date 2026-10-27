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
            NativeCharacterVitals requested = DeepCopy();
            NativeCharacterVital[] requestedRows = requested.Records.ToArray();
            requestedRows[0] = requestedRows[0] with { CharacterId = 8 };
            requested = requested with { DreamEnabled = true, Records = requestedRows, JunctionPartyBackup = [.. fieldParty.Select(identity => (byte)identity), .. CombatParty] };
            return new NativeJunctionResult(requested, fieldParty.Select(identity => (byte)identity).ToArray());
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
        return new NativeJunctionResult(next.DeepCopy(), party);
    }

    public NativeCharacterVitals EnterGuestSelectionMenu()
    {
        Validate();
        NativeCharacterVital[] rows = Records.ToArray();
        rows[0] = rows[0] with { CharacterId = 0 };
        return this with { DreamEnabled = false, Records = rows };
    }

    public NativeJunctionResult CompleteGuestSelectionMenu(IReadOnlyList<byte> selected, byte guestFlags)
    {
        Validate();
        if (selected.Count != 3 || selected[0] != 0 || selected.Any(row => row != 255 && row >= 8) || selected.Where(row => row != 255).Distinct().Count() != selected.Count(row => row != 255))
        {
            throw new InvalidDataException("Guest menu requires its exact three physical selection cells and fixed row0.");
        }
        NativeCharacterVital[] rows = Records.ToArray();
        rows[0] = rows[0] with { CharacterId = 0 };
        byte[] combat = [255, 255, 255];
        byte[] field = [255, 255, 255];
        int destination = 0;
        for (int slot = 0; slot < 3; slot++)
        {
            if ((guestFlags & 1 << slot) == 0)
            {
                continue;
            }
            byte row = selected[slot];
            if (row == 255 || slot != 0 && (rows[row].MemberFlags & 8) == 0)
            {
                throw new InvalidDataException("Guest menu confirmation requires every enabled guest's eligible selected physical row.");
            }
            rows[row] = rows[row] with { CharacterId = checked((byte)(8 + slot)) };
            field[destination] = checked((byte)(8 + slot));
            combat[destination++] = row;
        }
        (combat[0], combat[1]) = (combat[1], combat[0]);
        ReorderNativeDreamCombatParty(combat);
        NativeCharacterVitals next = (this with { DreamEnabled = true, Records = rows, CombatParty = combat }).RebuildParty();
        return new NativeJunctionResult(next, field);
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
