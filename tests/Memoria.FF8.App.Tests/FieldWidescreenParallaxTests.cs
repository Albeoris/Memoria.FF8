using System.Collections.Immutable;
using System.Numerics;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;

namespace Memoria.FF8.App.Tests;

public sealed partial class FieldWidescreenTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(32)]
    [InlineData(48)]
    [InlineData(64)]
    [InlineData(256)]
    [InlineData(-64)]
    [InlineData(320)]
    public void ClampedParallaxUsesTheVisibleCameraAndPreservesIndependentScroll(short ratio)
    {
        List<BackgroundTile> tiles = [];
        for (int x = -512; x < 512; x += 16)
        {
            tiles.AddRange(Tiles(x, x + 16, 1, Stripe((x + 512) / 16)));
        }
        FieldAssets field = Field(tiles, new FieldBounds(-112, 112, 240, -240));
        Dictionary<int, short> scales = new() { [1] = ratio };
        FieldRenderer renderer = new(field, 640, 240);
        // The entire 480-pixel image fits: its visible center is zero. Keep
        // a separate scripted translation so this cannot pass by freezing layers.
        foreach (int scripted in new[] { -11, 0, 19 })
        {
            byte[] reference = new FieldRenderer(field, 640, 240).Render(new Dictionary<int, int>(), [], TimeSpan.Zero, backgroundScrollOffsets: new Dictionary<int, Vector2> { [1] = new(scripted, 0) }).ToArray();
            foreach (int center in new[] { -64, -7, 0, 7, 64, -64 })
            {
                Dictionary<int, Vector2> offsets = new() { [1] = new(scripted - center * ratio / 256, 0) };
                byte[] actual = renderer.Render(new Dictionary<int, int>(), [], TimeSpan.Zero, backgroundScrollOffsets: offsets, actorScrollOffset: new(-center, 0), backgroundLayerScales: scales).ToArray();
                Assert.Equal(reference, actual);
            }
        }
    }

    [Theory]
    [InlineData(-64)]
    [InlineData(64)]
    public void ClampedWrappedParallaxRecomputesRawPlacementBeforePeriodRelocation(short center)
    {
        FieldAssets field = WrappedField(352);
        FieldBounds[] bounds = field.Information!.CameraBounds.ToArray();
        bounds[0] = new FieldBounds(-112, 112, 240, -240);
        field = field with { Information = field.Information with { CameraBounds = bounds } };
        foreach (short ratio in new short[] { 0, 32, 64, 256, -64 })
        {
            foreach (short current in new short[] { 0, 175, 351 })
            {
                ImmutableArray<FieldNativeBackgroundLayerCurrent> currents = [new(1, current, 0, 13, 0, ratio, 256)];
                FieldNativeBackgroundWrapFrame referenceFrame = FieldNativeNormalBackgroundWrap.Prepare(field, 0, new FieldNativeCameraCenter(0, 0), currents)!;
                FieldNativeBackgroundWrapFrame actualFrame = FieldNativeNormalBackgroundWrap.Prepare(field, 0, new FieldNativeCameraCenter(center, 0), currents)!;
                Dictionary<int, Vector2> referenceOffsets = new() { [1] = Vector2.Zero };
                Dictionary<int, Vector2> offsets = new() { [1] = new(-center, 0) };
                byte[] reference = new FieldRenderer(field, 640, 240).Render(new Dictionary<int, int>(), [], TimeSpan.Zero, backgroundScrollOffsets: referenceOffsets, nativeWrapFrame: referenceFrame).ToArray();
                byte[] actual = new FieldRenderer(field, 640, 240).Render(new Dictionary<int, int>(), [], TimeSpan.Zero, backgroundScrollOffsets: offsets, actorScrollOffset: new(-center, 0), nativeWrapFrame: actualFrame).ToArray();
                Assert.True(reference.SequenceEqual(actual), $"Wrapped placement differs: center={center}, ratio={ratio}, current={current}.");
            }
        }
    }

    [Theory]
    [InlineData(-64)]
    [InlineData(64)]
    public void HorizontalShakeSurvivesTheWideCameraClamp(int center)
    {
        List<BackgroundTile> tiles = [];
        for (int x = -320; x < 320; x += 16)
        {
            tiles.AddRange(Tiles(x, x + 16, 0, Stripe((x + 320) / 16)));
        }
        FieldAssets field = Field(tiles, new FieldBounds(-112, 112, 240, -240));
        Dictionary<int, Vector2> offsets = new() { [0] = new(-center, 0) };
        FieldRenderer renderer = new(field, 640, 240);
        byte[] still = renderer.Render(new Dictionary<int, int>(), [], TimeSpan.Zero, backgroundScrollOffsets: offsets, actorScrollOffset: new(-center, 0)).ToArray();
        foreach (sbyte shake in new sbyte[] { -7, 7 })
        {
            byte[] shaken = renderer.Render(new Dictionary<int, int>(), [], TimeSpan.Zero, backgroundScrollOffsets: offsets, actorScrollOffset: new(-center, 0), drawingJitterX: shake).ToArray();
            Assert.NotEqual(still, shaken);
            AssertGpuMatches(field, 640, 240, shaken, offsets, new(-center, 0), jitterX: shake);
            for (int x = 100; x < 540; x++)
            {
                Assert.True(still.AsSpan((120 * 640 + x - shake) * 4, 4).SequenceEqual(shaken.AsSpan((120 * 640 + x) * 4, 4)));
            }
        }
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(0.5f)]
    [InlineData(1f)]
    public void InterpolatedClampedParallaxDoesNotFollowTheHiddenNativeCamera(float fraction)
    {
        List<BackgroundTile> tiles = [];
        for (int x = -512; x < 512; x += 16)
        {
            tiles.AddRange(Tiles(x, x + 16, 1, Stripe((x + 512) / 16)));
        }
        FieldAssets field = Field(tiles, new FieldBounds(-112, 112, 240, -240));
        Dictionary<int, short> previousScales = new() { [1] = 64 };
        Dictionary<int, short> currentScales = new() { [1] = 32 };
        FieldBackgroundSample previous = new(new Dictionary<int, Vector2> { [1] = new(1, 0) }, new(7, 0), null, previousScales);
        FieldBackgroundSample current = new(new Dictionary<int, Vector2> { [1] = new(-1, 0) }, new(-9, 0), null, currentScales);
        byte[] expected = new FieldRenderer(field, 640, 240).Render(new Dictionary<int, int>(), [], TimeSpan.Zero).ToArray();
        byte[] actual = new FieldRenderer(field, 640, 240).Render(new Dictionary<int, int>(), [], TimeSpan.Zero, backgroundScrollOffsets: current.LayerOffsets, actorScrollOffset: current.ActorOffset, backgroundBlend: new(previous, current, fraction), backgroundLayerScales: currentScales).ToArray();
        Assert.Equal(expected, actual);
        AssertGpuMatches(field, 640, 240, actual, current.LayerOffsets, current.ActorOffset, layerScales: currentScales, blend: new(previous, current, fraction));
    }

    [Theory]
    [InlineData(-320, -192)]
    [InlineData(320, 192)]
    public void ParallaxStopsAtTheExpandedViewportEdge(int center, int visibleCenter)
    {
        List<BackgroundTile> tiles = [];
        for (int x = -640; x < 640; x += 16)
        {
            tiles.AddRange(Tiles(x, x + 16, 1, Stripe((x + 640) / 16)));
        }
        FieldAssets field = Field(tiles, new FieldBounds(-112, 112, 512, -512));
        Dictionary<int, short> scales = new() { [1] = 48 };
        Dictionary<int, Vector2> offsets = new() { [1] = new(11 - center * 48 / 256, 0) };
        Dictionary<int, Vector2> referenceOffsets = new() { [1] = new(11 - visibleCenter * 48 / 256, 0) };
        byte[] expected = new FieldRenderer(field, 640, 240).Render(new Dictionary<int, int>(), [], TimeSpan.Zero, backgroundScrollOffsets: referenceOffsets, actorScrollOffset: new(-visibleCenter, 0), backgroundLayerScales: scales).ToArray();
        byte[] actual = new FieldRenderer(field, 640, 240).Render(new Dictionary<int, int>(), [], TimeSpan.Zero, backgroundScrollOffsets: offsets, actorScrollOffset: new(-center, 0), backgroundLayerScales: scales).ToArray();
        Assert.Equal(expected, actual);
        AssertGpuMatches(field, 640, 240, actual, offsets, new(-center, 0), layerScales: scales);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(0.5f)]
    [InlineData(1f)]
    public void WrappedParallaxInterpolatesScriptPhaseAcrossThePeriodWithAClampedCamera(float fraction)
    {
        FieldAssets field = WrappedField(352);
        FieldBounds[] bounds = field.Information!.CameraBounds.ToArray();
        bounds[0] = new FieldBounds(-112, 112, 240, -240);
        field = field with { Information = field.Information with { CameraBounds = bounds } };
        FieldBackgroundSample Sample(short center, short phase)
        {
            ImmutableArray<FieldNativeBackgroundLayerCurrent> currents = [new(1, phase, 0, 13, 0, 64, 256)];
            FieldNativeBackgroundWrapFrame wrap = FieldNativeNormalBackgroundWrap.Prepare(field, 0, new FieldNativeCameraCenter(center, 0), currents)!;
            return new(new Dictionary<int, Vector2> { [1] = new(-center, 0) }, new(-center, 0), wrap);
        }
        FieldBackgroundSample previous = Sample(63, 351);
        FieldBackgroundSample current = Sample(65, 0);
        FieldBackgroundSample referencePrevious = Sample(0, 351);
        FieldBackgroundSample referenceCurrent = Sample(0, 352);
        FieldBackgroundBlend blend = new(previous, current, fraction);
        byte[] expected = new FieldRenderer(field, 640, 240).Render(new Dictionary<int, int>(), [], TimeSpan.Zero, backgroundScrollOffsets: referenceCurrent.LayerOffsets, actorScrollOffset: referenceCurrent.ActorOffset, nativeWrapFrame: referenceCurrent.NativeWrap, backgroundBlend: new(referencePrevious, referenceCurrent, fraction)).ToArray();
        byte[] actual = new FieldRenderer(field, 640, 240).Render(new Dictionary<int, int>(), [], TimeSpan.Zero, backgroundScrollOffsets: current.LayerOffsets, actorScrollOffset: current.ActorOffset, nativeWrapFrame: current.NativeWrap, backgroundBlend: blend).ToArray();
        Assert.Equal(expected, actual);
        AssertGpuMatches(field, 640, 240, actual, current.LayerOffsets, current.ActorOffset, nativeWrapFrame: current.NativeWrap, blend: blend);
    }

    [Theory]
    [InlineData(-64, -11.5f, 2.5f)]
    [InlineData(64, 11.5f, -2.5f)]
    public void FractionalWideImageEdgeRetainsNativeSignedScaleEndpoints(int center, float visibleCenter, float visibleLayerOffset)
    {
        List<BackgroundTile> tiles = [];
        for (int x = -512; x < 512; x += 16)
        {
            tiles.AddRange(Tiles(x, x + 16, 1, Stripe((x + 512) / 16)));
        }
        FieldAssets field = Field(tiles, new FieldBounds(-112, 112, 320, -320));
        Dictionary<int, short> scales = new() { [1] = 64 };
        Dictionary<int, Vector2> offsets = new() { [1] = new(-center / 4, 0) };
        byte[] expected = new FieldRenderer(field, 617, 240).Render(new Dictionary<int, int>(), [], TimeSpan.Zero, backgroundScrollOffsets: new Dictionary<int, Vector2> { [1] = new(visibleLayerOffset, 0) }, actorScrollOffset: new(-visibleCenter, 0), backgroundLayerScales: scales).ToArray();
        byte[] actual = new FieldRenderer(field, 617, 240).Render(new Dictionary<int, int>(), [], TimeSpan.Zero, backgroundScrollOffsets: offsets, actorScrollOffset: new(-center, 0), backgroundLayerScales: scales).ToArray();
        Assert.Equal(expected, actual);
        AssertGpuMatches(field, 617, 240, actual, offsets, new(-center, 0), layerScales: scales);
    }
}
