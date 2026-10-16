using System.Collections.Immutable;
using System.Numerics;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;

namespace Memoria.FF8.Assets.Tests;

public sealed class FieldNativeNormalBackgroundWrapTests
{
    [Fact]
    public void OpposingLinearCurrentsRelocateBeforeSharedCameraOffsetAndDisabledWrappingOnlyAdds()
    {
        using GameAssets assets = InstalledAssets();
        FieldAssets original = assets.LoadField(130);
        foreach (bool wrapping in new[] { true, false })
        {
            FieldAssets field = wrapping ? original : original with { Information = original.Information! with { BackgroundWrapMask = 0 } };
            foreach (int phase in new[] { 0, 81, 367 })
            {
                ImmutableArray<FieldNativeBackgroundLayerCurrent> currents = ImmutableArray.Create(new FieldNativeBackgroundLayerCurrent(1, (short)phase, 0, 0, 0, 256, 256), new FieldNativeBackgroundLayerCurrent(2, (short)-phase, 0, 0, 0, 256, 256));
                FieldNativeBackgroundWrapFrame frame = FieldNativeNormalBackgroundWrap.Prepare(field, 0, new FieldNativeCameraCenter(6, 8), currents)!;
                Assert.Equal(2, frame.NativeNormalVersion);
                List<BackgroundTile> placed = [];
                foreach (BackgroundTile tile in field.Background)
                {
                    FieldNativeBackgroundTileOrigin? origin;
                    if (tile.Layer is 1 or 2)
                    {
                        int shift = tile.Layer == 1 ? -phase : phase;
                        BackgroundTile shifted = tile with { X = unchecked((short)(tile.X + shift)) };
                        origin = wrapping ? NativePlacement(shifted, 6, 8, field.Information!) : new FieldNativeBackgroundTileOrigin((short)shifted.X, (short)shifted.Y);
                    }
                    else
                    {
                        origin = new FieldNativeBackgroundTileOrigin((short)tile.X, (short)tile.Y);
                    }
                    if (origin is FieldNativeBackgroundTileOrigin point)
                    {
                        placed.Add(tile with { X = point.X - 6, Y = point.Y - 8 });
                    }
                }
                Dictionary<int, Vector2> offsets = field.Background.Select(tile => tile.Layer).Distinct().ToDictionary(layer => layer, _ => new Vector2(-6, -8));
                FieldRenderer renderer = new(field, 320, 240);
                FieldRenderer reference = new(field with { Background = placed }, 320, 240);
                Assert.Equal(reference.Render(new Dictionary<int, int>(), [], TimeSpan.Zero), renderer.Render(new Dictionary<int, int>(), [], TimeSpan.Zero, backgroundScrollOffsets: offsets, actorScrollOffset: new Vector2(-6, -8), nativeWrapFrame: frame));
                Assert.False(FieldNativeNormalBackgroundWrap.Validate(field, 0, new Vector2(-6, -8), offsets, frame with { LayerCurrents = ImmutableArray.Create(currents[0] with { X = (short)(currents[0].X + 1) }, currents[1]) }));
            }
        }
    }

    [Fact]
    public void OriginalTwoEnabledLayersMatchNativeWordsAtAll289ClampedCenters()
    {
        using GameAssets assets = InstalledAssets();
        FieldAssets field = assets.LoadField(130);
        int wrapped = 0;
        for (int x = -8; x <= 8; x++)
        {
            for (int y = -8; y <= 8; y++)
            {
                FieldNativeBackgroundWrapFrame frame = FieldNativeNormalBackgroundWrap.Prepare(field, 0, new FieldNativeCameraCenter(x, y))!;
                Assert.NotNull(frame);
                Assert.Equal(2, frame.Layers.Length);
                foreach (BackgroundTile tile in field.Background.Where(tile => tile.Layer is 1 or 2))
                {
                    FieldNativeBackgroundTileOrigin? expected = NativePlacement(tile, x, y, field.Information!);
                    FieldNativeBackgroundWrapLayer layer = frame.Layers.Single(layer => layer.Layer == tile.Layer);
                    Assert.Equal(expected, FieldNativeBackgroundTileProbe.Relocate((short)tile.X, (short)tile.Y, layer, frame));
                    if (expected is FieldNativeBackgroundTileOrigin origin && (origin.X != tile.X || origin.Y != tile.Y))
                    {
                        wrapped++;
                    }
                }
            }
        }
        Assert.True(wrapped > 0);
    }

    [Fact]
    public void TwoLayerRelocationThenSharedNonzeroDrawingOffsetMatchesIndependentPixelsAndCache()
    {
        using GameAssets assets = InstalledAssets();
        FieldAssets field = assets.LoadField(130);
        Dictionary<int, int> states = field.Background.Where(tile => tile.AnimationId != 255).GroupBy(tile => tile.AnimationId).ToDictionary(group => group.Key, group => group.First().AnimationState);
        foreach ((int width, int height) in new[] { (320, 240), (640, 480) })
        {
            FieldRenderer renderer = new(field, width, height);
            byte[] baseline = renderer.Render(states, [], TimeSpan.Zero).ToArray();
            foreach ((int x, int y) in new[] { (6, 8), (-8, -8), (8, 8), (0, 0) })
            {
                FieldNativeBackgroundWrapFrame frame = FieldNativeNormalBackgroundWrap.Prepare(field, 0, new FieldNativeCameraCenter(x, y))!;
                Vector2 shared = new(-x, -y);
                Dictionary<int, Vector2> offsets = field.Background.Select(tile => tile.Layer).Distinct().ToDictionary(layer => layer, _ => shared);
                List<BackgroundTile> placed = [];
                foreach (BackgroundTile tile in field.Background)
                {
                    FieldNativeBackgroundTileOrigin? origin = tile.Layer is 1 or 2 ? NativePlacement(tile, x, y, field.Information!) : new FieldNativeBackgroundTileOrigin((short)tile.X, (short)tile.Y);
                    if (origin is FieldNativeBackgroundTileOrigin point)
                    {
                        placed.Add(tile with { X = point.X - x, Y = point.Y - y });
                    }
                }
                FieldRenderer reference = new(field with { Background = placed }, width, height);
                Assert.Equal(reference.Render(states, [], TimeSpan.Zero), renderer.Render(states, [], TimeSpan.Zero, backgroundScrollOffsets: offsets, actorScrollOffset: shared, nativeWrapFrame: frame));
                Assert.Equal(baseline, renderer.Render(states, [], TimeSpan.Zero));
            }
        }
    }

    [Fact]
    public void NormalFrameRejectsChangedWindowPeriodsLayerSetOffsetsRuntimeAndUnsupportedVersionsBeforeCacheMutation()
    {
        using GameAssets assets = InstalledAssets();
        FieldAssets field = assets.LoadField(130);
        FieldNativeBackgroundWrapFrame frame = FieldNativeNormalBackgroundWrap.Prepare(field, 0, new FieldNativeCameraCenter(6, 8))!;
        Vector2 shared = new(-6, -8);
        Dictionary<int, Vector2> offsets = field.Background.Select(tile => tile.Layer).Distinct().ToDictionary(layer => layer, _ => shared);
        FieldRenderer renderer = new(field);
        Dictionary<int, int> states = [];
        byte[] valid = renderer.Render(states, [], TimeSpan.Zero, backgroundScrollOffsets: offsets, actorScrollOffset: shared, nativeWrapFrame: frame).ToArray();
        foreach (FieldNativeBackgroundWrapFrame altered in new[]
        {
            frame with { NativeNormalVersion = 2 },
            frame with { FieldId = 131 },
            frame with { ProgramHash = "changed" },
            frame with { Left = -187 },
            frame with { Layers = ImmutableArray.Create(frame.Layers[0]) },
            frame with { Layers = ImmutableArray.Create(frame.Layers[0] with { PeriodX = 352 }, frame.Layers[1]) },
            frame with { Layers = ImmutableArray.Create(frame.Layers[0] with { OffsetX = 1 }, frame.Layers[1]) }
        })
        {
            Assert.Throws<NotSupportedException>(() => renderer.Render(states, [], TimeSpan.Zero, backgroundScrollOffsets: offsets, actorScrollOffset: shared, nativeWrapFrame: altered));
            Assert.Equal(valid, renderer.Render(states, [], TimeSpan.Zero, backgroundScrollOffsets: offsets, actorScrollOffset: shared, nativeWrapFrame: frame));
        }
        Assert.Throws<NotSupportedException>(() => renderer.Render(states, [], TimeSpan.Zero, backgroundScrollOffsets: new Dictionary<int, Vector2> { [0] = shared }, actorScrollOffset: shared, nativeWrapFrame: frame));
        Assert.Throws<NotSupportedException>(() => renderer.Render(states, [], TimeSpan.Zero, backgroundScrollOffsets: offsets, actorScrollOffset: Vector2.Zero, nativeWrapFrame: frame));
        Assert.False(FieldNativeNormalBackgroundWrap.Validate(field with { NativeExecutableHash = null }, 0, shared, offsets, frame));
    }

    private static FieldNativeBackgroundTileOrigin? NativePlacement(BackgroundTile tile, int centerX, int centerY, FieldInformation information)
    {
        int x = tile.X;
        int y = tile.Y;
        int left = centerX - 160 - 32;
        int right = centerX + 160 + 16;
        int top = centerY - 112 - 32;
        int bottom = centerY + 112 + 16;
        FieldBounds bounds = information.CameraBounds[tile.Layer];
        if (x < left)
        {
            x = unchecked((short)(x + 2 * bounds.Right));
        }
        else if (x > right)
        {
            x = unchecked((short)(x - 2 * bounds.Right));
        }
        if (y < top)
        {
            y = unchecked((short)(y + 2 * bounds.Bottom));
        }
        else if (y > bottom)
        {
            y = unchecked((short)(y - 2 * bounds.Bottom));
        }
        return x <= left || x >= right || y <= top || y >= bottom ? null : new FieldNativeBackgroundTileOrigin((short)x, (short)y);
    }

    private static GameAssets InstalledAssets()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original field corpus is required.");
        return new GameAssets(directory);
    }
}
