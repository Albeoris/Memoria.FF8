using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect66Profile;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleEffect66ResourceTests
{
    [Fact]
    public void Original_resource_fronts_are_exact_and_exclude_current_pointer_cells()
    {
        using GameAssets game = Open();
        NativeBattleEffect66Resources resources = game.ReadNativeEffect66Resources();
        resources.Validate();
        Assert.Equal(TextureLength, resources.MainTim.Data.Length);
        Assert.Equal(EmbeddedTimLength, resources.EmbeddedTim.Data.Length);
        Assert.Equal(CameraLength, resources.Camera.Data.Length);
        Assert.Equal(VibrationLength, resources.Vibration.Data.Length);
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(InitializedBase, InitializedLength));
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(PartialMatrixAddress, 32));
        Assert.Equal(116, resources.ReadStatic(0xDEA360, 116).Length);
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(0xDEA360, 117));
    }

    [Fact]
    public void Selected_raw_groups_and_projected_sprite_frames_use_original_bounded_sources()
    {
        using GameAssets game = Open();
        NativeBattleEffect66Resources resources = game.ReadNativeEffect66Resources();
        for (int index = 0; index < MeshAddresses.Length; index++)
        {
            Assert.Equal(MeshLengths[index], resources.ReadMesh(MeshAddresses[index]).Bytes.Length);
        }
        int[] frames = [16, 16, 16, 15, 12];
        for (int index = 0; index < SpriteAddresses.Length; index++)
        {
            NativeBattleModuleResource sprite = resources.Sprite(SpriteAddresses[index]);
            for (int frame = 0; frame < frames[index]; frame++)
            {
                _ = sprite.ProjectedFrame571C80(frame);
            }
        }
        Assert.Equal(70u, System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadMesh(0xDE90CC).Bytes.AsSpan(4, 4)));
    }

    [Fact]
    public void Selected_catalog_hydrates_same_resources_and_json_keeps_static_exclusions()
    {
        using GameAssets game = Open();
        NativeBattleEffect66Resources resources = game.ReadNativeEffect66Resources();
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([66])[65];
        Assert.Same(resources, binding.Effect66Resources);
        Assert.Equal(new NativeBattleEffectInitializerResource("mag065.tim", InitializerGlobal), binding.InitializerResource);
        NativeBattleEffect66Resources restored = JsonSerializer.Deserialize<NativeBattleEffect66Resources>(JsonSerializer.Serialize(resources))!;
        restored.Validate();
        Assert.Throws<NotSupportedException>(() => restored.ReadStatic(InitializedBase + 4, 4));
        Assert.Equal(resources.ReadStatic(0xDEA354, 8), restored.ReadStatic(0xDEA354, 8));
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original effect66 resources.");
        return new(directory!);
    }
}
