using System.Numerics;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class CrossoverForeignPlacementTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1024)]
    [InlineData(3072)]
    public void Native_world_delta_is_conjugated_through_the_active_sources_host_placement(int yaw)
    {
        CrossoverGunbladeHostPlacement placement = new(new(1300, -200, -900), yaw);
        NativeBattleActionMatrix delta = new([0, 0, 4096, 0, 4096, 0, -4096, 0, 0], 120, -430, 80, 4096);
        Matrix4x4 hostDelta = placement.ResolvePresentationTransform(delta);
        Vector3 original = new(20, -300, 2400);
        Vector3 host = Vector3.Transform(original, placement.Matrix);
        NativeBattleVector changed = NativeBattleGfMatrices.Transform460840(delta, new(20, -300, 2400));
        Vector3 expected = Vector3.Transform(new(changed.X, changed.Y, changed.Z), placement.Matrix);
        Vector3 actual = Vector3.Transform(host, hostDelta);
        Assert.InRange(Vector3.Distance(expected, actual), 0, 0.001f);
        Matrix4x4 guestPlacement = Matrix4x4.CreateRotationY(0.4f) * Matrix4x4.CreateTranslation(-100, 50, 700);
        Vector3 local = new(10, -40, 70);
        Assert.InRange(Vector3.Distance(Vector3.Transform(Vector3.Transform(local, guestPlacement), hostDelta), Vector3.Transform(local, guestPlacement * hostDelta)), 0, 0.001f);
    }

    [Fact]
    public void Identity_delta_keeps_the_base_visual_placement_exactly_unchanged()
    {
        CrossoverGunbladeHostPlacement placement = new(new(1300, -200, -900), 731);
        Matrix4x4 before = placement.Matrix;
        Assert.Equal(Matrix4x4.Identity, placement.ResolvePresentationTransform(NativeBattleForeignModelGeometry.Identity));
        Assert.Equal(before, placement.Matrix);
    }
}
