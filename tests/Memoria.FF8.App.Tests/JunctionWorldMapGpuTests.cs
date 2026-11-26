using Memoria.FF8.Events;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;
using System.Numerics;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class JunctionWorldMapGpuTests
{
    [Fact]
    public void LoadedFullMapDrawsStoryLocationsAndAllocatedVehiclesOnCpuAndGpu()
    {
        string input = TestResources.FixturePath("world-full-map-marker-source");
        string game = TestResources.GameDirectory;
        GameSession session = new(PlayerStateLoader.Load(input, game));
        using WorldMapScene scene = new(session, game);
        Assert.Equal((byte)1, WorldMapScene.MapDisplayMode(scene.State));
        Assert.True(scene.Apply(new(PlayerActionKind.Wait, Frames: 24)).Accepted);
        Assert.True(scene.Apply(new(PlayerActionKind.Wait, WorldButtons: 0x100)).Accepted);
        Assert.Equal((byte)2, WorldMapScene.MapDisplayMode(scene.State));
        Assert.True(scene.Apply(new(PlayerActionKind.Wait)).Accepted);
        (int x, int z) = (scene.State.X, scene.State.Z);
        Assert.True(scene.Apply(new(PlayerActionKind.Wait, WorldButtons: 0x100)).Accepted);
        Assert.Equal((byte)3, WorldMapScene.MapDisplayMode(scene.State));
        Assert.Equal((x, z), (scene.State.X, scene.State.Z));

        const int width = 640;
        const int height = 448;
        byte[] cpu = scene.Render(width, height);
        string snapshot = TestResources.ArtifactFile("full-map-markers.json");
        session.Save(snapshot);
        GameSession restored = new(GameSession.Load(snapshot));
        using WorldMapScene restoredScene = new(restored, game);
        Assert.Equal(cpu, restoredScene.Render(width, height));

        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        Assert.Empty(scene.Render(width, height, gpu));
        var pixels = GpuTestDevice.Read(gpu.Target);
        int locationPixel = (54 * 2 + 1) * width + (208 * 2 + 1);
        int gardenPixel = (108 * 2 + 1) * width + (178 * 2 + 1);
        byte phase = unchecked((byte)(128 - 8 * session.State.WorldProcess!.MainLoopCounter));
        Assert.Equal(new byte[] { 200, 200, 0 }, cpu.AsSpan(locationPixel * 4, 3).ToArray());
        Assert.Equal(new byte[] { phase, 0, 0 }, cpu.AsSpan(gardenPixel * 4, 3).ToArray());
        Assert.Equal(new Microsoft.Xna.Framework.Color(200, 200, 0), pixels[locationPixel]);
        Assert.Equal(new Microsoft.Xna.Framework.Color((int)phase, 0, 0), pixels[gardenPixel]);
    }

    [Theory]
    [InlineData(1, 230, 140, 65, 70, false)]
    [InlineData(1, 230, 140, 65, 70, true)]
    [InlineData(2, 186, 124, 128, 96, true)]
    [InlineData(3, 32, 16, 256, 192, true)]
    public void NativeMapModesRenderSelectedTexturesOnTheGpu(byte mode, int left, int top, int width, int height, bool replacements)
    {
        GameSession session = new(GameSession.Load(TestResources.FixturePath("junction-world-map-source")));
        string archive = TestResources.FixturePath("junction-world-textures");
        string mods = Directory.GetParent(Path.GetDirectoryName(archive)!)!.FullName;
        using GameResourceHost host = new(TestResources.GameDirectory, Path.Combine(mods, "test-application"), watch: false, modsDirectory: replacements ? mods : Path.Combine(TestResources.ArtifactDirectory, "Mods"));
        using WorldMapScene scene = new(session, TestResources.GameDirectory, host.Resources);
        Assert.True(scene.Apply(new(PlayerActionKind.Wait, Frames: checked((int)Math.Max(0, 24 - scene.State.Frame)))).Accepted);
        for (int current = 0; current < mode; current++)
        {
            Assert.True(scene.Apply(new(PlayerActionKind.Wait)).Accepted);
            Assert.True(scene.Apply(new(PlayerActionKind.Wait, WorldButtons: 0x100)).Accepted);
        }
        Assert.Equal(mode, WorldMapScene.MapDisplayMode(scene.State));
        const int renderWidth = 1280;
        const int renderHeight = 448;
        byte[] expected = scene.Render(renderWidth, renderHeight);
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        Assert.Empty(scene.Render(renderWidth, renderHeight, gpu));
        var actual = GpuTestDevice.Read(gpu.Target);
        int samples = 0;
        int matching = 0;
        Matrix3x2 viewport = WorldMapPresentation.ViewportTransform(mode, renderWidth, renderHeight);
        Vector2 start = Vector2.Transform(new(left, top), viewport);
        Vector2 end = Vector2.Transform(new(left + width, top + height), viewport);
        for (int y = (int)start.Y + 1; y < (int)end.Y - 1; y += 3)
        {
            for (int x = (int)start.X + 1; x < (int)end.X - 1; x += 3)
            {
                int pixel = y * renderWidth + x;
                int offset = pixel * 4;
                // Point sampling can choose either texel at a boundary after GPU interpolation rounding.
                // Compare stable interiors so detailed replacement textures do not turn those ties into color failures.
                bool stable = true;
                foreach (int neighbor in new[] { offset - 4, offset + 4, offset - renderWidth * 4, offset + renderWidth * 4 })
                {
                    for (int channel = 0; channel < 3; channel++)
                    {
                        stable &= Math.Abs(expected[offset + channel] - expected[neighbor + channel]) <= 8;
                    }
                }
                if (!stable)
                {
                    continue;
                }
                int difference = Math.Max(Math.Abs(expected[offset] - actual[pixel].R), Math.Max(Math.Abs(expected[offset + 1] - actual[pixel].G), Math.Abs(expected[offset + 2] - actual[pixel].B)));
                samples++;
                if (difference <= 8)
                {
                    matching++;
                }
            }
        }
        string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_JUNCTION_OUTPUT") ?? TestResources.ArtifactDirectory;
        Directory.CreateDirectory(output);
        string suffix = replacements ? $"mode{mode}" : $"mode{mode}-original";
        using FileStream file = File.Create(Path.Combine(output, $"world-map-gpu-{suffix}.png"));
        gpu.Target.SaveAsPng(file, renderWidth, renderHeight);
        using Microsoft.Xna.Framework.Graphics.Texture2D cpu = new(device.Device, renderWidth, renderHeight);
        cpu.SetData(expected);
        using FileStream cpuFile = File.Create(Path.Combine(output, $"world-map-cpu-{suffix}.png"));
        cpu.SaveAsPng(cpuFile, renderWidth, renderHeight);
        Assert.True(samples >= 100, $"Map mode {mode}: only {samples} stable samples were available.");
        Assert.True(matching >= samples * 0.95, $"Map mode {mode}: {matching}/{samples} stable GPU samples match the software map within eight channel levels.");
    }
}
