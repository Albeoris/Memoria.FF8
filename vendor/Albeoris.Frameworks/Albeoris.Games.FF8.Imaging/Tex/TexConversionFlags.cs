namespace Albeoris.Games.FF8.Imaging.Tex;

/// <summary>Describes conversions requested for indexed TEX pixel data.</summary>
[Flags]
public enum TexConversionFlags
{
    /// <summary>No indexed-pixel conversion is requested.</summary>
    None = 0,

    /// <summary>Indexed pixel data is converted to eight-bit indices when loaded.</summary>
    ConvertIndexedTo8Bit = 1 << 0,
}
