using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicPalette47Tests
{
    [Theory]
    [InlineData(0x001F, 0, 256, 31, 0)]
    [InlineData(0x03E0, 256, 256, 31, 0)]
    [InlineData(0x7C00, 512, 256, 31, 0)]
    [InlineData(0x03FF, 128, 256, 31, 0)]
    [InlineData(0x7FE0, 384, 256, 31, 0)]
    [InlineData(0x7C1F, -128, 256, 31, 0)]
    [InlineData(0x082A, -14, 230, 10, 0)]
    [InlineData(0x4210, 0, 0, 16, 0)]
    [InlineData(0xFFFF, 0, 0, 31, 0x8000)]
    public void Capture_preserves_native_signed_hue_saturation_value_and_original_flag(int color, short hue, short saturation, short value, ushort flag)
    {
        ImmutableArray<byte> rows = NativeBattleCinematicPalette47.CaptureB67710(Palette((ushort)color), 1);
        Assert.Equal(8, rows.Length);
        Assert.Equal(hue, BinaryPrimitives.ReadInt16LittleEndian(rows.AsSpan()));
        Assert.Equal(saturation, BinaryPrimitives.ReadInt16LittleEndian(rows.AsSpan().Slice(2)));
        Assert.Equal(value, BinaryPrimitives.ReadInt16LittleEndian(rows.AsSpan().Slice(4)));
        Assert.Equal(flag, BinaryPrimitives.ReadUInt16LittleEndian(rows.AsSpan().Slice(6)));
    }

    [Theory]
    [InlineData(0x001F, 128, 128, 0, 0, 0x01FF)]
    [InlineData(0x03E0, 128, 128, 0, 0, 0x03EF)]
    [InlineData(0x001F, 384, 128, 0, 0, 0x7C0F)]
    [InlineData(0x001F, -896, 256, 0, 0, 0x7C1F)]
    [InlineData(0x001F, -128, 123, 0, 0, 0x381F)]
    [InlineData(0x801F, 256, -1, 0, 0, 0x801F)]
    [InlineData(0x801F, 256, 300, 0, 0, 0x83E0)]
    [InlineData(0x801F, 0, 0, -500, 0, 0xFFFF)]
    [InlineData(0x801F, 0, 0, 500, 0, 0x801F)]
    [InlineData(0x801F, 0, 0, 0, 1, 0x8000)]
    [InlineData(0xFFFF, 0, 0, 0, 500, 0x8000)]
    [InlineData(0xFFFF, 0, 0, 0, -500, 0x8000)]
    public void Transform_uses_native_truncation_wrap_ties_clamps_and_value32_packing(int source, int target, int weight, int saturationAdd, int valueAdd, int expected)
    {
        ImmutableArray<byte> rows = NativeBattleCinematicPalette47.CaptureB67710(Palette((ushort)source), 1);
        NativeBattleCinematicPalette47Result transformed = NativeBattleCinematicPalette47.TransformB67810(rows.AsSpan(), 1, target, weight, saturationAdd, valueAdd);
        Assert.Equal((ushort)expected, BinaryPrimitives.ReadUInt16LittleEndian(transformed.Palette.AsSpan()));
    }

    [Fact]
    public void Transform_retains_adjusted_target_across_rows_and_reuses_original_capture_without_cumulative_drift()
    {
        ImmutableArray<byte> rows = NativeBattleCinematicPalette47.CaptureB67710(Palette(0x7FE0, 0x001F), 2);
        byte[] captured = rows.ToArray();
        NativeBattleCinematicPalette47Result first = NativeBattleCinematicPalette47.TransformB67810(rows.AsSpan(), 2, 0, 128, 0, 0);
        Assert.Equal(768, first.AdjustedTargetHue);
        Assert.Equal((ushort)0x7C0F, BinaryPrimitives.ReadUInt16LittleEndian(first.Palette.AsSpan()));
        Assert.Equal((ushort)0x001F, BinaryPrimitives.ReadUInt16LittleEndian(first.Palette.AsSpan().Slice(2)));
        Assert.Equal(captured, rows.ToArray());
        Assert.Equal(first.Palette.ToArray(), NativeBattleCinematicPalette47.TransformB67810(rows.AsSpan(), 2, 0, 128, 0, 0).Palette.ToArray());
        ImmutableArray<byte> red = NativeBattleCinematicPalette47.CaptureB67710(Palette(0x001F), 1);
        NativeBattleCinematicPalette47Result orange = NativeBattleCinematicPalette47.TransformB67810(red.AsSpan(), 1, 128, 128, 0, 0);
        ImmutableArray<byte> recaptured = NativeBattleCinematicPalette47.CaptureB67710(orange.Palette.AsSpan(), 1);
        Assert.NotEqual(orange.Palette.ToArray(), NativeBattleCinematicPalette47.TransformB67810(recaptured.AsSpan(), 1, 128, 128, 0, 0).Palette.ToArray());
    }

    [Fact]
    public void Endpoint_color_expands_low_five_channels_and_writes_only_two_fixed_actors_with_distinct_global_alpha()
    {
        ImmutableArray<NativeBattleCinematicPalette47ColorWrite> writes = NativeBattleCinematicPalette47.ActorColorWrites(Palette(0x001F, 0xFFFF, 0), 2);
        Assert.Equal(new NativeBattleCinematicPalette47ColorWrite(3, 0xB8BA00, 0x1D974BC, 0x03F8F8F8, 0x00F8F8F8), writes[0]);
        Assert.Equal(new NativeBattleCinematicPalette47ColorWrite(4, 0xB8BA04, 0x1D97558, 0x04F8F8F8, 0x00F8F8F8), writes[1]);
        Assert.Equal(2, writes.Length);
        Assert.Throws<InvalidDataException>(() => NativeBattleCinematicPalette47.ActorColorWrites(Palette(0), 0));
        Assert.Throws<InvalidDataException>(() => NativeBattleCinematicPalette47.ActorColorWrites(Palette(0), 2));
    }

    [Fact]
    public void Capture_and_transform_require_the_complete_requested_rows_and_preserve_native_empty_counts()
    {
        Assert.Empty(NativeBattleCinematicPalette47.CaptureB67710([], -1));
        Assert.Empty(NativeBattleCinematicPalette47.TransformB67810([], 0, -896, 0, 0, 0).Palette);
        Assert.Equal(-128, NativeBattleCinematicPalette47.TransformB67810([], 0, -896, 0, 0, 0).AdjustedTargetHue);
        Assert.Throws<InvalidDataException>(() => NativeBattleCinematicPalette47.CaptureB67710(Palette(0), 2));
        Assert.Throws<InvalidDataException>(() => NativeBattleCinematicPalette47.TransformB67810(new byte[7], 1, 0, 0, 0, 0));
        Assert.Equal(8, NativeBattleCinematicPalette47.CaptureB67710(Palette(0, 0), 1).Length);
    }

    private static byte[] Palette(params ushort[] colors)
    {
        byte[] bytes = new byte[colors.Length * 2];
        for (int index = 0; index < colors.Length; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(index * 2), colors[index]);
        }
        return bytes;
    }
}
