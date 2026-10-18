using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleBio22PaletteTests
{
    [Fact]
    public void Three_original_palette_sources_keep_directory_order_and_current_TIM_has_one_cold_owner()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleBio22Resources resources = game.ReadNativeBio22Resources();
        resources.Validate();
        uint[] addresses = [0x1614D48, 0x1614B28, 0x1614908];
        Assert.Equal("FF8_EN:1614F68", resources.PalettePointers.Identity);
        for (int index = 0; index < addresses.Length; index++)
        {
            Assert.Equal(addresses[index], BinaryPrimitives.ReadUInt32LittleEndian(resources.PalettePointers.Data.AsSpan().Slice(index * 4)));
            Assert.Equal($"FF8_EN:{addresses[index]:X}", resources.PaletteSources[index].Identity);
            Assert.Equal(0x220, resources.PaletteSources[index].Data.Length);
            NativeBattleBio22Palette.ValidateTim(resources.PaletteSources[index].Data);
        }
        Assert.Equal(resources.PaletteSources[2].Data.ToArray(), resources.PaletteBaseline.Data.ToArray());
        NativeBattleEffectMemory memory = game.ReadColdProcessNativeBattleMemory().EffectMemory!;
        Assert.Equal(resources.PaletteBaseline.Data.ToArray(), memory.Bio22Palette!.Bytes.ToArray());
        Assert.Equal("FF8_EN:162698C", resources.LaterPlanarCounts.Identity);
        Assert.Equal(25, resources.LaterPlanarCounts.Data.Length);
        Assert.Equal("FF8_EN:16269AC", resources.LaterBillboardCounts.Identity);
        Assert.Equal(21, resources.LaterBillboardCounts.Data.Length);
    }

    [Fact]
    public void Original_palette_morph_JSON_next_preserves_all_bytes_outside_first_half_CLUT()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleBio22Resources resources = game.ReadNativeBio22Resources();
        NativeBattleBio22Palette current = NativeBattleBio22Palette.FromColdProcess(resources.PaletteBaseline);
        NativeBattleBio22Palette next = current.Morph89CB90(resources.PaletteSources[0].Data, resources.PaletteSources[1].Data, 2048);
        Assert.Equal(current.Bytes.Take(0x14), next.Bytes.Take(0x14));
        Assert.Equal(current.Bytes.Skip(0x114), next.Bytes.Skip(0x114));
        Assert.False(current.Bytes.SequenceEqual(next.Bytes));
        for (int index = 0; index < 128; index++)
        {
            Assert.NotEqual(0, BinaryPrimitives.ReadUInt16LittleEndian(next.Bytes.AsSpan().Slice(0x14 + index * 2)) & 0x8000);
        }
        NativeBattleBio22Palette restored = JsonSerializer.Deserialize<NativeBattleBio22Palette>(JsonSerializer.Serialize(next))!;
        Assert.Equal(JsonSerializer.Serialize(next.Morph89CB90(resources.PaletteSources[1].Data, resources.PaletteSources[2].Data, -1)), JsonSerializer.Serialize(restored.Morph89CB90(resources.PaletteSources[1].Data, resources.PaletteSources[2].Data, -1)));
        Assert.Throws<InvalidDataException>(() => (resources with { PaletteSources = resources.PaletteSources.Reverse().ToImmutableArray() }).Validate());
    }
}
