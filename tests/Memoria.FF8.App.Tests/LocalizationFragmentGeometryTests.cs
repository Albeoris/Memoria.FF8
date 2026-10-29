using Memoria.FF8.GameUi;
using Memoria.FF8.Resources.Localization;
using Memoria.FF8.Resources.Text;
using Memoria.FF8.Ui;
using Microsoft.Xna.Framework;

namespace Memoria.FF8.App.Tests;

public sealed class LocalizationFragmentGeometryTests
{
    [Theory]
    [InlineData(1f)]
    [InlineData(1.25f)]
    [InlineData(1.75f)]
    public void RasterHitClippingUsesProductionHalfPixelEdgesAtFractionalDpi(float dpi)
    {
        Vector2 position = new(13.3f * dpi, 7.7f * dpi);
        Rectangle clip = new(25, 15, 70, 30);
        UiRect visible = LocalizationFragmentGeometry.SystemRasterClip(position, 240, 90, clip);
        Assert.True(visible.X >= clip.Left && visible.Y >= clip.Top);
        Assert.True(visible.Right <= clip.Right && visible.Bottom <= clip.Bottom);
        Assert.Equal(MathF.Ceiling((clip.Left - position.X) * SystemTextLayout.RasterScale), (visible.X - position.X) * SystemTextLayout.RasterScale, 3);
        Assert.Equal(MathF.Ceiling((clip.Top - position.Y) * SystemTextLayout.RasterScale), (visible.Y - position.Y) * SystemTextLayout.RasterScale, 3);
    }

    [Fact]
    public void IdenticalWordsKeepTheirOwnSourceKeysAcrossFiniteWrappingAndClipping()
    {
        WithTracing(() =>
        {
            string text = LocalizationTextComposition.Concat(Bound("one", "Same"), " / ", Bound("two", "Same"));
            PortableTextRasterResult raster = SystemTextLayout.RasterizeMapped(text, 24, 65, "Arial");
            using (raster.Bitmap)
            {
                LocalizationTextBinding binding = LocalizationTextTrace.Find(text)!;
                IReadOnlyList<LocalizationTextHit> hits = LocalizationFragmentGeometry.Map(binding, raster.Clusters, Vector2.Zero, 1, new(0, 0, 200, 200));
                Assert.Equal(new[] { "one", "two" }, hits.Select(hit => hit.Binding.Key).Distinct().Order());
                Assert.True(hits.Where(hit => hit.Binding.Key == "two").Min(hit => hit.Bounds.Y) > hits.Where(hit => hit.Binding.Key == "one").Max(hit => hit.Bounds.Y));
                float boundary = hits.Where(hit => hit.Binding.Key == "two").Min(hit => hit.Bounds.Y);
                IReadOnlyList<LocalizationTextHit> clipped = LocalizationFragmentGeometry.Map(binding, raster.Clusters, Vector2.Zero, 1, new(0, 0, 200, boundary));
                Assert.DoesNotContain(clipped, hit => hit.Binding.Key == "two");
            }
        });
    }

    [Fact]
    public void RightToLeftLeafOrderFollowsTheShapedGlyphPositions()
    {
        WithTracing(() =>
        {
            string text = LocalizationTextComposition.Concat(Bound("logical-first", "אב"), Bound("logical-second", "גד"));
            PortableTextRasterResult raster = SystemTextLayout.RasterizeMapped(text, 32, 200, "Arial");
            using (raster.Bitmap)
            {
                IReadOnlyList<LocalizationTextHit> hits = LocalizationFragmentGeometry.Map(LocalizationTextTrace.Find(text)!, raster.Clusters, Vector2.Zero, 1, new(0, 0, 200, 100));
                UiRect first = Assert.Single(hits, hit => hit.Binding.Key == "logical-first").Bounds;
                UiRect second = Assert.Single(hits, hit => hit.Binding.Key == "logical-second").Bounds;
                Assert.True(first.X > second.X);
            }
        });
    }

    [Fact]
    public void LigatureCrossingTwoCanonicalLeavesReturnsBothOwnersWithoutGuessingASplit()
    {
        WithTracing(() =>
        {
            string text = LocalizationTextComposition.Concat(Bound("lam", "ل"), Bound("alef", "ا"));
            PortableTextRasterResult raster = SystemTextLayout.RasterizeMapped(text, 32, 200, "Arial");
            using (raster.Bitmap)
            {
                IReadOnlyList<LocalizationTextHit> hits = LocalizationFragmentGeometry.Map(LocalizationTextTrace.Find(text)!, raster.Clusters, Vector2.Zero, 1, new(0, 0, 200, 100));
                Assert.Equal(2, hits.Count);
                Assert.Equal(new[] { "alef", "lam" }, hits.Select(hit => hit.Binding.Key).Order());
                Assert.Equal(hits[0].Bounds, hits[1].Bounds);
            }
        });
    }

    [Fact]
    public void FormattedParentOwnsOnlyItsLiteralSpansAndTheArgumentOwnsItsOwnGlyphs()
    {
        WithTracing(() =>
        {
            string text = LocalizationTextComposition.Format(Bound("parent", "Prefix {0} tail"), Bound("argument", "Same"));
            PortableTextRasterResult raster = SystemTextLayout.RasterizeMapped(text, 20, 400, "Arial");
            using (raster.Bitmap)
            {
                IReadOnlyList<LocalizationTextHit> hits = LocalizationFragmentGeometry.Map(LocalizationTextTrace.Find(text)!, raster.Clusters, Vector2.Zero, 1, new(0, 0, 400, 100));
                Assert.Equal(2, hits.Count(hit => hit.Binding.Key == "parent"));
                UiRect argument = Assert.Single(hits, hit => hit.Binding.Key == "argument").Bounds;
                Assert.DoesNotContain(hits.Where(hit => hit.Binding.Key == "parent"), hit => hit.Bounds.Intersect(argument).Width > 1);
            }
        });
    }

    [Fact]
    public void PhysicalScaleAndClipUseTheSameNativeGlyphRasterExtentsAsDrawing()
    {
        WithTracing(() =>
        {
            string text = LocalizationTextComposition.Concat(Bound("first", "A"), Bound("second", "B"));
            NativeFontProfile profile = new() { Id = "synthetic", DisplayName = "Synthetic", Atlases = [new("atlas", "atlas.png", 24, 12)], Images = [new(7, "atlas", 0, 0, 12, 12, 7), new(8, "atlas", 12, 0, 12, 12, 7)], Glyphs = [new("A", 7), new("B", 8)] };
            NativeFontLayoutResult layout = NativeFontLayout.Layout(profile, text, new("Classic", "Fields"));
            Rectangle clip = new(20, 0, 20, 30);
            IReadOnlyList<PortableTextCluster> clusters = LocalizationFragmentGeometry.ClippedNativeClusters(layout, new(13.2f, 7.8f), 1.25f, clip);
            IReadOnlyList<LocalizationTextHit> hits = LocalizationFragmentGeometry.Map(LocalizationTextTrace.Find(text)!, clusters, Vector2.Zero, 1, new(clip.X, clip.Y, clip.Width, clip.Height));
            Assert.Equal(2, hits.Count);
            Assert.All(hits, hit =>
            {
                Assert.InRange(hit.Bounds.X, clip.Left, clip.Right);
                Assert.InRange(hit.Bounds.Right, clip.Left, clip.Right);
                Assert.InRange(hit.Bounds.Y, clip.Top, clip.Bottom);
                Assert.InRange(hit.Bounds.Bottom, clip.Top, clip.Bottom);
            });
            Assert.Empty(LocalizationFragmentGeometry.Map(LocalizationTextTrace.Find(text)!, clusters, Vector2.Zero, 1, new(100, 100, 10, 10)));
        });
    }

    private static string Bound(string key, string text) => LocalizationTextTrace.Attach(text, new(key, text, text, text));

    private static void WithTracing(Action test)
    {
        bool previous = LocalizationTextTrace.Enabled;
        LocalizationTextTrace.Enabled = true;
        try
        {
            test();
        }
        finally
        {
            LocalizationTextTrace.Enabled = previous;
        }
    }
}
