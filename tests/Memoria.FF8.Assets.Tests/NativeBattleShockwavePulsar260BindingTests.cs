using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleShockwavePulsar260BindingTests
{
    [Fact]
    public void Original_catalog_selects_Shockwave_Pulsar_and_actual_initializer_global()
    {
        using GameAssets game = Open();
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([260])[259];
        Assert.Equal((ushort)260, binding.Effect);
        Assert.Equal(0x690690u, binding.Entry);
        Assert.Equal(0x6906A0u, binding.Initializer);
        Assert.False(binding.InitializerIsRet);
        Assert.Equal(new NativeBattleEffectInitializerResource("mag069-259.tim", 0x2508610), binding.InitializerResource);
        Assert.Null(binding.InitializerResources);
        Assert.Equal("mag069-259.tim", binding.InitializerAsset!.Name);
        Assert.Equal(234016, binding.InitializerAsset.Data.Length);
        Assert.Same(game.ReadNativeShockwavePulsar260Resources(), binding.ShockwavePulsar260Resources);
    }

    [Fact]
    public void Binding_JSON_retains_five_ACT_sources_and_both_streamed_TIM_payloads()
    {
        using GameAssets game = Open();
        NativeBattleEffectBinding source = game.ReadNativeEffectCatalogForEffects([260])[259];
        string json = JsonSerializer.Serialize(source);
        NativeBattleEffectBinding restored = JsonSerializer.Deserialize<NativeBattleEffectBinding>(json)!;
        Assert.Equal(json, JsonSerializer.Serialize(restored));
        NativeBattleShockwavePulsar260Resources resources = restored.ShockwavePulsar260Resources!;
        NativeBattleShockwavePulsar260ResourcesReader.ValidatePayloads(resources);
        Assert.Equal(new uint[] { 0x111D424, 0x111DEBC, 0x111F09C, 0x112015C, 0x112A974 }, resources.Acts.Select(act => act.Address));
        Assert.Equal("FF8_EN:111B3CC", resources.CasterDat.Identity);
        Assert.Equal(new short[] { 360, 361 }, resources.Files.Select(file => file.FileId));
        Assert.Equal(source.InitializerAsset!.Data.ToArray(), restored.InitializerAsset!.Data.ToArray());
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select original resources to validate Shockwave Pulsar binding.");
        return new(directory!);
    }
}
