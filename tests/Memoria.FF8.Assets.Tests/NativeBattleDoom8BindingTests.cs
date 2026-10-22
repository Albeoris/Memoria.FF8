using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleDoom8BindingTests
{
    [Fact]
    public void Unmaterialized_Doom_binding_does_not_serialize_a_missing_resource_owner()
    {
        NativeBattleEffectBinding binding = new(8, 0x8C4980, 0x8C4960);
        string json = JsonSerializer.Serialize(binding);
        Assert.DoesNotContain("\"Doom8Resources\"", json, StringComparison.Ordinal);
        Assert.Null(JsonSerializer.Deserialize<NativeBattleEffectBinding>(json)!.Doom8Resources);
    }

    [Fact]
    public void Selected_original_Doom_binding_materializes_its_single_initializer_and_typed_resources()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original resources to check Doom binding ingress.");
        using GameAssets game = new(directory!);
        NativeBattleEffectBinding metadata = game.ReadNativeEffectCatalog()[7];
        Assert.Equal((ushort)8, metadata.Effect);
        Assert.Equal(0x8C4980u, metadata.Entry);
        Assert.Equal(0x8C4960u, metadata.Initializer);
        Assert.False(metadata.InitializerIsRet);
        Assert.Equal(new NativeBattleEffectInitializerResource("mag007.tim", 0x2758500), metadata.InitializerResource);
        Assert.Null(metadata.InitializerResources);
        Assert.Null(metadata.InitializerAsset);
        Assert.Null(metadata.Doom8Resources);
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([8])[7];
        NativeBattleDoom8Resources resources = Assert.IsType<NativeBattleDoom8Resources>(binding.Doom8Resources);
        resources.Validate();
        Assert.Same(game.ReadNativeDoom8Resources(), resources);
        Assert.Same(resources.MainTim, binding.InitializerAsset);
        Assert.Equal(167968, binding.InitializerAsset!.Data.Length);
        Assert.Equal(metadata.InitializerResource, Assert.Single(NativeBattleEffectInitializerPolicy.Resources(binding)));
        Assert.Same(resources.MainTim, Assert.Single(NativeBattleEffectInitializerPolicy.Assets(binding)));
        NativeBattleEffectBinding restored = JsonSerializer.Deserialize<NativeBattleEffectBinding>(JsonSerializer.Serialize(binding))!;
        restored.Doom8Resources!.Validate();
        Assert.Equal(JsonSerializer.Serialize(binding), JsonSerializer.Serialize(restored));
        Assert.Null(game.ReadNativeEffectCatalog()[7].Doom8Resources);
        Assert.Same(resources, game.ReadNativeEffectCatalogForEffects([8])[7].Doom8Resources);
    }
}
