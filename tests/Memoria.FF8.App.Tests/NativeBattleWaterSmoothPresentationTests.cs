using System.Text.Json;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;
using SkiaSharp;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleWaterSmoothPresentationTests
{
    [Fact]
    public void Original_Water_host_renders_distinct_intermediate_geometry_and_pixels()
    {
        NativeBattleSmoothWholeActionTests.Capture capture = new();
        List<Interval> intervals = [];
        long previousCallback = -1;
        int rasterPairs = 0;
        int changedPixelPairs = 0;
        int movingWorldIntervals = 0;
        int movingProjectionIntervals = 0;
        string? artifacts = Environment.GetEnvironmentVariable("MEMORIA_FF8_TEST_ARTIFACTS");
        NativeBattleSmoothWholeActionTests.AssertWholeAction(222, 10, false, observePresentation: Observe);
        Assert.True(movingWorldIntervals > 0, "Water supplied no changing interpolated world geometry through the scene host.");
        Assert.True(movingProjectionIntervals > 0, "Water supplied no changing interpolated screen geometry through the scene host.");
        Assert.True(rasterPairs > 0, "Water supplied no intervals for pixel sampling.");
        Assert.True(changedPixelPairs > 0, "Water's intermediate raster frames were all identical.");
        if (artifacts is not null)
        {
            Directory.CreateDirectory(artifacts);
            File.WriteAllText(Path.Combine(artifacts, "water-motion.json"), JsonSerializer.Serialize(new { MovingWorldIntervals = movingWorldIntervals, MovingProjectionIntervals = movingProjectionIntervals, RasterPairs = rasterPairs, ChangedPixelPairs = changedPixelPairs, Intervals = intervals }));
        }

        void Observe(BattleSession battle, BattleScenePresentation scene)
        {
            NativeBattlePreparedPresentation? current = battle.PreparedPresentation;
            if (current is null || current.Callback == previousCallback)
            {
                return;
            }
            previousCallback = current.Callback;
            bool selected = current.PilotEffect == 222 && current.Primitives.LastCommands.Any(command => command.Command.PilotGeometry?.Identity.Effect == 222);
            bool raster = selected && intervals.Count % 16 == 8 && rasterPairs < 6;
            byte[] firstPixels = scene.Render(battle, 320, 216, raster ? null : capture, 0.25f).ToArray();
            NativeBattlePilotSubmission first = scene.PilotSubmission;
            byte[] secondPixels = scene.Render(battle, 320, 216, raster ? null : capture, 0.75f).ToArray();
            NativeBattlePilotSubmission second = scene.PilotSubmission;
            if (!selected || first.NextSourceCallback != first.SourceCallback + 1)
            {
                return;
            }
            Assert.Equal(0.25f, first.Alpha);
            Assert.Equal(0.75f, second.Alpha);
            Assert.Equal(0, scene.LastPilotFallbackCount);
            bool movingWorld = first.WorldSignature != second.WorldSignature;
            bool movingProjection = first.ProjectedSignature != second.ProjectedSignature;
            movingWorldIntervals += movingWorld ? 1 : 0;
            movingProjectionIntervals += movingProjection ? 1 : 0;
            int changedPixels = 0;
            if (raster)
            {
                rasterPairs++;
                for (int offset = 0; offset < firstPixels.Length; offset += 4)
                {
                    if (!firstPixels.AsSpan(offset, 3).SequenceEqual(secondPixels.AsSpan(offset, 3)))
                    {
                        changedPixels++;
                    }
                }
                changedPixelPairs += changedPixels > 0 ? 1 : 0;
                Save(firstPixels, first.SourceCallback, "quarter");
                Save(secondPixels, first.SourceCallback, "three-quarters");
            }
            intervals.Add(new(first.SourceCallback, first.Corners, first.ContinuousCorners, first.CoveredContinuousCorners, movingWorld, movingProjection, raster, changedPixels));
        }

        void Save(byte[] pixels, long callback, string phase)
        {
            if (artifacts is null)
            {
                return;
            }
            Directory.CreateDirectory(artifacts);
            using SKBitmap bitmap = new(320, 216);
            for (int y = 0; y < bitmap.Height; y++)
            {
                for (int x = 0; x < bitmap.Width; x++)
                {
                    int offset = (y * bitmap.Width + x) * 4;
                    bitmap.SetPixel(x, y, new SKColor(pixels[offset], pixels[offset + 1], pixels[offset + 2], pixels[offset + 3]));
                }
            }
            using FileStream stream = File.Create(Path.Combine(artifacts, $"water-{callback}-{phase}.png"));
            bitmap.SavePng(stream);
        }
    }

    private sealed record Interval(long Callback, int Corners, int ContinuousCorners, int CoveredContinuousCorners, bool MovingWorld, bool MovingProjection, bool Raster, int ChangedPixels);
}
