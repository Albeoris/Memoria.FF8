using SkiaSharp;

namespace Memoria.FF8;

public sealed class PortableTextSourceMapTests
{
    [Theory]
    [InlineData("one one\r\none", 4, "one|one|one")]
    [InlineData("  repeated   repeated  ", 6, " |repeat|ed  |repeat|ed  ")]
    [InlineData("e\u0301😀日本語", 2, "e\u0301|😀|日本|語")]
    [InlineData("a\n\n", 1, "a||")]
    public void ProductionWrappingRetainsOriginalPositionsWithoutMatchingRepeatedWords(string text, int width, string expected)
    {
        List<PortableTextWrappedLine> mapped = PortableTextRasterizer.WrapMapped(text, value => value.Length, width);
        Assert.Equal(PortableTextRasterizer.Wrap(text, value => value.Length, width), mapped.Select(line => line.Text));
        Assert.Equal(expected, string.Join('|', mapped.Select(line => line.Text)));
        foreach (PortableTextWrappedLine line in mapped)
        {
            Assert.Equal(line.Text.Length, line.SourcePositions.Count);
            Assert.Equal(line.Text, string.Concat(line.SourcePositions.Select(index => text[index])));
            Assert.True(line.SourcePositions.SequenceEqual(line.SourcePositions.Order()));
        }
        Assert.Equal(mapped.Sum(line => line.SourcePositions.Count), mapped.SelectMany(line => line.SourcePositions).Distinct().Count());
    }

    [Theory]
    [InlineData("AVATAR ffi e\u0301\r\nSame Same", false)]
    [InlineData("Latin سلام 123 عالم", false)]
    [InlineData("日本語 😀 e\u0301", true)]
    public void SourceMetadataDoesNotChangeProductionRasterDimensionsOrPixels(string text, bool preserveLineMetrics)
    {
        using SKBitmap production = PortableTextRasterizer.Rasterize(text, 24, 110, "Arial", preserveLineMetrics);
        PortableTextRasterResult mapped = PortableTextRasterizer.RasterizeMapped(text, 24, 110, "Arial", preserveLineMetrics);
        using (mapped.Bitmap)
        {
            Assert.Equal(production.Width, mapped.Bitmap.Width);
            Assert.Equal(production.Height, mapped.Bitmap.Height);
            Assert.Equal(PortableRaster.Rgba(production), PortableRaster.Rgba(mapped.Bitmap));
            Assert.NotEmpty(mapped.Clusters);
            Assert.All(mapped.Clusters, cluster => Assert.All(cluster.SourcePositions, position => Assert.InRange(position, 0, text.Length - 1)));
        }
    }

    [Fact]
    public void RightToLeftClustersUseVisualCoordinatesWhileKeepingLogicalSourcePositions()
    {
        using PortableTextShaper shaper = new(32, "Arial");
        IReadOnlyList<PortableShapedCluster> clusters = shaper.SourceClusters("שלום");
        Assert.NotEmpty(clusters);
        PortableShapedCluster first = clusters.First(cluster => cluster.Start == 0);
        PortableShapedCluster last = clusters.First(cluster => cluster.Start == 3);
        Assert.True(first.Bounds.Left > last.Bounds.Left);
        Assert.All(clusters, cluster => Assert.InRange(cluster.Start + cluster.Length, 1, 4));
    }

    [Fact]
    public void LigatureClustersRetainTheirWholeLogicalSourceSpan()
    {
        using PortableTextShaper shaper = new(32, "Arial");
        IReadOnlyList<PortableShapedCluster> clusters = shaper.SourceClusters("سلام");
        Assert.Contains(clusters, cluster => cluster.Length > 1);
        Assert.All(clusters, cluster => Assert.True(cluster.Start >= 0 && cluster.Start + cluster.Length <= 4));
    }
}
