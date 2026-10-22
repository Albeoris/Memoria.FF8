using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleDegenerator306BindingTests
{
    [Fact]
    public void Original_catalog_selects_Degenerator_and_actual_initializer_global()
    {
        using GameAssets game = Open();
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([306])[305];
        Assert.Equal((ushort)306, binding.Effect);
        Assert.Equal(0x5A2C90u, binding.Entry);
        Assert.Equal(0x5A2C70u, binding.Initializer);
        Assert.False(binding.InitializerIsRet);
        Assert.Equal(new NativeBattleEffectInitializerResource("mag173.tim", 0x2244220), binding.InitializerResource);
        Assert.Null(binding.InitializerResources);
        Assert.Equal("mag173.tim", binding.InitializerAsset!.Name);
        Assert.Equal(232480, binding.InitializerAsset.Data.Length);
        Assert.Same(game.ReadNativeDegenerator306Resources(), binding.Degenerator306Resources);
    }

    [Fact]
    public void Binding_JSON_keeps_original_camera_binding_mesh_and_sprite_sources()
    {
        using GameAssets game = Open();
        NativeBattleEffectBinding source = game.ReadNativeEffectCatalogForEffects([306])[305];
        string json = JsonSerializer.Serialize(source);
        NativeBattleEffectBinding restored = JsonSerializer.Deserialize<NativeBattleEffectBinding>(json)!;
        Assert.Equal(json, JsonSerializer.Serialize(restored));
        NativeBattleDegenerator306Resources resources = restored.Degenerator306Resources!;
        resources.Validate();
        Assert.Equal("FF8_EN:CE4164", resources.Camera.Identity);
        Assert.Equal(1000, resources.Camera.Data.Length);
        Assert.Equal("FF8_EN:CE454C", resources.CasterDat.Identity);
        Assert.Equal(0x1B74, resources.CasterDat.Data.Length);
        Assert.Equal(new uint[] { 0xCEAB94, 0xCEAF2C, 0xCE643C, 0xCEA2E4, 0xCE7A2C }, new uint[] { resources.MeshA.Address, resources.MeshB.Address, resources.MeshC.Address, resources.MeshD.Address, resources.CustomMesh.Address });
        Assert.Equal(0xCE60C0u, resources.SpriteA.Address);
        Assert.Equal(0xCE62A8u, resources.SpriteB.Address);
        Assert.Equal(12, resources.MaterialCycle.Length);
        Assert.Equal(0x55775u, resources.Sound);
        Assert.Equal(source.InitializerAsset!.Data.ToArray(), restored.InitializerAsset!.Data.ToArray());
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original resources for the Degenerator binding.");
        return new(directory!);
    }
}
