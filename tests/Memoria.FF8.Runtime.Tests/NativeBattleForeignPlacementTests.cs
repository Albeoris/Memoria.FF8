using static Memoria.FF8.Tests.NativeBattleTypedFixture;
namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleForeignPlacementTests
{
    [Fact]
    public void Relative_scale_uses_the_native_scale_product_and_root_zero_pivot_even_at_zero_axis_scale()
    {
        NativeBattleModelDatSource body = NativeBattleModelResourceCapacityTests.Source(false, 8);
        NativeBattlePoseSource source = new(body.Bytes.Slice(body.Section(0), body.Section(1) - body.Section(0)), body.Bytes.Slice(body.Section(2), body.Section(3) - body.Section(2)));
        NativeBattleModelTransform transform = new(new(100, 200, 300), new(100, 200, 300), 0, 0, 4096);
        NativeBattleVector root = new(40, 50, 60);
        NativeBattleActionMatrix identity = NativeBattleForeignModelGeometry.Identity;
        NativeBattleActionMatrix original = NativeBattleForeignPlacement.World(transform, 0, 0, 0);
        NativeBattleForeignActor actor = new(OriginalOwner(3), transform, new(0, 0, 0, 0), (_, _) => default) { Pose = new(original, [root, new(40, 50, 100)], (_, _) => root) };
        NativeBattleGraphicsModel model = new(2)
        {
            Transform = transform with { Scale154 = 2048, Scale148 = new(4096, 8192, 0) },
            Rotation12 = 0,
            Rotation14 = 0,
            Rotation16 = 0,
            ScaleVectorAddress48 = 0,
            SequenceState = new(),
            CurrentBodyBoneMatrices = new(NativeBattleBodyBoneMatrices.Identity(source), [identity with { X = root.X, Y = root.Y, Z = root.Z }])
        };
        NativeBattleActionMatrix native = NativeBattlePoseMatrices.PublishGeometryWorldMatrix502AB0(model, source);
        NativeBattleActionMatrix delta = NativeBattleForeignPlacement.Delta(actor, model);
        NativeBattleActionMatrix rebased = NativeBattleGfMatrices.Compose56C2F0(delta, original);
        Assert.Equal(native.Rotation.ToArray(), rebased.Rotation.ToArray());
        Assert.Equal((native.X, native.Y, native.Z, native.Scale), (rebased.X, rebased.Y, rebased.Z, rebased.Scale));
        Assert.Equal(new NativeBattleVector(140, 250, 360), NativeBattleGfMatrices.Transform460840(delta, new(140, 250, 360)));
        Assert.Equal(new NativeBattleVector(190, 350, 360), NativeBattleGfMatrices.Transform460840(delta, new(240, 350, 460)));
    }

    [Fact]
    public void Zero_scale_and_restoration_preserve_the_original_rigid_inverse_and_return_exact_identity()
    {
        NativeBattleModelTransform transform = new(new(100, 200, 300), new(100, 200, 300), 7, 0, 4096);
        NativeBattleActionMatrix original = NativeBattleForeignPlacement.World(transform, 0, 713, 0);
        NativeBattleForeignActor actor = new(OriginalOwner(3), transform, new(0, 0, 0, 0), (_, _) => default) { Facing = 713, Pose = new(original, [new(40, 50, 60), new(40, 50, 100)], (_, _) => new(40, 50, 60)) };
        NativeBattleGraphicsModel model = new(2) { Transform = transform with { Scale154 = 0 }, Rotation12 = 0, Rotation14 = 713, Rotation16 = 0 };
        NativeBattleActionMatrix collapsed = NativeBattleForeignPlacement.Delta(actor, model);
        Assert.All(collapsed.Rotation, value => Assert.Equal((short)0, value));
        NativeBattleActionMatrix unscaled = NativeBattleForeignPlacement.World(model.Transform, 0, 713, 0);
        Assert.Equal(original.Rotation.ToArray(), unscaled.Rotation.ToArray());
        Assert.Equal((original.X, original.Y, original.Z), (unscaled.X, unscaled.Y, unscaled.Z));
        Assert.Equal(NativeBattleForeignModelGeometry.Identity, NativeBattleForeignPlacement.Delta(actor, model with { Transform = transform }));
    }

    [Fact]
    public void Unchanged_host_pose_has_exact_identity_even_for_arbitrary_native_facing()
    {
        NativeBattleModelTransform transform = new(new(100, 200, 300), new(100, 200, 300), 7, 0, 4096);
        NativeBattleForeignActor actor = new(OriginalOwner(3), transform, new(0, 0, 0, 0), (_, _) => default) { Facing = 713 };
        NativeBattleGraphicsModel model = new(2) { Transform = transform, Rotation12 = 0, Rotation14 = 713, Rotation16 = 0 };
        Assert.Equal(NativeBattleForeignModelGeometry.Identity, NativeBattleForeignPlacement.Delta(actor, model));
    }

    [Fact]
    public void Pure_lift_at_arbitrary_facing_is_exact_translation_without_rounding_the_base_rotation_again()
    {
        NativeBattleModelTransform transform = new(new(100, 200, 300), new(100, 200, 300), 7, 0, 4096);
        NativeBattleForeignActor actor = new(OriginalOwner(3), transform, new(0, 0, 0, 0), (_, _) => default) { Facing = 713 };
        NativeBattleGraphicsModel model = new(2) { Transform = transform with { Position28 = new(100, -500, 300) }, Rotation12 = 0, Rotation14 = 713, Rotation16 = 0 };
        NativeBattleActionMatrix delta = NativeBattleForeignPlacement.Delta(actor, model);
        Assert.Equal(NativeBattleForeignModelGeometry.Identity.Rotation, delta.Rotation);
        Assert.Equal((0, -700, 0), (delta.X, delta.Y, delta.Z));
        Assert.Equal(new NativeBattleVector(1300, -1111, -500), NativeBattleGfMatrices.Transform460840(delta, new(1300, -411, -500)));
    }

    [Fact]
    public void Temporary_rotation_and_lift_move_posed_points_around_the_host_actor_root()
    {
        NativeBattleModelTransform transform = new(new(100, 200, 300), new(100, 200, 300), 0, 0, 4096);
        NativeBattleForeignActor actor = new(OriginalOwner(3), transform, new(0, 0, 0, 0), (_, _) => default);
        NativeBattleGraphicsModel model = new(2) { Transform = transform with { Position28 = new(100, -500, 300) }, Rotation12 = 0, Rotation14 = 1024, Rotation16 = 0 };
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
