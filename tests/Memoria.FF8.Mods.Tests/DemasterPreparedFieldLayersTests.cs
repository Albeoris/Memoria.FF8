using Albeoris.Games.FF8.Imaging;
using Albeoris.Games.FF8.Imaging.Map;
using Memoria.FF8.Assets;
using Memoria.FF8.Assets.Localization;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods.Tests;

public sealed class DemasterPreparedFieldLayersTests
{
    [Theory]
    [InlineData("textures/field_bg/sa/sample/sample_004FF000.png", true)]
    [InlineData("textures/field_bg/sa/sample/sample_en_004FF000.DDS", true)]
    [InlineData("textures/field_bg/sa/sample/sample_en_004FF000_A.dds", false)]
    [InlineData("textures/field_bg/sa/sample/sample_0.png", false)]
    [InlineData("textures/field_bg/sa/sample/sample_zz_004FF000.png", false)]
    [InlineData("textures/field_bg/xx/sample/sample_004FF000.png", false)]
    public void RecognizesOnlyPaintingAddresses(string path, bool isInput)
    {
        Assert.Equal(isInput, DemasterFieldLayers.IsInput(path));
    }

    [Fact]
    public void CropsPupuAnimationAndCoincidentTilesWithNativeCoverage()
    {
        PixelImage native = new(16, 16);
        native[0, 0] = new(0, 0, 0, 128);
        FieldAssets field = Field(
            new(-4, 0, 10, 1, 0, 2, MapBlendMode.Average, true, true, native),
            new(-4, 0, 11, 1, 0, 2, MapBlendMode.Average, true, true, native),
            new(16, 0, 12, 1, 1, 2, MapBlendMode.Average, true, true, native));
        ModFile generic = Painting("02001000", 72, 32, (_, _) => new(10, 0, 0, 200));
        ModFile localized = Painting("fr_02001000", 72, 32, (x, _) => new((byte)x, 20, 0, 200));
        ModFile coincident = Painting("02001001", 36, 16, (_, _) => new(30, 0, 0, 255));
        ModFile animation = Painting("02001010", 72, 32, (x, _) => new((byte)x, 40, 0, 255));
        List<ModDiagnostic> diagnostics = [];
        IReadOnlyDictionary<int, TextureReplacement> result = DemasterFieldLayers.Resolve(new(1, [], [generic, localized, coincident, animation]), field, "fr", diagnostics.Add);
        Assert.Empty(diagnostics);
        Assert.Equal(3, result.Count);
        Assert.Equal(new Rgba32(0, 20, 0, 100), result[0].Image[0, 0]);
        Assert.Equal(new Rgba32(2, 20, 0, 200), result[0].Image[2, 0]);
        Assert.Equal(new Rgba32(30, 0, 0, 128), result[1].Image[0, 0]);
        Assert.Equal(new Rgba32(40, 40, 0, 128), result[2].Image[0, 0]);
        Assert.Equal(32, result[2].Image.Width);
        Assert.Equal(16, result[2].LogicalWidth);
        Assert.Equal(1, field.Background[2].AnimationState);
    }

    [Fact]
    public void FallsBackToFmeShiftedCanvasAndReportsMalformedShapes()
    {
        PixelImage native = new(16, 16);
        FieldAssets field = Field(
            new(4, -16, 0, 255, 0, 3, MapBlendMode.None, false, true, native),
            new(16, 0, 0, 255, 0, 5, MapBlendMode.None, false, true, native));
        ModFile bad = Painting("034FF000", 17, 17, (_, _) => new(0, 0, 0, 255));
        ModFile fallback = Painting("834FF000", 44, 32, (x, y) => new((byte)x, (byte)y, 0, 255));
        List<ModDiagnostic> diagnostics = [];
        IReadOnlyDictionary<int, TextureReplacement> result = DemasterFieldLayers.Resolve(new(1, [], [bad, fallback]), field, null, diagnostics.Add);
        Assert.Equal("Demaster.FieldLayerDimensions", Assert.Single(diagnostics).Code);
        Assert.Single(result);
        Assert.Equal(new Rgba32(8, 0, 0, 255), result[0].Image[0, 0]);
        Assert.Equal(new Rgba32(23, 15, 0, 255), result[0].Image[15, 15]);
    }

    [Fact]
    public void ReportsRecognizedMaterialCompanionWithoutItsPainting()
    {
        FieldAssets field = Field(new BackgroundTile(0, 0, 0, 255, 0, 3, MapBlendMode.None, false, true, new(16, 16)));
        ModFile auxiliary = Painting("fr_034FF000_nt_A", 16, 16, (_, _) => new(0, 0, 0, 255));
        List<ModDiagnostic> diagnostics = [];
        Assert.Empty(DemasterFieldLayers.Resolve(new(1, [], [auxiliary]), field, "fr", diagnostics.Add));
        ModDiagnostic diagnostic = Assert.Single(diagnostics);
        Assert.Equal("Demaster.FieldLayerPaintingMissing", diagnostic.Code);
        Assert.Equal(auxiliary.RelativePath, diagnostic.RelativePath);
    }

    private static FieldAssets Field(params BackgroundTile[] tiles)
    {
        return new(0, "sample", [], [], tiles, null!, null!, []);
    }

    private static ModFile Painting(string id, int width, int height, Func<int, int, Rgba32> pixel)
    {
        PixelImage image = new(width, height);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                image[x, y] = pixel(x, y);
            }
        }
        return new("fixture", ModFormat.Demaster, $"textures/field_bg/sa/sample/sample_{id}.png", 1, () => LocalizationArtworkExporter.EncodePng(image));
    }
}
