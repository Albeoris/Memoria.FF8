using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleMeltingBubble43ResourcesTests
{
    [Fact]
    public void Original_Melting_Bubble_resources_keep_flag1_backwards_spans_and_real_sprite_aliases()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original resources to check Melting Bubble.");
        using GameAssets game = new(directory!);
        NativeBattleMeltingBubble43Resources resources = game.ReadNativeMeltingBubble43Resources();
        resources.Validate();
        Assert.Same(resources, game.ReadNativeMeltingBubble43Resources());
        Assert.Equal(296, resources.Animation.Data.Length);
        Assert.Equal(0u, resources.Sound);
        Assert.Equal(260, resources.Executable.Data.Length);
        Assert.Equal(10748, resources.StaticData.Bytes.Length);
        Assert.Equal((ushort)43, resources.GenericNodes().Effect);
        Assert.Same(resources.Executable, resources.GenericNodes().Executable);
        Assert.Equal(1u, BinaryPrimitives.ReadUInt32LittleEndian(resources.Executable.Data.AsSpan()));
        Assert.Equal(0x15C5310u, BinaryPrimitives.ReadUInt32LittleEndian(resources.Executable.Data.AsSpan().Slice(4)));
        NativeBattleCuragaExecutableState retained = new(1, resources.Executable.Data) { ResourceAddress = 0x15C6080 };
        retained.Validate();
        Assert.Same(retained, retained.Initialize87EA20(resources.Executable));
        Assert.Throws<InvalidDataException>(() => retained.Read(0x15C5310, 344));
        Assert.Equal(344, resources.ReadStatic(0x15C5310, 344).Length);
        Assert.Throws<InvalidDataException>(() => resources.ReadStatic(0x15C607F, 2));
        Assert.Throws<InvalidDataException>(() => resources.ReadStatic(0x2656328, 2));
        Assert.Throws<NotSupportedException>(() => resources.ReadResource(0x15C3685));
        Assert.Throws<NotSupportedException>(() => resources.ReadResource(0x15C3D14));
        for (int index = 0; index < 16; index++)
        {
            uint sprite = BinaryPrimitives.ReadUInt32LittleEndian(resources.Executable.Data.AsSpan().Slice(4 + 64 + index * 4));
            if (sprite == 0)
            {
                continue;
            }
            Assert.True(resources.ReadResource(sprite).AsSpan().SequenceEqual(resources.ReadStatic(sprite, resources.ReadResource(sprite).Length)));
        }
        Assert.Equal(0x15C429Cu, BinaryPrimitives.ReadUInt32LittleEndian(resources.Executable.Data.AsSpan().Slice(4 + 64 + 9 * 4)));
        Assert.Equal(0x15C4448u, BinaryPrimitives.ReadUInt32LittleEndian(resources.Executable.Data.AsSpan().Slice(4 + 64 + 10 * 4)));
        Assert.Equal(948, NativeBattleMeltingBubble43Profile.ChannelRegions.Sum(region => region.Length));
        NativeBattleMeltingBubble43Resources restored = JsonSerializer.Deserialize<NativeBattleMeltingBubble43Resources>(JsonSerializer.Serialize(resources))!;
        restored.Validate();
        Assert.Equal(JsonSerializer.Serialize(resources), JsonSerializer.Serialize(restored));
        Assert.Throws<InvalidDataException>(() => (resources with { Executable = resources.Executable with { Data = resources.Executable.Data.SetItem(0, 0) } }).Validate());
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([43])[42];
        Assert.Equal(0x839940u, binding.Entry);
        Assert.Equal("mag042.tim", binding.InitializerAsset!.Name);
        Assert.Equal(167968, binding.InitializerAsset.Data.Length);
        Assert.NotNull(binding.MeltingBubble43Resources);
    }
}
