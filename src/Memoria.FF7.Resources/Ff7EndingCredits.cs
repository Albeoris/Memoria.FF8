using System.Buffers.Binary;
using System.Reflection.PortableExecutable;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Resources;

public sealed record Ff7EndingCreditRow(byte Style, int DrawArgument, int CenteredX, int VerticalAdvance, ReadOnlyMemory<byte> Glyphs);

/// <summary>The installed English PC credit roll at its original 640-pixel layout scale.</summary>
public sealed class Ff7EndingCredits
{
    public const int CanvasWidth = 640;
    public const int CanvasHeight = 480;
    public const int LastScrollingRow = 528;
    public const int ScrollUpdatesPerSecond = 39;
    public const int ScrollPixelsPerUpdate = 2;
    public const int RollOutUpdatesPerSecond = 10;
    public const byte CenterCursor = 0x30;

    private const int RowCount = 539;
    private const int RowSize = 64;
    private const byte EndOfGlyphs = 0x7F;
    private readonly int[] glyphWidths;
    private readonly PcTexture[] fonts;

    private Ff7EndingCredits(Ff7EndingCreditRow[] rows, int[] glyphWidths, PcTexture[] fonts, PcTexture afterCreditsTexture)
    {
        Rows = Array.AsReadOnly(rows);
        this.glyphWidths = glyphWidths;
        this.fonts = fonts;
        AfterCreditsTexture = afterCreditsTexture;
        ScrollUpdateCount = rows.Take(LastScrollingRow + 1).Sum(row => (row.VerticalAdvance - 1) / ScrollPixelsPerUpdate + 1);
    }

    public IReadOnlyList<Ff7EndingCreditRow> Rows { get; }
    public PcTexture AfterCreditsTexture { get; }
    public int ScrollUpdateCount { get; }

    public static Ff7EndingCredits Load(Ff7Installation installation)
    {
        ArgumentNullException.ThrowIfNull(installation);
        if (installation.Language != "en")
        {
            throw new NotSupportedException("The native credit roll requires the English PC resource profile.");
        }

        int[] glyphWidths = Ff7PcExecutableData.ReadEndingCreditGlyphWidths(installation);
        using LgpArchive archive = LgpArchive.Open(Path.Combine(installation.DataDirectory, "cd", "cr_us.lgp"));
        Ff7EndingCreditRow[] rows = ReadRows(ReadMember("people.bin", RowCount * RowSize), glyphWidths);
        string[] fontNames = ["font.tex", "font_ub.tex", "font_ua.tex", "font_s.tex"];
        PcTexture[] fonts = fontNames.Select(name => PcTexture.Read(ReadMember(name))).ToArray();
        PcTexture afterCreditsTexture = PcTexture.Read(ReadMember("after_1.tex"));
        return new Ff7EndingCredits(rows, glyphWidths, fonts, afterCreditsTexture);

        byte[] ReadMember(string name, int maximumBytes = 1024 * 1024)
        {
            IReadOnlyList<LgpEntry> members = archive.Find(name);
            if (members.Count != 1)
            {
                throw new InvalidDataException($"Native FFVII credits require exactly one archive member named '{name}'.");
            }
            return archive.ReadEntry(members[0], maximumBytes);
        }
    }

    public PcTexture Font(int style)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(style);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(style, 5);
        return fonts[Math.Min(style, 3)];
    }

    public int GlyphAdvance(int style, byte glyph)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(style);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(style, 5);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(glyph, CenterCursor);
        return glyphWidths[glyph] >> (style / 3);
    }

    private static Ff7EndingCreditRow[] ReadRows(ReadOnlySpan<byte> source, int[] glyphWidths)
    {
        if (source.Length != RowCount * RowSize)
        {
            throw new InvalidDataException("The native FFVII English credit roll requires 539 complete 64-byte rows.");
        }

        Ff7EndingCreditRow[] rows = new Ff7EndingCreditRow[RowCount];
        for (int index = 0; index < rows.Length; index++)
        {
            ReadOnlySpan<byte> row = source.Slice(index * RowSize, RowSize);
            int style = BinaryPrimitives.ReadInt32LittleEndian(row);
            int drawArgument = BinaryPrimitives.ReadInt32LittleEndian(row[4..]);
            int verticalAdvance = BinaryPrimitives.ReadInt32LittleEndian(row[60..]);
            ReadOnlySpan<byte> glyphs = row.Slice(12, 48);
            int end = glyphs.IndexOf(EndOfGlyphs);
            if (style is < 0 or > 5 || drawArgument is < 0 or > 3 || verticalAdvance is <= 0 or > CanvasHeight || end < 0)
            {
                throw new InvalidDataException($"Native FFVII credit row {index} has invalid layout or glyph bounds.");
            }

            glyphs = glyphs[..end];
            bool centersCursor = false;
            int width = 0;
            foreach (byte glyph in glyphs)
            {
                if (glyph > CenterCursor)
                {
                    throw new InvalidDataException($"Native FFVII credit row {index} has an unsupported glyph control.");
                }
                if (glyph == CenterCursor)
                {
                    centersCursor = true;
                }
                else
                {
                    width += glyphWidths[glyph] >> (style / 3);
                }
            }

            int centeredX = centersCursor ? 0 : (CanvasWidth - width) >> 1;
            rows[index] = new Ff7EndingCreditRow((byte)style, drawArgument, centeredX, verticalAdvance, glyphs.ToArray());
        }
        return rows;
    }
}

public sealed partial class Ff7PcExecutableData
{
    internal static int[] ReadEndingCreditGlyphWidths(Ff7Installation installation)
    {
        using FileStream stream = OpenSupportedImage(installation);
        using PEReader pe = new(stream, PEStreamOptions.LeaveOpen);
        int[] widths = new int[48];
        ReadOnlySpan<byte> data = ReadVirtual(stream, pe, 0x99C5F0, widths.Length * sizeof(int));
        for (int glyph = 0; glyph < widths.Length; glyph++)
        {
            widths[glyph] = BinaryPrimitives.ReadInt32LittleEndian(data[(glyph * sizeof(int))..]);
        }
        return widths;
    }
}
