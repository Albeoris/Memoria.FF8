using System.Buffers.Binary;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

/// <summary>The original primary indexed PC texture upload, before renderer channel conversion or replacement.</summary>
internal static class NativeBattlePcTextureUploadSource
{
    public static TextureSourceUpload? Create(NativeBattleEffectTextureState state, ushort page, ushort clut, int u, int v, int width, int height, NativeBattlePcTextureWindowPixels? retainedPixels)
    {
        int depth = (page >> 7) & 3;
        if (depth >= 2 || u is < 0 or > 255 || v is < 0 or > 255 || width is < 1 or > 256 || height is < 1 or > 256 || u + width > 256 || v + height > 256)
        {
            return null;
        }
        return new(width, height, 4, 7, () => Read(state, page, clut, u, v, width, height, retainedPixels));
    }

    private static ReadOnlyMemory<byte> Read(NativeBattleEffectTextureState state, ushort page, ushort clut, int u, int v, int width, int height, NativeBattlePcTextureWindowPixels? retainedPixels)
    {
        state.Validate();
        retainedPixels?.Validate();
        int depth = (page >> 7) & 3;
        int pixelsPerWord = 4 >> depth;
        int pageX = (page & 15) * 64;
        int pageY = (page & 16) * 16;
        int paletteX = (clut & 63) * 16;
        int paletteY = (clut & 0x7FFF) >> 6;
        int paletteLength = depth == 0 ? 16 : 256;
        if (paletteX + paletteLength > 1024 || paletteY >= 512)
        {
            throw new InvalidDataException("Native indexed upload requires its bounded live CLUT.");
        }
        uint[] palette = new uint[paletteLength];
        for (int index = 0; index < palette.Length; index++)
        {
            palette[index] = NativeBattlePcPalette.Decode467370(state.ReadVramWord(paletteX + index, paletteY));
        }
        byte[] pixels = new byte[checked(width * height * 4)];
        for (int row = 0; row < height; row++)
        {
            for (int column = 0; column < width; column++)
            {
                int x = u + column;
                int wordX = pageX + x / pixelsPerWord;
                int wordY = pageY + v + row;
                ushort word = retainedPixels is null ? state.ReadVramWord(wordX, wordY) : retainedPixels.ReadWord(wordX, wordY);
                int index = depth == 0 ? word >> (4 * (x & 3)) & 15 : word >> (8 * (x & 1)) & 255;
                BinaryPrimitives.WriteUInt32LittleEndian(pixels.AsSpan(4 * (row * width + column), 4), palette[index]);
            }
        }
        return pixels;
    }
}
