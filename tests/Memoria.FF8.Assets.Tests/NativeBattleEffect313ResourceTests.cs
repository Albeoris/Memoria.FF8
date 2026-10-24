using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect313Profile;
using static Memoria.FF8.Runtime.NativeBattleEffect313Resources;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleEffect313ResourceTests
{
    [Fact]
    public void Original_initializer_camera_and_all_selected_raw_meshes_have_bounded_extents()
    {
        using GameAssets game = Open();
        NativeBattleEffect313Resources resources = game.ReadNativeEffect313Resources();
        resources.Validate();
        Assert.Equal(199200, resources.MainTim.Data.Length);
        Assert.Equal(348, resources.Camera.Data.Length);
        Assert.Equal(5, resources.Meshes.Count);
        for (int index = 0; index < MeshAddresses.Length; index++)
        {
            Assert.Equal(MeshLengths[index], resources.ReadMesh(MeshAddresses[index]).Bytes.Length);
        }
        for (int index = 0; index < 2; index++)
        {
            uint selected = BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadStatic(MeshTableAddress + (uint)(index * 4), 4));
            Assert.Equal(696, resources.ReadMesh(selected).Bytes.Length);
        }
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(MeshTableAddress, 9));
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(ArenaGlobal, 4));
    }

    [Fact]
    public void Original_sprite_directories_preserve_negative_prefix_without_extra_frame()
    {
        using GameAssets game = Open();
        NativeBattleEffect313Resources resources = game.ReadNativeEffect313Resources();
        int[] frames = [1, 16, 16, 16];
        for (int sprite = 0; sprite < SpriteAddresses.Length; sprite++)
        {
            NativeBattleModuleResource source = resources.Sprite(SpriteAddresses[sprite]);
            Assert.Equal(frames[sprite], (int)BinaryPrimitives.ReadUInt16LittleEndian(source.Bytes.AsSpan(8, 2)));
            for (int frame = 0; frame < frames[sprite]; frame++)
            {
                _ = source.ProjectedFrame571C80(frame);
                int offset = BinaryPrimitives.ReadUInt16LittleEndian(source.Bytes.AsSpan(10 + frame * 2, 2));
                int prefix = BinaryPrimitives.ReadInt32LittleEndian(source.Bytes.AsSpan(offset, 4));
                Assert.Equal(sprite == 1, prefix < 0);
            }
            Assert.True(BinaryPrimitives.ReadInt16LittleEndian(source.Bytes.AsSpan(10 + frames[sprite] * 2, 2)) < 0);
        }
    }

    [Fact]
    public void Resource_JSON_preserves_exact_scalar_read_widths_and_offset24_camera()
    {
        using GameAssets game = Open();
        NativeBattleEffect313Resources resources = JsonSerializer.Deserialize<NativeBattleEffect313Resources>(JsonSerializer.Serialize(game.ReadNativeEffect313Resources()))!;
        resources.Validate();
        foreach (uint table in ScalarTableAddresses)
        {
            for (int index = 0; index < 5; index++)
            {
                Assert.Equal(2, resources.ReadStatic(table + (uint)(index * 4), 2).Length);
                Assert.Throws<NotSupportedException>(() => resources.ReadStatic(table + (uint)(index * 4), 3));
            }
        }
        Assert.Equal(24, resources.ReadStatic(FirstPointTableAddress, 24).Length);
        Assert.Equal(24, resources.ReadStatic(SecondPointTableAddress, 24).Length);
        NativeBattleCameraBank camera = new(resources.Camera.Data.AsSpan().Slice(24).ToArray().ToImmutableArray());
        for (byte selector = 0; selector < 8; selector++)
        {
            int? offset = camera.TrackOffset(selector);
            Assert.NotNull(offset);
            Assert.InRange(offset.Value, 0, resources.Camera.Data.Length - 25);
        }
    }

    [Fact]
    public void Selected_catalog_hydrates_the_same_original_resource_binding()
    {
        using GameAssets game = Open();
        NativeBattleEffect313Resources resources = game.ReadNativeEffect313Resources();
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([313])[312];
        Assert.Same(resources, binding.Effect313Resources);
        Assert.Equal(Entry, binding.Entry);
        Assert.Equal(Initializer, binding.Initializer);
        Assert.False(binding.InitializerIsRet);
        Assert.Equal(new NativeBattleEffectInitializerResource("mag312.tim", InitializerGlobal), binding.InitializerResource);
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed effect313 resources.");
        return new(directory!);
    }
}
