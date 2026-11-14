using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattlePosedBoneMatrixTests
{
    [Fact]
    public void Bone_local_vector_uses_actual_orientation_and_current_world_placement()
    {
        NativeBattleActionMatrix world = NativeBattleForeignModelGeometry.Identity with { X = 100, Y = 200, Z = 300 };
        NativeBattleActionMatrix bone = new([0, -4096, 0, 4096, 0, 0, 0, 0, 4096], 10, 20, 30, 4096);
        NativeBattlePosedActor pose = new(world, [], (_, _) => throw new InvalidOperationException()) { BoneParents = [-1], BoneMatrices = [bone] };
        Assert.Equal(new NativeBattleVector(107, 222, 334), NativeBattleGfMatrices.Transform460840(pose.RequireBoneMatrix(0), new(2, 3, 4)));
        pose = pose with { World = world with { X = 1000 } };
        Assert.Equal(new NativeBattleVector(1007, 222, 334), NativeBattleGfMatrices.Transform460840(pose.RequireBoneMatrix(0), new(2, 3, 4)));
    }

    [Fact]
    public void Missing_or_out_of_range_bone_orientation_is_explicitly_unavailable()
    {
        NativeBattlePosedActor pose = new(NativeBattleForeignModelGeometry.Identity, [], (_, _) => default) { BoneParents = [-1] };
        Assert.Throws<NotSupportedException>(() => pose.RequireBoneMatrix(0));
        pose = pose with { BoneMatrices = [NativeBattleForeignModelGeometry.Identity] };
        Assert.Throws<NotSupportedException>(() => pose.RequireBoneMatrix(1));
    }
}
