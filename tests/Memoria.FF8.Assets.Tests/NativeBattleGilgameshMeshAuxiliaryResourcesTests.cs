using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleGilgameshMeshAuxiliaryResourcesTests
{
    [Fact]
    public void Original_auxiliary_meshes_and_three_signed_initializer_rows_are_authenticated()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleGilgameshMeshAuxiliaryResources resources = game.ReadNativeGilgameshMeshAuxiliaryResources();
        resources.Validate();
        Assert.Same(resources, game.ReadNativeGilgameshMeshAuxiliaryResources());
        Assert.Equal(0xCC78ECu, resources.RisingMesh.Address);
        Assert.Equal(0xCC7A94u, resources.DelayedMesh.Address);
        Assert.Equal(0x1A8, resources.RisingMesh.Bytes.Length);
        Assert.Equal(0xCE8, resources.DelayedMesh.Bytes.Length);
        Assert.Equal(new NativeBattleGilgameshMeshAuxiliaryRow[] { new(14, -550, 4608, 3072), new(28, -1000, 3072, 2560), new(36, -1300, 3584, 2944) }, resources.Rows.ToArray());
        Assert.Throws<NotSupportedException>(() => NativeBattleGilgameshMeshAuxiliaryResourcesReader.Read(new byte[128]));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Original_initialized_mesh_children_complete_and_json_resume_is_identical(bool delayed)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleGilgameshMeshAuxiliaryResources resources = game.ReadNativeGilgameshMeshAuxiliaryResources();
        NativeBattleGilgameshMeshAuxiliaryResources restoredResources = NativeBattleSnapshotJson.Deserialize<NativeBattleGilgameshMeshAuxiliaryResources>(NativeBattleSnapshotJson.Serialize(resources))!;
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x21FFD58, 0x21FFD68, 36, 100);
        NativeBattleGilgameshMeshAuxiliaryInitialization initialized = delayed ? NativeBattleGilgameshMeshAuxiliaries.Initialize594160(pool, new(1), resources) : NativeBattleGilgameshMeshAuxiliaries.Initialize593550(pool, new(1));
        NativeBattleActionMatrix view = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 30000, 4096);
        NativeBattleCameraProjection projection = new(null, 288, 160, 108);
        foreach (NativeBattleModulePoolTask task in initialized.Pool.Slots.Where(task => (task.Flags & 1) != 0))
        {
            NativeBattleModulePoolTask current = task;
            NativeBattleGilgameshMeshAuxiliaryStep? step = null;
            for (int frame = 0; frame < 60; frame++)
            {
                NativeBattleModulePoolTask restored = NativeBattleSnapshotJson.Deserialize<NativeBattleModulePoolTask>(NativeBattleSnapshotJson.Serialize(current))!;
                step = NativeBattleGilgameshMeshAuxiliaries.Step(current, resources, view, projection, 0);
                Assert.Equal(NativeBattleSnapshotJson.Serialize(step), NativeBattleSnapshotJson.Serialize(NativeBattleGilgameshMeshAuxiliaries.Step(restored, restoredResources, view, projection, 0)));
                current = step.UpdatedTask;
                if (step.Result == 2)
                {
                    break;
                }
            }
            Assert.NotNull(step);
            Assert.Equal(2, step.Result);
            Assert.Equal(delayed ? (ushort)14 : (ushort)30, current.ReadWord(12));
            Assert.Equal(delayed ? 96 : 10, step.Batch.Attempts.Length);
        }
    }
}
