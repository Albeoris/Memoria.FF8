using System.Buffers.Binary;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static partial class NativeBattleEffectTextureReader
{
    public static PixelImage ApplyModelReplacements(PixelImage original, NativeBattleEffectTextureState state, ushort page, ushort clut, int u, int v, NativeBattlePcTextureWindowPixels? retainedPixels, IReadOnlyList<NativeBattleTextureReplacement> replacements, string? textureSourceOwner = null)
    {
        List<(NativeBattleTextureReplacement Replacement, int Left, int Top, int Right, int Bottom)> selected = [];
        int densityWidth = 1;
        int densityLogicalWidth = 1;
        int densityHeight = 1;
        int densityLogicalHeight = 1;
        foreach (NativeBattleTextureReplacement replacement in replacements)
        {
            bool sourcePage = replacement.SourceOwner is not null;
            if ((page & 0x19F) != (replacement.Page & 0x19F) || (sourcePage ? !string.Equals(textureSourceOwner, replacement.SourceOwner, StringComparison.Ordinal) : (clut & 0x7FFF) != (replacement.Clut & 0x7FFF)))
            {
                continue;
            }
            int left = Math.Max(u, replacement.U);
            int top = Math.Max(v, replacement.V);
            int right = Math.Min(u + original.Width, replacement.U + replacement.Width);
            int bottom = Math.Min(v + original.Height, replacement.V + replacement.Height);
            if (left >= right || top >= bottom || !sourcePage && !Matches(replacement, left, top, right, bottom))
            {
                continue;
            }
            if (replacement.LogicalImageWidth <= 0 || replacement.LogicalImageHeight <= 0 || replacement.ImageSourceX < 0 || replacement.ImageSourceY < 0 || replacement.ImageSourceX + replacement.Width > replacement.LogicalImageWidth || replacement.ImageSourceY + replacement.Height > replacement.LogicalImageHeight)
            {
                throw new InvalidDataException("Native replacement image source leaves its original logical material canvas.");
            }
            if ((long)replacement.Image.Width * densityLogicalWidth > (long)densityWidth * replacement.LogicalImageWidth)
            {
                densityWidth = replacement.Image.Width;
                densityLogicalWidth = replacement.LogicalImageWidth;
            }
            if ((long)replacement.Image.Height * densityLogicalHeight > (long)densityHeight * replacement.LogicalImageHeight)
            {
                densityHeight = replacement.Image.Height;
                densityLogicalHeight = replacement.LogicalImageHeight;
            }
            selected.Add((replacement, left, top, right, bottom));
        }
        if (selected.Count == 0)
        {
            return original;
        }
        PixelImage result = new(DivideCeiling((long)original.Width * densityWidth, densityLogicalWidth), DivideCeiling((long)original.Height * densityHeight, densityLogicalHeight));
        for (int row = 0; row < result.Height; row++)
        {
            for (int column = 0; column < result.Width; column++)
            {
                result[column, row] = original[column * original.Width / result.Width, row * original.Height / result.Height];
            }
        }
        foreach (var (replacement, left, top, right, bottom) in selected)
        {
            int firstRow = DivideCeiling((top - v) * result.Height, original.Height);
            int endRow = DivideCeiling((bottom - v) * result.Height, original.Height);
            int firstColumn = DivideCeiling((left - u) * result.Width, original.Width);
            int endColumn = DivideCeiling((right - u) * result.Width, original.Width);
            for (int row = firstRow; row < endRow; row++)
            {
                int sourceY = checked((int)(((long)(v - replacement.V + replacement.ImageSourceY) * result.Height + (long)row * original.Height) * replacement.Image.Height / ((long)result.Height * replacement.LogicalImageHeight)));
                for (int column = firstColumn; column < endColumn; column++)
                {
                    int sourceX = checked((int)(((long)(u - replacement.U + replacement.ImageSourceX) * result.Width + (long)column * original.Width) * replacement.Image.Width / ((long)result.Width * replacement.LogicalImageWidth)));
                    result[column, row] = replacement.Image[sourceX, sourceY];
                }
            }
        }
        return result;

        bool Matches(NativeBattleTextureReplacement replacement, int left, int top, int right, int bottom)
        {
            int depth = (page >> 7) & 3;
            int paletteSize = depth == 0 ? 16 : depth == 1 ? 256 : 0;
            int paletteX = (clut & 63) * 16;
            int paletteY = (clut & 0x7FFF) >> 6;
            Span<byte> palette = stackalloc byte[512];
            if (replacement.PaletteWords.Length != paletteSize * 2 || paletteX + paletteSize > 1024)
            {
                return false;
            }
            state.ReadVramInto(2 * (paletteY * 1024 + paletteX), palette[..(paletteSize * 2)]);
            if (!palette[..(paletteSize * 2)].SequenceEqual(replacement.PaletteWords.AsSpan()))
            {
                return false;
            }
            int pixelsPerWord = 4 >> depth;
            int pageX = (page & 15) * 64;
            int pageY = (page & 16) * 16;
            for (int row = top; row < bottom; row++)
            {
                for (int column = left / pixelsPerWord * pixelsPerWord; column < right; column += pixelsPerWord)
                {
                    int x = pageX + column / pixelsPerWord;
                    int y = pageY + row;
                    ushort actual = retainedPixels is null ? state.ReadVramWord(x, y) : retainedPixels.ReadWord(x, y);
                    int expectedOffset = 2 * ((row - replacement.V) * replacement.WordWidth + (column - replacement.U) / pixelsPerWord);
                    if (actual != BinaryPrimitives.ReadUInt16LittleEndian(replacement.PixelWords.AsSpan()[expectedOffset..]))
                    {
                        return false;
                    }
                }
            }
            return true;
        }
    }

    private static int DivideCeiling(long numerator, int denominator) => checked((int)((numerator + denominator - 1) / denominator));
}
