using System.Numerics;
using System.Text.Json;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class JunctionMapArtworkGpuTests
{
    [Fact]
    public void InstalledMapPageRendersThroughTheGpuWithoutAdvancingTheStandaloneState()
    {
        string archive = TestResources.FixturePath("junction-world-map");
        string? junction = Path.GetDirectoryName(archive);
        while (junction is not null && !Path.GetFileName(junction).Equals("Junction", StringComparison.OrdinalIgnoreCase))
        {
            junction = Path.GetDirectoryName(junction);
        }
        Assert.NotNull(junction);
        string mods = Directory.GetParent(junction)!.FullName;
        GameSession session = new(GameSession.Load(TestResources.FixturePath("junction-map-presentation")));
        using GameResourceHost host = new(TestResources.GameDirectory, watch: false, modsDirectory: mods);
        using WorldMapScene scene = new(session, TestResources.GameDirectory, host.Resources);
        Assert.Equal(2, WorldMapScene.MapDisplayMode(scene.State));
        string before = JsonSerializer.Serialize(session.State);
        const int width = 1280;
        const int height = 720;
        byte[] cpu = scene.Render(width, height);
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        Assert.Empty(scene.Render(width, height, gpu));
        var pixels = GpuTestDevice.Read(gpu.Target);
        Matrix3x2 viewport = WorldMapPresentation.ViewportTransform(2, width, height);
        Vector2 start = Vector2.Transform(new(186, 124), viewport);
        Vector2 end = Vector2.Transform(new(314, 220), viewport);
        int samples = 0;
        int matching = 0;
        for (int y = (int)start.Y + 1; y < (int)end.Y - 1; y += 3)
        {
            for (int x = (int)start.X + 1; x < (int)end.X - 1; x += 3)
            {
                int pixel = y * width + x;
                int offset = pixel * 4;
                bool stable = true;
                foreach (int neighbor in new[] { offset - 4, offset + 4, offset - width * 4, offset + width * 4 })
                {
                    for (int channel = 0; channel < 3; channel++)
                    {
                        stable &= Math.Abs(cpu[offset + channel] - cpu[neighbor + channel]) <= 8;
                    }
                }
                if (!stable)
                {
                    continue;
                }
                samples++;
                int difference = Math.Max(Math.Abs(cpu[offset] - pixels[pixel].R), Math.Max(Math.Abs(cpu[offset + 1] - pixels[pixel].G), Math.Abs(cpu[offset + 2] - pixels[pixel].B)));
                if (difference <= 8)
                {
                    matching++;
                }
            }
        }
        Assert.True(samples >= 100);
        Assert.True(matching >= samples * 0.95, $"{matching}/{samples} stable map pixels match the CPU presentation.");
        Assert.Equal(before, JsonSerializer.Serialize(session.State));
        string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_JUNCTION_OUTPUT") ?? TestResources.ArtifactDirectory;
        Directory.CreateDirectory(output);
        using FileStream capture = File.Create(Path.Combine(output, "native-map-page-gpu.png"));
        gpu.Target.SaveAsPng(capture, width, height);
    }
}
