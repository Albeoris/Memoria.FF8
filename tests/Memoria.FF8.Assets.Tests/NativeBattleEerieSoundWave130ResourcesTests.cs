using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleEerieSoundWave130ResourcesTests
{
    [Fact]
    public void Original_catalog_binds_complete_Eerie_Sound_Wave_sources_and_initializer()
    {
        using GameAssets game = Open();
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([130])[129];
        Assert.Equal((ushort)130, binding.Effect);
        Assert.Equal(NativeBattleEerieSoundWave130Profile.Entry, binding.Entry);
        Assert.Equal(NativeBattleEerieSoundWave130Profile.Initializer, binding.Initializer);
        Assert.False(binding.InitializerIsRet);
        Assert.Equal(new NativeBattleEffectInitializerResource("mag129.tim", NativeBattleEerieSoundWave130Profile.InitializerGlobal), binding.InitializerResource);
        Assert.Null(binding.InitializerResources);
        Assert.Equal("mag129.tim", binding.InitializerAsset!.Name);
        Assert.Equal(99360, binding.InitializerAsset.Data.Length);
        NativeBattleEerieSoundWave130Resources resources = binding.EerieSoundWave130Resources!;
        Assert.Same(game.ReadNativeEerieSoundWave130Resources(), resources);
        resources.Validate();
        Assert.Equal(0x1221138u, resources.MainAct.Address);
        Assert.Equal(6000, resources.MainAct.Data.Length);
        Assert.Equal(0x12228A8u, resources.TravelAct.Address);
        Assert.Equal(3940, resources.TravelAct.Data.Length);
        Assert.Equal("FF8_EN:1223810", resources.Camera.Identity);
        Assert.Equal(160, resources.Camera.Data.Length);
        Assert.Equal(0x1220990u, resources.ImpactAct.Address);
        Assert.Equal(1960, resources.ImpactAct.Data.Length);
        Assert.Equal(0x64197u, resources.Sound);
    }

    [Fact]
    public void Binding_JSON_preserves_three_independent_ACT_sources_camera_and_TIM()
    {
        using GameAssets game = Open();
        NativeBattleEffectBinding source = game.ReadNativeEffectCatalogForEffects([130])[129];
        string json = NativeBattleSnapshotJson.Serialize(source);
        NativeBattleEffectBinding restored = NativeBattleSnapshotJson.Deserialize<NativeBattleEffectBinding>(json)!;
        Assert.Equal(json, NativeBattleSnapshotJson.Serialize(restored));
        restored.EerieSoundWave130Resources!.Validate();
        Assert.Equal(source.EerieSoundWave130Resources!.MainAct.Data.ToArray(), restored.EerieSoundWave130Resources.MainAct.Data.ToArray());
        Assert.Equal(source.EerieSoundWave130Resources.TravelAct.Data.ToArray(), restored.EerieSoundWave130Resources.TravelAct.Data.ToArray());
        Assert.Equal(source.EerieSoundWave130Resources.ImpactAct.Data.ToArray(), restored.EerieSoundWave130Resources.ImpactAct.Data.ToArray());
        Assert.Equal(source.EerieSoundWave130Resources.Camera.Data.ToArray(), restored.EerieSoundWave130Resources.Camera.Data.ToArray());
        Assert.Equal(source.InitializerAsset!.Data.ToArray(), restored.InitializerAsset!.Data.ToArray());
    }

    [Fact]
    public void Cold_and_unknown_globals_preserve_knowledge_and_exclude_physical_task_owners()
    {
        NativeBattleEerieSoundWave130State cold = NativeBattleEerieSoundWave130State.FromColdProcess();
        cold.Validate();
        Assert.Equal(0u, cold.ReadDword(NativeBattleEerieSoundWave130Profile.WorkingGlobal));
        NativeBattleEerieSoundWave130State live = cold.WriteDword(NativeBattleEerieSoundWave130Profile.WorkingGlobal, 0x20DFAB8);
        string json = NativeBattleSnapshotJson.Serialize(live);
        NativeBattleEerieSoundWave130State restored = NativeBattleSnapshotJson.Deserialize<NativeBattleEerieSoundWave130State>(json)!;
        restored.Validate();
        Assert.Equal(json, NativeBattleSnapshotJson.Serialize(restored));
        Assert.Equal(0x20DFAB8u, restored.ReadDword(NativeBattleEerieSoundWave130Profile.WorkingGlobal));
        Assert.Throws<InvalidDataException>(() => restored.ReadDword(NativeBattleEerieSoundWave130Profile.RootBank));
        NativeBattleEerieSoundWave130State unknown = NativeBattleEerieSoundWave130State.FromUnknown();
        unknown.Validate();
        string unknownJson = NativeBattleSnapshotJson.Serialize(unknown);
        NativeBattleEerieSoundWave130State unknownRestored = NativeBattleSnapshotJson.Deserialize<NativeBattleEerieSoundWave130State>(unknownJson)!;
        Assert.Equal(unknownJson, NativeBattleSnapshotJson.Serialize(unknownRestored));
        Assert.Throws<NotSupportedException>(() => unknownRestored.ReadDword(NativeBattleEerieSoundWave130Profile.WorkingGlobal));
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original resources for Eerie Sound Wave.");
        return new(directory!);
    }
}
