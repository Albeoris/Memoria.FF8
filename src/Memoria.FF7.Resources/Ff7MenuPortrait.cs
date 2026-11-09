using Memoria.FF7.Formats;

namespace Memoria.FF7.Resources;

/// <summary>The portrait region selected by the original menu's UV rectangle, retaining native palette alpha.</summary>
public sealed record Ff7MenuPortrait(int Width, int Height, ReadOnlyMemory<uint> Pixels)
{
    public static Ff7MenuPortrait Decode(PcTexture texture)
    {
        ArgumentNullException.ThrowIfNull(texture);
        PcTextureRenderInfo info = texture.RenderInfo;
        if (texture.BytesPerPixel != 1 || info.UsePalette != 1 || info.BitsPerIndex != 8 || info.ColorKey != 0 || info.UsePaletteColorKey != 0)
        {
            throw new NotSupportedException("Menu portraits require the supported unkeyed indexed8 TEX profile; unsupported color-key contracts cannot be ignored.");
        }
        if (texture.Width <= 0 || texture.Height <= 0 || texture.Width % 32 != 0 || texture.Height % 4 != 0 || (uint)info.PaletteIndex >= texture.Palettes.Count || texture.Pixels.Length != (long)texture.Width * texture.Height)
        {
            throw new InvalidDataException("The native portrait UV rectangle, palette or pixel dimensions are invalid.");
        }
        int width = checked(texture.Width / 32 * 21);
        int height = checked(texture.Height / 4 * 3);
        uint[] pixels = new uint[checked(width * height)];
        IReadOnlyList<uint> palette = texture.Palettes[(int)info.PaletteIndex];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                byte index = texture.Pixels.Span[y * texture.Width + x];
                if (index >= palette.Count)
                {
                    throw new InvalidDataException("A menu portrait texel references an absent selected-palette entry.");
                }
                pixels[y * width + x] = palette[index];
            }
        }
        return new(width, height, pixels);
    }
}
