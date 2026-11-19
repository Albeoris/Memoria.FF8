using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;

namespace Memoria.FF8.App.Tests;

public sealed class NativeUiPaperColorTests
{
    [Fact]
    public void Authored_paper_is_a_mean_rgb_palette_instead_of_a_pattern_or_alpha_texture()
    {
        PixelImage first = new(2, 1, [new Rgba32(0, 40, 80, 0), new Rgba32(200, 80, 40, 128)]);
        PixelImage reordered = new(1, 2, [new Rgba32(200, 80, 40, 255), new Rgba32(0, 40, 80, 255)]);
        Assert.Equal(new Rgba32(100, 60, 60, 255), NativeMagazineArtwork.GetPaperColor(first));
        Assert.Equal(NativeMagazineArtwork.GetPaperColor(first), NativeMagazineArtwork.GetPaperColor(reordered));
    }
}
