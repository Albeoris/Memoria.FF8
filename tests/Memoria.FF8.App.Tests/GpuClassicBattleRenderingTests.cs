using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Color = Microsoft.Xna.Framework.Color;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class GpuClassicBattleRenderingTests
{
    [Theory]
    [InlineData(255, 1f)]
    [InlineData(128, 1f)]
    [InlineData(128, 0.5f)]
    [InlineData(255, 0.25f)]
    [InlineData(0, 1f)]
    [InlineData(255, 0f)]
    public void Classic_actor_texture_alpha_and_opacity_match_software_rgba(byte textureAlpha, float opacity)
    {
        PixelImage texture = new(1, 1, [new Rgba32(160, 96, 32, textureAlpha)]);
        BattleMesh mesh = new([new(new Vector3(-1, -1, -2), 0), new(new Vector3(-1, 1, -2), 0), new(new Vector3(1, -1, -2), 0), new(new Vector3(1, 1, -2), 0)], [new([0, 1, 2, 3], [Vector2.Zero, Vector2.Zero, Vector2.Zero, Vector2.Zero], 0, new Rgba32(128, 128, 128))]);
        BattleModel model = new("triangle validation", [new(-1, 0)], [mesh], [texture], [new([new(Vector3.Zero, [Vector3.Zero], [Vector3.One])])], 1);
        BattleActorDraw actor = new(model, Vector3.Zero, Opacity: opacity);
        BattleStage stage = new(0, [], []);
        BattleCamera camera = new(Vector3.Zero, -Vector3.UnitZ, 1);
        BattleRenderer renderer = new(64, 64);
        byte[] software = renderer.Render(stage, [actor], camera).ToArray();
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        renderer.TriangleRenderer = gpu;
        renderer.Render(stage, [actor], camera);
        Color[] hardware = GpuTestDevice.Read(gpu.Target);
        for (int y = 8; y < 56; y++)
        {
            for (int x = 8; x < 56; x++)
            {
                int index = y * 64 + x;
                Assert.True(Math.Abs(hardware[index].R - software[index * 4]) <= 1, $"({x},{y}) GPU={hardware[index]} CPU=({software[index * 4]},{software[index * 4+1]},{software[index * 4+2]},{software[index * 4+3]}) triangles={renderer.DrawnTriangles}");
                Assert.InRange(Math.Abs(hardware[index].G - software[index * 4 + 1]), 0, 1);
                Assert.InRange(Math.Abs(hardware[index].B - software[index * 4 + 2]), 0, 1);
                Assert.InRange(Math.Abs(hardware[index].A - software[index * 4 + 3]), 0, 1);
            }
        }
        int center = 32 * 64 + 32;
        if (textureAlpha > 0 && opacity > 0)
        {
            Assert.Equal((byte)(textureAlpha * opacity), software[center * 4 + 3]);
            Assert.Equal(160, software[center * 4]);
        }
        else
        {
            Assert.Equal(255, software[center * 4 + 3]);
            Assert.Equal(17, software[center * 4]);
        }
    }
}
