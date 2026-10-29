using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleStageSourceTests
{
    [Theory]
    [InlineData(20, 2, 1)]
    [InlineData(26, 1, 2)]
    [InlineData(85, 1, 4)]
    public void InstalledStageObjectsKeepTheirOriginalMultipleVertexGroupsAndBoneSelection(int stageId, int groupCount, short firstBone)
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        BattleStage stage = assets.LoadStage(stageId);
        NativeBattleStageSource source = stage.NativeSource!;
        Assert.Contains(stage.Meshes, mesh => mesh.NativeBoneGroups is { } bones && bones.Length == groupCount && bones[0] == firstBone);
        NativeBattleStagePhysicalSource physical = source.PhysicalSource!;
        physical.Validate();
        foreach (IGrouping<int?, BattleMesh> group in stage.Meshes.GroupBy(mesh => mesh.NativeStageGroup))
        {
            int index = group.Key!.Value;
            uint address = physical.GroupGeometryAddress(index)!.Value;
            int length = physical.GroupGeometryLength(index)!.Value;
            byte[] bytes = physical.Prefix.AsSpan().Slice(checked((int)(address - physical.Address)), length).ToArray();
            NativeBattleRawMesh[] nativeMeshes = NativeBattleRawGeometryReader.Read(bytes, source.Groups[index]!.BoneCount).ToArray();
            BattleMesh[] decoded = group.ToArray();
            Assert.Equal(nativeMeshes.Length, decoded.Length);
            for (int mesh = 0; mesh < decoded.Length; mesh++)
            {
                Assert.Equal(nativeMeshes[mesh].BoneGroups!.Value.ToArray(), decoded[mesh].NativeBoneGroups!.Value.ToArray());
                Assert.Equal(nativeMeshes[mesh].Vertices.Select(vertex => (vertex.Position, vertex.Bone)), decoded[mesh].Vertices.Select(vertex => (vertex.NativePosition!.Value, checked((ushort)vertex.Bone))));
                Assert.Equal(nativeMeshes[mesh].Polygons.Select(polygon => Convert.ToHexString(polygon.Bytes.AsSpan())), decoded[mesh].Faces.Select(face => Convert.ToHexString(face.NativePolygon.AsSpan())));
            }
        }
        NativeBattleStageState state = NativeBattleStageState.FromColdProcess().Reset506C50().Initialize500EA0(source);
        state.Validate();
        NativeBattleStageState restored = NativeBattleSnapshotJson.Deserialize<NativeBattleStageState>(NativeBattleSnapshotJson.Serialize(state))!;
        NativeBattleActionMatrix camera = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
        Assert.Equal(NativeBattleSnapshotJson.Serialize(state.Step500FD0(camera)), NativeBattleSnapshotJson.Serialize(restored.Step500FD0(camera)));
    }

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
