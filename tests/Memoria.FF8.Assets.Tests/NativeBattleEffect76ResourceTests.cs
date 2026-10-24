using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect76Profile;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleEffect76ResourceTests
{
    [Fact]
    public void Original_selected_raw_meshes_music_and_bounded_static_fronts_are_complete()
    {
        using GameAssets game = Open();
        NativeBattleEffect76Resources resources = game.ReadNativeEffect76Resources();
        resources.Validate();
        Assert.Equal(TextureLength, resources.MainTim.Data.Length);
        Assert.Equal(CameraLength, resources.Camera.Data.Length);
        Assert.Equal(VibrationLength, resources.Vibration.Data.Length);
        for (int index = 0; index < MeshAddresses.Length; index++)
        {
            Assert.Equal(MeshLengths[index], resources.ReadMesh(MeshAddresses[index]).Bytes.Length);
        }
        NativeBattleMusicLoadSource music = NativeBattleMusicLoadSource.Read501A20(resources.Music.Data);
        Assert.Equal(131344, music.SoundBank.Length);
        Assert.Equal(6912, music.Music.Length);
        _ = music.ReadIdentity();
        Assert.Equal(12, resources.ReadStatic(AlignmentAxisAddress, 12).Length);
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(AlignmentAxisAddress, 13));
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(BaseAddress, 12));
    }

    [Fact]
    public void Selected_stage_file_decoder_uses_supplied_current_bytes_and_is_not_serialized()
    {
        using GameAssets game = Open();
        NativeBattleEffect76Resources resources = game.ReadNativeEffect76Resources();
        NativeBattleEffectFileAsset file = resources.ReadFile(StageFileId);
        Assert.Equal(StageFileLength, file.Data.Length);
        Assert.Throws<NotSupportedException>(() => resources.ReadFile(37));
        Assert.NotNull(resources.DecodeReloadStage);
        NativeBattleStageSource stage = resources.DecodeReloadStage!(file.Data.ToArray());
        Assert.NotNull(stage.Geometry);
        Assert.NotNull(stage.PhysicalSource);
        Assert.Equal(StageDirectoryOffset, stage.PhysicalSource!.DirectoryOffset);
        byte[] current = file.Data.ToArray();
        current[^1] ^= 0x5A;
        NativeBattleStageSource changed = resources.DecodeReloadStage!(current);
        Assert.Equal(current[^1], changed.Texture.Data[^1]);
        Assert.NotEqual(stage.Texture.Data[^1], changed.Texture.Data[^1]);
        NativeBattleEffect76Resources restored = JsonSerializer.Deserialize<NativeBattleEffect76Resources>(JsonSerializer.Serialize(resources))!;
        restored.Validate();
        Assert.Null(restored.DecodeReloadStage);
        Assert.Equal(file.Data.ToArray(), restored.ReadFile(StageFileId).Data.ToArray());
        Assert.Equal(stage.Geometry!.Value.Length, resources.DecodeReloadStage!(restored.ReadFile(StageFileId).Data.ToArray()).Geometry!.Value.Length);
    }

    [Fact]
    public void Selected_catalog_hydrates_the_same_original_resource_binding()
    {
        using GameAssets game = Open();
        NativeBattleEffect76Resources resources = game.ReadNativeEffect76Resources();
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([76])[75];
        Assert.Same(resources, binding.Effect76Resources);
        Assert.Equal(Entry, binding.Entry);
        Assert.Equal(Initializer, binding.Initializer);
        Assert.Equal(new NativeBattleEffectInitializerResource("mag075.tim", InitializerGlobal), binding.InitializerResource);
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original effect76 resources.");
        return new(directory!);
    }
}
