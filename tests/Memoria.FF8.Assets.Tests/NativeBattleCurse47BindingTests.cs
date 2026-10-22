using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleCurse47BindingTests
{
    [Fact]
    public void Unmaterialized_Curse_binding_does_not_serialize_a_missing_resource_owner()
    {
        NativeBattleEffectBinding binding = new(47, 0x82A7F0, 0x82A7D0);
        string json = JsonSerializer.Serialize(binding);
        Assert.DoesNotContain("\"Curse47Resources\"", json, StringComparison.Ordinal);
        Assert.Null(JsonSerializer.Deserialize<NativeBattleEffectBinding>(json)!.Curse47Resources);
    }

    [Fact]
    public void Selected_original_Curse_binding_materializes_its_single_initializer_and_typed_resources()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original resources to check Curse binding ingress.");
        using GameAssets game = new(directory!);
        NativeBattleEffectBinding metadata = game.ReadNativeEffectCatalog()[46];
        Assert.Equal((ushort)47, metadata.Effect);
        Assert.Equal(0x82A7F0u, metadata.Entry);
        Assert.Equal(0x82A7D0u, metadata.Initializer);
        Assert.False(metadata.InitializerIsRet);
        Assert.Equal(new NativeBattleEffectInitializerResource("mag046.tim", 0x264B3C8), metadata.InitializerResource);
        Assert.Null(metadata.InitializerResources);
        Assert.Null(metadata.InitializerAsset);
        Assert.Null(metadata.Curse47Resources);
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([47])[46];
        NativeBattleCurse47Resources resources = Assert.IsType<NativeBattleCurse47Resources>(binding.Curse47Resources);
        resources.Validate();
        Assert.Same(game.ReadNativeCurse47Resources(), resources);
        Assert.Same(resources.MainTim, binding.InitializerAsset);
        Assert.Equal(168480, binding.InitializerAsset!.Data.Length);
        Assert.Equal(metadata.InitializerResource, Assert.Single(NativeBattleEffectInitializerPolicy.Resources(binding)));
        Assert.Same(resources.MainTim, Assert.Single(NativeBattleEffectInitializerPolicy.Assets(binding)));
        NativeBattleEffectBinding restored = JsonSerializer.Deserialize<NativeBattleEffectBinding>(JsonSerializer.Serialize(binding))!;
        restored.Curse47Resources!.Validate();
        Assert.Equal(JsonSerializer.Serialize(binding), JsonSerializer.Serialize(restored));
        Assert.Null(game.ReadNativeEffectCatalog()[46].Curse47Resources);
        Assert.Same(resources, game.ReadNativeEffectCatalogForEffects([47])[46].Curse47Resources);
    }
}
