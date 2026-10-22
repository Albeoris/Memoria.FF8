using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleWhiteWind297BindingTests
{
    [Fact]
    public void Original_catalog_selects_Blue_Magic_White_Wind_and_actual_initializer_publication()
    {
        using GameAssets game = Open();
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([297])[296];
        Assert.Equal((ushort)297, binding.Effect);
        Assert.Equal(NativeBattleWhiteWind297Profile.Entry, binding.Entry);
        Assert.Equal(NativeBattleWhiteWind297Profile.Initializer, binding.Initializer);
        Assert.False(binding.InitializerIsRet);
        Assert.Equal(new NativeBattleEffectInitializerResource("mag296.tim", NativeBattleWhiteWind297Profile.InitializerGlobal), binding.InitializerResource);
        Assert.Null(binding.InitializerResources);
        Assert.Equal("mag296.tim", binding.InitializerAsset!.Name);
        Assert.NotEmpty(binding.InitializerAsset.Data);
        Assert.Same(game.ReadNativeWhiteWind297Resources(), binding.WhiteWind297Resources);
        Assert.Null(binding.WhiteWind226Resources);
        Assert.Equal(0x6730D0u, NativeBattleWhiteWind297Profile.RootCallback);
        Assert.Equal(0x6731D0u, NativeBattleWhiteWind297Profile.SceneCallback);
        Assert.Equal(0x673880u, NativeBattleWhiteWind297Profile.TargetCallback);
    }

    [Fact]
    public void Binding_JSON_preserves_selected_ACT_camera_caster_and_TIM_sources_without_enemy_alias()
    {
        using GameAssets game = Open();
        NativeBattleEffectBinding source = game.ReadNativeEffectCatalogForEffects([297])[296];
        string json = JsonSerializer.Serialize(source);
        NativeBattleEffectBinding restored = JsonSerializer.Deserialize<NativeBattleEffectBinding>(json)!;
        Assert.Equal(json, JsonSerializer.Serialize(restored));
        restored.WhiteWind297Resources!.Validate();
        Assert.Equal(0x105AEFCu, restored.WhiteWind297Resources.Caster.Address);
        Assert.Equal(0x105A494u, restored.WhiteWind297Resources.Sweep.Address);
        Assert.Equal(0x10583FCu, restored.WhiteWind297Resources.Target.Address);
        Assert.Equal("FF8_EN:1056888", restored.WhiteWind297Resources.CasterDat.Identity);
        Assert.Equal("FF8_EN:105B92C", restored.WhiteWind297Resources.Camera.Identity);
        Assert.Equal(0x168, restored.WhiteWind297Resources.Camera.Data.Length);
        Assert.Equal(source.InitializerAsset!.Data.ToArray(), restored.InitializerAsset!.Data.ToArray());
        Assert.Null(restored.WhiteWind226Resources);
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original resources for the White Wind binding.");
        return new(directory!);
    }
}
