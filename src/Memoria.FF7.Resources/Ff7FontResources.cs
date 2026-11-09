using Memoria.FF7.Formats;

namespace Memoria.FF7.Resources;

public sealed record Ff7FontGlyph(byte Code, int SourceX, int SourceY, int Width, int Height, int LeftPadding, int Advance, int Palette);
public sealed record Ff7FontResources(TimTexture Atlas, ReadOnlyMemory<byte> Metrics)
{
    public const int LineHeight = 16;
    public const int FixedAdvance = 13;

    public Ff7FontGlyph GetGlyph(byte code, int color = 7, bool fixedSpacing = false)
    {
        if (code >= 224 || color < 0 || color > 7)
        {
            throw new ArgumentOutOfRangeException(nameof(code), "Western field glyph IDs are 0–223 and colors are 0–7.");
        }
        byte metric = Metrics.Span[code];
        int padding = metric >> 5;
        int width = metric & 31;
        return new Ff7FontGlyph(code, code % 21 * 12, code / 21 * 12, 12, 12, padding, fixedSpacing ? FixedAdvance : width + padding, color * 2);
    }

    public static Ff7FontResources Load(Ff7Installation installation)
    {
        if (installation.Language != "en")
        {
            throw new NotSupportedException("Native field font layout currently supports the English resource profile.");
        }
        IReadOnlyList<KernelSection> window = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Window).Path));
        if (window.Count != 3 || window[0].Type != 0 || window[1].Type != 0 || window[2].Type != 1 || window[2].Data.Length != 1302)
        {
            throw new InvalidDataException("Unsupported western FF7 WINDOW archive profile.");
        }
        TimTexture atlas = TimTexture.Read(window[1].Data.Span);
        if (atlas.Width != 256 || atlas.Height != 252 || atlas.BitsPerPixel != 4 || atlas.Palettes.Count != 16)
        {
            throw new InvalidDataException("Unsupported native western FF7 field font atlas dimensions.");
        }
        return new Ff7FontResources(atlas, window[2].Data);
    }
}
