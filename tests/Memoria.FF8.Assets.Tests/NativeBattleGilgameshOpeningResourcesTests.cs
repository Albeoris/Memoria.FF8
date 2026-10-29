using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleGilgameshOpeningResourcesTests
{
    [Fact]
    public void Original_opening_binds_five_complete_meshes_and_each_finite_leaf_resumes_after_json()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleGilgameshOpeningResources resources = game.ReadNativeGilgameshOpeningResources();
        resources.Validate();
        Assert.Same(resources, game.ReadNativeGilgameshOpeningResources());
        Assert.Equal(new uint[] { 0xCC8F5C, 0xCCB92C, 0xCCC434, 0xCCA14C, 0xCCAD3C }, new[] { resources.BaseMesh.Address, resources.RingA.Address, resources.RingB.Address, resources.SpireA.Address, resources.SpireB.Address });
        Assert.Equal(new[] { 0x11F0, 0xB08, 0x6E8, 0xBF0, 0xBF0 }, new[] { resources.BaseMesh.Bytes.Length, resources.RingA.Bytes.Length, resources.RingB.Bytes.Length, resources.SpireA.Bytes.Length, resources.SpireB.Bytes.Length });
        NativeBattleGilgameshOpeningResources restored = NativeBattleSnapshotJson.Deserialize<NativeBattleGilgameshOpeningResources>(NativeBattleSnapshotJson.Serialize(resources))!;
        NativeBattleGilgameshOpeningInitialization initial = NativeBattleGilgameshOpening.Initialize592D00(NativeBattleEffectTaskPool.Initialize(0x21FFD58, 0x21FFD68, 36, 100), new(1));
        NativeBattleActionMatrix view = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 30000, 4096);
        NativeBattleCameraProjection projection = new(null, 288, 160, 108);
        foreach (NativeBattleModulePoolTask original in initial.Pool.Slots.Take(5))
        {
            NativeBattleModulePoolTask task = original.WriteWord(12, 67);
            NativeBattleModulePoolTask restoredTask = NativeBattleSnapshotJson.Deserialize<NativeBattleModulePoolTask>(NativeBattleSnapshotJson.Serialize(task))!;
            NativeBattleGilgameshOpeningStep step = NativeBattleGilgameshOpening.Step(task, resources, view, projection, 0);
            Assert.Equal(2, step.Result);
            Assert.NotEmpty(step.Batch.Attempts);
            Assert.Equal(NativeBattleSnapshotJson.Serialize(step), NativeBattleSnapshotJson.Serialize(NativeBattleGilgameshOpening.Step(restoredTask, restored, view, projection, 0)));
        }
        Assert.Throws<NotSupportedException>(() => NativeBattleGilgameshOpeningResourcesReader.Read(new byte[128]));
    }
}
