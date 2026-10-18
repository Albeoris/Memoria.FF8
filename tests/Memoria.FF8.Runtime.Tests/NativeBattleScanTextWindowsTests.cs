using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleScanTextWindowsTests
{
    [Fact]
    public void Text_setup_writes_only_native_fields_and_keeps_retained_handle_scale_and_unknown_bytes()
    {
        NativeBattleScanTextWindows windows = Windows();
        NativeBattleScanTextWindows updated = windows.SetText4A0410(2, 0x12345678).SetValue49FBC0(2, 0xABCD).Open4A0640(2);
        NativeBattleScanByteOwner row = updated.Row(2);
        Assert.Equal(0x12345678u, row.ReadDword(8));
        Assert.Equal(0x12345678u, row.ReadDword(12));
        Assert.Equal((ushort)0xABCD, row.ReadWord(0x10));
        Assert.Equal((ushort)0, row.ReadWord(0x12));
        Assert.Equal((ushort)0, row.ReadWord(0x14));
        Assert.Equal((byte)1, row.ReadByte(0x16));
        Assert.Equal((byte)0x77, row.ReadByte(0x17));
        Assert.Equal((byte)37, row.ReadByte(0x18));
        Assert.Equal((ushort)4096, row.ReadWord(0x1C));
        Assert.Equal((ushort)4096, row.ReadWord(0x1E));
        Assert.Equal((byte)255, row.ReadByte(0x29));
        Assert.Equal((byte)255, row.ReadByte(0x2A));
        Assert.False(row.Known[0x2B]);
        Assert.Same(windows.Row(0), updated.Row(0));
        NativeBattleScanTextWindows closed = updated.Close4A0680(2);
        Assert.Equal((ushort)0xF000, closed.Row(2).ReadWord(0x1E));
        Assert.Equal((byte)1, closed.Row(2).ReadByte(0x16));
        for (int index = 0; index < 60; index++)
        {
            if (index is not (0x1E or 0x1F))
            {
                Assert.Equal(row.Bytes[index], closed.Row(2).Bytes[index]);
                Assert.Equal(row.Known[index], closed.Row(2).Known[index]);
            }
        }
    }

    [Theory]
    [InlineData(0, 8, 0x38000000u)]
    [InlineData(1, 9, 0x3A000000u)]
    [InlineData(254, 254, 0x38000000u)]
    public void Backend_flags_use_actual_retained_handle_and_preserve_every_other_byte(byte flags, byte stored, uint command)
    {
        NativeBattleScanTextWindows windows = Windows().SetFlags4A0700(2, flags);
        NativeBattleScanByteOwner backend = windows.Backend(37);
        Assert.Equal(stored, backend.ReadByte(0x38));
        Assert.Equal(command, backend.ReadDword(0x30));
        Assert.Equal(5, backend.Known.Count(value => value));
        Assert.Throws<NotSupportedException>(() => windows.Backend(2));
    }

    [Fact]
    public void Scan_rectangles_keep_original_rows_but_publish_scaled_outer_and_clamped_inner()
    {
        NativeBattleScanTextWindows windows = Windows();
        NativeBattleScanRect name = new(170, 30, 136, 28);
        windows = windows.SetRect4A07A0(2, name);
        Assert.Equal(name.ToBytes(), windows.Row(2).Bytes.AsSpan(0, 8).ToArray());
        Assert.Equal((byte)1, windows.Row(2).ReadByte(0x23));
        Assert.Equal(name.ToBytes(), windows.Backend(37).Bytes.AsSpan(8, 8).ToArray());
        Assert.Equal(new NativeBattleScanRect(176, 36, 124, 16).ToBytes(), windows.Backend(37).Bytes.AsSpan(0x10, 8).ToArray());
        NativeBattleScanByteOwner scaled = windows.Row(2).WriteWord(0x1C, 0);
        windows = windows with { TextRows = windows.TextRows.SetItem(2, scaled) };
        windows = windows.SetRect4A07A0(2, name);
        Assert.Equal(new NativeBattleScanRect(238, 44, 0, 0).ToBytes(), windows.Backend(37).Bytes.AsSpan(8, 8).ToArray());
        Assert.Equal(new NativeBattleScanRect(244, 50, 2, 1).ToBytes(), windows.Backend(37).Bytes.AsSpan(0x10, 8).ToArray());
        Assert.False(windows.Row(2).Known[0x1E]);
    }

    [Fact]
    public void Rectangle_scaling_uses_logical_shifts_on_signed_dimensions_and_wraps_word_stores()
    {
        NativeBattleScanRect original = new(-10, -20, -3, -5);
        Assert.Equal(new NativeBattleScanRect(-11, -21, -2, -3), NativeBattleScanTextWindows.Scale4A03A0(original, 2048));
        Assert.Same(original, NativeBattleScanTextWindows.Scale4A03A0(original, 4096));
        NativeBattleScanTextWindows windows = Windows().SetRect4A07A0(2, new(0, 0, 10, -20));
        Assert.Equal((byte)254, windows.Row(2).ReadByte(0x23));
        NativeBattleScanTextWindows restored = JsonSerializer.Deserialize<NativeBattleScanTextWindows>(JsonSerializer.Serialize(windows))!;
        Assert.Equal(JsonSerializer.Serialize(windows.Close4A0680(2)), JsonSerializer.Serialize(restored.Close4A0680(2)));
    }

    private static NativeBattleScanTextWindows Windows()
    {
        NativeBattleScanByteOwner row = Unknown(60).WriteByte(0x18, 37).WriteWord(0x1C, 4096);
        return new(Enumerable.Repeat(Unknown(60), 8).ToImmutableArray().SetItem(2, row), ImmutableDictionary<byte, NativeBattleScanByteOwner>.Empty.Add(37, Unknown(64)));
    }

    private static NativeBattleScanByteOwner Unknown(int length) => new(new byte[length].ToImmutableArray(), new bool[length].ToImmutableArray());
}
