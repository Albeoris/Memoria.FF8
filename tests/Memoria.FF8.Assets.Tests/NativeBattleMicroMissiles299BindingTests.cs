using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleMicroMissiles299BindingTests
{
    [Fact]
    public void Original_catalog_selects_Micro_Missiles_and_actual_initializer_global()
    {
        using GameAssets game = Open();
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([299])[298];
        Assert.Equal((ushort)299, binding.Effect);
        Assert.Equal(NativeBattleMicroMissiles299Profile.Entry, binding.Entry);
        Assert.Equal(NativeBattleMicroMissiles299Profile.Initializer, binding.Initializer);
        Assert.False(binding.InitializerIsRet);
        Assert.Equal(new NativeBattleEffectInitializerResource("mag283-298.tim", NativeBattleMicroMissiles299Profile.InitializerGlobal), binding.InitializerResource);
        Assert.Null(binding.InitializerResources);
        Assert.Equal("mag283-298.tim", binding.InitializerAsset!.Name);
        Assert.NotEmpty(binding.InitializerAsset.Data);
        Assert.Same(game.ReadNativeMicroMissiles299Resources(), binding.MicroMissiles299Resources);
        Assert.Equal(0x66FD70u, NativeBattleMicroMissiles299Profile.RootCallback);
        Assert.Equal(0x6700C0u, NativeBattleMicroMissiles299Profile.MissileCallback);
        Assert.Equal(0x670870u, NativeBattleMicroMissiles299Profile.ImpactCallback);
        Assert.Equal(0x6707B0u, NativeBattleMicroMissiles299Profile.ParticleCallback);
    }

    [Fact]
    public void Binding_JSON_keeps_original_ACT_sprite_camera_and_caster_sources()
    {
        using GameAssets game = Open();
        NativeBattleEffectBinding source = game.ReadNativeEffectCatalogForEffects([299])[298];
        string json = JsonSerializer.Serialize(source);
        NativeBattleEffectBinding restored = JsonSerializer.Deserialize<NativeBattleEffectBinding>(json)!;
        Assert.Equal(json, JsonSerializer.Serialize(restored));
        NativeBattleMicroMissiles299Resources resources = restored.MicroMissiles299Resources!;
        resources.Validate();
        Assert.Equal(0x10490A8u, resources.Missile.Address);
        Assert.Equal(0x104B63Cu, resources.Impact.Address);
        Assert.Equal(0x104C910u, resources.WideImpact.Address);
        Assert.Equal(0x1048C94u, resources.ParticleSprite.Address);
        Assert.Equal(0x1048DD8u, resources.TrailSprite.Address);
        Assert.Equal(0x1048F1Cu, resources.ImpactSprite.Address);
        Assert.Equal("FF8_EN:104DF68", resources.Camera.Identity);
        Assert.Equal("FF8_EN:1047120", resources.CasterDat.Identity);
        Assert.Equal(source.InitializerAsset!.Data.ToArray(), restored.InitializerAsset!.Data.ToArray());
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original resources for the Micro Missiles binding.");
        return new(directory!);
    }
}
