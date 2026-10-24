using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect68Profile;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleEffect68ResourceTests
{
    [Fact]
    public void Original_resource_directories_keep_mutable_destinations_out_of_static_lookup()
    {
        using GameAssets game = Open();
        NativeBattleEffect68Resources resources = game.ReadNativeEffect68Resources();
        resources.Validate();
        Assert.Equal(TextureLength, resources.MainTim.Data.Length);
        for (int index = 0; index < MeshAddresses.Length; index++)
        {
            Assert.Equal(MeshLengths[index], resources.ReadMesh(MeshAddresses[index]).Bytes.Length);
        }
        foreach (uint address in MutableMeshAddresses)
        {
            Assert.Throws<NotSupportedException>(() => resources.ReadStatic(address, 8));
            Assert.Throws<NotSupportedException>(() => resources.ReadCurrentMesh(NativeBattleEffectMemory.FromColdProcess(), address));
        }
        Assert.Equal(CameraAddress, System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadStatic(CameraPointerAddress, 4)));
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(CameraAddress, 80));
    }

    [Fact]
    public void Original_profiles_vertex_banks_trajectory_and_sprite_frames_are_bounded()
    {
        using GameAssets game = Open();
        NativeBattleEffect68Resources resources = game.ReadNativeEffect68Resources();
        for (ushort index = 0; index < ProfileCount; index++)
        {
            Assert.Equal(16, resources.ReadCameraProfile(index).Bytes.Length);
        }
        Assert.Throws<InvalidDataException>(() => resources.ReadCameraProfile(ProfileCount));
        for (int index = 0; index < VertexBankAddresses.Length; index++)
        {
            Assert.Equal(VertexBankLengths[index], resources.ReadVertexBank(VertexBankAddresses[index]).Length);
        }
        Assert.Equal(84, resources.ReadStatic(0x15956F0, 84).Length);
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(0x15956F0, 85));
        for (int index = 0; index < 8; index++)
        {
            Assert.Equal(34, resources.ReadStatic(checked(0x1595570u + (uint)(index * 48)), 34).Length);
            Assert.Throws<NotSupportedException>(() => resources.ReadStatic(checked(0x1595570u + (uint)(index * 48)), 35));
        }
        int[] frames = [32, 4];
        for (int index = 0; index < SpriteAddresses.Length; index++)
        {
            for (int frame = 0; frame < frames[index]; frame++)
            {
                _ = resources.Sprite(SpriteAddresses[index]).ProjectedFrame571C80(frame);
            }
        }
    }

    [Fact]
    public void Selected_catalog_json_and_cold_binding_use_same_original_inputs_without_warm_reset()
    {
        using GameAssets game = Open();
        NativeBattleEffect68Resources resources = game.ReadNativeEffect68Resources();
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([68])[67];
        Assert.Same(resources, binding.Effect68Resources);
        Assert.Same(game.ReadNativeScanTextResources(), binding.ScanTextResources);
        Assert.Equal(new NativeBattleEffectInitializerResource("mag039-067.tim", InitializerGlobal), binding.InitializerResource);
        NativeBattleEffect68Resources restored = JsonSerializer.Deserialize<NativeBattleEffect68Resources>(JsonSerializer.Serialize(resources))!;
        restored.Validate();
        (NativeBattleEffect68State state, NativeBattleEffectMemory memory) = NativeBattleEffect68State.FromColdProcess().BindMeshSources(NativeBattleEffectMemory.FromColdProcess(), restored);
        foreach (uint address in MutableMeshAddresses)
        {
            Assert.Equal(resources.ReadMesh(address).Bytes.ToArray(), restored.ReadCurrentMesh(memory, address).Bytes.ToArray());
        }
        Assert.True(state.MeshSourcesBound);
        Assert.Throws<NotSupportedException>(() => restored.ReadStatic(MutableMeshAddresses[0], 8));
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original effect68 resources.");
        return new(directory!);
    }
}
