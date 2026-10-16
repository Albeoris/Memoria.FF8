using System.Globalization;
using System.Text;

namespace Albeoris.Games.FF8.Imaging.Workspace.Internal;

internal static class TdwWidthTable
{
    public const String FileName = "widths.tsv";

    public static void Write(IWorkspaceWriter output, IReadOnlyList<Byte> widths)
    {
        StringBuilder text = new("index\twidth\n");
        for (Int32 index = 0; index < widths.Count; index++)
            text.Append(index.ToString(CultureInfo.InvariantCulture)).Append('\t').Append(widths[index]).Append('\n');
        Byte[] bytes = Encoding.UTF8.GetBytes(text.ToString());
        output.Write(FileName, stream => stream.Write(bytes));
    }

    public static Byte[] Read(IWorkspaceReader input)
    {
        String text = Encoding.UTF8.GetString(input.Read(FileName));
        String[] lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0 || !String.Equals(lines[0], "index\twidth", StringComparison.Ordinal))
            throw new InvalidDataException("The TDW width table must start with 'index\\twidth'.");

        Byte[] widths = new Byte[lines.Length - 1];
        for (Int32 lineIndex = 1; lineIndex < lines.Length; lineIndex++)
        {
            String[] cells = lines[lineIndex].Split('\t');
            if (cells.Length != 2
                || !Int32.TryParse(cells[0], NumberStyles.None, CultureInfo.InvariantCulture, out Int32 index)
                || index != lineIndex - 1
                || !Byte.TryParse(cells[1], NumberStyles.None, CultureInfo.InvariantCulture, out Byte width)
                || width > 0x0F)
            {
                throw new InvalidDataException($"Invalid TDW width table row {lineIndex + 1}: '{lines[lineIndex]}'.");
            }
            widths[index] = width;
        }

        if (widths.Length % 2 != 0)
            throw new InvalidDataException("A TDW width table must contain an even number of entries.");
        return widths;
    }
}
