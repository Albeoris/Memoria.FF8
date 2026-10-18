namespace Memoria.FF8.Runtime;

public sealed record NativeBattleScanTextConsumeStep(NativeBattleScanTextFrame Frame, int Token);

public static class NativeBattleScanTextConsumption
{
    public static NativeBattleScanTextConsumeStep Consume4B9170(NativeBattleScanTextFrame frame, int index, NativeBattleScanTextServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(services.Expand4B8B30);
        ArgumentNullException.ThrowIfNull(services.ReadOriginalByte);
        frame.ExpandedContext128.RequireLength(128);
        frame.Globals1D2B524.RequireLength(12);
        NativeBattleScanByteOwner row = frame.Windows.Row(index);
        int cursor = row.ReadByte(0x20);
        byte token = frame.ExpandedContext128.ReadByte(cursor++);
        int result = token;
        if (token < 0x20 && (token >= 0x19 || token < 0x10 && token is not (0 or 1 or 2 or 7)))
        {
            result = token << 8 | frame.ExpandedContext128.ReadByte(cursor++);
        }
        else if (token == 0)
        {
            cursor--;
        }
        else if (token is 1 or 2 or 7)
        {
            bool line = token == 2;
            row = line
                ? row.WriteByte(0x21, unchecked((byte)(row.ReadByte(0x21) + 1))).WriteByte(0x22, unchecked((byte)(row.ReadByte(0x22) + 1)))
                : row.WriteByte(0x21, 0).WriteByte(0x22, 0).WriteWord(0x12, 0);
            uint pointer = row.ReadDword(line ? 0x0C : 8);
            if (pointer != 0)
            {
                while (true)
                {
                    byte source = services.ReadOriginalByte(pointer);
                    pointer = unchecked(pointer + 1);
                    if (source == 0)
                    {
                        pointer = 0;
                        break;
                    }
                    if (source is 1 or 7 || line && source == 2)
                    {
                        break;
                    }
                    if (source == 6)
                    {
                        frame = frame with { Formatting1D7660C = services.ReadOriginalByte(pointer) };
                        pointer = unchecked(pointer + 1);
                    }
                }
            }
            row = row.WriteDword(0x0C, pointer);
            if (!line)
            {
                row = row.WriteDword(8, pointer);
            }
            frame = frame with { Windows = frame.Windows with { TextRows = frame.Windows.TextRows.SetItem(index, row) } };
            frame = services.Expand4B8B30(frame, pointer);
            row = frame.Windows.Row(index);
            cursor = 0;
        }
        row = row.WriteByte(0x20, unchecked((byte)cursor));
        return new(frame with { Windows = frame.Windows with { TextRows = frame.Windows.TextRows.SetItem(index, row) } }, result);
    }
}
