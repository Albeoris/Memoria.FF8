using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Assets.Localization;
using Memoria.FF8.Mods;
using Memoria.FF8.Rendering;
using Memoria.FF8.Resources;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class InstalledTexturePublicationTests
{
    [Fact]
    public void NativeFieldPngRoundTripPublishesThroughActualBackgroundAtlasesWithoutChangingRenderedPixels()
    {
        using IResourceSource source = GameDataSourceFactory.Open(TestResources.GameDirectory);
        ResourceService resources = new(source);
        using GameAssets assets = new(TestResources.GameDirectory, resources: resources);
        FieldAssets field = assets.LoadField(229);
        FieldRenderer renderer = new(field, 640, 480);
        Dictionary<int, int> states = field.Background.Where(tile => tile.AnimationId != 255).GroupBy(tile => tile.AnimationId).ToDictionary(group => group.Key, group => (int)group.First().AnimationState);
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        renderer.TriangleRenderer = gpu;
        renderer.Render(states, [], TimeSpan.Zero);
        var expected = GpuTestDevice.Read(gpu.Target);
        var snapshots = resources.CaptureTextureSources(field.Background.Select(tile => tile.Texture!.TextureKey!.Value));
        List<ModFile> files = [];
        foreach (TextureSourceSnapshot snapshot in snapshots)
        {
            byte[] png = LocalizationArtworkExporter.EncodePng(TexturePixelExchange.ToEditable(snapshot.Original));
            files.Add(new("Memoria/roundtrip", ModFormat.Memoria, snapshot.Target.CanonicalPath(), 1, () => png));
        }
        Assert.True(NativeTextureMods.TryPrepare(new(1, [], files), out NativeTextureMods mods, out var diagnostics, cancellationToken: TestContext.Current.CancellationToken), string.Join("; ", diagnostics.Select(error => error.Message)));
        ResourceService.PreparedResources candidate = resources.Prepare(mods);
        PreparedFieldTextures background = Assert.IsType<PreparedFieldTextures>(renderer.PrepareTexturePublication(candidate.TextureValues, resources.Generation + 1));
        using TexturePublication graphics = gpu.PrepareTexturePublication(background.Images);
        renderer.Render(states, [], TimeSpan.Zero);
        Assert.Equal(expected, GpuTestDevice.Read(gpu.Target));
        candidate.Commit(beforePublish: () =>
        {
            graphics.Commit();
            background.Commit();
        });
        renderer.Render(states, [], TimeSpan.Zero);
        Assert.Equal(expected, GpuTestDevice.Read(gpu.Target));
        Assert.Equal(0, gpu.FrameTextureUploads);
        Assert.Equal(1, resources.Generation);
    }

    [Fact]
    public void HdFieldTileUsesNativeLogicalCoordinatesAndChangesTheActualGpuFrame()
    {
        using IResourceSource source = GameDataSourceFactory.Open(TestResources.GameDirectory);
        ResourceService resources = new(source);
        using GameAssets assets = new(TestResources.GameDirectory, resources: resources);
        FieldAssets field = assets.LoadField(229);
        FieldRenderer renderer = new(field, 640, 480);
        Dictionary<int, int> states = field.Background.Where(tile => tile.AnimationId != 255).GroupBy(tile => tile.AnimationId).ToDictionary(group => group.Key, group => (int)group.First().AnimationState);
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        renderer.TriangleRenderer = gpu;
        renderer.Render(states, [], TimeSpan.Zero);
        var before = GpuTestDevice.Read(gpu.Target);
        var originals = resources.CaptureTextureSources(field.Background.Select(tile => tile.Texture!.TextureKey!.Value));
        Dictionary<TextureKey, TextureReplacement> replacements = [];
        foreach (var snapshot in originals)
        {
            PixelImage hd = new(32, 32, Enumerable.Repeat(new Rgba32(255, 0, 255), 1024).ToArray());
            replacements.Add(snapshot.Context.Key, new(hd, "synthetic-hd", 16, 16));
        }
        ResourceService.PreparedResources candidate = resources.Prepare(new Resolver(replacements));
        PreparedFieldTextures background = Assert.IsType<PreparedFieldTextures>(renderer.PrepareTexturePublication(candidate.TextureValues, resources.Generation + 1));
        using TexturePublication graphics = gpu.PrepareTexturePublication(background.Images);
        candidate.Commit(beforePublish: () =>
        {
            graphics.Commit();
            background.Commit();
        });
        renderer.Render(states, [], TimeSpan.Zero);
        var after = GpuTestDevice.Read(gpu.Target);
        Assert.Contains(after.Zip(before), pair => pair.First != pair.Second);
        Assert.All(originals, snapshot =>
        {
            TextureValue value = resources.CaptureTextureValues([snapshot.Context.Key])[snapshot.Context.Key];
            Assert.Equal((16, 16, 32, 32), (value.LogicalWidth, value.LogicalHeight, value.Image.Width, value.Image.Height));
        });
        Assert.Equal(0, gpu.FrameTextureUploads);
    }

    private sealed class Resolver(IReadOnlyDictionary<TextureKey, TextureReplacement> replacements) : ITextureReplacementResolver
    {
        public TextureReplacement? Resolve(TextureBuildContext context) => replacements.GetValueOrDefault(context.Key);
    }
}
