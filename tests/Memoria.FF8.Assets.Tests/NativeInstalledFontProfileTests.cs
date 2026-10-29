using Albeoris.Games.FF8.Imaging;
using Albeoris.Games.FF8.Imaging.Tdw;
using Memoria.FF8.Resources;
using Memoria.FF8.Resources.Text;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeInstalledFontProfileTests(ITestOutputHelper output)
{
    private static readonly NativeTextContext Context = new("Classic", "Fields", DictionaryLanguage: "en");

    [Fact]
    public void OverflowUsesPaintedRectanglesWhileLineLeadingStillAdvancesTheNextLine()
    {
        NativeFontProfile profile = Create(Enumerable.Repeat((byte)7, 200).ToArray(), 256, 128);
        NativeFontLayoutResult single = NativeFontLayout.Layout(profile, "A", Context, maximumHeight: 12);
        Assert.True(single.Success);
        Assert.Equal(16, single.Height);
        Assert.Contains(NativeFontLayout.Layout(profile, "A", Context, maximumHeight: 11).Diagnostics, issue => issue.Code == "overflow" && issue.Position == 0);
        NativeFontLayoutResult multiple = NativeFontLayout.Layout(profile, "A{Line}A", Context, maximumHeight: 28);
        Assert.True(multiple.Success);
        Assert.Equal(16, multiple.Glyphs[1].Y);
        Assert.Equal(32, multiple.Height);
        Assert.Contains(NativeFontLayout.Layout(profile, "A{Line}A", Context, maximumHeight: 27).Diagnostics, issue => issue.Code == "overflow" && issue.Position == 7);
        NativeFontProfile bearing = profile with { Images = profile.Images.Select(image => image with { BearingY = 2 }).ToArray() };
        Assert.True(NativeFontLayout.Layout(bearing, "A", Context, maximumHeight: 14).Success);
        Assert.Contains(NativeFontLayout.Layout(bearing, "A", Context, maximumHeight: 13).Diagnostics, issue => issue.Code == "overflow");
    }

    [Fact]
    public void DeclaredMetricsBoundTheUsableFontWithoutRejectingItsSupportedGlyphs()
    {
        byte[] widths = Enumerable.Range(0, 200).Select(index => (byte)(index % 11)).ToArray();
        NativeFontProfile profile = Create(widths, 256, 128);
        Assert.Empty(NativeFontProfileValidation.Validate(profile));
        Assert.All(profile.Images, image => Assert.InRange(image.Index, 0, widths.Length - 1));
        NativeFontLayoutResult text = NativeFontLayout.Layout(profile, "A", Context);
        Assert.True(text.Success);
        NativeGlyphRun glyph = Assert.Single(text.Glyphs);
        Assert.Equal(widths[glyph.Image.Index], glyph.Image.Advance);
        Assert.Equal(glyph.Image.Index % 21 * 12, glyph.Image.X);
        Assert.Equal(glyph.Image.Index / 21 * 12, glyph.Image.Y);
        Assert.Contains(NativeFontLayout.Layout(profile, "A\u2c77", Context).Diagnostics, issue => issue.Code == "missing-glyph" && issue.Position == 1);
        Assert.False(NativeStrictCodec.Encode(profile, "\u2c77", Context).Success);
        Assert.DoesNotContain(profile.Codec, entry => entry.Hex.Length == 2 && Convert.ToInt32(entry.Hex, 16) >= 0xE8);
        Assert.True(NativeStrictCodec.Encode(profile, "{Char Squall}{Line}A{End}", Context).Success);
        NativeFontProfile oversized = Create(Enumerable.Repeat((byte)7, 512).ToArray(), 256, 256);
        Assert.DoesNotContain(oversized.Codec, entry => entry.Hex.Length == 2 && Convert.ToInt32(entry.Hex, 16) >= 0xE8);
        Assert.False(NativeFontLayout.Layout(oversized, "\u2c77", Context).Success);
    }

    [Fact]
    public void CompleteRectanglesBoundImagesWithoutChangingTheNativeColumnStride()
    {
        NativeFontProfile profile = Create(Enumerable.Repeat((byte)7, 512).ToArray(), 24, 25);
        Assert.Equal(new[] { 0, 1, 21, 22 }, profile.Images.Select(image => image.Index));
        Assert.All(profile.Images, image => Assert.True(image.X + image.Width <= 24 && image.Y + image.Height <= 25));
        Assert.Throws<InvalidDataException>(() => Create([], 256, 128));
        Assert.Throws<InvalidDataException>(() => Create([7, 7], 11, 12));
    }

    [Fact]
    public void OptionalInstalledSourceBuildsAUsableProfileFromItsDeclaredMetricsAndActualAtlas()
    {
        using IResourceSource source = GameDataSourceFactory.Open(TestResources.GameDirectory);
        ResourceService resources = new(source);
        string language = resources.ResourceLanguage;
        Assert.SkipWhen(language is "ja" or "jp", "Automatic Japanese font construction requires explicit page assignments.");
        byte[] tdw = resources.Read(new("main/sysfnt.tdw"));
        PixelImage atlas = NativeInstalledFontProfile.DecodeAtlas(resources.Read(new("menu/sysfnt.tex")), "font.tex");
        NativeTextContext context = new(resources.Source.Edition.ToString(), "Fields", DictionaryLanguage: language);
        NativeFontProfile profile = NativeInstalledFontProfile.CreateEuropean("installed", "Installed", language, context, "font.tex", atlas, tdw);
        output.WriteLine($"Declared widths: {TdwFile.Read(tdw).Widths.Length}; atlas: {atlas.Width}x{atlas.Height}; usable images: {profile.Images.Count}.");
        Assert.Empty(NativeFontProfileValidation.Validate(profile));
        Assert.NotEmpty(profile.Images);
        Assert.True(NativeFontLayout.Layout(profile, "Final Fantasy VIII", context).Success);
        Assert.True(NativeFontLayout.Layout(profile, "Squall", context, maximumHeight: 12).Success);
        Assert.True(NativeFontLayout.Layout(profile, "Lv 7", context, maximumHeight: 12).Success);
        Assert.True(NativeFontLayout.Layout(profile, "PLAY 00:42", context, maximumHeight: 12).Success);
        Assert.Contains(NativeFontLayout.Layout(profile, "Squall", context, maximumHeight: 11).Diagnostics, issue => issue.Code == "overflow");
        Assert.All(profile.Images, image => Assert.True(image.Index < TdwFile.Read(tdw).Widths.Length && image.X + image.Width <= atlas.Width && image.Y + image.Height <= atlas.Height));
    }

    private static NativeFontProfile Create(byte[] widths, int width, int height) => NativeInstalledFontProfile.CreateEuropean("synthetic", "Synthetic", "en", Context, "font.tex", new PixelImage(width, height), new TdwFile(widths, null).Write());
}
