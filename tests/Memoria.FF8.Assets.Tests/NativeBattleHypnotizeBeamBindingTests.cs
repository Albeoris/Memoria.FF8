using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleHypnotizeBeamBindingTests
{
    [Theory]
    [InlineData((ushort)133)]
    [InlineData((ushort)134)]
    public void Original_catalog_retains_the_selected_initializer_and_dedicated_typed_sources(ushort effect)
    {
        using GameAssets game = Open();
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([effect])[effect - 1];
        Assert.Equal(effect, binding.Effect);
        Assert.False(binding.InitializerIsRet);
        Assert.Null(binding.InitializerResources);
        if (effect == 133)
        {
            Assert.Equal(NativeBattleHypnotize133Profile.Entry, binding.Entry);
            Assert.Equal(NativeBattleHypnotize133Profile.Initializer, binding.Initializer);
            Assert.Equal(new NativeBattleEffectInitializerResource("mag132.tim", NativeBattleHypnotize133Profile.InitializerGlobal), binding.InitializerResource);
            Assert.Equal(165920, binding.InitializerAsset!.Data.Length);
            Assert.Equal("mag132.tim", binding.InitializerAsset.Name);
            Assert.Same(game.ReadNativeHypnotize133Resources(), binding.Hypnotize133Resources);
            Assert.Null(binding.BeamLaser134Resources);
        }
        else
        {
            Assert.Equal(NativeBattleBeamLaser134Profile.Entry, binding.Entry);
            Assert.Equal(NativeBattleBeamLaser134Profile.Initializer, binding.Initializer);
            Assert.Equal(new NativeBattleEffectInitializerResource("mag133.tim", NativeBattleBeamLaser134Profile.InitializerGlobal), binding.InitializerResource);
            Assert.Equal(166944, binding.InitializerAsset!.Data.Length);
            Assert.Equal("mag133.tim", binding.InitializerAsset.Name);
            Assert.Same(game.ReadNativeBeamLaser134Resources(), binding.BeamLaser134Resources);
            Assert.Null(binding.Hypnotize133Resources);
        }
        string json = JsonSerializer.Serialize(binding);
        NativeBattleEffectBinding restored = JsonSerializer.Deserialize<NativeBattleEffectBinding>(json)!;
        restored.Hypnotize133Resources?.Validate();
        restored.BeamLaser134Resources?.Validate();
        Assert.Equal(json, JsonSerializer.Serialize(restored));
        Assert.Equal(binding.InitializerAsset!.Data.ToArray(), restored.InitializerAsset!.Data.ToArray());
    }
    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original Hypnotize and Beam Laser resources.");
        return new(directory!);
    }
}
