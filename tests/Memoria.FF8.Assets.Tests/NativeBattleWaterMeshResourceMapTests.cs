using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleWaterMeshResourceMapTests
{
    [Fact]
    public void Original_mesh_resource_map_is_supplied_raw_and_survives_independent_json()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Original resources are optional.");
        using GameAssets game = new(directory);
        NativeBattleEffectHeapAllocation map = game.ReadNativeWaterMeshResourceMap();
        Assert.Same(map, game.ReadNativeWaterMeshResourceMap());
        Assert.Equal(16, map.Data.Length);
        Assert.All(map.Data, value => Assert.Equal((byte)0, value));
        NativeBattleEffectHeapAllocation changed = map with { Data = map.Data.SetItem(5, 1) };
        NativeBattleEffectHeapAllocation restored = JsonSerializer.Deserialize<NativeBattleEffectHeapAllocation>(JsonSerializer.Serialize(changed))!;
        Assert.Equal(changed.Data.ToArray(), restored.Data.ToArray());
        Assert.Equal((byte)0, map.Data[5]);
        Assert.Equal((byte)1, restored.Data[5]);
    }
}
