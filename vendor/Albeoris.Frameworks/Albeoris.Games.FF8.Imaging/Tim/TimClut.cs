namespace Albeoris.Games.FF8.Imaging.Tim;

/// <summary>A lossless TIM color lookup table in its native PSX BGR555 representation.</summary>
public sealed class TimClut
{
    private readonly IReadOnlyDictionary<TimPixelMode, IReadOnlyList<TimPalette>> _palettes;

    public TimClut(Int16 x, Int16 y, Int16 width, Int16 height, UInt16[] colors)
    {
        ArgumentNullException.ThrowIfNull(colors);
        if (width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0)
            throw new ArgumentOutOfRangeException(nameof(height));
        if (colors.Length == 0 || colors.Length % width != 0)
            throw new ArgumentException("The CLUT colors must form complete rows.", nameof(colors));

        X = x;
        Y = y;
        Width = width;
        Height = height;
        Colors = colors;
        _palettes = new Dictionary<TimPixelMode, IReadOnlyList<TimPalette>>
        {
            [TimPixelMode.Indexed4] = CreatePalettes(16),
            [TimPixelMode.Indexed8] = CreatePalettes(256),
        };
    }

    public Int16 X { get; set; }
    public Int16 Y { get; set; }
    public Int16 Width { get; }
    public Int16 Height { get; }
    public UInt16[] Colors { get; }

    /// <summary>Returns conventional 16- or 256-color palettes for the indexed pixel mode.</summary>
    public IReadOnlyList<TimPalette> GetPalettes(TimPixelMode pixelMode)
    {
        if (_palettes.TryGetValue(pixelMode, out IReadOnlyList<TimPalette>? palettes))
            return palettes;
        throw new ArgumentException($"Pixel mode {pixelMode} does not use palettes.", nameof(pixelMode));
    }

    private IReadOnlyList<TimPalette> CreatePalettes(Int32 colorCount)
    {
        TimPalette[] result = new TimPalette[Colors.Length / colorCount];
        for (Int32 index = 0; index < result.Length; index++)
            result[index] = new TimPalette(Colors.AsSpan(index * colorCount, colorCount));
        return result;
    }
}
