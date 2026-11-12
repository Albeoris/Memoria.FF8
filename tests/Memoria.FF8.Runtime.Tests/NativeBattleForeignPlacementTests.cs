namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleForeignPlacementTests
{
    [Fact]
    public void Unchanged_host_pose_has_exact_identity_even_for_arbitrary_native_facing()
    {
        NativeBattleModelTransform transform = new(new(100, 200, 300), new(100, 200, 300), 7, 0, 4096);
        NativeBattleForeignActor actor = new(3, transform, new(0, 0, 0, 0), (_, _) => default) { Facing = 713 };
        NativeBattleGraphicsModel model = new(3, 2) { Transform = transform, Rotation12 = 0, Rotation14 = 713, Rotation16 = 0 };
        Assert.Equal(NativeBattleForeignModelGeometry.Identity, NativeBattleForeignPlacement.Delta(actor, model));
    }

    [Fact]
    public void Pure_lift_at_arbitrary_facing_is_exact_translation_without_rounding_the_base_rotation_again()
    {
        NativeBattleModelTransform transform = new(new(100, 200, 300), new(100, 200, 300), 7, 0, 4096);
        NativeBattleForeignActor actor = new(3, transform, new(0, 0, 0, 0), (_, _) => default) { Facing = 713 };
        NativeBattleGraphicsModel model = new(3, 2) { Transform = transform with { Position28 = new(100, -500, 300) }, Rotation12 = 0, Rotation14 = 713, Rotation16 = 0 };
        NativeBattleActionMatrix delta = NativeBattleForeignPlacement.Delta(actor, model);
        Assert.Equal(NativeBattleForeignModelGeometry.Identity.Rotation, delta.Rotation);
        Assert.Equal((0, -700, 0), (delta.X, delta.Y, delta.Z));
        Assert.Equal(new NativeBattleVector(1300, -1111, -500), NativeBattleGfMatrices.Transform460840(delta, new(1300, -411, -500)));
    }

    [Fact]
    public void Temporary_rotation_and_lift_move_posed_points_around_the_host_actor_root()
    {
        NativeBattleModelTransform transform = new(new(100, 200, 300), new(100, 200, 300), 0, 0, 4096);
        NativeBattleForeignActor actor = new(3, transform, new(0, 0, 0, 0), (_, _) => default);
        NativeBattleGraphicsModel model = new(3, 2) { Transform = transform with { Position28 = new(100, -500, 300) }, Rotation12 = 0, Rotation14 = 1024, Rotation16 = 0 };
        NativeBattleActionMatrix delta = NativeBattleForeignPlacement.Delta(actor, model);
        Assert.Equal(new NativeBattleVector(100, -500, 300), NativeBattleGfMatrices.Transform460840(delta, new(100, 200, 300)));
        NativeBattleActionMatrix rotation = NativeBattleGfMatrices.Euler56CE30(new(0, 1024, 0));
        NativeBattleVector offset = NativeBattleGfMatrices.Transform460840(rotation, new(50, 0, 0));
        Assert.Equal(new NativeBattleVector(unchecked((short)(100 + offset.X)), -500, unchecked((short)(300 + offset.Z))), NativeBattleGfMatrices.Transform460840(delta, new(150, 200, 300)));
    }

    [Fact]
    public void Rigid_inverse_preserves_full_translation_dwords()
    {
        NativeBattleActionMatrix matrix = NativeBattleForeignModelGeometry.Identity with { X = 100000, Y = -200000, Z = 300000 };
        NativeBattleActionMatrix inverse = NativeBattleForeignPlacement.Inverse(matrix);
        Assert.Equal((-100000, 200000, -300000), (inverse.X, inverse.Y, inverse.Z));
        NativeBattleActionMatrix roundTrip = NativeBattleGfMatrices.Compose56C2F0(matrix, inverse);
        Assert.Equal((0, 0, 0), (roundTrip.X, roundTrip.Y, roundTrip.Z));
    }
}
