using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class JunctionWorldMapGpuTests
{
    [Theory]
    [InlineData(1, 230, 140, 65, 70)]
    [InlineData(2, 186, 124, 128, 96)]
    [InlineData(3, 32, 16, 256, 192)]
    public void NativeMapModesRenderSelectedTexturesOnTheGpu(byte mode, int left, int top, int width, int height)
    {
        GameSession session = new(GameSession.Load(TestResources.FixturePath("junction-world-map-source")));
        string archive = TestResources.FixturePath("junction-world-textures");
        string mods = Directory.GetParent(Path.GetDirectoryName(archive)!)!.FullName;
        using GameResourceHost host = new(TestResources.GameDirectory, Path.Combine(mods, "test-application"), watch: false, modsDirectory: mods);
        using WorldMapScene scene = new(session, TestResources.GameDirectory, host.Resources);
        Assert.True(scene.Apply(new(PlayerActionKind.Wait, Frames: checked((int)Math.Max(0, 24 - scene.State.Frame)))).Accepted);
        for (int current = 0; current < mode; current++)
        {
            Assert.True(scene.Apply(new(PlayerActionKind.Wait)).Accepted);
            Assert.True(scene.Apply(new(PlayerActionKind.Wait, WorldButtons: 0x100)).Accepted);
        }
        Assert.Equal(mode, WorldMapScene.MapDisplayMode(scene.State));
        byte[] expected = scene.Render(640, 448);
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        Assert.Empty(scene.Render(640, 448, gpu));
        var actual = GpuTestDevice.Read(gpu.Target);
        int samples = 0;
        int matching = 0;
        for (int y = top * 2 + 1; y < (top + height) * 2 - 1; y += 3)
        {
            for (int x = left * 2 + 1; x < (left + width) * 2 - 1; x += 3)
            {
                int pixel = y * 640 + x;
                int offset = pixel * 4;
                int difference = Math.Max(Math.Abs(expected[offset] - actual[pixel].R), Math.Max(Math.Abs(expected[offset + 1] - actual[pixel].G), Math.Abs(expected[offset + 2] - actual[pixel].B)));
                samples++;
                if (difference <= 8)
                {
                    matching++;
                }
            }
        }
        Assert.True(matching >= samples * 0.95, $"Map mode {mode}: {matching}/{samples} GPU samples match the software map within eight channel levels.");
        string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_JUNCTION_OUTPUT") ?? TestResources.ArtifactDirectory;
        Directory.CreateDirectory(output);
        using FileStream file = File.Create(Path.Combine(output, $"world-map-gpu-mode{mode}.png"));
        gpu.Target.SaveAsPng(file, 640, 448);
    }
}
