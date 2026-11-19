using System.Diagnostics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Assets.Localization;
using Memoria.FF8.Mods;
using Memoria.FF8.Resources;
using Memoria.FF8.Resources.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class InstalledFontTexturePublicationTests
{
    [Fact]
    public void HdInstalledFontPublishesPreparedPixelsWithoutChangingGlyphMetrics()
    {
        using IResourceSource source = GameDataSourceFactory.Open(TestResources.GameDirectory);
        ResourceService resources = new(source);
        using GpuTestDevice graphics = new();
        using SpriteBatch batch = new(graphics.Device);
        using NativeFontPresentation font = new(graphics.Device);
        Stopwatch clock = Stopwatch.StartNew();
        while (!font.Ready && clock.Elapsed < TimeSpan.FromSeconds(8))
        {
            font.Prepare(resources, [], "Auto", "en");
            Thread.Sleep(2);
        }
        Assert.True(font.Ready, font.Diagnostic);
        NativeTextContext context = new(source.Descriptor.Edition.ToString(), "Fields", "synthetic", "en");
        const string text = "AB12";
        float advance = font.Measure(text, context);
        Assert.True(advance > 0, font.Diagnostic);
        using RenderTarget2D target = new(graphics.Device, 128, 48);
        Color[] Draw()
        {
            font.Prepare(resources, [], "Auto", "en");
            graphics.Device.SetRenderTarget(target);
            graphics.Device.Clear(Color.Black);
            batch.Begin(samplerState: SamplerState.PointClamp);
            Assert.True(font.Draw(batch, text, new(8, 8), 1, Color.White, context, new(0, 0, 128, 48)), font.Diagnostic);
            batch.End();
            graphics.Device.SetRenderTarget(null);
            return GpuTestDevice.Read(target);
        }
        Color[] before = Draw();
        TextureSourceSnapshot snapshot = Assert.Single(resources.CaptureTextureSourcesForImages(font.PresentedTextureImages));
        Assert.Equal("menu/sysfnt.tex", snapshot.Target.Owner);
        PixelImage painted = new(snapshot.Original.Width * 2, snapshot.Original.Height * 2, Enumerable.Repeat(new Rgba32(255, 0, 255), snapshot.Original.Width * snapshot.Original.Height * 4).ToArray());
        byte[] png = LocalizationArtworkExporter.EncodePng(painted);
        ModFile file = new("Memoria/font-texture", ModFormat.Memoria, snapshot.Target.CanonicalPath(), 1, () => png);
        Assert.True(NativeTextureMods.TryPrepare(new(1, [], [file]), out NativeTextureMods native, out var diagnostics, cancellationToken: TestContext.Current.CancellationToken), string.Join("; ", diagnostics.Select(item => item.Message)));
        ResourceService.PreparedResources candidate = resources.Prepare(native);
        using (TexturePublication discarded = Assert.IsType<TexturePublication>(font.PrepareTexturePublication(candidate.TextureValues)))
        {
            Assert.Equal(before, Draw());
        }
        Assert.Equal(before, Draw());
        using TexturePublication publication = Assert.IsType<TexturePublication>(font.PrepareTexturePublication(candidate.TextureValues));
        candidate.Commit(beforePublish: publication.Commit);
        Assert.Equal(advance, font.Measure(text, context));
        Assert.Contains(Draw().Zip(before), pair => pair.First != pair.Second);
        Assert.Equal(painted.Width, Assert.Single(font.PresentedTextureImages).Width);
    }
}
