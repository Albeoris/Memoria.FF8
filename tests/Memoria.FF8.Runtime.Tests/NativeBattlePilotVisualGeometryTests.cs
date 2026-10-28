using System.Numerics;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattlePilotVisualGeometryTests
{
    [Theory]
    [InlineData(0f)]
    [InlineData(0.25f)]
    [InlineData(0.5f)]
    [InlineData(0.75f)]
    [InlineData(1f)]
    public void Continuous_world_geometry_has_distinct_fractional_positions(float alpha)
    {
        NativeBattlePilotVisualIdentity identity = new(2, 1, 2, 3, 4);
        NativeBattlePilotVisualGeometry earlier = new(identity, Vector3.Zero, Vector3.UnitX, Vector3.UnitY, default, 3);
        NativeBattlePilotVisualGeometry later = earlier with { A = new(100, 200, 300) };
        Assert.Equal(new Vector3(100, 200, 300) * alpha, NativeBattlePilotVisualGeometry.Sample(earlier, later, alpha).A);
    }

    [Fact]
    public void Reused_slot_and_discrete_thunder_geometry_hold_the_previous_endpoint()
    {
        NativeBattlePilotVisualGeometry earlier = new(new(3, 1, 2, 3, 4), Vector3.Zero, Vector3.UnitX, Vector3.UnitY, default, 3, false);
        NativeBattlePilotVisualGeometry later = earlier with { A = Vector3.One };
        Assert.Equal(earlier, NativeBattlePilotVisualGeometry.Sample(earlier, later, 0.5f));
        Assert.Equal(later, NativeBattlePilotVisualGeometry.Sample(earlier, later, 1));
        earlier = earlier with { Continuous = true };
        later = later with { Continuous = true, Identity = later.Identity with { Generation = 5 } };
        Assert.Equal(earlier, NativeBattlePilotVisualGeometry.Sample(earlier, later, 0.5f));
    }

    [Fact]
    public void Exact_preprojection_geometry_roundtrips_through_a_moving_view()
    {
        NativeBattleActionMatrix view = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 100, -200, 1000, 4096);
        NativeBattleCameraProjection projection = new(view, 200, 160, 108);
        Vector3 world = NativeBattlePilotVisualGeometry.CameraToWorld(new(300, -400, 2000), projection);
        Assert.Equal(new Vector3(200, -200, 1000), world);
        Assert.Equal(new Vector3(1520, 544, 2000), NativeBattlePilotVisualGeometry.Project(world, projection));
    }

    [Fact]
    public void Fire_holds_authored_rectangle_while_particle_translation_and_scale_move()
    {
        NativeBattlePilotSpriteGeometry sprite = new(new(-10, -20, 0), new(10, -20, 0), new(-10, 20, 0), new(10, 20, 0), Vector3.Zero, Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ);
        NativeBattlePilotVisualGeometry earlier = new(new(2, 1, 2, 3, 4), sprite.LocalA, sprite.LocalB, sprite.LocalC, sprite.LocalD, 4) { Sprite = sprite };
        NativeBattlePilotSpriteGeometry nextSprite = sprite with { LocalA = new(-100, -200, 0), Origin = new(100, 0, 0), AxisX = Vector3.UnitX * 3, AxisY = Vector3.UnitY * 3 };
        NativeBattlePilotVisualGeometry later = earlier with { A = new(-200, -600, 0), Sprite = nextSprite };
        Assert.Equal(new Vector3(30, -40, 0), NativeBattlePilotVisualGeometry.SampleCorner(earlier, later, 0, 0.5f));
        Assert.Equal(earlier.A, NativeBattlePilotVisualGeometry.SampleCorner(earlier, later, 0, 0));
        Assert.Equal(later.A, NativeBattlePilotVisualGeometry.SampleCorner(earlier, later, 0, 1));
    }

    [Fact]
    public void Captured_sprite_basis_keeps_fractional_scale_and_rotation_independent_of_translation()
    {
        NativeBattleActionMatrix view = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
        NativeBattleCameraProjection projection = new(view, 200, 160, 108);
        NativeBattleActionMatrix main = new([0, -2048, 0, 2048, 0, 0, 0, 0, 2048], 16777216, -16777216, 1000, 4096);
        byte[] quad = new byte[20];
        quad[0] = 2;
        quad[2] = 3;
        NativeBattlePilotSpriteGeometry captured = NativeBattlePilotSpriteGeometry.Capture(main, quad, 16, 24, projection);
        Assert.Equal(new Vector3(0, 0.5f, 0), captured.AxisX);
        Assert.Equal(new Vector3(-0.5f, 0, 0), captured.AxisY);
        Assert.Equal(new Vector3(0, 0, 0.5f), captured.AxisZ);
        Assert.NotEqual(captured.LocalA, captured.LocalD);
        Assert.Equal(new Vector3(16777216, -16777216, 1000), captured.Origin);
    }

    [Fact]
    public void Camera_endpoint_samples_preserve_exact_native_projection_objects()
    {
        NativeBattleCameraPose a = new(new(0, 0, -1000), new(0, 0, 0), 200, 0);
        NativeBattleCameraPose b = a with { Eye = new(200, 0, -1000) };
        NativeBattleCameraProjection earlier = new(NativeBattlePoseMatrices.CameraView(a), 200, 160, 108);
        NativeBattleCameraProjection later = new(NativeBattlePoseMatrices.CameraView(b), 200, 160, 108);
        Assert.Same(earlier, NativeBattlePilotVisualGeometry.SampleProjection(earlier, later, a, b, 0));
        Assert.Same(later, NativeBattlePilotVisualGeometry.SampleProjection(earlier, later, a, b, 1));
        Assert.Equal((short)100, NativeBattlePilotVisualGeometry.SampleCamera(a, b, 0.5f).Eye.X);
    }
}
