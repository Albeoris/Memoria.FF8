using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleElectrocute275BindingTests
{
    [Fact]
    public void Original_catalog_selects_Electrocute_and_actual_initializer_global()
    {
        using GameAssets game = Open();
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([275])[274];
        Assert.Equal((ushort)275, binding.Effect);
        Assert.Equal(0x687B40u, binding.Entry);
        Assert.Equal(0x687B50u, binding.Initializer);
        Assert.False(binding.InitializerIsRet);
        Assert.Equal(new NativeBattleEffectInitializerResource("mag125.tim", 0x2508350), binding.InitializerResource);
        Assert.Null(binding.InitializerResources);
        Assert.Equal("mag125.tim", binding.InitializerAsset!.Name);
        Assert.Equal(166944, binding.InitializerAsset.Data.Length);
        Assert.Same(game.ReadNativeElectrocute275Resources(), binding.Electrocute275Resources);
    }

    [Fact]
    public void Binding_JSON_keeps_original_ACT_camera_binding_sprites_and_procedural_tables()
    {
        using GameAssets game = Open();
        NativeBattleEffectBinding source = game.ReadNativeEffectCatalogForEffects([275])[274];
        string json = JsonSerializer.Serialize(source);
        NativeBattleEffectBinding restored = JsonSerializer.Deserialize<NativeBattleEffectBinding>(json)!;
        Assert.Equal(json, JsonSerializer.Serialize(restored));
        NativeBattleElectrocute275Resources resources = restored.Electrocute275Resources!;
        resources.Validate();
        Assert.Equal("FF8_EN:10DC0E8", resources.Camera.Identity);
        Assert.Equal(0x1E8, resources.Camera.Data.Length);
        Assert.Equal("FF8_EN:10DA574", resources.CasterDat.Identity);
        Assert.Equal(0x1B74, resources.CasterDat.Data.Length);
        Assert.Equal(0x10D9B64u, resources.Act.Address);
        Assert.Equal(0xA10, resources.Act.Data.Length);
        Assert.Equal(new uint[] { 0x10D9934, 0x10D9A88 }, new uint[] { resources.SpriteA.Address, resources.SpriteB.Address });
        Assert.Equal(new int[] { 0x154, 0xDC }, new int[] { resources.SpriteA.Bytes.Length, resources.SpriteB.Bytes.Length });
        Assert.Equal(16384, resources.DistanceTable.Length);
        Assert.Equal(4096, resources.WaveTable.Length);
        Assert.Equal(new byte[] { 25, 26, 11, 12, 8, 10 }, resources.ColorIndices.ToArray());
        Assert.Equal(0x6A8, resources.BonePaths.Length);
        Assert.Equal(0x00C8DCFAu, resources.ActorColor);
        Assert.Equal(0x61AE8u, resources.Sound);
        Assert.Equal(4096u, resources.InitialFlashThreshold);
        Assert.Equal(source.InitializerAsset!.Data.ToArray(), restored.InitializerAsset!.Data.ToArray());
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original resources for the Electrocute binding.");
        return new(directory!);
    }
}
