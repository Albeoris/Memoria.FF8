using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Assets;

/// <summary>Resolves edition artwork before composing native sprite parts.</summary>
internal static class NativeArtworkTexture
{
    public static PixelImage Resolve(IResourceSource source, ResourceKey owner, PixelImage original, int palette = 0, string subresource = "atlas")
    {
        TextureBuildContext context = new(new TextureKey(owner, subresource, palette), source.Descriptor, 1, "native-artwork", original.Width, original.Height, () => original);
        return (source as ITextureReplacementResolver)?.Resolve(context)?.Image ?? original;
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
