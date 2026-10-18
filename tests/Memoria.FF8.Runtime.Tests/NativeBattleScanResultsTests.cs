using System.Collections.Immutable;
using System.Text;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleScanResultsTests
{
    [Theory]
    [InlineData(0u, "0")]
    [InlineData(100000u, "0")]
    [InlineData(123456u, "23456")]
    [InlineData(4294967295u, "67295")]
    public void Numeric_formatting_uses_unsigned_low_five_digits_and_actual_digit_bytes(uint value, string expected)
    {
        Assert.Equal(expected + "\0", Encoding.ASCII.GetString(NativeBattleScanResults.Format495330(value, digit => (byte)('0' + digit)).ToArray()));
    }

    [Theory]
    [InlineData(99999u, 100000u, "L 15 H 99999/?")]
    [InlineData(100000u, 99999u, "L 15 H ?/99999")]
    [InlineData(4294967295u, 1000u, "L 15 H 67295/1000")]
    public void Current_and_maximum_HP_use_independent_signed_thresholds(uint current, uint maximum, string expected)
    {
        ScanResultTestSource source = new();
        source.Descriptor = source.Descriptor.WriteDword(0x18, current).WriteDword(0x1C, maximum);
        NativeBattleScanResultMemory result = NativeBattleScanResults.BuildB68390(NativeBattleScanResultMemory.FromColdProcess(), 0, source.Services);
        Assert.Equal(1u, result.Count);
        Assert.Equal(expected, source.Page(result, 0));
    }

    [Fact]
    public void Hidden_HP_emits_actual_unknown_template_and_does_not_read_HP_or_affinity_fields()
    {
        ScanResultTestSource source = new();
        source.Descriptor = ScanResultTestSource.Unknown(208).WriteDword(0x7C, 0x2000).WriteByte(0xBC, 15);
        NativeBattleScanResultMemory result = NativeBattleScanResults.BuildB68390(NativeBattleScanResultMemory.FromColdProcess(), 0, source.Services);
        Assert.Equal(1u, result.Count);
        Assert.Equal("L 15 H ?/?", source.Page(result, 0));
    }

    [Theory]
    [InlineData(0, null)]
    [InlineData(1, "A C  ")]
    [InlineData(2, "B C  ")]
    [InlineData(3, "A B C ")]
    public void Enemy_flags_use_masks_one_and_two_and_preserve_all_empty_slot_separators(byte flags, string? expected)
    {
        ScanResultTestSource source = new() { InfoFlags = flags };
        NativeBattleScanResultMemory result = NativeBattleScanResults.BuildB68390(NativeBattleScanResultMemory.FromColdProcess(), 3, source.Services);
        Assert.Equal(3, source.LastInfoActor);
        Assert.False(source.Descriptor.Known[0]);
        Assert.Equal(expected is null ? 1u : 2u, result.Count);
        if (expected is not null)
        {
            Assert.Equal(expected, source.Page(result, 1));
        }
    }

    [Fact]
    public void Affinity_boundaries_exclude_exact_800_and_keep_native_group_and_element_order()
    {
        ScanResultTestSource source = new();
        ushort[] values = [600, 601, 799, 800, 801, 899, 900, 901];
        for (int element = 0; element < 8; element++)
        {
            source.Descriptor = source.Descriptor.WriteWord(0x44 + element * 2, values[element]);
        }
        NativeBattleScanResultMemory result = NativeBattleScanResults.BuildB68390(NativeBattleScanResultMemory.FromColdProcess(), 0, source.Services);
        Assert.Equal(6u, result.Count);
        Assert.Equal("a A       ", source.Page(result, 1));
        Assert.Equal("b B C      ", source.Page(result, 2));
        Assert.Equal("c E F      ", source.Page(result, 3));
        Assert.Equal("d G       ", source.Page(result, 4));
        Assert.Equal("e H       ", source.Page(result, 5));
        Assert.Equal((byte)7, result.Element8.ReadByte(0));
        Assert.Equal((byte)5, result.Element8.ReadByte(1));
    }

    [Fact]
    public void Direct_extra_page_keeps_physical_pointer_and_ring_cursor_does_not_advance_for_it()
    {
        ScanResultTestSource source = new();
        source.Extra = source.Extra.Write(0, new byte[] { (byte)'X', 0 });
        NativeBattleScanResultMemory initial = NativeBattleScanResultMemory.FromColdProcess() with { Cursor = 15 };
        NativeBattleScanResultMemory result = NativeBattleScanResults.BuildB68390(initial, 4, source.Services);
        Assert.Equal(2u, result.Count);
        Assert.Equal(NativeBattleScanResults.RingAddress + 15u * 48, NativeBattleScanResults.PageB68810(result, 0, 0));
        Assert.Equal(NativeBattleScanResults.EnemyExtraAddress + 71u, NativeBattleScanResults.PageB68810(result, 255, 1));
        Assert.Equal((byte)16, result.Cursor);
        Assert.Equal("X", source.Page(result, 1));
        NativeBattleScanResultMemory next = NativeBattleScanResults.BuildB68390(result, 4, source.Services);
        Assert.Equal(NativeBattleScanResults.RingAddress, NativeBattleScanResults.PageB68810(next, 4, 0));
        Assert.Equal((byte)1, next.Cursor);
    }

    [Fact]
    public void Shared_scratch_and_ring_copy_only_through_zero_preserving_warm_tail_and_unpublished_page_slots()
    {
        NativeBattleScanResultMemory memory = NativeBattleScanResultMemory.FromColdProcess() with { Scratch48 = ScanResultTestSource.Unknown(48), Ring768 = ScanResultTestSource.Unknown(768), Pages32 = ScanResultTestSource.Unknown(32).WriteDword(28, 0x11223344), Cursor = 0xFF };
        memory = NativeBattleScanResults.Concat495210(memory, [(byte)'A', 0], 7, [(byte)'B', 0]);
        Assert.Equal(new byte[] { (byte)'A', (byte)'B', 0 }, memory.Scratch48.Bytes.Take(3));
        Assert.False(memory.Scratch48.Known[3]);
        (memory, uint pointer) = NativeBattleScanResults.Store495280(memory, [(byte)'Z', 0]);
        Assert.Equal(NativeBattleScanResults.RingAddress + 15u * 48, pointer);
        Assert.Equal((byte)16, memory.Cursor);
        Assert.False(memory.Ring768.Known[15 * 48 + 2]);
        Assert.Equal(0x11223344u, NativeBattleScanResults.PageB68810(memory, 42, 263));
    }

    [Fact]
    public void Missing_actual_kernel_or_enemy_extra_owner_fails_without_empty_text_replacement()
    {
        ScanResultTestSource source = new();
        Assert.Throws<NotSupportedException>(() => NativeBattleScanResults.BuildB68390(NativeBattleScanResultMemory.FromColdProcess(), 0, source.Services with { ReadMiscText = _ => default }));
        source.Extra = ScanResultTestSource.Unknown(71);
        Assert.Throws<NotSupportedException>(() => NativeBattleScanResults.BuildB68390(NativeBattleScanResultMemory.FromColdProcess(), 3, source.Services));
        Assert.Throws<NotSupportedException>(() => NativeBattleScanResults.Store495280(NativeBattleScanResultMemory.FromColdProcess(), default));
    }

    [Fact]
    public void Independent_results_JSON_continuation_preserves_ring_wrap_and_actual_pointer_aliases()
    {
        ScanResultTestSource source = new() { InfoFlags = 3 };
        NativeBattleScanResultMemory initial = NativeBattleScanResultMemory.FromColdProcess() with { Cursor = 15 };
        NativeBattleScanResultMemory cloned = JsonSerializer.Deserialize<NativeBattleScanResultMemory>(JsonSerializer.Serialize(initial))!;
        NativeBattleScanResultMemory left = NativeBattleScanResults.BuildB68390(initial, 3, source.Services);
        NativeBattleScanResultMemory right = NativeBattleScanResults.BuildB68390(cloned, 3, source.Services);
        Assert.Equal(JsonSerializer.Serialize(left), JsonSerializer.Serialize(right));
        Assert.Equal((byte)1, left.Cursor);
        Assert.Throws<InvalidDataException>(() => NativeBattleScanResults.PageB68810(left, 0, 8));
    }
}

internal sealed class ScanResultTestSource
{
    public NativeBattleScanByteOwner Descriptor { get; set; }
    public NativeBattleScanByteOwner Extra { get; set; } = Unknown(71).WriteByte(0, 0);
    public byte InfoFlags { get; init; }
    public int? LastInfoActor { get; private set; }
    public NativeBattleScanResultServices Services => new(_ => Descriptor, Misc, actor =>
    {
        LastInfoActor = actor;
        return InfoFlags;
    }, _ => Extra);

    public ScanResultTestSource()
    {
        Descriptor = Unknown(208).WriteDword(0x18, 125).WriteDword(0x1C, 1000).WriteDword(0x7C, 0).WriteByte(0xBC, 15);
        for (int element = 0; element < 8; element++)
        {
            Descriptor = Descriptor.WriteWord(0x44 + element * 2, 800);
        }
    }

    public string Page(NativeBattleScanResultMemory memory, int index)
    {
        uint pointer = NativeBattleScanResults.PageB68810(memory, 0, index);
        List<byte> bytes = [];
        while (true)
        {
            byte value = NativeBattleScanResults.ReadByte(memory, pointer++, Read);
            if (value == 0)
            {
                return Encoding.ASCII.GetString(bytes.ToArray());
            }
            bytes.Add(value);
        }
    }

    private byte Read(uint pointer)
    {
        for (int actor = 3; actor < 7; actor++)
        {
            uint offset = unchecked(pointer - NativeBattleScanResults.EnemyExtraAddress - (uint)(71 * (actor - 3)));
            if (offset < 71)
            {
                return Extra.ReadByte((int)offset);
            }
        }
        throw new NotSupportedException("Missing required source byte.");
    }

    private static ImmutableArray<byte> Misc(int id)
    {
        if (id == 0x0B)
        {
            byte[] numbers = Enumerable.Repeat((byte)'!', 20).ToArray();
            numbers[0] = (byte)' ';
            for (int digit = 0; digit < 10; digit++)
            {
                numbers[digit + 1] = (byte)('0' + digit);
            }
            numbers[0x12] = (byte)'/';
            numbers[^1] = 0;
            return numbers.ToImmutableArray();
        }
        char value = id switch
        {
            0x21 => 'L',
            0x22 => 'H',
            0x71 => '?',
            0x72 => 'A',
            0x63 => 'B',
            0x24 => 'C',
            >= 0x25 and <= 0x29 => (char)('a' + id - 0x25),
            >= 0x65 and <= 0x6C => (char)('A' + id - 0x65),
            _ => throw new NotSupportedException("Missing required kernel text.")
        };
        return [(byte)value, 0];
    }

    public static NativeBattleScanByteOwner Unknown(int length) => new(Enumerable.Repeat((byte)0xA5, length).ToImmutableArray(), Enumerable.Repeat(false, length).ToImmutableArray());
}
