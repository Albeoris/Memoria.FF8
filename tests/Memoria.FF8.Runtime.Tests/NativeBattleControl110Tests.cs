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

    [Theory]
    [InlineData(4)]
    [InlineData(14)]
    [InlineData(33)]
    [InlineData(110)]
    public void Expanded_sweep_births_cover_every_ordinal_in_order_before_the_original_director_ends(int count)
    {
        BattleExecutionReference.Control110Sweep sweep = new(0, 0);
        List<(int Frame, int Ordinal)> births = [(0, 0)];
        for (int frame = 15; frame <= 68; frame++)
        {
            sweep = sweep.Advance(count, ordinal => births.Add((frame, ordinal)));
            Assert.InRange(sweep.Fraction(count), 0, 4095);
        }
        Assert.Equal(Enumerable.Range(0, count), births.Select(birth => birth.Ordinal));
        Assert.All(births.Skip(1), birth => Assert.Equal(14 + (54 * birth.Ordinal + count - 1) / count, birth.Frame));
        Assert.Equal(count - 1, sweep.Current);
        Assert.Equal(count - 1, sweep.Next(count));
        Assert.Equal(0, sweep.Fraction(count));
        if (count > 54)
        {
            Assert.True(births.Count(birth => birth.Frame == 15) > 1);
        }
    }

    [Theory]
    [InlineData(1, 0, 0, 0)]
    [InlineData(2, 0, 1, 4095)]
    [InlineData(3, 1, 2, 4095)]
    public void Expanded_small_sweep_projects_the_actual_original_cursor_pair(int count, int current, ushort next, ushort phase)
    {
        NativeBattleEffectTaskPool director = NativeBattleEffectTaskPool.Initialize(4, 8, 100, 1);
        NativeBattleModulePoolTask task = director.Slots[0].WriteContactCount(14, count);
        BattleExecutionReference.Control110Sweep sweep = new(current, phase);
        NativeBattleControl110SweepStorage.Write(task, sweep, count);
        Assert.Equal((ushort)count, task.ReadWord(14));
        Assert.Equal((ushort)current, task.ReadWord(28));
        Assert.Equal(next, task.ReadWord(30));
        Assert.Equal(phase, task.ReadWord(32));
        Assert.Equal(sweep, task.ReadExecutionReference<BattleExecutionReference.Control110Sweep>(28, 4));
    }

    [Fact]
    public void Expanded_retirement_copies_only_the_original_target_prefix_and_its_knownness()
    {
        const int stride = 44;
        const int offset = 256;
        const int prefix = stride * 2;
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x25219F0, 0x80000000, stride, 4);
        for (int index = 0; index < 3; index++)
        {
            (pool, int slot) = pool.Append(0x6CE610);
            pool.Slots[slot].WriteDword(16, (uint)(0x2521A30 + index)).WriteWord(32, (ushort)index);
        }
        pool.Slots[1].MarkPayloadUnknown(28, 4);
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().Write(offset - 1, [0xA8]).Write(offset + prefix, [0x5D]);
        Assert.Throws<InvalidDataException>(() => NativeBattleControl110SweepStorage.MirrorRetiredTargetPrefix(arena, offset, pool));
        pool = pool.Advance((current, _) => new(current, 2)).Pool;
        NativeBattleTaskBankOwner owner = pool.RequireOwner();
        NativeBattleEffectArena source = NativeBattleEffectArena.BorrowWorking(owner.Storage, owner.StorageOffset, prefix);
        NativeBattleEffectArena mirrored = NativeBattleControl110SweepStorage.MirrorRetiredTargetPrefix(arena, offset, pool);
        var expected = source.ReadSnapshot(0, prefix);
        var actual = mirrored.ReadSnapshot(offset, prefix);
        Assert.Equal(expected.Data, actual.Data);
        Assert.Equal(expected.Known, actual.Known);
        Assert.False(actual.Known[stride + 28]);
        Assert.Equal(new byte[] { 0xA8 }, mirrored.Read(offset - 1, 1));
        Assert.Equal(new byte[] { 0x5D }, mirrored.Read(offset + prefix, 1));
    }
}
