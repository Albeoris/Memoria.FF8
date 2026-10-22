using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleGatlingGun305BindingTests
{
    [Fact]
    public void Original_catalog_selects_Gatling_Gun_and_actual_initializer_global()
    {
        using GameAssets game = Open();
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([305])[304];
        Assert.Equal((ushort)305, binding.Effect);
        Assert.Equal(0x5A57D0u, binding.Entry);
        Assert.Equal(0x5A57B0u, binding.Initializer);
        Assert.False(binding.InitializerIsRet);
        Assert.Equal(new NativeBattleEffectInitializerResource("mag149.tim", 0x2247780), binding.InitializerResource);
        Assert.Null(binding.InitializerResources);
        Assert.Equal("mag149.tim", binding.InitializerAsset!.Name);
        Assert.Equal(68128, binding.InitializerAsset.Data.Length);
        Assert.Same(game.ReadNativeGatlingGun305Resources(), binding.GatlingGun305Resources);
    }

    [Fact]
    public void Binding_JSON_keeps_original_camera_binding_mesh_and_sprite_sources()
    {
        using GameAssets game = Open();
        NativeBattleEffectBinding source = game.ReadNativeEffectCatalogForEffects([305])[304];
        string json = JsonSerializer.Serialize(source);
        NativeBattleEffectBinding restored = JsonSerializer.Deserialize<NativeBattleEffectBinding>(json)!;
        Assert.Equal(json, JsonSerializer.Serialize(restored));
        NativeBattleGatlingGun305Resources resources = restored.GatlingGun305Resources!;
        resources.Validate();
        Assert.Equal("FF8_EN:CEB2DC", resources.Camera.Identity);
        Assert.Equal(780, resources.Camera.Data.Length);
        Assert.Equal("FF8_EN:CEB5E8", resources.CasterDat.Identity);
        Assert.Equal(0x1B74, resources.CasterDat.Data.Length);
        Assert.Equal(new uint[] { 0xCED698, 0xCED998, 0xCEDC60 }, new uint[] { resources.MeshA.Address, resources.MeshB.Address, resources.MeshC.Address });
        Assert.Equal(new uint[] { 0xCED358, 0xCED15C, 0xCED504 }, new uint[] { resources.SpriteA.Address, resources.SpriteB.Address, resources.SpriteC.Address });
        Assert.Equal(new int[] { 428, 508, 404 }, new int[] { resources.SpriteA.Bytes.Length, resources.SpriteB.Bytes.Length, resources.SpriteC.Bytes.Length });
        Assert.Equal(0x61AD6u, resources.Sound);
        Assert.Equal(source.InitializerAsset!.Data.ToArray(), restored.InitializerAsset!.Data.ToArray());
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original resources for the Gatling Gun binding.");
        return new(directory!);
    }
}
