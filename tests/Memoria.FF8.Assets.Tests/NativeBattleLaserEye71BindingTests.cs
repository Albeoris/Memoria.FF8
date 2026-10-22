using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleLaserEye71BindingTests
{
    [Fact]
    public void Strict_original_catalog_binds_Laser_Eye_and_its_executed_TIM_initializer()
    {
        using GameAssets game = Open();
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([71])[70];
        Assert.Equal((ushort)71, binding.Effect);
        Assert.Equal(NativeBattleLaserEye71Profile.Entry, binding.Entry);
        Assert.Equal(NativeBattleLaserEye71Profile.Initializer, binding.Initializer);
        Assert.False(binding.InitializerIsRet);
        Assert.Equal(new NativeBattleEffectInitializerResource("mag070.tim", NativeBattleLaserEye71Profile.InitializerGlobal), binding.InitializerResource);
        Assert.Equal("mag070.tim", binding.InitializerAsset!.Name);
        Assert.Equal(168480, binding.InitializerAsset.Data.Length);
        Assert.Same(game.ReadNativeLaserEye71Resources(), binding.LaserEye71Resources);
    }

    [Fact]
    public void Binding_JSON_keeps_original_node_seed_caster_model_camera_and_distinct_meshes()
    {
        using GameAssets game = Open();
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([71])[70];
        string json = JsonSerializer.Serialize(binding);
        NativeBattleEffectBinding restored = JsonSerializer.Deserialize<NativeBattleEffectBinding>(json)!;
        Assert.Equal(json, JsonSerializer.Serialize(restored));
        NativeBattleLaserEye71Resources resources = restored.LaserEye71Resources!;
        resources.Validate();
        Assert.Equal(0x1789344u, resources.Executable.ResourceAddress);
        Assert.Equal(0x60B0, resources.Executable.Data.Length);
        Assert.Equal("FF8_EN:1590024", resources.CasterDat.Identity);
        Assert.Equal(new uint[] { 0x158F688, 0x158FDA0, 0x158FE48 }, resources.Meshes.Select(mesh => mesh.Address).ToArray());
        Assert.Equal(new int[] { 1816, 168, 280 }, resources.Meshes.Select(mesh => mesh.Bytes.Length).ToArray());
        Assert.Equal(binding.InitializerAsset!.Data.ToArray(), restored.InitializerAsset!.Data.ToArray());
        Assert.Equal(binding.LaserEye71Resources!.Executable.Data.ToArray(), resources.Executable.Data.ToArray());
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original resources for Laser Eye.");
        return new(directory!);
    }
}
