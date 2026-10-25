using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>Remastered's seven search rows; the rare row adds Boko's rename item with equal weight.</summary>
public static class ChocoboAngeloSearch
{
    private static readonly ImmutableArray<ImmutableArray<int>> Rows =
    [
        [1, 2, 8, 7, 4, 1, 1, 5, 8],
        [145, 196, 124, 187, 160, 129, 155, 149, 160],
        [19, 18, 9, 26, 22, 26, 23, 23, 11],
        [68, 89, 89, 84, 91, 86, 82, 72, 83],
        [49, 34, 56, 55, 60, 33, 57, 53, 64],
        [35, 37, 35, 35, 36, 33, 38, 39, 36],
        [32, 100, 65, 165, 32, 100, 65, 165, 32, 100, 65, 165]
    ];

    public static int Select(ushort categoryRoll, ushort itemRoll, int categoryModulus)
    {
        if (categoryModulus is not (258 or 259))
        {
            throw new InvalidDataException("Angelo Search requires its saved native category modulus.");
        }
        int category = categoryRoll % categoryModulus;
        int row = category switch { < 128 => 0, < 160 => 1, < 176 => 2, < 192 => 3, < 200 => 4, < 256 => 5, _ => 6 };
        return Rows[row][itemRoll % Rows[row].Length];
    }
}
