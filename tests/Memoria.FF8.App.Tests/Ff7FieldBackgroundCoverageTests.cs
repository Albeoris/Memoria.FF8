using System.Numerics;
using Memoria.FF7.Resources;
using Memoria.FF8.Rendering;
using Color = Microsoft.Xna.Framework.Color;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class Ff7FieldBackgroundCoverageTests
{
    [Theory]
    [InlineData("junonr2", 1292, 969)]
    [InlineData("junonr2", 1780, 1335)]
    [InlineData("junonr2", 1920, 1080)]
    [InlineData("junonr1", 1915, 969)]
    [InlineData("junonl2", 1915, 969)]
    [InlineData("md1stin", 1915, 969)]
    public void InstalledBackgroundPixelsDoNotChangeWithMultisampling(string name, int width, int height)
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(root), "Set MEMORIA_FF7_GAME_DIR for original field background coverage.");
        Ff7FieldResources field = Ff7FieldResources.Load(Ff7Installation.Discover(root!), name);
        Ff7FieldRenderer renderer = new(field, new Dictionary<int, Ff7FieldModelResources>(), new(50, 32000));
        Vector2[] offsets = [Vector2.Zero, new(-240.25f, 40.5f), new(-160.5f, -16.125f)];
        List<Color[]> expected = [];
        foreach (int samples in new[] { 0, 8 })
        {
            using GpuTestDevice device = new(samples);
            using GpuTriangleRenderer gpu = new(device.Device);
            for (int index = 0; index < offsets.Length; index++)
            {
                renderer.Render(gpu, width, height, new(0, offsets[index], [], new ushort[256]));
                Color[] pixels = GpuTestDevice.Read(gpu.Target);
                if (samples == 0)
                {
                    expected.Add(pixels);
                    Assert.Contains(pixels, pixel => pixel.R != 0 || pixel.G != 0 || pixel.B != 0);
                }
                else
                {
                    Assert.True(gpu.Target.MultiSampleCount > 0);
                    int differences = expected[index].Zip(pixels).Count(pair => pair.First != pair.Second);
                    Assert.True(differences == 0, $"{name} {width}x{height} camera {offsets[index]}: {differences} pixels changed at tile boundaries.");
                }
            }
        }
    }
}
