using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect316Profile;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleEffect316ResourceTests
{
    [Fact]
    public void Original_selected_fronts_have_exact_extents_and_no_current_geometry_clone()
    {
        using GameAssets game = Open();
        NativeBattleEffect316Resources resources = game.ReadNativeEffect316Resources();
        resources.Validate();
        Assert.Equal(67616, resources.MainTim.Data.Length);
        Assert.Equal(66080, resources.EmbeddedTim.Length);
        Assert.Equal(256, resources.Camera.Data.Length);
        Assert.Equal(270, resources.Vibration.Data.Length);
        foreach ((uint address, int length) in NativeBattleEffect316Resources.StaticRanges)
        {
            Assert.Equal(length, resources.ReadStatic(address, length).Length);
            Assert.Throws<NotSupportedException>(() => resources.ReadStatic(address, length + 1));
        }
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(RandomGlobal, 4));
        Assert.Throws<NotSupportedException>(() => resources.ReadProjectedQuads(ProjectedQuadsAddress + 1));
    }

    [Fact]
    public void Native_life_directory_selects_exact_original_one_and_two_quad_records()
    {
        using GameAssets game = Open();
        NativeBattleModuleResource source = game.ReadNativeEffect316Resources().ReadProjectedQuads(ProjectedQuadsAddress);
        int[] counts = [1, 1, 2, 1, 2, 1, 1, 1, 1, 1, 1];
        for (int life = 11; life >= 1; life--)
        {
            int index = 12 - life;
            int offset = BinaryPrimitives.ReadUInt16LittleEndian(source.Bytes.AsSpan(8 + index * 2, 2));
            int count = BinaryPrimitives.ReadInt32LittleEndian(source.Bytes.AsSpan(offset, 4));
            Assert.Equal(counts[11 - life], count);
            Assert.InRange(offset + 4 + 20 * count, 0, source.Bytes.Length);
        }
        Assert.Equal((short)-1, BinaryPrimitives.ReadInt16LittleEndian(source.Bytes.AsSpan(34, 2)));
    }

    [Fact]
    public void Resource_JSON_preserves_both_TIMs_and_actual_offset24_camera_tracks()
    {
        using GameAssets game = Open();
        NativeBattleEffect316Resources resources = game.ReadNativeEffect316Resources();
        NativeBattleEffect316Resources restored = JsonSerializer.Deserialize<NativeBattleEffect316Resources>(JsonSerializer.Serialize(resources))!;
        restored.Validate();
        Assert.Equal(resources.EmbeddedTim.ToArray(), restored.EmbeddedTim.ToArray());
        Assert.Equal(resources.MainTim.Data.ToArray(), restored.MainTim.Data.ToArray());
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
    public void Selected_catalog_binds_authentic_entry_initializer_and_cached_resources()
    {
        using GameAssets game = Open();
        NativeBattleEffect316Resources resources = game.ReadNativeEffect316Resources();
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([316])[315];
        Assert.Same(resources, binding.Effect316Resources);
        Assert.Equal(Entry, binding.Entry);
        Assert.Equal(Initializer, binding.Initializer);
        Assert.Equal(new NativeBattleEffectInitializerResource("mag315.tim", InitializerGlobal), binding.InitializerResource);
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed effect316 resources.");
        return new(directory!);
    }
}
