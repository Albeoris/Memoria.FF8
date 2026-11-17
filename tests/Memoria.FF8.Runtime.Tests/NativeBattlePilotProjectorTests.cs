using System.Numerics;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattlePilotProjectorTests
{
    [Theory]
    [InlineData(147f, -1)]
    [InlineData(59f, -1)]
    [InlineData(147f, 239)]
    [InlineData(59f, 239)]
    [InlineData(147f, 0)]
    public void Prepared_projection_preserves_the_native_matrix_clamp_and_focal_bits(float worldZ, int focalOverride)
    {
        NativeBattleCameraProjection projection = new(new([0, -4096, 2048, 3072, 0, -1024, 512, 1024, 4096], 30, -40, -50, 4096), 193, 147, -19);
        Matrix4x4 expanded = new(0, 0.75f, 0.125f, 0, -1, 0, 0.25f, 0, 0.5f, -0.25f, 1, 0, 30, -40, -50, 1);
        Vector3 world = new(8.125f, -12.25f, worldZ);
        Vector3 camera = Vector3.Transform(world, expanded);
        ushort? selected = focalOverride < 0 ? null : (ushort)focalOverride;
        float h = selected ?? 193;
        float factor = h / Math.Max(camera.Z, h / 2);
        Vector3 expected = new(camera.X * factor * 8 + 147 * 8, camera.Y * factor * 8 - 19 * 8, camera.Z);

        NativeBattlePilotProjector projector = NativeBattlePilotVisualGeometry.PrepareProjector(projection);
        AssertBits(expected, projector.Project(world, selected));
        AssertBits(expected, NativeBattlePilotVisualGeometry.Project(world, selected is ushort focal ? projection with { FocalLength = focal } : projection));
    }

    [Fact]
    public void Unspecified_offsets_use_native_centres_and_the_focal_override_is_explicit()
    {
        NativeBattleActionMatrix view = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
        NativeBattlePilotProjector projector = NativeBattlePilotVisualGeometry.PrepareProjector(new(view, null, null, null));
        Vector3 world = new(15, -5, 100);

        Assert.Throws<InvalidOperationException>(() => projector.Project(world));
        AssertBits(new(1280, 864, 100), projector.Project(world, 0));
    }

    private static void AssertBits(Vector3 expected, Vector3 actual)
    {
        Assert.Equal(BitConverter.SingleToInt32Bits(expected.X), BitConverter.SingleToInt32Bits(actual.X));
        Assert.Equal(BitConverter.SingleToInt32Bits(expected.Y), BitConverter.SingleToInt32Bits(actual.Y));
        Assert.Equal(BitConverter.SingleToInt32Bits(expected.Z), BitConverter.SingleToInt32Bits(actual.Z));
    }
}
