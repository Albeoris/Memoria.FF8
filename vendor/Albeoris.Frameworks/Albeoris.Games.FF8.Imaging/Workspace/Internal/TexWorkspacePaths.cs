namespace Albeoris.Games.FF8.Imaging.Workspace.Internal;

internal static class TexWorkspacePaths
{
    public const String Image = "image.png";
    public const String Palettes = "palettes.png";

    public static String Preview(Int32 paletteIndex) => $"previews/{paletteIndex + 1:D3}.png";
}