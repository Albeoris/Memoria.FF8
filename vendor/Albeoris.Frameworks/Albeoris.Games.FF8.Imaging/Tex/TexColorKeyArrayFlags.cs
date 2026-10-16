namespace Albeoris.Games.FF8.Imaging.Tex;

/// <summary>Describes per-palette color-key data stored in a TEX file.</summary>
[Flags]
public enum TexColorKeyArrayFlags
{
    /// <summary>The texture has no per-palette color-key array.</summary>
    None = 0,

    /// <summary>A color-key byte for every palette is stored after the pixel data.</summary>
    HasColorKeyArray = 1 << 0,
}
