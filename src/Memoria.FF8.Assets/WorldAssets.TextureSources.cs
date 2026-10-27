using Albeoris.Games.FF8.Imaging.Tim;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Assets;

public sealed partial class WorldAssets
{
    private readonly Dictionary<(int Section, int Member, int Palette), AssetHandle<TextureValue>> nativeSectionTextures = [];

    public PixelImage ReadNativeSectionTexture(int section, int member, int palette = 0)
    {
        if (nativeSectionTextures.TryGetValue((section, member, palette), out AssetHandle<TextureValue>? cached))
        {
            return cached.Value.Image;
        }
        if (section is not (38 or 39 or 40 or 42))
        {
            throw new ArgumentOutOfRangeException(nameof(section));
        }
        int[] pointers = WorldAssetReader.InnerPointers(wmset, sections[section - 1]);
        ArgumentOutOfRangeException.ThrowIfNegative(member);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(member, pointers.Length);
        TimImage tim = WorldAssetReader.ReadTim(wmset, sections[section - 1] + pointers[member]).Image;
        PixelImage original = tim.Decode(palette);
        TextureKey key = new(worldSettingsKey, $"section/{section}/texture/{member}", palette);
        AssetHandle<TextureValue>? handle = textureScope.Register(key, original, "classic-world-section", SectionTextureRegion(tim, palette), value => tim.Decode(value), tim.Palettes.Count);
        if (handle is not null)
        {
            nativeSectionTextures.Add((section, member, palette), handle);
        }
        return handle?.Value.Image ?? original;
    }

    private static TextureSourceRegion SectionTextureRegion(TimImage tim, int palette)
    {
        int bits = tim.PixelMode == TimPixelMode.Indexed4 ? 4 : tim.PixelMode == TimPixelMode.Indexed8 ? 8 : 16;
        if (tim.Clut is not { } clut)
        {
            return new(tim.X, tim.Y, bits);
        }
        int colors = bits == 4 ? 16 : 256;
        int offset = checked(palette * colors);
        return new(tim.X, tim.Y, bits, PaletteX: clut.X + offset % clut.Width, PaletteY: clut.Y + offset / clut.Width);
    }

    private TextureSourceAddress ReadSectionTextureAddress(int section, TimImage source, int palette, int x, int y, int width, int height)
    {
        int[] pointers = WorldAssetReader.InnerPointers(wmset, sections[section - 1]);
        for (int member = 0; member < pointers.Length; member++)
        {
            TimImage tim = WorldAssetReader.ReadTim(wmset, sections[section - 1] + pointers[member]).Image;
            if (tim.X == source.X && tim.Y == source.Y && tim.WidthInWords == source.WidthInWords && tim.Height == source.Height)
            {
                return new(worldSettingsKey, $"section/{section}/texture/{member}", palette, x, y, width, height, tim.PixelWidth, tim.Height);
            }
        }
        throw new InvalidDataException("The world texture has no member in its original section.");
    }

    public static int SelectTerrainTexturePage(int page, ushort replacementMask)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(page);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(page, 15);
        return (replacementMask & 1) != 0 && page is 5 or 7 ? page + 12 : page;
    }

    private TextureSourceAddress? ReadTerrainTextureAlias(int page, TimImage enlarged, ushort replacementMask)
    {
        int x = 16 * (page - 2 * ((page / 2) & 1) + (page & 1));
        int y = ((page / 2) & 1) == 0 ? 256 : 384;
        int widthWords = enlarged.WidthInWords / 4;
        int height = enlarged.Height / 2;
        int section = (replacementMask & 1) != 0 && page is 5 or 7 ? 40 : 38;
        int[] pointers = WorldAssetReader.InnerPointers(wmset, sections[section - 1]);
        for (int member = 0; member < Math.Min(8, pointers.Length); member++)
        {
            TimImage tim = WorldAssetReader.ReadTim(wmset, sections[section - 1] + pointers[member]).Image;
            if (tim.PixelMode == TimPixelMode.Indexed4 && x >= tim.X && x + widthWords <= tim.X + tim.WidthInWords && y >= tim.Y && y + height <= tim.Y + tim.Height)
            {
                return new(worldSettingsKey, $"section/{section}/texture/{member}", 0, (x - tim.X) * 4, y - tim.Y, widthWords * 4, height, tim.PixelWidth, tim.Height);
            }
        }
        if (section == 40)
        {
            return null;
        }
        throw new InvalidDataException("The enlarged world texture has no matching original terrain rectangle.");
    }
}
