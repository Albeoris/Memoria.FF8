using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleUltraWaves184BindingTests
{
    [Fact]
    public void Original_catalog_selects_Blue_Ultra_Waves_and_actual_initializer_global()
    {
        using GameAssets game = Open();
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([184])[183];
        Assert.Equal((ushort)184, binding.Effect);
        Assert.Equal(0x5C7FC0u, binding.Entry);
        Assert.Equal(0x5C7FA0u, binding.Initializer);
        Assert.False(binding.InitializerIsRet);
        Assert.Equal(new NativeBattleEffectInitializerResource("mag153.tim", 0x22D7DE8), binding.InitializerResource);
        Assert.Null(binding.InitializerResources);
        Assert.Equal("mag153.tim", binding.InitializerAsset!.Name);
        Assert.Equal(16928, binding.InitializerAsset.Data.Length);
        Assert.Same(game.ReadNativeUltraWaves184Resources(), binding.UltraWaves184Resources);
    }

    [Fact]
    public void Binding_JSON_keeps_actual_ring_caster_camera_tables_and_capture_template()
    {
        using GameAssets game = Open();
        NativeBattleEffectBinding source = game.ReadNativeEffectCatalogForEffects([184])[183];
        string json = JsonSerializer.Serialize(source);
        NativeBattleEffectBinding restored = JsonSerializer.Deserialize<NativeBattleEffectBinding>(json)!;
        Assert.Equal(json, JsonSerializer.Serialize(restored));
        NativeBattleUltraWaves184Resources resources = restored.UltraWaves184Resources!;
        resources.Validate();
        Assert.Equal("FF8_EN:D32750", resources.Camera.Identity);
        Assert.Equal(0x164, resources.Camera.Data.Length);
        Assert.Equal("FF8_EN:D328B4", resources.CasterDat.Identity);
        Assert.Equal(0x1B74, resources.CasterDat.Data.Length);
        Assert.Equal(0xD34450u, resources.Ring.Address);
        Assert.Equal(624, resources.Ring.Bytes.Length);
        Assert.Equal(35, resources.WarpColors.Length);
        Assert.Equal(16, resources.RingColors.Length);
        Assert.Equal(8, resources.EmitterVectors.Length);
        Assert.Equal(8, resources.StageDraw.Length);
        Assert.Equal(48, resources.CapturePackets.Length);
        Assert.Equal(source.InitializerAsset!.Data.ToArray(), restored.InitializerAsset!.Data.ToArray());
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original resources for the Blue Ultra Waves binding.");
        return new(directory!);
    }
}
