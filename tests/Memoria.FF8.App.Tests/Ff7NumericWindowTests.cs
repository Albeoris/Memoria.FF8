using System.Text.Json;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.Rendering;
using Microsoft.Xna.Framework;

namespace Memoria.FF8.App.Tests;

public sealed class Ff7NumericWindowTests
{
    [Fact]
    public void SignedDivisionsRetainLeadingZeroTruncationOverflowAndRawAtlasCoordinates()
    {
        IReadOnlyList<Ff7SpecialNumberGlyph> two = Layout(12345, 2);
        Assert.Equal(new[] { (224, 160), (96, 208) }, two.Select(glyph => (glyph.SourceX, glyph.SourceY)));
        Assert.Equal(new[] { (96, 160), (96, 208) }, Layout(5, 2).Select(glyph => (glyph.SourceX, glyph.SourceY)));
        Assert.All(Layout(0, 6), glyph => Assert.Equal((96, 160), (glyph.SourceX, glyph.SourceY)));
        Assert.Equal(6, Layout(0, 6).Count);
        Assert.Equal(new[] { (224, 208), (160, 208), (224, 160), (192, 208), (192, 160), (128, 208), (224, 160), (160, 208) }, Layout(int.MaxValue, 8).Select(glyph => (glyph.SourceX, glyph.SourceY)));
        Assert.Equal((96, 208), (Layout(100000000, 8)[0].SourceX, Layout(100000000, 8)[0].SourceY));
        Assert.Equal(new[] { -32, 32, -32, 0, 0, 64, -32, 0 }, Layout(int.MinValue, 8).Select(glyph => glyph.SourceX));
        Assert.All(Layout(int.MinValue, 8), glyph => Assert.Equal(160, glyph.SourceY));
        Assert.Equal(new[] { 96, 64 }, Layout(-1, 2).Select(glyph => glyph.SourceX));
        Assert.Empty(Layout(123, 0));
        Assert.Empty(Layout(123, 128));
        Assert.Empty(Layout(123, 255));
        Assert.Equal(8, Layout(123, 9).Count);
        Assert.Equal(8, Layout(123, 127).Count);
    }

    [Fact]
    public void NumberPlacementUsesSpecialMarginsWithoutDialoguePaddingOrDrawDrivenChanges()
    {
        Ff7Window window = new(2, 10, 20, 160, 70)
        {
            SpecialType = 2,
            SpecialX = 3,
            SpecialY = 4,
            NativeNumber = 5,
            NativeNumberDigits = 2
        };
        string saved = JsonSerializer.Serialize(window);
        IReadOnlyList<Ff7SpecialNumberGlyph> first = Ff7NumberWindowPresentation.Create(window);
        Assert.Equal(new[] { 13, 29 }, first.Select(glyph => glyph.X));
        Assert.All(first, glyph => Assert.Equal(24, glyph.Y));
        Assert.Equal(first, Ff7NumberWindowPresentation.Create(window));
        Assert.Equal(first, Ff7NumberWindowPresentation.Create(JsonSerializer.Deserialize<Ff7Window>(saved)!));
        Assert.Equal(first, Ff7NumberWindowPresentation.Create(window with { Style = 2, Flags = 255, Rows = 3 }));
        Assert.Equal(saved, JsonSerializer.Serialize(window));
        Assert.Equal(6, new Ff7Window(0, 0, 0, 1, 1).NativeNumberDigits);
    }

    [Fact]
    public void OriginalFieldAtlasUsesMaskedOpaqueRgbAndNativeWrappedNegativeColumns()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(root), "Select original FFVII resources for the native numeric-window atlas.");
        Ff7Installation installation = Ff7Installation.Discover(root!);
        var atlas = Ff7MenuResources.Load(installation).GetRequiredTexture("btl_win_b_l.tex");
        Assert.Equal(256, atlas.Width);
        Assert.Equal(256, atlas.Height);
        Assert.Equal(0U, atlas.RenderInfo.PaletteIndex);
        byte[] original = atlas.Pixels.ToArray();
        var decoded = Ff7FieldRenderer.DecodeTexture(atlas, useColorKey: true);
        Color[] colors = Ff7NumberWindowPresentation.AtlasColors(atlas);
        int transparent = Array.FindIndex(decoded.Pixels, pixel => pixel.A == 0);
        int marked = Array.FindIndex(decoded.Pixels, pixel => pixel.A == 254);
        Assert.True(transparent >= 0 && marked >= 0);
        Assert.Equal(Color.Transparent, colors[transparent]);
        Assert.Equal(new Color(decoded.Pixels[marked].R, decoded.Pixels[marked].G, decoded.Pixels[marked].B, (byte)255), colors[marked]);
        Assert.All(colors, color => Assert.True(color.A is 0 or 255));
        Assert.Equal(original, atlas.Pixels.ToArray());
        Ff7SpecialNumberGlyph negative = Layout(int.MinValue, 8)[0];
        Assert.Equal(-32, negative.SourceX);
        Assert.Equal(224, (negative.SourceX + atlas.Width) % atlas.Width);
        Assert.Equal(160, negative.SourceY);
    }

    private static IReadOnlyList<Ff7SpecialNumberGlyph> Layout(int value, byte digits)
    {
        return Ff7NumberWindowPresentation.Create(new(0, 0, 0, 160, 70) { SpecialType = 2, NativeNumber = value, NativeNumberDigits = digits });
    }
}
