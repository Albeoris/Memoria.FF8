using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

internal static class SavedStatusArtwork
{
    public static int Icon(uint mask) => mask switch
    {
        1 => 272,
        2 => 273,
        4 => 274,
        8 => 275,
        16 => 276,
        32 => 277,
        64 => 278,
        _ => throw new ArgumentOutOfRangeException(nameof(mask))
    };

    public static IReadOnlyList<UiImageSource> Images(uint statuses)
    {
        List<UiImageSource> images = [];
        for (uint mask = 1; mask <= 64; mask <<= 1)
        {
            if ((statuses & mask) != 0)
            {
                images.Add(new UiImageSource(UiImageKind.Icon, Icon(mask)));
            }
        }
        return images;
    }
}
