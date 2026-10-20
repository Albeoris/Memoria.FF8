using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRinoa89MemoryTests
{
    [Fact]
    public void Entry_particle_initialization_preserves_every_unassigned_tail()
    {
        byte[] retained = Enumerable.Repeat((byte)0xA5, 48132).ToArray();
        NativeBattleRinoa89Frame frame = Frame(NativeBattleEffectArena.Clear().Write(0, retained));
        frame = NativeBattleRinoa89Controller.ClearEmitters6FA2C0(frame);
        frame = NativeBattleRinoa89Controller.ClearSparks6FA2F0(frame);
        NativeBattleEffectArena arena = frame.Memory.Arena!;
        Assert.Equal(new byte[] { 0, 0xA5, 0xA5 }, arena.Read(16960, 3));
        Assert.Equal(new byte[] { 0xA5, 0, 0, 0xA5 }, arena.Read(4681, 4));
        Assert.Equal(0x20DFAB8u + 16960, BinaryPrimitives.ReadUInt32LittleEndian(frame.Staging.Read(0x2556594, 4)));
        Assert.Equal(0x20DFAB8u + 4672, BinaryPrimitives.ReadUInt32LittleEndian(frame.Staging.Read(0x2556590, 4)));
    }

    [Fact]
    public void Shard_initialization_consumes_four_CRT_draws_per_rotation_and_only_clears_life_words()
    {
        NativeBattleRinoa89Frame frame = Frame(NativeBattleEffectArena.Clear().Write(24228, Enumerable.Repeat((byte)0xA5, 120 * 52).ToArray()));
        NativeCrtRandomState expected = frame.Random;
        for (int index = 0; index < 256; index++)
        {
            expected = expected.Draw(out _);
        }
        frame = NativeBattleRinoa89Controller.InitializeShardRotations6FA320(frame);
        Assert.Equal(expected, frame.Random);
        Assert.Equal(new byte[] { 0xA5, 0, 0, 0xA5 }, frame.Memory.Arena!.Read(24233, 4));
        Assert.Throws<NotSupportedException>(() => frame.Memory.Arena.Read(30468 + 18, 2));
    }

    [Fact]
    public void Spark_allocator_scans_from_the_first_record_and_wraps_after_the_last()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear();
        arena = arena.Write(4672 + 10, BitConverter.GetBytes((ushort)1));
        NativeBattleRinoa89Frame frame = Frame(arena);
        frame = frame with { Staging = frame.Staging.Write(0x2556590, BitConverter.GetBytes(0x20DFAB8u + 4672)) };
        NativeBattleRinoa89SparkAllocation first = NativeBattleRinoa89Controller.AllocateSpark6FCC90(frame);
        Assert.Equal(4696, first.Offset);
        Assert.Equal(0x20DFAB8u + 4720, BinaryPrimitives.ReadUInt32LittleEndian(first.Frame.Staging.Read(0x2556590, 4)));
        frame = frame with { Staging = frame.Staging.Write(0x2556590, BitConverter.GetBytes(0x20DFAB8u + 16936)) };
        NativeBattleRinoa89SparkAllocation last = NativeBattleRinoa89Controller.AllocateSpark6FCC90(frame);
        Assert.Equal(16936, last.Offset);
        Assert.Equal(0x20DFAB8u + 4672, BinaryPrimitives.ReadUInt32LittleEndian(last.Frame.Staging.Read(0x2556590, 4)));
    }

    [Fact]
    public void Spark_physics_applies_Z_acceleration_to_position_without_storing_Z_velocity()
    {
        byte[] spark = new byte[24];
        Set(4, 100);
        Set(6, -100);
        Set(8, 200);
        Set(10, 1);
        Set(12, 5);
        Set(14, -6);
        Set(16, 7);
        spark[20] = 2;
        spark[21] = unchecked((byte)-3);
        spark[22] = 4;
        NativeBattleRinoa89Frame frame = NativeBattleRinoa89Controller.AdvanceSparks6FCCF0(Frame(NativeBattleEffectArena.Clear().Write(4672, spark)));
        byte[] actual = frame.Memory.Arena!.Read(4672, 24);
        Assert.Equal((short)105, Word(4));
        Assert.Equal((short)-106, Word(6));
        Assert.Equal((short)223, Word(8));
        Assert.Equal((short)0, Word(10));
        Assert.Equal((short)13, Word(12));
        Assert.Equal((short)-18, Word(14));
        Assert.Equal((short)7, Word(16));

        void Set(int offset, short value) => BinaryPrimitives.WriteInt16LittleEndian(spark.AsSpan(offset), value);
        short Word(int offset) => BinaryPrimitives.ReadInt16LittleEndian(actual.AsSpan(offset));
    }

    [Theory]
    [InlineData(250, 10, 251, -10)]
    [InlineData(2, -10, 7, 10)]
    [InlineData(200, -2, 198, -2)]
    public void Star_alpha_reflects_the_native_unsigned_WORD_sum(int alpha, int increment, int expectedAlpha, int expectedIncrement)
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().Write(31748 + 11, [(byte)alpha]).Write(31748 + 15, [unchecked((byte)increment)]);
        NativeBattleRinoa89Frame frame = Frame(arena);
        frame = frame with { Staging = frame.Staging.Write(0x14AF498, new byte[128]) };
        frame = NativeBattleRinoa89Controller.AdvanceStars6FCF70(frame);
        Assert.Equal((byte)expectedAlpha, frame.Memory.Arena!.Read(31748 + 11, 1)[0]);
        Assert.Equal(unchecked((byte)expectedIncrement), frame.Memory.Arena.Read(31748 + 15, 1)[0]);
    }

    private static NativeBattleRinoa89Frame Frame(NativeBattleEffectArena arena)
    {
        return new(new(arena, 0, 0, ImmutableArray.CreateRange(new NativeBattleEffectHeapAllocation?[256]), 0, 0, 0, 0), null!, NativeBattleFileStaging.Unknown(), new(1));
    }
}
