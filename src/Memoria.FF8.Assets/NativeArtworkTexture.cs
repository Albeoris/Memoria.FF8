using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Assets;

/// <summary>Resolves edition artwork before composing native sprite parts.</summary>
internal static class NativeArtworkTexture
{
    public static PixelImage Resolve(IResourceSource source, ResourceKey owner, PixelImage original, int palette = 0, string subresource = "atlas", ResourceService? resources = null)
    {
        TextureBuildContext context = new(new TextureKey(owner, subresource, palette), resources?.Source ?? source.Descriptor, 1, "native-artwork", original.Width, original.Height, () => original);
        return resources is null ? (source as ITextureReplacementResolver)?.Resolve(context)?.Image ?? original : resources.GetTexture(context).Value.Image;
    }

    public static int Scale(PixelImage image, int logicalWidth, int logicalHeight)
    {
        int scale = image.Width / logicalWidth;
        if (scale < 1 || image.Width != logicalWidth * scale || image.Height != logicalHeight * scale)
        {
            throw new InvalidDataException("Artwork must preserve its native logical canvas at an integer scale.");
        }
        return scale;
    }
}
