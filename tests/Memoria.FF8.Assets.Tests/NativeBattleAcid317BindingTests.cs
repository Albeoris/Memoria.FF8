using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleAcid317BindingTests
{
    [Fact]
    public void Original_catalog_selects_Acid_and_actual_initializer_global()
    {
        using GameAssets game = Open();
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([317])[316];
        Assert.Equal((ushort)317, binding.Effect);
        Assert.Equal(0x65F840u, binding.Entry);
        Assert.Equal(0x65F850u, binding.Initializer);
        Assert.False(binding.InitializerIsRet);
        Assert.Equal(new NativeBattleEffectInitializerResource("mag127.tim", 0x2505494), binding.InitializerResource);
        Assert.Null(binding.InitializerResources);
        Assert.Equal("mag127.tim", binding.InitializerAsset!.Name);
        Assert.Equal(166432, binding.InitializerAsset.Data.Length);
        Assert.Same(game.ReadNativeAcid317Resources(), binding.Acid317Resources);
    }

    [Fact]
    public void Binding_JSON_keeps_complete_ACT_camera_caster_sprite_and_distance_sources()
    {
        using GameAssets game = Open();
        NativeBattleEffectBinding source = game.ReadNativeEffectCatalogForEffects([317])[316];
        string json = JsonSerializer.Serialize(source);
        NativeBattleEffectBinding restored = JsonSerializer.Deserialize<NativeBattleEffectBinding>(json)!;
        Assert.Equal(json, JsonSerializer.Serialize(restored));
        NativeBattleAcid317Resources resources = restored.Acid317Resources!;
        resources.Validate();
        Assert.Equal("FF8_EN:10017A0", resources.Camera.Identity);
        Assert.Equal(0x328, resources.Camera.Data.Length);
        Assert.Equal("FF8_EN:FFE6B0", resources.CasterDat.Identity);
        Assert.Equal(0x1B74, resources.CasterDat.Data.Length);
        Assert.Equal(0x1000224u, resources.TargetAct.Address);
        Assert.Equal(0x1178, resources.TargetAct.Data.Length);
        Assert.Equal(new uint[] { 0x100139C, 0x10014FC, 0x10016A8 }, new uint[] { resources.SpriteA.Address, resources.SpriteB.Address, resources.SpriteC.Address });
        Assert.Equal(new int[] { 0x160, 0x1AC, 0xF8 }, new int[] { resources.SpriteA.Bytes.Length, resources.SpriteB.Bytes.Length, resources.SpriteC.Bytes.Length });
        Assert.Equal(128 * 128, resources.DistanceTable.Length);
        Assert.Equal(new byte[] { 0x0C, 5, 0, 0, 0x23, 2, 9, 0 }, resources.MouthAnchor8.ToArray());
        Assert.Equal(0x55784u, resources.Sound);
        Assert.Equal(source.InitializerAsset!.Data.ToArray(), restored.InitializerAsset!.Data.ToArray());
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original resources for the Acid binding.");
        return new(directory!);
    }
}
