using Albeoris.Games.FF8.TextEncoding;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.GameUi;

internal sealed record NativeScanTextLine(DialogueLine Line, int X, int Y);
internal sealed record NativeScanWindowText(int Index, NativeBattleScanRect Outer, NativeBattleScanRect Inner, ushort Progress, ushort Intensity, byte Priority, IReadOnlyList<NativeScanTextLine> Lines)
{
    public DialogueDocument Document => new([new DialoguePage(Lines.Select(line => line.Line).ToArray())]);
}

/// <summary>Projects current native text progress into semantic runs for the system text renderer.</summary>
internal static class NativeScanWindowTextProjection
{
    public static IReadOnlyList<NativeScanWindowText> Project(NativeBattleScanTextFrame frame, NativeBattleScanTextExpansionServices services)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(services);
        List<NativeScanWindowText> windows = [];
        FF8Encoding encoding = FF8Encoding.CreateEuropean();
        for (int index = 0; index < 8; index++)
        {
            NativeBattleScanByteOwner row = frame.Windows.Row(index);
            NativeBattleScanByteOwner backend = frame.Windows.Backend(row.ReadByte(0x18));
            if (backend.ReadByte(0x34) == 0 || backend.ReadByte(0x35) == 0 || backend.ReadByte(0x36) != 0)
            {
                continue;
            }
            List<NativeScanTextLine> lines = [];
            uint source = unchecked((short)backend.ReadWord(0x28)) >= 12 && unchecked((short)backend.ReadWord(0x2A)) >= 12 && row.ReadByte(0x16) != 0 ? row.ReadDword(8) : 0;
            int lineIndex = 0;
            int y = row.ReadByte(0x32) - unchecked((short)row.ReadWord(0x12)) + 2;
            int style = row.ReadByte(0x17) >> 4;
            NativeBattleScanTextFrame working = frame with { Formatting1D7660C = (byte)style };
            while (y < -16 && source != 0)
            {
                NativeBattleScanTextLineStep skipped = NativeBattleScanTextExpansion.AdvanceLine4B8AC0(working, source, services.ReadOriginalByte);
                working = skipped.Frame;
                source = skipped.SourcePointer;
                style = working.Formatting1D7660C & 0xF;
                y += 16;
                lineIndex++;
            }
            while (source != 0)
            {
                int maximum = lineIndex < row.ReadByte(0x22) ? -1 : row.ReadByte(0x20);
                working = NativeBattleScanTextExpansion.Expand4B8B30(working, source, maximum, services);
                NativeBattleScanTextLineStep next = NativeBattleScanTextExpansion.AdvanceLine4B8AC0(working with { Formatting1D7660C = (byte)style }, source, services.ReadOriginalByte);
                List<byte> text = [6, (byte)(0x20 | style)];
                int cursor = 0;
                byte terminal;
                while (true)
                {
                    byte token = working.ExpandedContext128.ReadByte(cursor++);
                    if (token is 0 or 1 or 2 or 7)
                    {
                        terminal = token;
                        break;
                    }
                    if (token == 6)
                    {
                        style = working.ExpandedContext128.ReadByte(cursor++) & 0xF;
                        text.Add(6);
                        text.Add((byte)(0x20 | style));
                    }
                    else
                    {
                        text.Add(token);
                    }
                }
                DialogueDocument document = DialogueTextFormatter.Parse(encoding.GetString(text.ToArray()));
                DialogueLine line = document.Pages.SelectMany(page => page.Lines).Single() with { SourceLineIndex = lineIndex };
                int x = row.ReadWord(0x30) + 2;
                if (row.ReadByte(0x29) <= lineIndex && lineIndex <= row.ReadByte(0x2A))
                {
                    x += 32;
                }
                lines.Add(new(line, x, y));
                if (terminal != 2)
                {
                    break;
                }
                working = next.Frame;
                source = next.SourcePointer;
                lineIndex++;
                y += 16;
            }
            windows.Add(new(index, Rect(backend, 0x18), Rect(backend, 0x24), row.ReadWord(0x1C), row.ReadWord(0x1A), backend.ReadByte(0x37), lines));
        }
        return windows;
    }

    private static NativeBattleScanRect Rect(NativeBattleScanByteOwner owner, int offset) => new(unchecked((short)owner.ReadWord(offset)), unchecked((short)owner.ReadWord(offset + 2)), unchecked((short)owner.ReadWord(offset + 4)), unchecked((short)owner.ReadWord(offset + 6)));
}
