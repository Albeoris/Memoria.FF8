using Albeoris.Games.FF8.Imaging;
using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

/// <summary>Material sampling of the shared native VRAM owner, preserving texture-page depth and CLUT selection.</summary>
public static partial class NativeBattleEffectTextureReader
{
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
        previous.Validate();
        current.Validate();
        if (depth < 2)
        {
            int paletteX = (clut & 63) * 16;
            int paletteWords = depth == 0 ? 16 : 256;
            if (paletteX + paletteWords > 1024)
            {
                return false;
            }
            int paletteOffset = 2 * (((clut & 0x7FFF) >> 6) * 1024 + paletteX);
            if (!HasSameVramRange(previous, current, paletteOffset, paletteWords * 2))
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
            if (!HasSameVramRange(previous, current, line + firstWord * 2, (endWord - firstWord) * 2))
            {
                return false;
            }
            int wrappedWords = (width - firstPixels + pixelsPerWord - 1) / pixelsPerWord;
            if (!HasSameVramRange(previous, current, line, wrappedWords * 2))
            {
                return false;
            }
        }
        return true;
    }

    private static bool HasSameVramRange(NativeBattleEffectTextureState previous, NativeBattleEffectTextureState current, int offset, int length)
    {
        if (offset < 0 || length < 0 || offset > NativeBattleEffectArena.Length - length)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }
        if (ReferenceEquals(previous.VramPublicationIdentity, current.VramPublicationIdentity))
        {
            return true;
        }
        Span<byte> before = stackalloc byte[512];
        Span<byte> after = stackalloc byte[512];
        for (int copied = 0; copied < length;)
        {
            int count = Math.Min(before.Length, length - copied);
            previous.ReadVramInto(offset + copied, before[..count]);
            current.ReadVramInto(offset + copied, after[..count]);
            if (!before[..count].SequenceEqual(after[..count]))
            {
                return false;
            }
            copied += count;
        }
        return true;
    }

    public static PixelImage DecodeRegion(NativeBattleEffectTextureState state, ushort texturePage, ushort clut, int u, int v, int width, int height)
    {
        return DecodeRegionCore(state, texturePage, clut, u, v, width, height, false, null, null, null, false, out _);
    }

    public static PixelImage DecodePcRegion(NativeBattleEffectTextureState state, ushort texturePage, ushort clut, int u, int v, int width, int height, byte? alternatePaletteShift = null, NativeBattlePcDirectTextureFormat? directTextureFormat = null, NativeBattlePcTextureWindowPixels? pixelWords = null)
    {
        if ((texturePage & 0x180) == 0x100 && directTextureFormat is null)
        {
            throw new NotSupportedException("Native PC direct16-bit texture sampling requires its selected original555/565 device format.");
        }
        pixelWords?.Validate();
        return DecodeRegionCore(state, texturePage, clut, u, v, width, height, true, alternatePaletteShift, directTextureFormat, pixelWords, false, out _);
    }

    public static PixelImage DecodePcRegion(NativeBattleEffectTextureState state, ushort texturePage, ushort clut, int u, int v, int width, int height, out NativeBattlePcPaletteReceipt? paletteReceipt, byte? alternatePaletteShift = null, NativeBattlePcDirectTextureFormat? directTextureFormat = null, NativeBattlePcTextureWindowPixels? pixelWords = null)
    {
        if ((texturePage & 0x180) == 0x100 && directTextureFormat is null)
        {
            throw new NotSupportedException("Native PC direct16-bit texture sampling requires its selected original555/565 device format.");
        }
        pixelWords?.Validate();
        return DecodeRegionCore(state, texturePage, clut, u, v, width, height, true, alternatePaletteShift, directTextureFormat, pixelWords, true, out paletteReceipt);
    }

    private static PixelImage DecodeRegionCore(NativeBattleEffectTextureState state, ushort texturePage, ushort clut, int u, int v, int width, int height, bool pcPalette, byte? alternatePaletteShift, NativeBattlePcDirectTextureFormat? directTextureFormat, NativeBattlePcTextureWindowPixels? pixelWords, bool capturePalette, out NativeBattlePcPaletteReceipt? paletteReceipt)
    {
        paletteReceipt = null;
        if (u is < 0 or > 255 || v is < 0 or > 255 || width is < 1 or > 256 || height is < 1 or > 256)
        {
            throw new InvalidDataException("Native texture region requires byte UVs and dimensions within one256x256 page.");
        }
        int depth = (texturePage >> 7) & 3;
        if (depth == 3)
        {
            throw new NotSupportedException("Native texture page pixel depth3 is reserved.");
        }
        state.Validate();
        int pageX = (texturePage & 15) * 64;
        int pageY = (texturePage & 16) * 16;
        int paletteX = (clut & 63) * 16;
        int paletteY = (pcPalette ? clut & 0x7FFF : clut) >> 6;
        PixelImage image = new(width, height);
        int paletteLength = depth == 0 ? 16 : depth == 1 ? 256 : 0;
        Rgba32[] palette = paletteLength == 0 ? [] : new Rgba32[paletteLength];
        bool[] decoded = paletteLength == 0 ? [] : new bool[paletteLength];
        Span<ushort> originalPalette = capturePalette && paletteLength > 0 ? stackalloc ushort[paletteLength] : Span<ushort>.Empty;
        int shift = depth == 0 ? 2 : depth == 1 ? 1 : 0;
        int pixelsPerWord = 1 << shift;
        int bitsPerPixel = 16 >> shift;
        for (int row = 0; row < height; row++)
        {
            int y = pageY + ((v + row) & 255);
            int output = row * width;
            ReadOnlySpan<byte> retainedRun = default;
            int runFirstWord = 0;
            int runEnd = 0;
            for (int column = 0; column < width;)
            {
                int x = (u + column) & 255;
                int wordX = pageX + (x >> shift);
                if (pixelWords is not null && column == runEnd)
                {
                    int runPixels = Math.Min(width - column, 256 - x);
                    int lastWord = pageX + ((x + runPixels - 1) >> shift);
                    runFirstWord = wordX;
                    runEnd = column + runPixels;
                    retainedRun = default;
                    if (y >= pixelWords.Y && y < pixelWords.Y + pixelWords.Height && wordX >= pixelWords.X && lastWord < pixelWords.X + pixelWords.Width)
                    {
                        int offset = 2 * ((y - pixelWords.Y) * pixelWords.Width + wordX - pixelWords.X);
                        retainedRun = pixelWords.Data.AsSpan(offset, 2 * (lastWord - wordX + 1));
                    }
                    // Invalid runs retain scalar reads so the first invalid sample or used CLUT entry still fails in order.
                }
                ushort word = pixelWords is null ? Word(wordX, y) : retainedRun.IsEmpty ? pixelWords.ReadWord(wordX, y) : BinaryPrimitives.ReadUInt16LittleEndian(retainedRun[(2 * (wordX - runFirstWord))..]);
                int first = x & (pixelsPerWord - 1);
                int count = Math.Min(width - column, pixelsPerWord - first);
                if (depth < 2)
                {
                    int indices = word >> (bitsPerPixel * first);
                    int mask = paletteLength - 1;
                    for (int pixel = 0; pixel < count; pixel++)
                    {
                        int index = indices & mask;
                        indices >>= bitsPerPixel;
                        if (!decoded[index])
                        {
                            ushort color = Word(paletteX + index, paletteY);
                            palette[index] = Decode(color);
                            decoded[index] = true;
                            if (capturePalette)
                            {
                                originalPalette[index] = color;
                            }
                        }
                        image.Pixels[output + column + pixel] = palette[index];
                    }
                }
                else
                {
                    image.Pixels[output + column] = Decode(word);
                }
                column += count;
            }
        }
        if (capturePalette && paletteLength > 0)
        {
            int used = 0;
            foreach (bool entry in decoded)
            {
                if (entry)
                {
                    used++;
                }
            }
            ImmutableArray<NativeBattlePcPaletteEntry>.Builder entries = ImmutableArray.CreateBuilder<NativeBattlePcPaletteEntry>(used);
            for (int index = 0; index < decoded.Length; index++)
            {
                if (decoded[index])
                {
                    entries.Add(new(checked((byte)index), originalPalette[index]));
                }
            }
            paletteReceipt = new(clut, entries.MoveToImmutable());
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
            return state.ReadVramWord(x, y);
        }
    }
}
