namespace Albeoris.Games.FF8.Imaging.Workspace.Internal;

internal static class WorkspacePaths
{
    public static String Image(Int32 index) => $"{index + 1:D3}.png";
    public static String Clut(Int32 index) => $"{index + 1:D3}_clut.png";
    public static String Palette(Int32 imageIndex, Int32 paletteIndex) => $"palettes/{imageIndex + 1:D3}/{paletteIndex + 1:D2}.png";
    public static String Preview(Int32 index) => $"{index + 1:D3}_previews.png";
}
