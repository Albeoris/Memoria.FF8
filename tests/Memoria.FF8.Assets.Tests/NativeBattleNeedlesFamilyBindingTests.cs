using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleNeedlesFamilyBindingTests
{
    [Theory]
    [InlineData((ushort)177)]
    [InlineData((ushort)178)]
    public void Original_catalog_binds_only_the_selected_native_module_and_initializer(ushort effect)
    {
        using GameAssets game = Open();
        NativeBattleNeedlesFamilyProfile profile = NativeBattleNeedlesFamilyProfile.ForEffect(effect);
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([effect])[effect - 1];
        Assert.Equal(effect, binding.Effect);
        Assert.Equal(profile.Entry, binding.Entry);
        Assert.Equal(profile.Initializer, binding.Initializer);
        Assert.False(binding.InitializerIsRet);
        Assert.Equal(new NativeBattleEffectInitializerResource("mag176.tim", profile.InitializerGlobal), binding.InitializerResource);
        Assert.Null(binding.InitializerResources);
        Assert.Equal("mag176.tim", binding.InitializerAsset!.Name);
        Assert.Equal(192, binding.InitializerAsset.Data.Length);
        Assert.Same(game.ReadNativeNeedlesFamilyResources(effect), binding.NeedlesFamilyResources);
        Assert.Equal(effect, binding.NeedlesFamilyResources!.Effect);
        binding.NeedlesFamilyResources.Validate();
        NativeBattleEffectBinding restored = JsonSerializer.Deserialize<NativeBattleEffectBinding>(JsonSerializer.Serialize(binding))!;
        restored.NeedlesFamilyResources!.Validate();
        Assert.Equal(JsonSerializer.Serialize(binding), JsonSerializer.Serialize(restored));
        Assert.Equal(profile.CameraAddress, restored.NeedlesFamilyResources.Profile.CameraAddress);
        Assert.Equal(binding.InitializerAsset.Data.ToArray(), restored.InitializerAsset!.Data.ToArray());
    }
    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original resources for Needles bindings.");
        return new(directory!);
    }
}
