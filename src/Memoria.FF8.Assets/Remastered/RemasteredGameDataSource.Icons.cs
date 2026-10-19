using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Assets.Remastered;

public sealed partial class RemasteredGameDataSource
{
    private readonly Dictionary<int, PixelImage> iconAtlases = [];

    private PixelImage? ReadSplitIconAtlas(int palette)
    {
        if (iconAtlases.TryGetValue(palette, out PixelImage? atlas))
        {
            return atlas;
        }
        string[] paths = Enumerable.Range(0, 4).Select(page => $"textures/hires/iconfl{page:D2}.tex/{language}/{palette}.png").ToArray();
        if (!paths.Any(Contains))
        {
            return null;
        }
        if (!paths.All(Contains))
        {
            throw new InvalidDataException("A split menu icon atlas requires all four pages for the selected language and palette.");
        }
        int scale = 0;
        for (int page = 0; page < paths.Length; page++)
        {
            using Stream stream = Open(paths[page]);
            PixelImage image = TextureImageDecoder.Decode(Read(stream), paths[page]).Image;
            int pageScale = NativeArtworkTexture.Scale(image, 128, 128);
            if (page == 0)
            {
                scale = pageScale;
                atlas = new PixelImage(256 * scale, 256 * scale);
            }
            else if (pageScale != scale)
            {
                throw new InvalidDataException("Split menu icon pages must use the same integer scale.");
            }
            // Pages retain their CLUT number and cover the native atlas in row-major 128x128 quadrants.
            int x = page % 2 * image.Width;
            int y = page / 2 * image.Height;
            for (int row = 0; row < image.Height; row++)
            {
                image.Pixels.AsSpan(row * image.Width, image.Width).CopyTo(atlas!.Pixels.AsSpan((y + row) * atlas.Width + x, image.Width));
            }
        }
        iconAtlases.Add(palette, atlas!);
        return atlas;
    }
}
