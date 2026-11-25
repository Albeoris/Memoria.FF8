using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

/// <summary>Pixel artwork for battle statuses without an icon in the FFVIII menu atlas.</summary>
internal static class BattleStatusSymbolArtwork
{
    private static readonly Dictionary<NativeStatusId, PixelImage> Symbols = new()
    {
        [NativeStatusId.Doom] = Sprite(
        [
            "................",
            "...##########...",
            "..#**********#..",
            "...##########...",
            "....#*....+#....",
            "....##*..+##....",
            ".....##++##.....",
            "......#++#......",
            "......#++#......",
            ".....##..##.....",
            "....##.++*##....",
            "....#++***+#....",
            "...##########...",
            "..#********++#..",
            "...##########...",
            "................"
        ]),
        [NativeStatusId.Petrifying] = Sprite(
        [
            "................",
            "......####......",
            "....##*+++##....",
            "...#***+++++#...",
            "..#**++#+++-#...",
            "..#*+++#++--#...",
            "..#+++#++---#...",
            "..#++#++---#....",
            "...#++----#.....",
            "....######......",
            "................",
            "..############..",
            "..#***#+++#--#..",
            "..#+++#+++#--#..",
            "..############..",
            "................"
        ]),
        [NativeStatusId.Vit0] = Sprite(
        [
            "................",
            "..#####..#####..",
            ".#****#..#***+#.",
            ".#*+++#.##+++-#.",
            ".#++++#.#+++--#.",
            ".#+++#.##++---#.",
            "..#++#.#++---#..",
            "..#++##.##---#..",
            "...#++#..#--#...",
            "...#++#.##--#...",
            "....##+##--#....",
            ".....#+#--#.....",
            "......#.-#......",
            ".......##.......",
            "................",
            "................"
        ])
    };

    internal static UiImageSource? Source(NativeStatusId status)
    {
        return Symbols.ContainsKey(status) ? new UiImageSource(UiImageKind.StatusSymbol, (int)status) : null;
    }

    internal static PixelImage Read(NativeStatusId status) => Symbols[status];

    private static PixelImage Sprite(string[] rows)
    {
        PixelImage image = new(rows[0].Length, rows.Length);
        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                image[x, y] = rows[y][x] switch
                {
                    '.' => default,
                    '#' => new Rgba32(49, 40, 62, 255),
                    '-' => new Rgba32(111, 87, 126, 255),
                    '+' => new Rgba32(173, 144, 187, 255),
                    '*' => new Rgba32(234, 219, 242, 255),
                    _ => throw new InvalidDataException("Unknown status symbol palette entry.")
                };
            }
        }
        return image;
    }
}
