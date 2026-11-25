using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Assets.Remastered;

public sealed partial class RemasteredGameDataSource
{
    private readonly Dictionary<string, PixelImage> artworkImages = new(StringComparer.OrdinalIgnoreCase);

    private TextureReplacement? ResolveArtwork(TextureBuildContext context)
    {
        if (context.Layout != "native-artwork")
        {
            return null;
        }
        string? path = null;
        string owner = context.Key.Owner.Path;
        if (context.Key.Subresource == "atlas" && owner.StartsWith("menu/", StringComparison.Ordinal))
        {
            string name = owner[5..];
            if (name == "icon.tex" && ReadSplitIconAtlas(context.Key.Palette) is { } icons)
            {
                _ = NativeArtworkTexture.Scale(icons, context.LogicalWidth, context.LogicalHeight);
                return new TextureReplacement(icons, $"{Descriptor.Id}:{context.Key}", context.LogicalWidth, context.LogicalHeight);
            }
            if (RemasteredArtworkPaths.MenuAtlasStem(context.Key, language) is { } stem)
            {
                path = stem + ".png";
            }
            else if (name.Length == 8 && name.StartsWith("mc", StringComparison.Ordinal) && int.TryParse(name.AsSpan(2, 2), out int cardAtlas) && cardAtlas is >= 0 and <= 9)
            {
                path = "textures/" + name + (cardAtlas == 5 && language != "jp" ? "_en" : "") + ".png";
            }
        }
        else if (owner == "cards/cards" && context.Key.Subresource.StartsWith("front/", StringComparison.Ordinal) && int.TryParse(context.Key.Subresource.AsSpan(6), out int cardId) && cardId is >= 0 and < CardArtwork.CardCount)
        {
            int atlas = cardId / 8;
            path = $"textures/cards/cards_{atlas}" + (atlas == 7 && language != "jp" ? "_en" : "") + ".png";
            PixelImage? sheet = ReadArtworkImage(path);
            if (sheet is null)
            {
                return null;
            }
            int scale = NativeArtworkTexture.Scale(sheet, 256, 256);
            int cell = cardId % 8;
            PixelImage front = CropArtwork(sheet, cell % 2 * 64 * scale, cell / 2 * 64 * scale, 64 * scale, 64 * scale);
            return new TextureReplacement(front, $"{Descriptor.Id}:{context.Key}", context.LogicalWidth, context.LogicalHeight);
        }
        else if (owner == "cards/cards" && context.Key.Subresource.StartsWith("element/", StringComparison.Ordinal) && int.TryParse(context.Key.Subresource.AsSpan(8), out int element) && element is >= 0 and < 8)
        {
            PixelImage? sheet = ReadArtworkImage("textures/cards/text_0.png");
            if (sheet is null)
            {
                return null;
            }
            int scale = NativeArtworkTexture.Scale(sheet, 256, 256);
            PixelImage sprite = CropArtwork(sheet, element % 4 * 64 * scale, (element / 4 + 1) * 16 * scale, 16 * scale, 16 * scale);
            return new TextureReplacement(sprite, $"{Descriptor.Id}:{context.Key}", context.LogicalWidth, context.LogicalHeight);
        }
        else if (owner == "cards/cards" && context.Key.Subresource == "back")
        {
            PixelImage? sheet = ReadArtworkImage("textures/cards/cards_13.png");
            if (sheet is null)
            {
                return null;
            }
            int scale = NativeArtworkTexture.Scale(sheet, 256, 256);
            PixelImage back = CropArtwork(sheet, 0, 192 * scale, 64 * scale, 64 * scale);
            return new TextureReplacement(back, $"{Descriptor.Id}:{context.Key}", context.LogicalWidth, context.LogicalHeight);
        }
        if (path is null || ReadArtworkImage(path) is not { } image)
        {
            return null;
        }
        _ = NativeArtworkTexture.Scale(image, context.LogicalWidth, context.LogicalHeight);
        return new TextureReplacement(image, $"{Descriptor.Id}:{context.Key}", context.LogicalWidth, context.LogicalHeight);
    }

    private PixelImage? ReadArtworkImage(string path)
    {
        if (!Contains(path))
        {
            return null;
        }
        if (!artworkImages.TryGetValue(path, out PixelImage? image))
        {
            using Stream stream = Open(path);
            image = TextureImageDecoder.Decode(Read(stream), path).Image;
            artworkImages.Add(path, image);
        }
        return image;
    }

    private static PixelImage CropArtwork(PixelImage source, int x, int y, int width, int height)
    {
        PixelImage result = new(width, height);
        for (int row = 0; row < height; row++)
        {
            source.Pixels.AsSpan((y + row) * source.Width + x, width).CopyTo(result.Pixels.AsSpan(row * width));
        }
        return result;
    }
}
