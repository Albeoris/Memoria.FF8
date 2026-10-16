namespace Albeoris.Games.FF8.Imaging.Tex;

/// <summary>Describes palette data stored in a TEX file.</summary>
[Flags]
public enum TexPaletteFlags
{
    /// <summary>The texture has no embedded palette data.</summary>
    None = 0,

    /// <summary>One or more palettes are stored between the header and pixel data.</summary>
    HasPalette = 1 << 0,
}
