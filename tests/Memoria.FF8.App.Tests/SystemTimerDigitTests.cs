using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.GameUi;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class SystemTimerDigitTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    public async Task Every_digit_has_transparent_padding_bright_fill_and_dark_outline(int digit)
    {
        await SystemFontCatalog.PrepareAsync(TestContext.Current.CancellationToken);
        PixelImage image = SystemTimerDigitArtwork.Render(digit);

        Assert.Equal(256, image.Width);
        Assert.Equal(512, image.Height);
        Assert.Contains(image.Pixels, pixel => pixel.A == 0);
        Assert.True(image.Pixels.Count(pixel => pixel.A == 255 && pixel.R > 120 && pixel.G > 120 && pixel.B > 120) > 100);
        Assert.True(image.Pixels.Count(pixel => pixel.A == 255 && pixel.R < 20 && pixel.G < 20 && pixel.B < 20) > 100);
        for (int x = 0; x < image.Width; x++)
        {
            Assert.Equal(0, image[x, 0].A);
            Assert.Equal(0, image[x, image.Height - 1].A);
        }
        for (int y = 0; y < image.Height; y++)
        {
            Assert.Equal(0, image[0, y].A);
            Assert.Equal(0, image[image.Width - 1, y].A);
        }
    }

    [Fact]
    public async Task Provider_reuses_neutral_digits_and_dims_rgb_without_changing_coverage()
    {
        await SystemFontCatalog.PrepareAsync(TestContext.Current.CancellationToken);
        MenuArtworkProvider provider = new(string.Empty);
        for (int digit = 0; digit < 10; digit++)
        {
            UiImageSource source = new(UiImageKind.SystemTimerDigit, digit);
            PixelImage neutral = provider.Read(source);
            Assert.Same(neutral, provider.Read(source));
            PixelImage dimmed = provider.Read(source with { RgbModulation = 64 });
            Assert.Equal(neutral.Width, dimmed.Width);
            Assert.Equal(neutral.Height, dimmed.Height);
            for (int index = 0; index < neutral.Pixels.Length; index++)
            {
                Rgba32 original = neutral.Pixels[index];
                Rgba32 dim = dimmed.Pixels[index];
                Assert.Equal(original.A, dim.A);
                Assert.Equal(original.R / 2, dim.R);
                Assert.Equal(original.G / 2, dim.G);
                Assert.Equal(original.B / 2, dim.B);
            }
            Assert.Same(neutral, provider.Read(source));
        }
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(10)]
    public async Task Unsupported_digit_ids_are_rejected(int digit)
    {
        await SystemFontCatalog.PrepareAsync(TestContext.Current.CancellationToken);
        Assert.Throws<ArgumentOutOfRangeException>(() => SystemTimerDigitArtwork.Render(digit));
        MenuArtworkProvider provider = new(string.Empty);
        Assert.Throws<ArgumentOutOfRangeException>(() => provider.Read(new UiImageSource(UiImageKind.SystemTimerDigit, digit)));
    }
}
