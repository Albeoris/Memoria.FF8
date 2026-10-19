using System.Collections.Immutable;
using System.Numerics;
using System.Text.Json;
using Albeoris.Games.FF8.Imaging;
using Albeoris.Games.FF8.Imaging.Map;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;
using Color = Microsoft.Xna.Framework.Color;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class FieldWidescreenTests
{
    private static readonly Rgba32 Ground = new(64, 128, 192, 255);
    private static readonly Rgba32 Parallax = new(192, 64, 128, 255);
    private static readonly Rgba32 Black = new(0, 0, 0, 255);

    [Theory]
    [InlineData(1280, 720, 2048, 1280, 720)]
    [InlineData(3840, 2160, 2048, 2048, 1152)]
    [InlineData(3840, 2160, 4096, 3840, 2160)]
    [InlineData(720, 1280, 2048, 720, 540)]
    [InlineData(640, 480, 2048, 640, 480)]
    public void FieldTargetUsesWideAspectAndFitsNativeAspectInPortrait(int width, int height, int limit, int expectedWidth, int expectedHeight)
    {
        Assert.Equal(new PixelBounds(0, 0, expectedWidth, expectedHeight), ViewportLayout.FieldRenderTarget(width, height, limit));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(160)]
    [InlineData(-160)]
    public void CameraBoundsExposeContinuationAndMaskParallaxBeyondImageEdges(int center)
    {
        List<BackgroundTile> tiles = Tiles(-320, 320, 0, Ground);
        tiles.AddRange(Tiles(-1024, 1024, 1, Parallax, depth: 200));
        FieldAssets field = Field(tiles, new FieldBounds(-112, 112, 320, -320));
        FieldRenderer renderer = new(field, 640, 240);
        Vector2 actorOffset = new(-center, 0);
        Dictionary<int, Vector2> offsets = new() { [0] = actorOffset, [1] = Vector2.Zero };
        byte[] actual = renderer.Render(new Dictionary<int, int>(), [], TimeSpan.Zero, backgroundScrollOffsets: offsets, actorScrollOffset: actorOffset);
        for (int x = 0; x < 640; x++)
        {
            AssertPixel(actual, 640, x, 120, Ground);
        }
        AssertGpuMatches(field, 640, 240, actual, offsets, actorOffset);
    }

    [Fact]
    public void ResizedFractionalTargetsClipAtImageEdgesWithoutStretching()
    {
        FieldAssets field = Field(Tiles(-240, 240, 0, Ground), new FieldBounds(-112, 112, 240, -240));
        foreach ((int width, int height) in new[] { (1280, 720), (1915, 969), (641, 361), (3440, 1440), (720, 1280) })
        {
            FieldRenderer renderer = new(field, width, height);
            byte[] pixels = renderer.Render(new Dictionary<int, int>(), [], TimeSpan.Zero);
            float scale = Math.Min(width / 320f, height / 240f);
            float origin = (width - 320 * scale) / 2 + 160 * scale;
            for (int x = 0; x < width; x++)
            {
                bool covered = x + 0.5f >= origin - 240 * scale && x + 0.5f < origin + 240 * scale;
                AssertPixel(pixels, width, x, height / 2, covered ? Ground : Black);
            }
            AssertGpuMatches(field, width, height, pixels);
        }
    }

    [Fact]
    public void FixedImageBoundsKeepSidesBlackEvenWhenParallaxHasMorePixels()
    {
        List<BackgroundTile> tiles = Tiles(-160, 160, 0, Ground);
        tiles.AddRange(Tiles(-1024, 1024, 1, Parallax, depth: 200));
        FieldAssets field = Field(tiles, new FieldBounds(-112, 112, 160, -160));
        byte[] actual = new FieldRenderer(field, 640, 240).Render(new Dictionary<int, int>(), [], TimeSpan.Zero);
        AssertPixel(actual, 640, 159, 120, Black);
        AssertPixel(actual, 640, 160, 120, Ground);
        AssertPixel(actual, 640, 479, 120, Ground);
        AssertPixel(actual, 640, 480, 120, Black);
        AssertGpuMatches(field, 640, 240, actual);
    }

    [Fact]
    public void InstalledFieldImagesRetainTheNativeCenterAcrossWideAndPortraitTargets()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        List<object> metadata = [];
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_WIDESCREEN_OUTPUT");
        foreach (int fieldId in new[] { 323, 229, 264, 821, 130 })
        {
            FieldAssets field = assets.LoadField(fieldId);
            FieldBounds bounds = field.Information!.CameraBounds[0];
            int centerX = (bounds.Left + bounds.Right) / 2;
            int centerY = (bounds.Top + bounds.Bottom) / 2;
            Vector2 scroll = new(-centerX, -centerY);
            Dictionary<int, Vector2> offsets = field.Background.Select(tile => tile.Layer).Distinct().ToDictionary(layer => layer, _ => scroll);
            FieldNativeBackgroundWrapFrame? frame = field.Information.BackgroundWrapMask == 0 ? null : FieldNativeNormalBackgroundWrap.Prepare(field, 0, new FieldNativeCameraCenter(centerX, centerY));
            Assert.True(field.Information.BackgroundWrapMask == 0 || frame is not null, "The original field requires its supported native wrap metadata.");
            FieldRenderer native = new(field) { TriangleRenderer = gpu };
            native.Render(new Dictionary<int, int>(), [], TimeSpan.Zero, backgroundScrollOffsets: offsets, actorScrollOffset: scroll, nativeWrapFrame: frame);
            Color[] original = GpuTestDevice.Read(gpu.Target);
            foreach (int width in new[] { 426, 640, 1280 })
            {
                FieldRenderer wide = new(field, width, 240) { TriangleRenderer = gpu };
                wide.Render(new Dictionary<int, int>(), [], TimeSpan.Zero, backgroundScrollOffsets: offsets, actorScrollOffset: scroll, nativeWrapFrame: frame);
                Color[] pixels = GpuTestDevice.Read(gpu.Target);
                int left = (width - 320) / 2;
                for (int y = 0; y < 240; y++)
                {
                    Assert.Equal(original.AsSpan(y * 320, 320).ToArray(), pixels.AsSpan(y * width + left, 320).ToArray());
                }
                int sidePixels = pixels.Where((pixel, index) => (index % width < left || index % width >= left + 320) && pixel != Color.Black).Count();
                metadata.Add(new { fieldId, field.Name, bounds, width, sidePixels, wide.ContentBounds });
                if (output is not null)
                {
                    Directory.CreateDirectory(output);
                    using FileStream stream = File.Create(Path.Combine(output, $"field{fieldId}-wide{width}.png"));
                    gpu.Target.SaveAsPng(stream, width, 240);
                }
            }
            FieldRenderer portrait = new(field, 320, 480) { TriangleRenderer = gpu };
            portrait.Render(new Dictionary<int, int>(), [], TimeSpan.Zero, backgroundScrollOffsets: offsets, actorScrollOffset: scroll, nativeWrapFrame: frame);
            Color[] portraitPixels = GpuTestDevice.Read(gpu.Target);
            for (int y = 0; y < 240; y++)
            {
                float sourceY = (y + 0.5f) * 224 / 240;
                // An exact source-texel edge can round to either adjacent row
                // after the additional portrait presentation translation.
                if (MathF.Abs(sourceY - MathF.Round(sourceY)) > 0.0001f)
                {
                    Assert.Equal(original.AsSpan(y * 320, 320).ToArray(), portraitPixels.AsSpan((y + 120) * 320, 320).ToArray());
                }
            }
        }
        if (output is not null)
        {
            File.WriteAllText(Path.Combine(output, "original-field-metadata.json"), JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    [Fact]
    public void ModelsAndAdditiveFadeUseTheExpandedImageClip()
    {
        FieldModel model = new("viewport-actor", [new(-1, 0)], [new(new Vector3(0, -10, -10), 0), new(new Vector3(0, 10, -10), 0), new(new Vector3(0, 0, 10), 0)], [new([0, 2, 1], [Vector2.Zero, Vector2.Zero, Vector2.Zero], 0, false)], [Solid(Parallax)], [new([new(Vector3.Zero, [Vector3.Zero])])]);
        FieldAssets field = Field(Tiles(-240, 240, 0, Ground), new FieldBounds(-112, 112, 240, -240)) with { Models = [model] };
        ActorVisual actor = new(0, 0, 220, 0, 0, 192, 0, 0, true, PolygonColor: new Vector3(128));
        FieldRenderer software = new(field, 640, 240);
        byte[] expected = software.Render(new Dictionary<int, int>(), [actor, actor with { ObjectId = 1, X = 280 }], TimeSpan.Zero);
        NativeFadeDraw fade = new(NativeFadeDrawMode.Add, 10, 20, 30);
        FieldColorComposite.Apply(expected, fade, 640, software.ContentBounds);
        AssertPixel(expected, 640, 540, 120, new Rgba32(202, 84, 158, 255));
        AssertPixel(expected, 640, 600, 120, Black);
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        FieldRenderer hardware = new(field, 640, 240) { TriangleRenderer = gpu };
        hardware.Render(new Dictionary<int, int>(), [actor, actor with { ObjectId = 1, X = 280 }], TimeSpan.Zero, fade: fade);
        Color[] actual = GpuTestDevice.Read(gpu.Target);
        Assert.Equal(new Color(202, 84, 158, 255), actual[120 * 640 + 540]);
        Assert.Equal(Color.Black, actual[120 * 640 + 600]);
        Assert.Equal(Color.Black, actual[120 * 640 + 79]);
    }

    [Fact]
    public void CameraMotionRefreshesSideDepthsWhenParallaxStaysFixed()
    {
        PixelImage transparent = new(1, 1, [default]);
        FieldModel model = new("viewport-actor", [new(-1, 0)], [new(new Vector3(0, -10, -10), 0), new(new Vector3(0, 10, -10), 0), new(new Vector3(0, 0, 10), 0)], [new([0, 2, 1], [Vector2.Zero, Vector2.Zero, Vector2.Zero], 0, false)], [Solid(Parallax)], [new([new(Vector3.Zero, [Vector3.Zero])])]);
        FieldAssets field = Field([
            Tile(-1000, 0, 100, 0, transparent) with { PackedPalette = 0x100 },
            Tile(32, 0, 100, 0, transparent) with { PackedPalette = 0x101 },
            Tile(224, 0, 100, 0, Solid(Ground)) with { PackedPalette = 0x102 }
        ], new FieldBounds(-112, 112, 400, -400)) with { Models = [model] };
        ActorVisual actor = new(0, 0, 264 * 400.375f / 256, 8, 144.375f, 192, 0, 0, true, PolygonColor: new Vector3(128));
        FieldRenderer reused = new(field, 640, 240);
        byte[] before = reused.Render(new Dictionary<int, int>(), [actor], TimeSpan.Zero).ToArray();
        Vector2 scroll = new(-40, 0);
        byte[] actual = reused.Render(new Dictionary<int, int>(), [actor], TimeSpan.Zero, actorScrollOffset: scroll).ToArray();
        byte[] expected = new FieldRenderer(field, 640, 240).Render(new Dictionary<int, int>(), [actor], TimeSpan.Zero, actorScrollOffset: scroll);
        AssertPixel(actual, 640, 545, 125, Parallax);
        Assert.Equal(expected, actual);
        Assert.NotEqual(before, actual);
    }

    [Fact]
    public void SelectedCameraUsesItsOwnImageBoundsAndDoesNotAddPanToTheClip()
    {
        FieldAssets field = Field(Tiles(-640, 640, 0, Ground), new FieldBounds(-112, 112, 160, -160));
        FieldBounds[] bounds = field.Information!.CameraBounds.ToArray();
        bounds[1] = new FieldBounds(-112, 112, 400, -240);
        field = field with { Cameras = new[] { field.Camera, field.Camera with { Pan = new Vector2(37, 0) } }, Information = field.Information with { CameraBounds = bounds } };
        FieldRenderer renderer = new(field, 800, 240);
        byte[] first = renderer.Render(new Dictionary<int, int>(), [], TimeSpan.Zero).ToArray();
        byte[] second = renderer.Render(new Dictionary<int, int>(), [], TimeSpan.Zero, cameraIndex: 1).ToArray();
        AssertPixel(first, 800, 200, 120, Black);
        AssertPixel(second, 800, 79, 120, Black);
        AssertPixel(second, 800, 80, 120, Ground);
        AssertPixel(second, 800, 719, 120, Ground);
        AssertPixel(second, 800, 720, 120, Black);
        Assert.Equal(first, renderer.Render(new Dictionary<int, int>(), [], TimeSpan.Zero));
        AssertGpuMatches(field, 800, 240, second, cameraIndex: 1);
    }

    [Fact]
    public void MissingMetadataAndNativeMovieViewportRetainTheirOriginalFit()
    {
        FieldAssets field = Field(Tiles(-640, 640, 0, Ground), new FieldBounds(-112, 112, 640, -640));
        byte[] ordinary = new FieldRenderer(field, 640, 240).Render(new Dictionary<int, int>(), [], TimeSpan.Zero);
        AssertPixel(ordinary, 640, 100, 120, Ground);
        byte[] missing = new FieldRenderer(field with { Information = null }, 640, 240).Render(new Dictionary<int, int>(), [], TimeSpan.Zero);
        AssertPixel(missing, 640, 100, 120, Black);
        AssertPixel(missing, 640, 160, 120, Ground);
        byte[] movie = new FieldRenderer(field, 640, 240, nativeMovieViewport: true).Render(new Dictionary<int, int>(), [], TimeSpan.Zero);
        AssertPixel(movie, 640, 100, 120, Black);
        AssertPixel(movie, 640, 320, 120, Ground);
    }

    [Fact]
    public void PresentationOnlyTilesCannotResetTheNativeCenterClutOrder()
    {
        PixelImage transparent = new(1, 1, [default]);
        FieldAssets field = Field([
            Tile(-1000, 0, 4094, 0, transparent) with { PackedPalette = 0x100 },
            Tile(-224, 0, 20, 0, transparent) with { PackedPalette = 0x200 },
            Tile(0, 0, 4094, 0, Solid(Ground)) with { PackedPalette = 0x101 },
            Tile(0, 0, 4094, 0, Solid(Parallax)) with { PackedPalette = 0x100 }
        ], new FieldBounds(-112, 112, 640, -640));
        byte[] native = new FieldRenderer(field).Render(new Dictionary<int, int>(), [], TimeSpan.Zero).ToArray();
        byte[] wide = new FieldRenderer(field, 640, 240).Render(new Dictionary<int, int>(), [], TimeSpan.Zero).ToArray();
        AssertPixel(native, 320, 161, 121, Parallax);
        AssertCenterEquals(native, wide, 640);
        AssertGpuMatches(field, 640, 240, wide);
    }

    [Fact]
    public void SidePaletteCapacityDoesNotIncludeInvisibleNativeCenterPalettes()
    {
        PixelImage transparent = new(1, 1, [default]);
        List<BackgroundTile> tiles = [];
        for (int index = 0; index < 16; index++)
        {
            tiles.Add(Tile(64, 0, 100, 0, index == 0 ? Solid(Ground) : transparent) with { PackedPalette = (ushort)(0x100 + index) });
        }
        tiles.Add(Tile(-224, 0, 100, 0, Solid(Parallax)) with { PackedPalette = 0x200 });
        FieldAssets field = Field(tiles, new FieldBounds(-112, 112, 640, -640));
        byte[] native = new FieldRenderer(field).Render(new Dictionary<int, int>(), [], TimeSpan.Zero).ToArray();
        byte[] wide = new FieldRenderer(field, 640, 240).Render(new Dictionary<int, int>(), [], TimeSpan.Zero).ToArray();
        AssertPixel(native, 320, 225, 121, Ground);
        AssertPixel(wide, 640, 97, 121, Parallax);
        AssertCenterEquals(native, wide, 640);
        AssertGpuMatches(field, 640, 240, wide);
    }

    [Theory]
    [InlineData(64, 0)]
    [InlineData(64, 17)]
    [InlineData(352, 0)]
    [InlineData(352, 351)]
    [InlineData(1024, 0)]
    [InlineData(1024, 1023)]
    public void WrappedSidesRepeatEveryPeriodWithoutChangingNativeCenterOrInputs(int period, short phase)
    {
        FieldAssets field = WrappedField(period);
        ImmutableArray<FieldNativeBackgroundLayerCurrent> currents = ImmutableArray.Create(new FieldNativeBackgroundLayerCurrent(1, phase, 0, 0, 0, 256, 256));
        FieldNativeBackgroundWrapFrame frame = FieldNativeNormalBackgroundWrap.Prepare(field, 0, new FieldNativeCameraCenter(0, 0), currents)!;
        Assert.NotNull(frame);
        Dictionary<int, Vector2> offsets = new() { [1] = Vector2.Zero };
        string originalFrame = JsonSerializer.Serialize(frame);
        BackgroundTile[] originalTiles = field.Background.ToArray();
        FieldRenderer renderer = new(field, 640, 240);
        byte[] wide = renderer.Render(new Dictionary<int, int>(), [], TimeSpan.Zero, backgroundScrollOffsets: offsets, nativeWrapFrame: frame).ToArray();
        byte[] native = new FieldRenderer(field).Render(new Dictionary<int, int>(), [], TimeSpan.Zero, backgroundScrollOffsets: offsets, nativeWrapFrame: frame).ToArray();
        AssertCenterEquals(native, wide, 640);
        foreach (int x in Enumerable.Range(0, 160).Concat(Enumerable.Range(480, 160)))
        {
            int sourceX = PositiveMod(x - 320 + phase + period / 2, period);
            AssertPixel(wide, 640, x, 120, Stripe(sourceX / 16));
        }
        Assert.Equal(originalFrame, JsonSerializer.Serialize(frame));
        Assert.Equal(originalTiles, field.Background);
        Assert.Equal(Vector2.Zero, Assert.Single(offsets).Value);
        Assert.True(FieldNativeNormalBackgroundWrap.Validate(field, 0, Vector2.Zero, offsets, frame));
        Assert.Equal(wide, renderer.Render(new Dictionary<int, int>(), [], TimeSpan.Zero, backgroundScrollOffsets: offsets, nativeWrapFrame: frame));
        AssertGpuMatches(field, 640, 240, wide, offsets, nativeWrapFrame: frame);
    }

    [Theory]
    [InlineData(640, -160, -160)]
    [InlineData(640, 0, 0)]
    [InlineData(640, 160, 160)]
    [InlineData(640, 320, 192)]
    [InlineData(640, -320, -192)]
    [InlineData(1280, 320, 0)]
    public void WideCameraStopsAtBothEdgesWithoutExposingBlackPixels(int width, int center, int expectedCenter)
    {
        List<BackgroundTile> tiles = [];
        for (int x = -512; x < 512; x += 16)
        {
            tiles.AddRange(Tiles(x, x + 16, 0, Stripe((x + 512) / 16)));
        }
        FieldAssets field = Field(tiles, new FieldBounds(-112, 112, 512, -512));
        Vector2 offset = new(-center, 0);
        Dictionary<int, Vector2> offsets = new() { [0] = offset };
        FieldRenderer renderer = new(field, width, 240);
        byte[] pixels = renderer.Render(new Dictionary<int, int>(), [], TimeSpan.Zero, backgroundScrollOffsets: offsets, actorScrollOffset: offset).ToArray();
        for (int x = 0; x < width; x++)
        {
            float imageX = x + 0.5f - width / 2f + expectedCenter;
            Rgba32 expected = imageX >= -512 && imageX < 512 ? Stripe((int)(imageX + 512) / 16) : Black;
            AssertPixel(pixels, width, x, 120, expected);
        }
        AssertGpuMatches(field, width, 240, pixels, offsets, offset);
        Assert.Equal(offset, offsets[0]);
        Assert.Equal(pixels, renderer.Render(new Dictionary<int, int>(), [], TimeSpan.Zero, backgroundScrollOffsets: offsets, actorScrollOffset: offset));
    }

    [Theory]
    [InlineData(-160)]
    [InlineData(0)]
    [InlineData(160)]
    public void NarrowImageStaysCenteredAsNativeCameraMoves(int center)
    {
        FieldAssets field = Field(Tiles(-240, 240, 0, Ground), new FieldBounds(-112, 112, 240, -240));
        Vector2 offset = new(-center, 0);
        Dictionary<int, Vector2> offsets = new() { [0] = offset };
        byte[] pixels = new FieldRenderer(field, 640, 240).Render(new Dictionary<int, int>(), [], TimeSpan.Zero, backgroundScrollOffsets: offsets, actorScrollOffset: offset);
        for (int x = 0; x < 640; x++)
        {
            AssertPixel(pixels, 640, x, 120, x >= 80 && x < 560 ? Ground : Black);
        }
        AssertGpuMatches(field, 640, 240, pixels, offsets, offset);
    }

    [Fact]
    public void ClampedCameraMovesModelsWithTheBackgroundAndRefreshesReusedRasters()
    {
        FieldModel model = new("viewport-actor", [new(-1, 0)], [new(new Vector3(0, -10, -10), 0), new(new Vector3(0, 10, -10), 0), new(new Vector3(0, 0, 10), 0)], [new([0, 2, 1], [Vector2.Zero, Vector2.Zero, Vector2.Zero], 0, false)], [Solid(Parallax)], [new([new(Vector3.Zero, [Vector3.Zero])])]);
        FieldAssets field = Field(Tiles(-320, 320, 0, Ground), new FieldBounds(-112, 112, 320, -320)) with { Models = [model] };
        ActorVisual actor = new(0, 0, 0, 0, 0, 192, 0, 0, true, PolygonColor: new Vector3(128));
        FieldRenderer renderer = new(field, 640, 240);
        byte[] expected = renderer.Render(new Dictionary<int, int>(), [actor], TimeSpan.Zero).ToArray();
        AssertPixel(expected, 640, 320, 120, Parallax);
        foreach (int center in new[] { -160, 160, 0, -160 })
        {
            Vector2 offset = new(-center, 0);
            Dictionary<int, Vector2> offsets = new() { [0] = offset };
            Assert.Equal(expected, renderer.Render(new Dictionary<int, int>(), [actor], TimeSpan.Zero, backgroundScrollOffsets: offsets, actorScrollOffset: offset));
        }
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        renderer.TriangleRenderer = gpu;
        renderer.Render(new Dictionary<int, int>(), [actor], TimeSpan.Zero, backgroundScrollOffsets: new Dictionary<int, Vector2> { [0] = new(-160, 0) }, actorScrollOffset: new(-160, 0));
        Assert.Equal(new Color(Parallax.R, Parallax.G, Parallax.B), GpuTestDevice.Read(gpu.Target)[120 * 640 + 320]);
    }

    [Theory]
    [InlineData(-160)]
    [InlineData(160)]
    public void ClampedWrappedBackgroundRetainsItsPeriodAndNativeFrame(short center)
    {
        FieldAssets field = WrappedField(352);
        FieldBounds[] bounds = field.Information!.CameraBounds.ToArray();
        bounds[0] = new FieldBounds(-112, 112, 320, -320);
        field = field with { Information = field.Information with { CameraBounds = bounds } };
        Vector2 offset = new(-center, 0);
        Dictionary<int, Vector2> offsets = new() { [1] = offset };
        FieldNativeBackgroundWrapFrame frame = FieldNativeNormalBackgroundWrap.Prepare(field, 0, new FieldNativeCameraCenter(center, 0))!;
        Assert.NotNull(frame);
        string originalFrame = JsonSerializer.Serialize(frame);
        byte[] pixels = new FieldRenderer(field, 640, 240).Render(new Dictionary<int, int>(), [], TimeSpan.Zero, backgroundScrollOffsets: offsets, actorScrollOffset: offset, nativeWrapFrame: frame);
        for (int x = 0; x < 640; x++)
        {
            AssertPixel(pixels, 640, x, 120, Stripe(PositiveMod(x - 320 + 176, 352) / 16));
        }
        AssertGpuMatches(field, 640, 240, pixels, offsets, offset, nativeWrapFrame: frame);
        Assert.Equal(originalFrame, JsonSerializer.Serialize(frame));
        Assert.True(FieldNativeNormalBackgroundWrap.Validate(field, 0, offset, offsets, frame));
    }

    [Fact]
    public void MoviePresentationClearsThePreviousFieldCameraClamp()
    {
        FieldAssets field = Field(Tiles(-320, 320, 0, Ground), new FieldBounds(-112, 112, 320, -320));
        FieldRenderer renderer = new(field, 640, 240);
        renderer.Render(new Dictionary<int, int>(), [], TimeSpan.Zero, backgroundScrollOffsets: new Dictionary<int, Vector2> { [0] = new(-160, 0) }, actorScrollOffset: new(-160, 0));
        byte[] movie = Enumerable.Repeat(new byte[] { 64, 128, 192, 255 }, 320 * 224).SelectMany(pixel => pixel).ToArray();
        byte[] actual = renderer.RenderMovieFrame(movie, 320, 224, field.Camera, [], null);
        byte[] expected = new FieldRenderer(field, 640, 240).RenderMovieFrame(movie, 320, 224, field.Camera, [], null);
        Assert.Equal(expected, actual);
        Assert.Equal(new PixelBounds(160, 0, 320, 240), renderer.ContentBounds);
    }

    [Fact]
    public void InstalledBalambImageNeverExposesSideBarsAtItsNativeCameraLimits()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        FieldAssets field = assets.LoadField(102);
        Dictionary<int, int> states = field.Background.Where(tile => tile.AnimationId != 255).GroupBy(tile => tile.AnimationId).ToDictionary(group => group.Key, group => (int)group.First().AnimationState);
        FieldBounds bounds = field.Information!.CameraBounds[0];
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_WIDESCREEN_OUTPUT");
        List<object> metadata = [];
        foreach ((int width, int height) in new[] { (426, 240), (1912, 978), (1280, 240) })
        {
            foreach (int center in new[] { bounds.Left + 160, (bounds.Left + bounds.Right) / 2, bounds.Right - 160 })
            {
                Vector2 offset = new(-center, -(bounds.Top + bounds.Bottom) / 2);
                Dictionary<int, Vector2> offsets = field.Background.Select(tile => tile.Layer).Distinct().ToDictionary(layer => layer, _ => offset);
                FieldNativeBackgroundWrapFrame? frame = field.Information.BackgroundWrapMask == 0 ? null : FieldNativeNormalBackgroundWrap.Prepare(field, 0, new FieldNativeCameraCenter((short)center, (short)-offset.Y));
                FieldRenderer renderer = new(field, width, height) { TriangleRenderer = gpu };
                renderer.Render(states, [], TimeSpan.Zero, backgroundScrollOffsets: offsets, actorScrollOffset: offset, nativeWrapFrame: frame);
                float scale = Math.Min(width / 320f, height / 240f);
                int imageWidth = Math.Min(width, (int)MathF.Round((bounds.Right - bounds.Left) * scale));
                Assert.InRange(renderer.ContentBounds.Width, imageWidth - 1, imageWidth + 1);
                Assert.InRange(Math.Abs(renderer.ContentBounds.X - (width - renderer.ContentBounds.Width) / 2f), 0, 0.5f);
                Color[] pixels = GpuTestDevice.Read(gpu.Target);
                int y = height / 2;
                Assert.NotEqual(Color.Black, pixels[y * width + renderer.ContentBounds.X]);
                Assert.NotEqual(Color.Black, pixels[y * width + renderer.ContentBounds.X + renderer.ContentBounds.Width - 1]);

                metadata.Add(new { width, height, center, bounds, renderer.ContentBounds });
                if (output is not null)
                {
                    Directory.CreateDirectory(output);
                    using FileStream stream = File.Create(Path.Combine(output, $"field102-{width}x{height}-center{center}.png"));
                    gpu.Target.SaveAsPng(stream, width, height);
                }
            }
        }
        if (output is not null)
        {
            File.WriteAllText(Path.Combine(output, "field102-camera-bounds.json"), JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    private static FieldAssets WrappedField(int period)
    {
        List<BackgroundTile> tiles = [];
        for (int x = -period / 2; x < period / 2; x += 16)
        {
            tiles.AddRange(Tiles(x, x + 16, 1, Stripe((x + period / 2) / 16)));
        }
        FieldAssets field = Field(tiles, new FieldBounds(-112, 112, 1024, -1024));
        FieldBounds[] bounds = field.Information!.CameraBounds.ToArray();
        bounds[1] = new FieldBounds(-112, 112, (short)(period / 2), (short)(-period / 2));
        return field with { Information = field.Information with { CameraBounds = bounds, BackgroundWrapMask = 2 } };
    }

    private static FieldAssets Field(IReadOnlyList<BackgroundTile> tiles, FieldBounds bounds)
    {
        FieldCamera camera = new(Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, new Vector3(0, 0, 256), Vector2.Zero, 256);
        FieldInformation information = new(0, 0, Enumerable.Repeat(bounds, 8).ToArray(), [new FieldBounds(0, 224, 320, 0)], [], []);
        return new FieldAssets(600, "field-viewport-component", [0], [], tiles, camera, new FieldWalkmesh([]), []) { Information = information, NativeExecutableHash = NativeFieldRuntimeProfile.FF8EnExecutableHash };
    }

    private static List<BackgroundTile> Tiles(int left, int right, int layer, Rgba32 color, int depth = 100)
    {
        PixelImage image = Solid(color);
        List<BackgroundTile> tiles = [];
        for (int y = -112; y < 112; y += 16)
        {
            for (int x = left; x < right; x += 16)
            {
                tiles.Add(Tile(x, y, depth, layer, image));
            }
        }
        return tiles;
    }

    private static BackgroundTile Tile(int x, int y, int depth, int layer, PixelImage image)
    {
        return new BackgroundTile(x, y, depth, 255, 0, layer, MapBlendMode.None, false, true, image);
    }

    private static PixelImage Solid(Rgba32 color)
    {
        return new PixelImage(1, 1, [color]);
    }

    private static Rgba32 Stripe(int column)
    {
        return column % 2 == 0 ? Ground : Parallax;
    }

    private static int PositiveMod(int value, int modulus)
    {
        return (value % modulus + modulus) % modulus;
    }

    private static void AssertPixel(byte[] pixels, int width, int x, int y, Rgba32 expected)
    {
        int offset = (y * width + x) * 4;
        Assert.True(pixels[offset] == expected.R && pixels[offset + 1] == expected.G && pixels[offset + 2] == expected.B && pixels[offset + 3] == expected.A, $"Pixel ({x},{y}) expected {expected.R},{expected.G},{expected.B},{expected.A}; actual {pixels[offset]},{pixels[offset + 1]},{pixels[offset + 2]},{pixels[offset + 3]}.");
    }

    private static void AssertCenterEquals(byte[] native, byte[] wide, int wideWidth)
    {
        int left = (wideWidth - 320) / 2;
        for (int y = 0; y < 240; y++)
        {
            Assert.True(native.AsSpan(y * 320 * 4, 320 * 4).SequenceEqual(wide.AsSpan((y * wideWidth + left) * 4, 320 * 4)), $"Native center differs at row {y}.");
        }
    }

    private static void AssertGpuMatches(FieldAssets field, int width, int height, byte[] expected, IReadOnlyDictionary<int, Vector2>? offsets = null, Vector2 actorOffset = default, int cameraIndex = 0, FieldNativeBackgroundWrapFrame? nativeWrapFrame = null)
    {
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        FieldRenderer renderer = new(field, width, height) { TriangleRenderer = gpu };
        renderer.Render(new Dictionary<int, int>(), [], TimeSpan.Zero, cameraIndex: cameraIndex, backgroundScrollOffsets: offsets, actorScrollOffset: actorOffset, nativeWrapFrame: nativeWrapFrame);
        Color[] actual = GpuTestDevice.Read(gpu.Target);
        Assert.Equal(width * height, actual.Length);
        for (int index = 0; index < actual.Length; index++)
        {
            int offset = index * 4;
            Color pixel = actual[index];
            Assert.True(expected[offset] == pixel.R && expected[offset + 1] == pixel.G && expected[offset + 2] == pixel.B && expected[offset + 3] == pixel.A, $"CPU/GPU mismatch at ({index % width},{index / width}): expected {expected[offset]},{expected[offset + 1]},{expected[offset + 2]},{expected[offset + 3]}; actual {pixel.R},{pixel.G},{pixel.B},{pixel.A}.");
        }
    }
}
