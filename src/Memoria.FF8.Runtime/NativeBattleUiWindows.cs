using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleUiWindow(uint Update0, uint? Retained4, uint Draw8, uint Context12, sbyte Current16, sbyte Requested17, byte Dirty18, sbyte? Saved19);
public sealed record NativeBattleUiWindowBounds(short Left, short Top, short Right, short Bottom);

public sealed record NativeBattleUiWindows(ImmutableArray<NativeBattleUiWindow> Rows, int VitalsState6868, short VitalsProgress686C, NativeBattleUiWindowBounds? VitalsDirtyBounds)
{
    public static NativeBattleUiWindows Initialize4A94D0()
    {
        //4BA060 resets nine physical rows; DWORD4 and BYTE19 retain unknown prior contents.
        ImmutableArray<NativeBattleUiWindow> rows = Enumerable.Range(0, 9).Select(_ => new NativeBattleUiWindow(0, null, 0, 0, -1, -1, 0, null)).ToImmutableArray();
        rows = rows.SetItem(3, rows[3] with { Draw8 = 0x4BCCD0 });
        rows = rows.SetItem(4, rows[4] with { Update0 = 0x4BB9E0, Draw8 = 0x4BCE10 });
        rows = rows.SetItem(5, rows[5] with { Update0 = 0x4B1ED0, Draw8 = 0x4B2130 });
        rows = rows.SetItem(1, rows[1] with { Update0 = 0x4BB030 });
        rows = rows.SetItem(7, rows[7] with { Update0 = 0x4B0500, Draw8 = 0x4B1740, Current16 = 2, Requested17 = 2, Dirty18 = 1 });
        //4B1B00 initializes state0/progress0 and immediately invokes4B0450.
        return new(rows, 1, 0, null);
    }

    public void Validate()
    {
        if (Rows.IsDefault || Rows.Length != 9 || Rows.Any(row => row is null))
        {
            throw new InvalidDataException("Native UI window bank requires its nine physical twenty-byte rows.");
        }
    }

    public NativeBattleUiWindows Refresh4B9C40(int last)
    {
        Validate();
        if (last >= Rows.Length)
        {
            throw new InvalidDataException("Native UI window refresh exceeds its actual physical bank.");
        }
        ImmutableArray<NativeBattleUiWindow> rows = Rows;
        for (int index = last; index >= 0; index--)
        {
            NativeBattleUiWindow row = rows[index];
            sbyte? saved = row.Current16 != 1 ? row.Current16 : row.Saved19;
            rows = rows.SetItem(index, row.Current16 <= 0 ? row with { Current16 = -1, Saved19 = -1 } : row with { Current16 = 1, Saved19 = saved });
        }
        return this with { Rows = rows };
    }

    public NativeBattleUiWindows OpenVitals4B0430() => VitalsState6868 <= 1 ? this with { VitalsState6868 = 2 } : this;

    public NativeBattleUiWindows AdvanceVitals4B0450()
    {
        Validate();
        NativeBattleUiWindows state = this;
        int phase = VitalsState6868;
        short progress = VitalsProgress686C;
        if (phase == 0)
        {
            phase = 1;
        }
        else if (phase is 2 or 3)
        {
            phase = 3;
            int next = progress + 128;
            state = state.Refresh4B9C40(7);
            progress = unchecked((short)next);
            if (next >= 4096)
            {
                progress = 4096;
                phase = 5;
            }
        }
        else if (phase == 4)
        {
            phase = 5;
        }
        //4B0450 supplies rect122/171/190/45 to4B9B00's current dirty-bounds union.
        return state with { VitalsState6868 = phase, VitalsProgress686C = progress, VitalsDirtyBounds = new(122, 171, 312, 216) };
    }

    public NativeBattleUiWindows CommitDraw4B9DB0(bool publicationFrame, ushort flags)
    {
        Validate();
        ImmutableArray<NativeBattleUiWindow> rows = Rows;
        for (int index = 0; index < rows.Length; index++)
        {
            if (((flags & 0x801) != 0 && (index < 6 || index == 7)) || ((flags & 0x800) != 0 && index == 7) || ((flags & 0x40) == 0 && index != 8))
            {
                continue;
            }
            NativeBattleUiWindow row = rows[index];
            sbyte phase = row.Current16;
            if (row.Dirty18 != 0 && publicationFrame)
            {
                phase = row.Requested17;
                row = row with { Saved19 = phase, Dirty18 = 0 };
            }
            if (phase == 1 && row.Draw8 != 0)
            {
                phase = row.Saved19 ?? throw new NotSupportedException("Native UI phase1 requires its actually retained saved phase BYTE19.");
            }
            rows = rows.SetItem(index, row with { Current16 = phase });
        }
        return this with { Rows = rows };
    }

    public bool DrawsVitals4B1740(ushort flags)
    {
        Validate();
        return (flags & 0x841) == 0x40 && Rows[7].Current16 > 0 && VitalsProgress686C >= 4096;
    }
}
