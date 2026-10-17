using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleStageSourceTests
{
    [Fact]
    public void InstalledStage31RetainsItsOriginalFourSkeletonsAndAnimationSources()
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        BattleStage stage = assets.LoadStage(31);
        NativeBattleStageSource source = Assert.IsType<NativeBattleStageSource>(stage.NativeSource);
        Assert.Equal((short)16, source.RotationSpeed);
        Assert.Equal(4, source.Groups.Length);
        NativeBattleStageState state = NativeBattleStageState.FromColdProcess().Reset506C50().Initialize500EA0(source);
        state.Validate();
        for (int group = 0; group < 4; group++)
        {
            NativeBattlePoseSource pose = Assert.IsType<NativeBattlePoseSource>(source.Groups[group]);
            pose.Validate();
            Assert.Equal(pose.BoneCount, state.Groups[group].Matrices.Length);
            Assert.True(state.Groups[group].Counter > 0);
            Assert.All(stage.Meshes.Where(mesh => mesh.NativeStageGroup == group).SelectMany(mesh => mesh.Vertices), vertex => Assert.InRange(vertex.Bone, 0, pose.BoneCount - 1));
        }
        NativeBattleActionMatrix camera = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
        state = state.Step500FD0(camera);
        Assert.Equal(16, state.Rotation);
        Assert.False(source.Texture.Data.IsEmpty);
    }
}
