namespace Albeoris.Games.FF8.Imaging.Workspace.Internal;

internal sealed class PaletteColorMapper
{
    private readonly IReadOnlyList<Rgba32> _palette;
    private readonly WorkspaceImportSpec _spec;
    private readonly Dictionary<Rgba32, Byte> _cache = [];

    public PaletteColorMapper(IReadOnlyList<Rgba32> palette, WorkspaceImportSpec spec)
    {
        if (palette.Count is < 1 or > 256)
            throw new ArgumentOutOfRangeException(nameof(palette));
        _palette = palette;
        _spec = spec;
    }

    public Byte Map(Rgba32 color, String description)
    {
        if (_cache.TryGetValue(color, out Byte cached))
            return cached;

        for (Int32 index = 0; index < _palette.Count; index++)
        {
            if (_palette[index] == color)
                return Cache(color, index);
        }

        if (_spec.MissingPaletteColor == PaletteColorHandling.RequireExactMatch)
            throw new InvalidDataException($"{description} uses color {Format(color)}, which is absent from the selected palette.");

        Int32 bestIndex = 0;
        Double bestDistance = Double.PositiveInfinity;
        for (Int32 index = 0; index < _palette.Count; index++)
        {
            Double distance = GetDistance(color, _palette[index], _spec.DistanceAlgorithm);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = index;
            }
        }
        return Cache(color, bestIndex);
    }

    private Byte Cache(Rgba32 color, Int32 index)
    {
        Byte result = checked((Byte)index);
        _cache.Add(color, result);
        return result;
    }

    private static Double GetDistance(Rgba32 left, Rgba32 right, PaletteDistanceAlgorithm algorithm)
    {
        Double alpha = Normalize(left.A) - Normalize(right.A);
        return algorithm switch
        {
            PaletteDistanceAlgorithm.SrgbEuclidean => Squared(
                Normalize(left.R) - Normalize(right.R),
                Normalize(left.G) - Normalize(right.G),
                Normalize(left.B) - Normalize(right.B),
                alpha),
            PaletteDistanceAlgorithm.LinearRgbEuclidean => Squared(
                ToLinear(left.R) - ToLinear(right.R),
                ToLinear(left.G) - ToLinear(right.G),
                ToLinear(left.B) - ToLinear(right.B),
                alpha),
            PaletteDistanceAlgorithm.Oklab => OklabDistance(left, right, alpha),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm)),
        };
    }

    private static Double OklabDistance(Rgba32 left, Rgba32 right, Double alpha)
    {
        (Double l1, Double a1, Double b1) = ToOklab(left);
        (Double l2, Double a2, Double b2) = ToOklab(right);
        return Squared(l1 - l2, a1 - a2, b1 - b2, alpha);
    }

    private static (Double L, Double A, Double B) ToOklab(Rgba32 color)
    {
        Double red = ToLinear(color.R);
        Double green = ToLinear(color.G);
        Double blue = ToLinear(color.B);
        Double l = Math.Cbrt(0.4122214708 * red + 0.5363325363 * green + 0.0514459929 * blue);
        Double m = Math.Cbrt(0.2119034982 * red + 0.6806995451 * green + 0.1073969566 * blue);
        Double s = Math.Cbrt(0.0883024619 * red + 0.2817188376 * green + 0.6299787005 * blue);
        return (
            0.2104542553 * l + 0.7936177850 * m - 0.0040720468 * s,
            1.9779984951 * l - 2.4285922050 * m + 0.4505937099 * s,
            0.0259040371 * l + 0.7827717662 * m - 0.8086757660 * s);
    }

    private static Double ToLinear(Byte value)
    {
        Double normalized = Normalize(value);
        return normalized <= 0.04045
            ? normalized / 12.92
            : Math.Pow((normalized + 0.055) / 1.055, 2.4);
    }

    private static Double Normalize(Byte value) => value / (Double)Byte.MaxValue;
    private static Double Squared(Double x, Double y, Double z, Double alpha) => x * x + y * y + z * z + alpha * alpha;
    private static String Format(Rgba32 color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}{color.A:X2}";
}
