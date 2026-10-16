namespace Albeoris.Games.FF8.Imaging.Workspace.Internal;

internal static class MimMapWorkspacePaths
{
    public const String MimeType = "mimetype";
    public const String Stack = "stack.xml";
    public const String MergedImage = "mergedimage.png";
    public const String Thumbnail = "Thumbnails/thumbnail.png";
    public const String Manifest = "data/albeoris/manifest.json";
    public const String SourceMim = "data/albeoris/source.mim";
    public const String SourceMap = "data/albeoris/source.map";
    public const String SourceMimLayer = "data/layers/source-mim.png";
    public const String SourceMapLayer = "data/layers/source-map.png";
    public const String Palette = "data/palettes.png";

    public static String Texture(UInt32 pupuId) => $"data/layers/{pupuId:X8}.png";
}
