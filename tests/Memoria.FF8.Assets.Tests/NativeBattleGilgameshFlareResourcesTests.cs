using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleGilgameshFlareResourcesTests
{
    [Fact]
    public void Original_two_flare_meshes_are_authenticated_complete_and_resume_after_json()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleGilgameshFlareResources resources = game.ReadNativeGilgameshFlareResources();
        resources.Validate();
        Assert.Equal(0xCCE5DCu, resources.SourceA.Address);
        Assert.Equal(0xCCEC5Cu, resources.SourceB.Address);
        Assert.Same(resources, game.ReadNativeGilgameshFlareResources());
        NativeBattleGilgameshFlareResources restored = JsonSerializer.Deserialize<NativeBattleGilgameshFlareResources>(JsonSerializer.Serialize(resources))!;
        foreach (ushort selector in new ushort[] { 0, 1 })
        {
            NativeBattleModulePoolTask task = NativeBattleEffectTaskPool.Initialize(4, 8, 0x24, 1).Slots[0].WriteWord(12, 7).WriteWord(14, selector).WriteWord(0x1C, 4096).WriteWord(0x20, 4096);
            NativeBattleCameraProjection projection = new(null, 777, 160, 108);
            NativeBattleGilgameshFlareStep original = NativeBattleGilgameshFlare.Step591470(task, resources, projection, 0);
            NativeBattleGilgameshFlareStep reloaded = NativeBattleGilgameshFlare.Step591470(task, restored, projection, 0);
            Assert.Equal(48, original.Batch.Attempts.Length);
            Assert.Equal(2, original.Result);
            Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize(reloaded));
            Assert.Equal((ushort)777, projection.FocalLength);
        }
        Assert.Throws<NotSupportedException>(() => NativeBattleGilgameshFlareResourcesReader.Read(new byte[128]));
    }
}
