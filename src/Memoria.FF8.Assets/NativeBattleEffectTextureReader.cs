using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

/// <summary>Material sampling of the shared native VRAM owner, preserving texture-page depth and CLUT selection.</summary>
public static class NativeBattleEffectTextureReader
{
    private static readonly ConditionalWeakTable<NativeBattleEffectTextureState, byte[]> Banks = new();

    public static PixelImage DecodeRegion(NativeBattleEffectTextureState state, ushort texturePage, ushort clut, int u, int v, int width, int height)
    {
        if (u is < 0 or > 255 || v is < 0 or > 255 || width is < 1 or > 256 || height is < 1 or > 256)
        {
            throw new InvalidDataException("Native texture region requires byte UVs and dimensions within one256x256 page.");
        }
        int depth = (texturePage >> 7) & 3;
        if (depth == 3)
        {
            throw new NotSupportedException("Native texture page pixel depth3 is reserved.");
        }
        byte[] bank = Banks.GetValue(state, key => key.ReadVram());
        int pageX = (texturePage & 15) * 64;
        int pageY = (texturePage & 16) * 16;
        int paletteX = (clut & 63) * 16;
        int paletteY = clut >> 6;
        PixelImage image = new(width, height);
        for (int row = 0; row < height; row++)
        {
            for (int column = 0; column < width; column++)
            {
                int x = (u + column) & 255;
                int y = pageY + ((v + row) & 255);
                int shift = depth == 0 ? 2 : depth == 1 ? 1 : 0;
                ushort word = Word(pageX + (x >> shift), y);
                ushort color = depth switch
                {
                    0 => Word(paletteX + ((word >> (4 * (x & 3))) & 15), paletteY),
                    1 => Word(paletteX + ((word >> (8 * (x & 1))) & 255), paletteY),
                    _ => word
                };
                image[column, row] = Rgba32.FromPsxBgr555(color);
            }
        }
        return image;

        ushort Word(int x, int y)
        {
            if (x is < 0 or >= 1024 || y is < 0 or >= 512)
            {
                throw new InvalidDataException("Native texture page or CLUT sample leaves1024x512 word VRAM.");
            }
            return BinaryPrimitives.ReadUInt16LittleEndian(bank.AsSpan(2 * (y * 1024 + x), 2));
        }
    }
}
