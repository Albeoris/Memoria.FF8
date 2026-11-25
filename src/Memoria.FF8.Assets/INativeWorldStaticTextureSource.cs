using Albeoris.Games.FF8.Imaging;
using Albeoris.Games.FF8.Imaging.Tim;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Assets;

/// <summary>A full-color source and its logical native UV canvas, independent of image resolution.</summary>
public sealed record NativeWorldStaticTextureSheet(PixelImage Image, int Width, int Height, int OriginU, int OriginV)
{
    public TextureSourceAddress? SourceAlias { get; init; }
}

/// <summary>Resolves a native full-color sheet from its field context, upload and texture page.</summary>
public interface INativeWorldStaticTextureSource
{
    NativeWorldStaticTextureSheet? ReadWorldStaticTextureSheet(string? sourceFieldName, TimImage upload, ushort texturePage);
}
