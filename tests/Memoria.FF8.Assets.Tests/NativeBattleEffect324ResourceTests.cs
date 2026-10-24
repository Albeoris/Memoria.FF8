using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect324Profile;
using static Memoria.FF8.Runtime.NativeBattleEffect324Resources;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleEffect324ResourceTests
{
    [Fact]
    public void Original_selected_sprite_camera_vibration_and_material_ranges_are_bounded()
    {
        using GameAssets game = Open();
        NativeBattleEffect324Resources resources = game.ReadNativeEffect324Resources();
        resources.Validate();
        Assert.Equal(66656, resources.MainTim.Data.Length);
        Assert.Equal(316, resources.Camera.Data.Length);
        Assert.Equal(268, resources.Vibration.Data.Length);
        Assert.Equal(24, resources.MaterialColors24.Length);
        for (int index = 0; index < 24; index++)
        {
            Assert.Equal(BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadStatic(0xCD1218u + (uint)(index * 4), 4)), resources.MaterialColors24[index]);
        }
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(0xCD1218, 97));
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(0xCD1210, 5));
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(0x2201AC8, 4));
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(0x220BC60, 4));
    }

    [Fact]
    public void All_selected_sprite_phases_have_the_original_negative_next_terminal()
    {
        using GameAssets game = Open();
        NativeBattleEffect324Resources resources = game.ReadNativeEffect324Resources();
        for (int index = 0; index < SpriteAddresses.Length; index++)
        {
            NativeBattleModuleResource source = resources.Sprite(SpriteAddresses[index]);
            Assert.Equal(SpriteLengths[index], source.Bytes.Length);
            Assert.Equal(SpriteFrameCounts[index], BinaryPrimitives.ReadUInt16LittleEndian(source.Bytes.AsSpan(8, 2)));
            for (int frame = 0; frame < SpriteFrameCounts[index]; frame++)
            {
                _ = source.ProjectedFrame571C80(frame);
            }
            Assert.Equal((short)-1, BinaryPrimitives.ReadInt16LittleEndian(source.Bytes.AsSpan(10 + SpriteFrameCounts[index] * 2, 2)));
        }
    }

    [Fact]
    public void Resource_JSON_preserves_actual_inputs_and_all_eight_offset24_camera_tracks()
    {
        using GameAssets game = Open();
        NativeBattleEffect324Resources resources = game.ReadNativeEffect324Resources();
        NativeBattleEffect324Resources restored = JsonSerializer.Deserialize<NativeBattleEffect324Resources>(JsonSerializer.Serialize(resources))!;
        restored.Validate();
        Assert.Equal(resources.MaterialColors24.ToArray(), restored.MaterialColors24.ToArray());
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
    public void Selected_catalog_hydrates_same_immutable_resource_binding()
    {
        using GameAssets game = Open();
        NativeBattleEffect324Resources resources = game.ReadNativeEffect324Resources();
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([324])[323];
        Assert.Same(resources, binding.Effect324Resources);
        Assert.Equal(Entry, binding.Entry);
        Assert.Equal(Initializer, binding.Initializer);
        Assert.Equal(new NativeBattleEffectInitializerResource("mag323.tim", InitializerGlobal), binding.InitializerResource);
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed effect324 resources.");
        return new(directory!);
    }
}
