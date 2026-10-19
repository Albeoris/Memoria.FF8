using System.Buffers.Binary;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static partial class NativeBattleEffectTextureReader
{
    public static PixelImage ApplyModelReplacements(PixelImage original, NativeBattleEffectTextureState state, ushort page, ushort clut, int u, int v, NativeBattlePcTextureWindowPixels? retainedPixels, IReadOnlyList<NativeBattleTextureReplacement> replacements)
    {
        PixelImage? result = null;
        byte[] bank = Banks.GetValue(state, key => key.ReadVram());
        foreach (NativeBattleTextureReplacement replacement in replacements)
        {
            if ((page & 0x19F) != (replacement.Page & 0x19F) || (clut & 0x7FFF) != (replacement.Clut & 0x7FFF))
            {
                continue;
            }
            int left = Math.Max(u, replacement.U);
            int top = Math.Max(v, replacement.V);
            int right = Math.Min(u + original.Width, replacement.U + replacement.Width);
            int bottom = Math.Min(v + original.Height, replacement.V + replacement.Height);
            if (left >= right || top >= bottom || !Matches(replacement, left, top, right, bottom))
            {
                continue;
            }
            if (result is null)
            {
                result = new PixelImage(original.Width * 3, original.Height * 3);
                for (int row = 0; row < result.Height; row++)
                {
                    for (int column = 0; column < result.Width; column++)
                    {
                        result[column, row] = original[column / 3, row / 3];
                    }
                }
            }
            for (int row = top * 3; row < bottom * 3; row++)
            {
                replacement.Image.Pixels.AsSpan((row - replacement.V * 3) * replacement.Image.Width + (left - replacement.U) * 3, (right - left) * 3).CopyTo(result.Pixels.AsSpan((row - v * 3) * result.Width + (left - u) * 3));
            }
        }
        return result ?? original;

        bool Matches(NativeBattleTextureReplacement replacement, int left, int top, int right, int bottom)
        {
            int paletteX = (clut & 63) * 16;
            int paletteY = (clut & 0x7FFF) >> 6;
            if (replacement.PaletteWords.Length != 512 || paletteX + 256 > 1024 || !bank.AsSpan(2 * (paletteY * 1024 + paletteX), 512).SequenceEqual(replacement.PaletteWords.AsSpan()))
            {
                return false;
            }
            int pageX = (page & 15) * 64;
            int pageY = (page & 16) * 16;
            for (int row = top; row < bottom; row++)
            {
                for (int column = left & ~1; column < right; column += 2)
                {
                    int x = pageX + column / 2;
                    int y = pageY + row;
                    ushort actual = retainedPixels is null ? BinaryPrimitives.ReadUInt16LittleEndian(bank.AsSpan(2 * (y * 1024 + x))) : retainedPixels.ReadWord(x, y);
                    int expectedOffset = 2 * ((row - replacement.V) * replacement.WordWidth + (column - replacement.U) / 2);
                    if (actual != BinaryPrimitives.ReadUInt16LittleEndian(replacement.PixelWords.AsSpan(expectedOffset)))
                    {
                        return false;
                    }
                }
            }
            return true;
        }
    }
}
