using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleDevour62ResourcesTests
{
    [Fact]
    public void Original_Devour_resources_retain_camera_and_sound_descriptor_directory()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original resources to check Devour.");
        using GameAssets game = new(directory!);
        NativeBattleDevour62Resources resources = game.ReadNativeDevour62Resources();
        resources.Validate();
        Assert.Same(resources, game.ReadNativeDevour62Resources());
        Assert.Equal(1968, resources.Animation.Data.Length);
        for (int index = 0; index < 9; index++)
        {
            NativeBattleModuleSound sound = resources.Sound(index);
            Assert.Equal(0x1596290u + (uint)index * 4, resources.SoundSources[index].Address);
            Assert.Equal(resources.SoundSources[index].Value, sound.Sound);
            Assert.Equal(0u, sound.Flags);
            Assert.Equal((byte?)128, sound.Pan);
            Assert.Equal((uint?)128, sound.RawPan);
            Assert.Null(sound.Anchor);
        }
        Assert.Throws<InvalidDataException>(() => resources.Sound(-1));
        Assert.Throws<InvalidDataException>(() => resources.Sound(9));
        Assert.Throws<InvalidDataException>(() => (resources with { SoundSources = resources.SoundSources.SetItem(0, resources.SoundSources[0] with { Value = 0 }) }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Animation = resources.Animation with { Data = resources.Animation.Data.RemoveAt(0) } }).Validate());
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([62])[61];
        Assert.Equal(0x7E4B00u, binding.Entry);
        Assert.Equal(0x7E4AB0u, binding.Initializer);
        Assert.NotNull(binding.Devour62Resources);
        Assert.Equal(new uint[] { 0x2601900, 0x2601904, 0x26018F8, 0x26018FC, 0x2601318 }, binding.InitializerResources!.Value.Select(resource => resource.GlobalAddress));
        NativeBattleEffectInitializerAsset[] textures = binding.InitializerAssets!.Value.ToArray();
        Assert.Equal(new[] { "mag061_0us.tim", "mag061_1.tim", "mag061_2.tim", "mag061_3.tim", "mag061.tim" }, textures.Select(texture => texture.Name).ToArray());
        Assert.Equal(new[] { 18464, 69664, 69664, 69664, 132128 }, textures.Select(texture => texture.Data.Length).ToArray());
        foreach (NativeBattleEffectInitializerAsset texture in textures)
        {
            Assert.Equal(16u, System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(texture.Data.AsSpan()));
            int cursor = 8;
            for (int block = 0; block < 2; block++)
            {
                int length = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(texture.Data.AsSpan().Slice(cursor));
                int width = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(texture.Data.AsSpan().Slice(cursor + 8));
                int height = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(texture.Data.AsSpan().Slice(cursor + 10));
                Assert.Equal(12 + width * height * 2, length);
                cursor += length;
            }
            Assert.Equal(texture.Data.Length, cursor);
        }
        NativeBattleDevour62Resources restored = JsonSerializer.Deserialize<NativeBattleDevour62Resources>(JsonSerializer.Serialize(resources))!;
        restored.Validate();
        Assert.Equal(JsonSerializer.Serialize(resources), JsonSerializer.Serialize(restored));
    }

}
