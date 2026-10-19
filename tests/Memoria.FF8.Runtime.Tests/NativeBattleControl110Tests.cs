using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleControl110Tests
{
    [Fact]
    public void Normalization_uses_signed_Z_range_without_subtracting_minimum_and_keeps_XYZ()
    {
        byte[] source = new byte[308 * 8];
        for (int index = 0; index < 308; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(source.AsSpan(index * 8), 123);
            BinaryPrimitives.WriteInt16LittleEndian(source.AsSpan(index * 8 + 4), index == 0 ? (short)-100 : (short)300);
            BinaryPrimitives.WriteUInt16LittleEndian(source.AsSpan(index * 8 + 6), 0xAABB);
        }
        ImmutableArray<byte> actual = NativeBattleControl110Motion.Normalize(source.ToImmutableArray());
        Assert.Equal((short)-1024, BinaryPrimitives.ReadInt16LittleEndian(actual.AsSpan().Slice(6)));
        Assert.Equal((short)3072, BinaryPrimitives.ReadInt16LittleEndian(actual.AsSpan().Slice(14)));
        Assert.Equal(source.AsSpan(0, 6).ToArray(), actual.AsSpan().Slice(0, 6).ToArray());
        Assert.Equal((ushort)0xAABB, BinaryPrimitives.ReadUInt16LittleEndian(source.AsSpan(6)));
    }

    [Fact]
    public void Native_stream_matrix_scales_basis_columns_and_keeps_the_plane_translation()
    {
        NativeBattleActionMatrix basis = new([4096, 0, 0, 0, 0, -4096, 0, 4096, 0], 0, 0, 0, 4096);
        NativeBattleActionMatrix actual = NativeBattleControl110Motion.StreamMatrix(basis, 2000, 8, new(12, -4000, -4000));
        Assert.Equal((short)-2000, actual.Rotation[5]);
        Assert.Equal((short)4096, actual.Rotation[7]);
        Assert.Equal((12, -4000, -4000), (actual.X, actual.Y, actual.Z));
        NativeBattleActionMatrix born = NativeBattleControl110Motion.StreamMatrix(basis, 2000, 0, new(12, -4000, -4000));
        Assert.Equal((short)0, born.Rotation[5]);
        Assert.Equal((short)4096, born.Rotation[7]);
    }

    [Fact]
    public void Reused_native_debris_motion_bounces_after_gravity_then_reverses_all_angular_velocities()
    {
        NativeBattleModulePoolTask task = NativeBattleEffectTaskPool.Initialize(4, 8, 48, 1).Slots[0];
        task = task.WriteWord(18, unchecked((ushort)-1)).WriteWord(26, 0).WriteWord(40, 10).WriteWord(42, unchecked((ushort)-20)).WriteWord(44, 30);
        task = NativeBattleStorm11Motion.MoveDebris6EB790(task, out bool ended);
        Assert.False(ended);
        Assert.Equal((ushort)19, task.ReadWord(18));
        Assert.Equal(unchecked((ushort)-40), task.ReadWord(26));
        Assert.Equal(unchecked((ushort)-10), task.ReadWord(40));
        Assert.Equal((ushort)20, task.ReadWord(42));
        Assert.Equal(unchecked((ushort)-30), task.ReadWord(44));
    }
}
