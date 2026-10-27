using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleBio22PaletteTests
{
    [Theory]
    [InlineData(0, 0x801F)]
    [InlineData(2048, 0xC210)]
    [InlineData(4096, 0xFFE0)]
    [InlineData(-4096, 0x843E)]
    public void Shared_weighted_morph_rounds_products_separately_and_forces_STP(short ratio, int expected)
    {
        NativeBattleBio22Palette current = new(Tim(0x1234));
        NativeBattleBio22Palette next = current.Morph89CB90(Tim(31), Tim(0x7FE0), ratio);
        Assert.All(Enumerable.Range(0, 128), index => Assert.Equal((ushort)expected, BinaryPrimitives.ReadUInt16LittleEndian(next.Bytes.AsSpan().Slice(0x14 + index * 2))));
        Assert.Equal(current.Bytes.Take(0x14), next.Bytes.Take(0x14));
        Assert.Equal(current.Bytes.Skip(0x114), next.Bytes.Skip(0x114));
        NativeBattleBio22Palette black = current.Morph89CB90(Tim(0), Tim(0), ratio);
        Assert.Equal((ushort)0x8000, BinaryPrimitives.ReadUInt16LittleEndian(black.Bytes.AsSpan().Slice(0x14)));
    }

    [Fact]
    public void Palette_callback_preserves_initial_ratio_and_queues_even_on_terminal_signal()
    {
        NativeBattleBio22Resources resources = NativeBattleBio22GeometryTaskTests.Fixture().Resources;
        resources = resources with { PaletteSources = [new("FF8_EN:1614D48", Tim(31)), new("FF8_EN:1614B28", Tim(0)), new("FF8_EN:1614908", Tim(0x7C00))] };
        NativeBattleBio22Palette palette = new(Tim(0x1234));
        NativeBattleModulePoolTask task = Task().WriteWord(0x36, 17);
        NativeBattleModulePoolTask primary = new(1, 0, null, 1, new byte[0x98 - 12].ToImmutableArray());
        NativeCrtRandomState random = new(1);
        NativeBattleBio22PaletteStep initialized = NativeBattleBio22PaletteTask.Advance89CA70(task, primary, palette, resources, random);
        Assert.False(initialized.QueueTim);
        Assert.Equal((ushort)17, initialized.Task.ReadWord(0x36));
        Assert.Equal(random, initialized.Random);
        NativeBattleBio22PaletteStep terminal = NativeBattleBio22PaletteTask.Advance89CA70(initialized.Task.WriteWord(0x36, 0), primary.WriteWord(0x80, 1), palette, resources, random);
        Assert.True(terminal.QueueTim);
        Assert.Equal((byte)2, terminal.Task.ReadByte(0x29));
        Assert.Equal(2u, terminal.Result);
        Assert.Equal((ushort)0x8010, BinaryPrimitives.ReadUInt16LittleEndian(terminal.Palette.Bytes.AsSpan().Slice(0x14)));
        NativeBattleBio22PaletteStep retained = NativeBattleBio22PaletteTask.Advance89CA70(terminal.Task, primary, terminal.Palette, resources, terminal.Random);
        Assert.False(retained.QueueTim);
        Assert.Same(terminal.Palette, retained.Palette);
    }

    [Fact]
    public void Wrap_consumes_one_random_and_JSON_preserves_next_palette_and_child_hold()
    {
        NativeBattleBio22Resources resources = NativeBattleBio22GeometryTaskTests.Fixture().Resources;
        NativeBattleModulePoolTask task = Task().WriteByte(0x29, 1).WriteByte(0x28, 1).WriteWord(0x32, 0).WriteWord(0x34, 2).WriteWord(0x36, 0x800).WriteWord(0x24, 0xFFFF);
        NativeBattleModulePoolTask primary = new NativeBattleModulePoolTask(1, 0, null, 1, new byte[0x98 - 12].ToImmutableArray()).WriteWord(0x80, 1);
        NativeCrtRandomState random = new(17);
        NativeCrtRandomState expectedRandom = random.Draw(out ushort value);
        NativeBattleBio22Palette palette = new(Tim(0));
        NativeBattleBio22PaletteStep expected = NativeBattleBio22PaletteTask.Advance89CA70(NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(task)), NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(primary)), NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(palette)), resources, random);
        Assert.Equal(expectedRandom, expected.Random);
        Assert.Equal((ushort)2, expected.Task.ReadWord(0x32));
        Assert.Equal((ushort)((2 + (value & 1) + 1) % 3), expected.Task.ReadWord(0x34));
        Assert.Equal((ushort)0, expected.Task.ReadWord(0x24));
        Assert.Equal(0u, expected.Result);
        NativeBattleBio22PaletteStep restored = NativeBattleBio22PaletteTask.Advance89CA70(RoundTrip(task), RoundTrip(primary), RoundTrip(palette), RoundTrip(resources), RoundTrip(random));
        Assert.Equal(NativeBattleTestSnapshots.Json(expected), NativeBattleTestSnapshots.Json(restored));
        Assert.Null(NativeBattleEffectMemory.FromColdProcess().Bio22Palette);
        Assert.Throws<InvalidDataException>(() => NativeBattleBio22Palette.FromColdProcess(new("FF8_EN:1614908", Tim(0))));
    }

    private static T RoundTrip<T>(T value) => NativeBattleTestSnapshots.RestoreJson<T>(NativeBattleTestSnapshots.Json(value))!;
    private static NativeBattleModulePoolTask Task() => new(1, 0, null, 0x89CA70, new byte[0x38 - 12].ToImmutableArray());

    internal static ImmutableArray<byte> Tim(ushort color)
    {
        byte[] bytes = new byte[0x220];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 0x10);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 9);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), 0x20C);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0xC), 0x140);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0xE), 0xF1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x10), 256);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x12), 1);
        for (int index = 0; index < 256; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x14 + index * 2), color);
        }
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0x214), 12);
        return bytes.ToImmutableArray();
    }
}
