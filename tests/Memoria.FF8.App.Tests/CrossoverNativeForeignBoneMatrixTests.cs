using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Numerics;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class CrossoverNativeForeignBoneMatrixTests
{
    [Fact]
    public void Original_host_bone_orientation_and_local_offset_reach_the_source_effect_coordinate_space()
    {
        string? hostRoot = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(hostRoot), "Select the original host game installation.");
        using GameAssets game = new(TestResources.GameDirectory);
        var (foreign, _, _) = CrossoverNativeGuardianInvocationTests.ReadHostTarget(hostRoot!, game.ReadNativeBattleGraphicsRules());
        NativeBattlePosedActor pose = foreign.ToActor(OriginalOwner(3)).Pose!;
        Assert.NotEmpty(foreign.Geometry.BoneTransforms);
        Assert.Equal(foreign.Geometry.BoneTransforms.Length, pose.BoneMatrices.Length);
        Vector3 local = new(12, -7, 19);
        foreach (int index in Enumerable.Range(0, pose.BoneMatrices.Length))
        {
            Vector3 expected = Vector3.Transform(Vector3.Transform(local, foreign.Geometry.BoneTransforms[index]), foreign.HostToOrigin);
            NativeBattleVector actual = NativeBattleGfMatrices.Transform460840(pose.RequireBoneMatrix(index), new(12, -7, 19));
            Assert.InRange(Math.Abs(actual.X - expected.X), 0, 2);
            Assert.InRange(Math.Abs(actual.Y - expected.Y), 0, 2);
            Assert.InRange(Math.Abs(actual.Z - expected.Z), 0, 2);
        }
    }
}
