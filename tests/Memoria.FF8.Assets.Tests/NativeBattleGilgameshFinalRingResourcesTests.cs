using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleGilgameshFinalRingResourcesTests
{
    [Fact]
    public void Original_final_ring_contains_all39_vertices_and24_GT4_and_resumes_after_json()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleStaticMeshResource mesh = game.ReadNativeGilgameshFinalRingResource();
        Assert.Same(mesh, game.ReadNativeGilgameshFinalRingResource());
        Assert.Equal(0xCCFAB4u, mesh.Address);
        Assert.Equal(0x3A0, mesh.Bytes.Length);
        Assert.Equal(39u, BinaryPrimitives.ReadUInt32LittleEndian(mesh.Bytes.AsSpan().Slice(4)));
        for (int group = 0; group < 8; group++)
        {
            Assert.Equal(group == 3 ? 24 : 0, NativeBattleIceMeshVertices.Group(mesh.Bytes.AsSpan(), group).Count);
        }
        NativeBattleModulePoolTask task = new NativeBattleModulePoolTask(1, 0, null, 0x591F50, new byte[24].ToImmutableArray()).WriteWord(12, 15).WriteWord(28, 4096).WriteWord(32, 4096);
        NativeBattleActionMatrix view = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 30000, 4096);
        NativeBattleCameraProjection projection = new(null, 288, 160, 108);
        NativeBattleModulePoolTask restoredTask = NativeBattleSnapshotJson.Deserialize<NativeBattleModulePoolTask>(NativeBattleSnapshotJson.Serialize(task))!;
        NativeBattleGilgameshFinalRingStep step = NativeBattleGilgameshFinalRing.Step591F50(task, mesh, view, projection, 0);
        Assert.Equal(24, step.Batch.Attempts.Length);
        Assert.Equal(2, step.Result);
        NativeBattleStaticMeshResource restored = NativeBattleSnapshotJson.Deserialize<NativeBattleStaticMeshResource>(NativeBattleSnapshotJson.Serialize(mesh))!;
        Assert.Equal(NativeBattleSnapshotJson.Serialize(step), NativeBattleSnapshotJson.Serialize(NativeBattleGilgameshFinalRing.Step591F50(restoredTask, restored, view, projection, 0)));
        Assert.Throws<NotSupportedException>(() => NativeBattleGilgameshFinalRingResourcesReader.Read(new byte[128]));
    }
}
