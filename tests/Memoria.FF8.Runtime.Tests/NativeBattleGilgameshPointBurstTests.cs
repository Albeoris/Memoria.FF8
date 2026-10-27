using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGilgameshPointBurstTests
{
    [Fact]
    public void Dying_record_is_projected_before_age_and_rejected_prefix_keeps_cursor_and_precision_untouched()
    {
        NativeBattleEffectArena arena = Record(NativeBattleEffectArena.Clear(), 0, 15);
        NativeBattleGilgameshPointStep step = Step(Task(41), arena, 0, _ => new(0x12345678, 4096, 0x60000));
        Assert.Equal(0u, Dword(step.Arena, 0x2F800));
        Assert.Equal(2, step.Result);
        Assert.Equal(0, step.Packets.AllocationBytes);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(step.Packets.Attempts);
        Assert.Null(attempt.Primitive);
        Assert.Equal(0, attempt.ByteOffsetRelative);
        Assert.All(attempt.WriteMask.Take(8), state => Assert.Equal(NativeBattleGfPacketByteState.Known, state));
        Assert.All(attempt.WriteMask.Skip(8), state => Assert.Equal(NativeBattleGfPacketByteState.Untouched, state));
        Assert.Equal(0x6A00000Fu, BinaryPrimitives.ReadUInt32LittleEndian(attempt.Data.AsSpan().Slice(4)));
    }

    [Fact]
    public void Physics_precedes_rejection_and_uses_arithmetic_shifts_for_negative_velocity()
    {
        NativeBattleEffectArena arena = Record(NativeBattleEffectArena.Clear(), 0, 0);
        arena = WriteWord(arena, 0x2F810, -7);
        arena = WriteWord(arena, 0x2F812, -17);
        arena = WriteWord(arena, 0x2F814, -9);
        NativeBattleGilgameshPointStep step = Step(Task(41), arena, 0, point =>
        {
            Assert.Equal(new NativeBattleVector(10, 20, 30), point);
            return new(0, 1000, 0x20000);
        });
        Assert.Equal((short)3, Word(step.Arena, 0x2F808));
        Assert.Equal((short)3, Word(step.Arena, 0x2F80A));
        Assert.Equal((short)21, Word(step.Arena, 0x2F80C));
        Assert.Equal((short)-6, Word(step.Arena, 0x2F810));
        Assert.Equal((short)-19, Word(step.Arena, 0x2F812));
        Assert.Equal((short)-7, Word(step.Arena, 0x2F814));
        Assert.Equal(0, step.Result);
        Assert.Equal(0, step.Packets.AllocationBytes);
    }

    [Fact]
    public void Paused_screen_clamp_points_are_admitted_with_original_zero_proxy_depth_and_no_physics_or_counter_change()
    {
        NativeBattleEffectArena arena = Record(NativeBattleEffectArena.Clear(), 0, 4);
        NativeBattleModulePoolTask task = Task(41);
        NativeBattleGilgameshPointStep step = NativeBattleGilgameshPointBurst.Step593E80(task, arena, new(1), Resources(), 0x200, _ => new(0xABCD1234, 0x1234, 0x6000));
        Assert.Same(task, step.Task);
        Assert.Same(arena, step.Arena);
        Assert.Equal(0, step.Result);
        Assert.Equal(12, step.Packets.AllocationBytes);
        NativeBattleGeometryPrimitive primitive = Assert.Single(step.Packets.Attempts).Primitive!;
        Assert.Equal(17 + (0x1234 >> 4), primitive.Bucket);
        Assert.Equal(new uint[] { 0x02000000, 0x6A000004, 0xABCD1234 }, primitive.Words.ToArray());
        Assert.Equal(new int[] { 0, 0, 0, 0 }, primitive.VertexPayload4.ToArray());
    }

    [Fact]
    public void Full_bank_spends_no_CRT_and_one_free_physical_record_spends_exactly_four()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear();
        for (int index = 0; index < 200; index++)
        {
            arena = WriteDword(arena, 0x2F800 + index * 32, 2);
        }
        NativeBattleGilgameshPointStep full = Step(Task(0), arena, 0, _ => throw new InvalidOperationException());
        Assert.Equal(new NativeCrtRandomState(1), full.Random);
        Assert.Equal(2, full.Result);
        arena = WriteDword(arena, 0x2F800 + 199 * 32, 0);
        NativeBattleGilgameshPointStep step = Step(Task(0), arena, 0, _ => new(0, 1000, 0));
        NativeCrtRandomState expected = new(1);
        for (int index = 0; index < 4; index++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected, step.Random);
        Assert.Single(step.Packets.Attempts);
        Assert.Equal(1u, Dword(step.Arena, 0x2F800 + 199 * 32));
    }

    [Fact]
    public void Cold_JSON_keeps_current_raw_records_and_next_packet_order()
    {
        NativeBattleEffectArena arena = Record(Record(NativeBattleEffectArena.Clear(), 0, 2), 1, 5);
        NativeBattleModulePoolTask task = Task(41);
        NativeBattleGilgameshPointStep expected = Step(NativeBattleTestSnapshots.Independent(task), NativeBattleTestSnapshots.Independent(arena), 0, _ => new(100, 1024, 0));
        NativeBattleGilgameshPointStep restored = Step(NativeBattleTestSnapshots.RestoreJson<NativeBattleModulePoolTask>(NativeBattleTestSnapshots.Json(task))!, NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectArena>(NativeBattleTestSnapshots.Json(arena))!, 0, _ => new(100, 1024, 0));
        Assert.Equal(NativeBattleTestSnapshots.Json(expected), NativeBattleTestSnapshots.Json(restored));
        arena = WriteWord(arena, 0x2F804, -1);
        Assert.Throws<InvalidDataException>(() => Step(task, arena, 0, _ => new(0, 1024, 0)));
    }

    private static NativeBattleGilgameshPointStep Step(NativeBattleModulePoolTask task, NativeBattleEffectArena arena, uint flags, Func<NativeBattleVector, NativeBattleModuleProjectedVertex> project) => NativeBattleGilgameshPointBurst.Step593E80(task, arena, new(1), Resources(), flags, project);
    private static NativeBattleModulePoolTask Task(ushort counter) => new NativeBattleModulePoolTask(1, 0, null, 0x593E80, new byte[24].ToImmutableArray()).WriteWord(12, counter);

    private static NativeBattleGilgameshPointResources Resources()
    {
        byte[] colors = new byte[64];
        for (int index = 0; index < 16; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(colors.AsSpan(index * 4), 0x6A000000u | (uint)index);
        }
        return new(new("FF8_EN:CD0428", colors.ToImmutableArray()));
    }

    private static NativeBattleEffectArena Record(NativeBattleEffectArena arena, int index, int phase)
    {
        int offset = 0x2F800 + index * 32;
        arena = WriteDword(arena, offset, 1);
        arena = WriteWord(arena, offset + 4, phase);
        arena = WriteWord(arena, offset + 8, 10);
        arena = WriteWord(arena, offset + 10, 20);
        return WriteWord(arena, offset + 12, 30);
    }

    private static short Word(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2));
    private static uint Dword(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));

    private static NativeBattleEffectArena WriteWord(NativeBattleEffectArena arena, int offset, int value)
    {
        byte[] bytes = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, unchecked((ushort)value));
        return arena.Write(offset, bytes);
    }

    private static NativeBattleEffectArena WriteDword(NativeBattleEffectArena arena, int offset, uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return arena.Write(offset, bytes);
    }
}
