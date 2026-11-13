namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattlePosedJointTests
{
    [Fact]
    public void Joint_selection_preserves_root_rejection_and_transforms_the_real_bone_origin()
    {
        NativeBattleActionMatrix world = NativeBattleForeignModelGeometry.Identity with { X = 120, Y = -40, Z = 600 };
        NativeBattlePosedActor pose = new(world, [default, default, new(10, 20, 30), new(10, 20, 70)], (bone, fraction) => bone == 1 && fraction == 0 ? new(10, 20, 30) : throw new InvalidOperationException("Only the selected actual joint should be read.")) { BoneParents = [-1, 0] };
        Assert.Equal(2, pose.RequireJointCount());
        Assert.Null(pose.Joint(0));
        Assert.Equal(new NativeBattleVector(130, -20, 630), pose.Joint(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => pose.Joint(2));
        Assert.Throws<NotSupportedException>(() => (pose with { BoneParents = [] }).RequireJointCount());
        Assert.Throws<NotSupportedException>(() => (pose with { BoneParents = [-1, -1] }).RequireJointCount());
        Assert.Throws<NotSupportedException>(() => (pose with { BoneParents = [-1, 1] }).RequireJointCount());
    }
}
