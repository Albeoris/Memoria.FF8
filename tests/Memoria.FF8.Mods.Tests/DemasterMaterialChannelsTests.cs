using System.Numerics;
using System.Text;
using Albeoris.Games.FF8.Imaging;
using Albeoris.Games.FF8.Imaging.Map;
using Memoria.FF8.Assets;
using Memoria.FF8.Assets.Localization;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods.Tests;

public sealed class DemasterMaterialChannelsTests
{
    [Fact]
    public void PreparedFieldUsesSeparateCoverageSteadyDepthAndEmissionChannels()
    {
        PreparedFieldMaterial field = DemasterMaterialChannels.LoadField(
            Image("_A", new(128, 128, 128, 90)),
            Image("_LF", new(128, 128, 128, 40)), 2,
            Image("_ND", new(128, 255, 128, 121)),
            Image("_ORME", new(12, 34, 56, 78)),
            Image("_EC", new(128, 0, 0, 64)));
        PreparedFieldTexel texel = field.ReadTexel(0, 0);
        Assert.Equal(new Rgba32(86, 86, 86, 90), field.BakePaintedLight()[0, 0]);
        Assert.Equal(90 / 255f, texel.Coverage);
        Assert.Equal(40 / 255f, texel.SteadyLight);
        Assert.Equal(121 / 255f, texel.Depth);
        Assert.InRange(texel.ViewNormal.Y, -1, -0.99f);
        Assert.Equal(new PreparedSurfaceTexel(12 / 255f, 34 / 255f, 56 / 255f), texel.Surface);
        Assert.Equal(0, texel.EmissiveMask);
        Vector3 glow = texel.CompositeGlow(Vector3.One, 0.5f, 1);
        Assert.Equal(1 - 32 / 255f, glow.Y, 5);
        Assert.Equal(0.982437f, glow.X, 5);

        PreparedFieldMaterial masked = DemasterMaterialChannels.LoadField(Image("_A", new(255, 255, 255, 255)), Image("_LF", new(255, 255, 255, 255)), 1, surface: Image("_ORME", new(12, 34, 56, 78)));
        Assert.Equal(78 / 255f, masked.ReadTexel(0, 0).EmissiveMask);
    }

    [Fact]
    public void PreparedSceneUsesHeightAndIgnoresAdditiveGlowAlpha()
    {
        ModFile surface = Image("_ORMH", new(12, 34, 56, 78));
        ModFile glow = Image("_E", new(255, 0, 0, 254));
        PreparedSceneTexel scene = DemasterMaterialChannels.LoadScene(surface, null, glow).ReadTexel(0, 0);
        Assert.Equal(78 / 255f, scene.Height);
        Assert.Equal(new PreparedSurfaceTexel(12 / 255f, 34 / 255f, 56 / 255f), scene.Surface);
        Assert.Equal(new Vector3(1.5f, 1, 1), scene.Glow.Composite(Vector3.One, 0.5f, 1));

        PreparedSceneTexel covered = DemasterMaterialChannels.LoadScene(surface, glowPicture: Image("_EC", new(255, 0, 0, 255)), additiveGlow: glow).ReadTexel(0, 0);
        Assert.Equal(Vector3.UnitX, covered.Glow.Composite(Vector3.One, 1, 1));
    }

    [Fact]
    public void OrdinaryOpaqueTileReplacementBakesItsPreparedLayerAndMetadata()
    {
        const string folder = "textures/field_bg/sa/sample/";
        PixelImage native = new(16, 16);
        FieldAssets field = new(0, "sample", [], [], [new BackgroundTile(0, 0, 0, 255, 0, 3, MapBlendMode.None, false, true, native)], null!, null!, []);
        ModFile painting = Image("", new(10, 20, 30, 255), 16, 16);
        ModFile albedo = Image("_A", new(128, 128, 128, 90), 16, 16);
        ModFile lightmap = Image("_LF", new(128, 128, 128, 40), 16, 16);
        ModFile metadata = new("fixture", ModFormat.Demaster, folder + "sample_pbr.txt", 5, () => Encoding.UTF8.GetBytes("lightmap_scale = 1; base\nlightmap_scale.034FF000 = 2 # layer"));
        List<ModDiagnostic> diagnostics = [];
        ModCatalogSnapshot catalog = new(1, [], [painting, albedo, lightmap, metadata]);
        TextureReplacement result = Assert.Single(DemasterFieldLayers.Resolve(catalog, field, "en", diagnostics.Add)).Value;
        Assert.Empty(diagnostics);
        Assert.Equal(new Rgba32(86, 86, 86, 90), result.Image[0, 0]);
        Assert.Contains("_LF.png", result.Provenance);

        ModFile invalidLightmap = Image("_LF", new(128, 128, 128, 40));
        ModCatalogSnapshot invalid = new(2, [], [painting, albedo, invalidLightmap, metadata]);
        TextureReplacement original = Assert.Single(DemasterFieldLayers.Resolve(invalid, field, "en", diagnostics.Add)).Value;
        Assert.Equal(new Rgba32(10, 20, 30, 255), original.Image[0, 0]);
        Assert.Equal("Demaster.FieldMaterialImage", Assert.Single(diagnostics).Code);
    }

    private static ModFile Image(string suffix, Rgba32 color, int width = 1, int height = 1)
    {
        PixelImage image = new(width, height, Enumerable.Repeat(color, width * height).ToArray());
        return new("fixture", ModFormat.Demaster, $"textures/field_bg/sa/sample/sample_034FF000{suffix}.png", 1, () => LocalizationArtworkExporter.EncodePng(image));
    }
}
