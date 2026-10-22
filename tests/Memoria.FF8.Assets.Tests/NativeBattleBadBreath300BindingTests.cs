using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleBadBreath300BindingTests
{
    [Fact]
    public void Original_catalog_selects_Bad_Breath_and_actual_initializer_global()
    {
        using GameAssets game = Open();
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([300])[299];
        Assert.Equal((ushort)300, binding.Effect);
        Assert.Equal(0x66EF00u, binding.Entry);
        Assert.Equal(0x66EF10u, binding.Initializer);
        Assert.False(binding.InitializerIsRet);
        Assert.Equal(new NativeBattleEffectInitializerResource("mag130-299.tim", 0x25077A0), binding.InitializerResource);
        Assert.Null(binding.InitializerResources);
        Assert.Equal("mag130-299.tim", binding.InitializerAsset!.Name);
        Assert.NotEmpty(binding.InitializerAsset.Data);
        Assert.Same(game.ReadNativeBadBreath300Resources(), binding.BadBreath300Resources);
        Assert.Equal(0x66EFB0u, NativeBattleBadBreath300Profile.RootCallback);
        Assert.Equal(0x66F270u, NativeBattleBadBreath300Profile.MainCallback);
        Assert.Equal(0x66F790u, NativeBattleBadBreath300Profile.TargetCallback);
        Assert.Equal(0x66FB20u, NativeBattleBadBreath300Profile.ParticleCallback);
    }

    [Fact]
    public void Binding_JSON_keeps_original_ACT_sprite_camera_caster_and_mesh_sources()
    {
        using GameAssets game = Open();
        NativeBattleEffectBinding source = game.ReadNativeEffectCatalogForEffects([300])[299];
        string json = JsonSerializer.Serialize(source);
        NativeBattleEffectBinding restored = JsonSerializer.Deserialize<NativeBattleEffectBinding>(json)!;
        Assert.Equal(json, JsonSerializer.Serialize(restored));
        NativeBattleBadBreath300Resources resources = restored.BadBreath300Resources!;
        resources.Validate();
        Assert.Equal(0x1042304u, resources.Target.Address);
        Assert.Equal(0xC7C, resources.Target.Data.Length);
        Assert.Equal(0x103F824u, resources.ParticleSprite.Address);
        Assert.Equal(480, resources.ParticleSprite.Bytes.Length);
        Assert.Equal("FF8_EN:1042F80", resources.Camera.Identity);
        Assert.Equal(372, resources.Camera.Data.Length);
        Assert.Equal("FF8_EN:103DCB0", resources.CasterDat.Identity);
        Assert.Equal(0x1B74, resources.CasterDat.Data.Length);
        Assert.Equal(5248, resources.MeshAInitial.Length);
        Assert.Equal(5248, resources.MeshBInitial.Length);
        Assert.Equal(8192, resources.TrigTable.Length);
        Assert.Equal(4, resources.Gradient.Length);
        Assert.Equal(8, resources.MouthAnchor.Length);
        Assert.Equal(0x55781u, resources.Sound);
        Assert.Equal(source.BadBreath300Resources!.MeshAInitial.ToArray(), resources.MeshAInitial.ToArray());
        Assert.Equal(source.BadBreath300Resources.MeshBInitial.ToArray(), resources.MeshBInitial.ToArray());
        Assert.Equal(source.InitializerAsset!.Data.ToArray(), restored.InitializerAsset!.Data.ToArray());
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original resources for the Bad Breath binding.");
        return new(directory!);
    }
}
