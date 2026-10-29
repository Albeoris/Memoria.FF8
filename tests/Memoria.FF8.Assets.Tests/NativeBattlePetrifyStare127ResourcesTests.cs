using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattlePetrifyStare127ResourcesTests
{
    [Fact]
    public void Original_catalog_binds_complete_Petrify_Stare_sources_and_initializer()
    {
        using GameAssets game = Open();
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([127])[126];
        Assert.Equal((ushort)127, binding.Effect);
        Assert.Equal(NativeBattlePetrifyStare127Profile.Entry, binding.Entry);
        Assert.Equal(NativeBattlePetrifyStare127Profile.Initializer, binding.Initializer);
        Assert.False(binding.InitializerIsRet);
        Assert.Equal(new NativeBattleEffectInitializerResource("mag126.tim", NativeBattlePetrifyStare127Profile.InitializerGlobal), binding.InitializerResource);
        Assert.Null(binding.InitializerResources);
        Assert.Equal("mag126.tim", binding.InitializerAsset!.Name);
        Assert.Equal(99360, binding.InitializerAsset.Data.Length);
        NativeBattlePetrifyStare127Resources resources = binding.PetrifyStare127Resources!;
        Assert.Same(game.ReadNativePetrifyStare127Resources(), resources);
        resources.Validate();
        Assert.Equal(0x12377BCu, resources.FirstAct.Address);
        Assert.Equal(0x44C8, resources.FirstAct.Data.Length);
        Assert.Equal(0x123BC84u, resources.SecondAct.Address);
        Assert.Equal(0x5828, resources.SecondAct.Data.Length);
        Assert.Equal("FF8_EN:12414AC", resources.Camera.Identity);
        Assert.Equal(0xEC, resources.Camera.Data.Length);
        Assert.Equal(0x61AC6u, resources.Sound);
    }

    [Fact]
    public void Binding_JSON_preserves_both_independent_ACT_sources_camera_and_TIM()
    {
        using GameAssets game = Open();
        NativeBattleEffectBinding source = game.ReadNativeEffectCatalogForEffects([127])[126];
        string json = NativeBattleSnapshotJson.Serialize(source);
        NativeBattleEffectBinding restored = NativeBattleSnapshotJson.Deserialize<NativeBattleEffectBinding>(json)!;
        Assert.Equal(json, NativeBattleSnapshotJson.Serialize(restored));
        restored.PetrifyStare127Resources!.Validate();
        Assert.Equal(source.PetrifyStare127Resources!.FirstAct.Data.ToArray(), restored.PetrifyStare127Resources.FirstAct.Data.ToArray());
        Assert.Equal(source.PetrifyStare127Resources.SecondAct.Data.ToArray(), restored.PetrifyStare127Resources.SecondAct.Data.ToArray());
        Assert.Equal(source.PetrifyStare127Resources.Camera.Data.ToArray(), restored.PetrifyStare127Resources.Camera.Data.ToArray());
        Assert.Equal(source.InitializerAsset!.Data.ToArray(), restored.InitializerAsset!.Data.ToArray());
    }

    [Fact]
    public void Cold_and_unknown_globals_preserve_knowledge_and_exclude_physical_task_owners()
    {
        NativeBattlePetrifyStare127State cold = NativeBattlePetrifyStare127State.FromColdProcess();
        cold.Validate();
        Assert.Equal(0u, cold.ReadDword(NativeBattlePetrifyStare127Profile.WorkingGlobal));
        NativeBattlePetrifyStare127State live = cold.WriteDword(NativeBattlePetrifyStare127Profile.WorkingGlobal, 0x20DFAB8);
        string json = NativeBattleSnapshotJson.Serialize(live);
        NativeBattlePetrifyStare127State restored = NativeBattleSnapshotJson.Deserialize<NativeBattlePetrifyStare127State>(json)!;
        restored.Validate();
        Assert.Equal(json, NativeBattleSnapshotJson.Serialize(restored));
        Assert.Equal(0x20DFAB8u, restored.ReadDword(NativeBattlePetrifyStare127Profile.WorkingGlobal));
        Assert.Throws<InvalidDataException>(() => restored.ReadDword(NativeBattlePetrifyStare127Profile.RootBank));
        NativeBattlePetrifyStare127State unknown = NativeBattlePetrifyStare127State.FromUnknown();
        unknown.Validate();
        string unknownJson = NativeBattleSnapshotJson.Serialize(unknown);
        NativeBattlePetrifyStare127State unknownRestored = NativeBattleSnapshotJson.Deserialize<NativeBattlePetrifyStare127State>(unknownJson)!;
        Assert.Equal(unknownJson, NativeBattleSnapshotJson.Serialize(unknownRestored));
        Assert.Throws<NotSupportedException>(() => unknownRestored.ReadDword(NativeBattlePetrifyStare127Profile.WorkingGlobal));
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original resources for Petrify Stare.");
        return new(directory!);
    }
}
