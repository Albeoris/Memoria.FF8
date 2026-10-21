using System.Numerics;
using System.Text.Json;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;
using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class GpuMultisampleTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    public void Scene_silhouette_uses_presentation_samples_across_swaps_and_resize(int samples)
    {
        using GpuTestDevice device = new(samples);
        using GpuTriangleRenderer renderer = new(device.Device);
        foreach (int size in new[] { 16, 16, 24, 16 })
        {
            renderer.Begin(size, size, new Vector4(0, 0, 0, 1));
            Triangle(renderer, Vector4.One, 0.25f);
            renderer.End();
            Assert.Equal(SupportedSamples(device), renderer.Target.MultiSampleCount);
            Color[] pixels = GpuTestDevice.Read(renderer.Target);
            Assert.Contains(pixels, pixel => pixel.R == 255);
            Assert.Contains(pixels, pixel => pixel.R == 0);
            Assert.Equal(renderer.Target.MultiSampleCount > 0, pixels.Any(pixel => pixel.R > 0 && pixel.R < 255));
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    public void Active_capture_resolves_color_and_preserves_subsequent_depth(int samples)
    {
        using GpuTestDevice device = new(samples);
        using GpuTriangleRenderer renderer = new(device.Device);
        renderer.Begin(16, 16, new Vector4(0, 0, 0, 1));
        Triangle(renderer, new Vector4(1, 0, 0, 1), 0.25f);
        Assert.True(renderer.TryCaptureArgb(out uint[] captured));
        Assert.Contains(captured, pixel => (pixel & 0x00FFFFFF) == 0x00FF0000);
        Assert.Equal(SupportedSamples(device) > 0, captured.Any(pixel => ((pixel >> 16) & 255) is > 0 and < 255));
        Triangle(renderer, new Vector4(0, 1, 0, 1), 0.75f);
        renderer.End();
        Assert.Equal(captured, GpuTestDevice.Read(renderer.Target).Select(pixel => (uint)(pixel.A << 24 | pixel.R << 16 | pixel.G << 8 | pixel.B)));
    }

    [Fact]
    public void Optional_standalone_compares_scene_multisampling_without_advancing_gameplay()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_FIELD_SHADOW_SNAPSHOT");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_TEST_OUTPUT_DIRECTORY");
        Assert.SkipUnless(source is not null && File.Exists(source) && output is not null, "An optional standalone field fixture and output directory are required.");
        using GameWorld world = new(new GameSession(GameSession.Load(source!)), TestResources.GameDirectory);
        world.EnsureField();
        world.SetRenderSize(1920, 1080);
        JsonElement before = world.Events.Capture();
        Directory.CreateDirectory(output!);
        Color[][] images = new Color[2][];
        int index = 0;
        foreach (int samples in new[] { 0, 8 })
        {
            using GpuTestDevice device = new(samples);
            using GpuTriangleRenderer renderer = new(device.Device);
            world.Render(TimeSpan.Zero, triangleRenderer: renderer);
            images[index++] = GpuTestDevice.Read(renderer.Target);
            using FileStream image = File.Create(Path.Combine(output!, $"field-msaa{samples}.png"));
            renderer.Target.SaveAsPng(image, renderer.Target.Width, renderer.Target.Height);
            Assert.Equal(SupportedSamples(device), renderer.Target.MultiSampleCount);
            Assert.True(JsonElement.DeepEquals(before, world.Events.Capture()));
        }
        int changed = images[0].Zip(images[1]).Count(pair => pair.First != pair.Second);
        Assert.True(changed > 0, "The supplied scene should contain visible geometric edges.");
        File.WriteAllText(Path.Combine(output!, "comparison.json"), JsonSerializer.Serialize(new { world.Events.FieldId, world.Events.Frame, Width = 1920, Height = 1080, ChangedPixels = changed, GameplayUnchanged = true }));
    }

    private static void Triangle(GpuTriangleRenderer renderer, Vector4 color, float depth)
    {
        TriangleMaterial material = new(null);
        renderer.Draw(new(new Vector2(1.25f, 1.25f), depth, 1, Vector2.Zero, color), new(new Vector2(14.25f, 3.25f), depth, 1, Vector2.Zero, color), new(new Vector2(4.25f, 14.25f), depth, 1, Vector2.Zero, color), material);
    }

    private static int SupportedSamples(GpuTestDevice device)
    {
        device.Device.Adapter.QueryRenderTargetFormat(device.Device.GraphicsProfile, SurfaceFormat.Color, DepthFormat.Depth24, device.Device.PresentationParameters.MultiSampleCount, out _, out _, out int samples);
        return samples;
    }
}
