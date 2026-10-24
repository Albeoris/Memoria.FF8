using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect321Profile;
using static Memoria.FF8.Runtime.NativeBattleEffect321Resources;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleEffect321ResourceTests
{
    [Fact]
    public void Original_meshes_have_only_the_selected_groups_and_physical_vertex_extents()
    {
        using GameAssets game = Open();
        NativeBattleEffect321Resources resources = game.ReadNativeEffect321Resources();
        resources.Validate();
        int[][] expected = [[0, 0, 0, 0, 0, 48, 0, 0], [0, 0, 0, 0, 0, 0, 8, 0], [0, 0, 0, 72, 0, 0, 0, 12]];
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        int[] vertices = [72, 6, 125];
        for (int index = 0; index < MeshAddresses.Length; index++)
        {
            NativeBattleStaticMeshResource source = resources.ReadMesh(MeshAddresses[index]);
            Assert.Equal(MeshLengths[index], source.Bytes.Length);
            Assert.Equal(vertices[index], (int)BinaryPrimitives.ReadUInt32LittleEndian(source.Bytes.AsSpan(4, 4)));
            int cursor = (int)BinaryPrimitives.ReadUInt32LittleEndian(source.Bytes.AsSpan(0, 4));
            for (int group = 0; group < 8; group++)
            {
                int count = (int)BinaryPrimitives.ReadUInt32LittleEndian(source.Bytes.AsSpan(cursor, 4));
                Assert.Equal(expected[index][group], count);
                cursor += 4 + count * strides[group];
            }
            Assert.Equal(source.Bytes.Length, cursor);
        }
        Assert.Throws<NotSupportedException>(() => resources.ReadMesh(0xCD153D));
    }

    [Fact]
    public void Actual_sprite_sixteen_phases_terminate_and_static_ranges_exclude_current_process_owners()
    {
        using GameAssets game = Open();
        NativeBattleEffect321Resources resources = game.ReadNativeEffect321Resources();
        NativeBattleModuleResource sprite = resources.Sprite(0xCD1390);
        Assert.Equal(428, sprite.Bytes.Length);
        for (int frame = 0; frame < 16; frame++)
        {
            _ = sprite.ProjectedFrame571C80(frame);
        }
        Assert.Equal((short)-1, BinaryPrimitives.ReadInt16LittleEndian(sprite.Bytes.AsSpan(42, 2)));
        Assert.Equal(4, resources.ReadStatic(0xCD2AF0, 4).Length);
        Assert.Equal(4, resources.ReadStatic(0xCD2AF4, 4).Length);
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(0xCD2AF0, 5));
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(0x221BCA0, 4));
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(0x221D528, 4));
    }

    [Fact]
    public void JSON_preserves_original_TIM_vibration_and_eight_camera_tracks_at_offset24()
    {
        using GameAssets game = Open();
        NativeBattleEffect321Resources resources = game.ReadNativeEffect321Resources();
        NativeBattleEffect321Resources restored = JsonSerializer.Deserialize<NativeBattleEffect321Resources>(JsonSerializer.Serialize(resources))!;
        restored.Validate();
        Assert.Equal(99872, restored.MainTim.Data.Length);
        Assert.Equal(268, restored.Camera.Data.Length);
        Assert.Equal(276, restored.Vibration.Data.Length);
        Assert.Equal(resources.Vibration.Data.ToArray(), restored.Vibration.Data.ToArray());
        NativeBattleCameraBank bank = new(restored.Camera.Data.AsSpan().Slice(24).ToArray().ToImmutableArray());
        for (byte selector = 0; selector < 8; selector++)
        {
            NativeBattleCameraState camera = NativeBattleCameraState.FromColdProcess().Start(bank, selector);
            for (int callback = 0; callback < 2000 && !camera.Order.IsEmpty; callback++)
            {
                camera = camera.Tick(0, new() { TransformReference = (_, point) => point });
            }
            Assert.Empty(camera.Order);
        }
    }

    [Fact]
    public void Catalog_binds_the_same_selected_original_resource_and_initializer_owner()
    {
        using GameAssets game = Open();
        NativeBattleEffect321Resources resources = game.ReadNativeEffect321Resources();
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([321])[320];
        Assert.Same(resources, binding.Effect321Resources);
        Assert.Equal(Entry, binding.Entry);
        Assert.Equal(Initializer, binding.Initializer);
        Assert.Equal(new NativeBattleEffectInitializerResource("mag320.tim", InitializerGlobal), binding.InitializerResource);
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed effect321 resources.");
        return new(directory!);
    }
}
