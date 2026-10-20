using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleWall189Tests
{
    [Fact]
    public void Entry_clears_only_each_static_row_flag_dword()
    {
        byte[] bytes = Enumerable.Range(0, 48).Select(value => (byte)(value + 1)).ToArray();
        ImmutableArray<byte> result = NativeBattleWall189Motion.ClearFlags(bytes.ToImmutableArray(), 2);
        Assert.Equal(new byte[4], result.AsSpan().Slice(0, 4).ToArray());
        Assert.Equal(new byte[4], result.AsSpan().Slice(24, 4).ToArray());
        Assert.Equal(bytes.AsSpan(4, 20).ToArray(), result.AsSpan().Slice(4, 20).ToArray());
        Assert.Equal(bytes.AsSpan(28, 20).ToArray(), result.AsSpan().Slice(28, 20).ToArray());
    }

    [Fact]
    public void Mesh_particle_keeps_signed_arithmetic_damping_and_low_word_rotation()
    {
        byte[] bytes = new byte[24];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 1);
        Word(4, 11);
        Word(6, 129);
        Word(10, -100);
        Word(14, -25);
        Word(16, 120);
        Word(18, 32760);
        Word(20, -60);
        Word(22, -32760);
        ImmutableArray<byte> result = NativeBattleWall189Motion.Mesh(bytes.ToImmutableArray());
        Assert.Equal(12, Read(4));
        Assert.Equal(113, Read(6));
        Assert.Equal(-125, Read(10));
        Assert.Equal(-21, Read(14));
        Assert.Equal(-32656, Read(18));
        Assert.Equal(32716, Read(22));
        void Word(int offset, short value) => BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), value);
        short Read(int offset) => BinaryPrimitives.ReadInt16LittleEndian(result.AsSpan().Slice(offset));
    }

    [Fact]
    public void Mesh_particle_last_frame_releases_flags_without_moving_retained_coordinates()
    {
        byte[] bytes = Enumerable.Repeat((byte)0xA5, 24).ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(4), 15);
        ImmutableArray<byte> result = NativeBattleWall189Motion.Mesh(bytes.ToImmutableArray());
        Assert.Equal(new byte[4], result.AsSpan().Slice(0, 4).ToArray());
        Assert.Equal(bytes.AsSpan(6).ToArray(), result.AsSpan().Slice(6).ToArray());
    }

    [Fact]
    public void Capture_uses_top_down_scaled_stage_and_native_integer_division()
    {
        NativeBattleCameraProjection capture = NativeBattleWall189Motion.Capture(new(-35, 1234, -15));
        Assert.Equal(0, capture.View!.X);
        Assert.Equal(0, capture.View.Y);
        Assert.Equal(32767, capture.View.Z);
        Assert.Equal((ushort)32767, capture.FocalLength);
        Assert.Equal((short)128, capture.OffsetX);
        Assert.Equal((short)128, capture.OffsetY);
    }
}
