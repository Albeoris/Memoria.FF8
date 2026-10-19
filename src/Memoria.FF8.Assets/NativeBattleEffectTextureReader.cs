using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

/// <summary>Material sampling of the shared native VRAM owner, preserving texture-page depth and CLUT selection.</summary>
public static class NativeBattleEffectTextureReader
{
    private static readonly ConditionalWeakTable<NativeBattleEffectTextureState, byte[]> Banks = new();

    /// <summary>Compare the raw texture words and palette used by an already decoded PC material.</summary>
    public static bool HasSamePcRegionSource(NativeBattleEffectTextureState previous, NativeBattleEffectTextureState current, ushort texturePage, ushort clut, int u, int v, int width, int height, NativeBattlePcTextureWindowPixels? pixelWords = null)
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
        pixelWords?.Validate();
        byte[] before = Banks.GetValue(previous, key => key.ReadVram());
        byte[] after = Banks.GetValue(current, key => key.ReadVram());
        if (depth < 2)
        {
            int paletteX = (clut & 63) * 16;
            int paletteWords = depth == 0 ? 16 : 256;
            if (paletteX + paletteWords > 1024)
            {
                return false;
            }
            int paletteOffset = 2 * (((clut & 0x7FFF) >> 6) * 1024 + paletteX);
            if (!before.AsSpan(paletteOffset, paletteWords * 2).SequenceEqual(after.AsSpan(paletteOffset, paletteWords * 2)))
            {
                return false;
            }
        }
        if (pixelWords is not null)
        {
            return true;
        }
        int pixelsPerWord = 4 >> depth;
        int pageX = (texturePage & 15) * 64;
        int pageY = (texturePage & 16) * 16;
        int firstPixels = Math.Min(width, 256 - u);
        for (int row = 0; row < height; row++)
        {
            int line = 2 * ((pageY + ((v + row) & 255)) * 1024 + pageX);
            int firstWord = u / pixelsPerWord;
            int endWord = (u + firstPixels + pixelsPerWord - 1) / pixelsPerWord;
            if (!before.AsSpan(line + firstWord * 2, (endWord - firstWord) * 2).SequenceEqual(after.AsSpan(line + firstWord * 2, (endWord - firstWord) * 2)))
            {
                return false;
            }
            int wrappedWords = (width - firstPixels + pixelsPerWord - 1) / pixelsPerWord;
            if (!before.AsSpan(line, wrappedWords * 2).SequenceEqual(after.AsSpan(line, wrappedWords * 2)))
            {
                return false;
            }
        }
        return true;
    }

    public static PixelImage DecodeRegion(NativeBattleEffectTextureState state, ushort texturePage, ushort clut, int u, int v, int width, int height)
    {
        return DecodeRegionCore(state, texturePage, clut, u, v, width, height, false, null, null, null);
    }

    public static PixelImage DecodePcRegion(NativeBattleEffectTextureState state, ushort texturePage, ushort clut, int u, int v, int width, int height, byte? alternatePaletteShift = null, NativeBattlePcDirectTextureFormat? directTextureFormat = null, NativeBattlePcTextureWindowPixels? pixelWords = null)
    {
        if ((texturePage & 0x180) == 0x100 && directTextureFormat is null)
        {
            throw new NotSupportedException("Native PC direct16-bit texture sampling requires its selected original555/565 device format.");
        }
        pixelWords?.Validate();
        return DecodeRegionCore(state, texturePage, clut, u, v, width, height, true, alternatePaletteShift, directTextureFormat, pixelWords);
    }

    private static PixelImage DecodeRegionCore(NativeBattleEffectTextureState state, ushort texturePage, ushort clut, int u, int v, int width, int height, bool pcPalette, byte? alternatePaletteShift, NativeBattlePcDirectTextureFormat? directTextureFormat, NativeBattlePcTextureWindowPixels? pixelWords)
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
        int paletteY = (pcPalette ? clut & 0x7FFF : clut) >> 6;
        PixelImage image = new(width, height);
        int paletteLength = depth == 0 ? 16 : depth == 1 ? 256 : 0;
        Rgba32[] palette = paletteLength == 0 ? [] : new Rgba32[paletteLength];
        bool[] decoded = paletteLength == 0 ? [] : new bool[paletteLength];
        int shift = depth == 0 ? 2 : depth == 1 ? 1 : 0;
        for (int row = 0; row < height; row++)
        {
            for (int column = 0; column < width; column++)
            {
                int x = (u + column) & 255;
                int y = pageY + ((v + row) & 255);
                ushort word = pixelWords is not null ? pixelWords.ReadWord(pageX + (x >> shift), y) : Word(pageX + (x >> shift), y);
                if (depth < 2)
                {
                    int index = depth == 0 ? (word >> (4 * (x & 3))) & 15 : (word >> (8 * (x & 1))) & 255;
                    if (!decoded[index])
                    {
                        palette[index] = Decode(Word(paletteX + index, paletteY));
                        decoded[index] = true;
                    }
                    image[column, row] = palette[index];
                }
                else
                {
                    image[column, row] = Decode(word);
                }
            }
        }
        return image;

        Rgba32 Decode(ushort color)
        {
            if (!pcPalette)
            {
                return Rgba32.FromPsxBgr555(color);
            }
            uint packed = depth == 2 ? NativeBattlePcDirectTexture.DecodeArgb(color, directTextureFormat!.Value) : alternatePaletteShift is byte paletteShift ? NativeBattlePcPalette.Decode467470(color, paletteShift) : NativeBattlePcPalette.Decode467370(color);
            return new((byte)(packed >> 16), (byte)(packed >> 8), (byte)packed, (byte)(packed >> 24));
        }

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
