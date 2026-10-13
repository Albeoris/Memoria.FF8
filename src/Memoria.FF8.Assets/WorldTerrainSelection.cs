using System.Collections.ObjectModel;
using Albeoris.Games.FF8.Imaging;

namespace Memoria.FF8.Assets;

/// <summary>Native world replacement rectangles and WMSET section33 regional sky selection.</summary>
public static class WorldTerrainSelection
{
    public static ushort ReplacementMask(byte eventByte264, byte eventByte266)
    {
        // Native544860 initializes this word from the naturally persisted story byte.
        // Unrecognized states retain the previous native word, which this stateless API cannot infer.
        ushort mask = (eventByte266 & 31) switch
        {
            0 => 285,
            1 => 269,
            2 => 257,
            3 => 256,
            4 => 384,
            _ => throw new InvalidDataException($"World replacement state {eventByte266 & 31} retains an unavailable previous native mask.")
        };
        if ((eventByte264 & 8) != 0)
        {
            mask |= 64;
        }
        return mask;
    }

    public static IReadOnlyDictionary<int, int> Replacements(ushort mask)
    {
        Dictionary<int, int> result = [];
        if ((mask & 1) != 0)
        {
            // Original rectangle373..572 spans rows11..17 and columns21..28, with gaps between rows.
            for (int row = 11; row <= 17; row++)
            {
                for (int column = 21; column <= 28; column++)
                {
                    result.Add(row * 32 + column, 768 + (row - 11) * 8 + column - 21);
                }
            }
        }
        if ((mask & 2) != 0)
        {
            result.Add(149, 824);
            result.Add(150, 825);
        }
        if ((mask & 4) != 0)
        {
            result.Add(267, 826);
        }
        if ((mask & 8) != 0)
        {
            result.Add(274, 827);
            result.Add(275, 828);
        }
        if ((mask & 16) != 0)
        {
            result.Add(327, 829);
        }
        if ((mask & 32) != 0)
        {
            result.Add(214, 830);
            result.Add(215, 831);
            result.Add(246, 832);
            result.Add(247, 833);
        }
        if ((mask & 64) != 0)
        {
            result.Add(361, 834);
        }
        return new ReadOnlyDictionary<int, int>(result);
    }

    public static WorldSky SelectSky(IReadOnlyList<WorldSky> skies, int x, int z, byte eventByte266)
    {
        if (skies.Count == 0)
        {
            throw new InvalidDataException("World regional sky table has no default entry.");
        }
        WorldSky fallback = skies[0];
        int firstRegion = (eventByte266 & 31) == 4 ? 1 : 2;
        for (int index = firstRegion; index < skies.Count; index++)
        {
            WorldSky region = skies[index];
            // Native555630 shifts before storing signed WORD components and truncates square root to int.
            short dx = unchecked((short)(((int)region.Position.X - x) >> 5));
            short dz = unchecked((short)(((int)region.Position.Z - z) >> 5));
            int distance = (int)Math.Sqrt((int)dx * dx + (int)dz * dz);
            int radius = region.Radius >> 5;
            if (distance >= radius)
            {
                continue;
            }
            int blend = distance * (4096 / radius);
            return fallback with
            {
                Top = Blend(region.Top, fallback.Top, blend),
                Center = Blend(region.Center, fallback.Center, blend),
                Bottom = Blend(region.Bottom, fallback.Bottom, blend)
            };
        }
        return fallback;
    }

    private static Rgba32 Blend(Rgba32 region, Rgba32 fallback, int blend)
    {
        return new Rgba32((byte)(region.R + blend * (fallback.R - region.R) / 4096), (byte)(region.G + blend * (fallback.G - region.G) / 4096), (byte)(region.B + blend * (fallback.B - region.B) / 4096));
    }
}
