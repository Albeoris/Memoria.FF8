using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicBreakupAuxiliaryTests
{
    private const uint Basis = NativeBattleEffectNodeMemory.ArenaAddress;
    private const uint Payload = Basis + 0x4000;
    private const uint State = Basis + 0x5000;
    private const uint Fragment = Basis + 0x6000;
    private const uint Pool = Basis + 0x10000;
    private const uint Output = Basis + 0x50000;

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void Initialization_clears_only_active_bytes_and_ring_in_the_actual_allocator_bank(ushort mode)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(mode);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x10000, 56) };
        frame = NativeBattleCinematicBreakupAuxiliary.Initialize(frame, State, Payload);
        var snapshot = frame.Arena.ReadSnapshot(0x10000, 56);
        Assert.Equal(new[] { 24, 52 }, Enumerable.Range(0, 56).Where(index => snapshot.Known[index]));
        Assert.Equal(Pool, WaterSchedulerFixture.ReadSlotDword(frame, 0, 0xA0));
        Assert.Equal(Pool + 56, WaterSchedulerFixture.ReadCoreDword(frame, 0x2797704));
        Assert.Equal((ushort)0, Word(frame, State + 0x1C));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(65535, 1)]
    [InlineData(0, 2)]
    [InlineData(1, 2)]
    [InlineData(65535, 2)]
    [InlineData(0, 5)]
    [InlineData(1, 5)]
    [InlineData(65535, 5)]
    [InlineData(0, 6)]
    [InlineData(1, 6)]
    [InlineData(65535, 6)]
    [InlineData(0, 7)]
    [InlineData(1, 7)]
    [InlineData(65535, 7)]
    public void Emitter_uses_old_unsigned_ring_modulo_and_six_ordered_draws(int old, ushort mode)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(mode);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x10000, 56) };
        frame = PutWord(frame, State + 0x1C, (ushort)old);
        NativeCrtRandomState expected = frame.Random;
        for (int index = 0; index < 6; index++)
        {
            expected = expected.Draw(out _);
        }
        frame = NativeBattleCinematicBreakupAuxiliary.Emit(frame, Fragment);
        uint row = Pool + (uint)((old % 2) * 28);
        Assert.Equal(unchecked((ushort)(old + 1)), Word(frame, State + 0x1C));
        Assert.Equal(expected, frame.Random);
        Assert.Equal(256u, Dword(frame, row));
        Assert.Equal(512u, Dword(frame, row + 4));
        Assert.Equal(768u, Dword(frame, row + 8));
        for (int index = 0; index < 6; index++)
        {
            Assert.Equal((ushort)(10 + index), Word(frame, row + (uint)(12 + index * 2)));
        }
        var snapshot = frame.Arena.ReadSnapshot(checked((int)(row - Basis)), 28);
        Assert.All(snapshot.Known.Take(26), Assert.True);
        Assert.All(snapshot.Known.Skip(26), Assert.False);
        Assert.Equal((byte)1, snapshot.Data[24]);
        Assert.Equal((byte)2, snapshot.Data[25]);
    }

    [Theory]
    [InlineData(100, true, 25, 2)]
    [InlineData(16380, false, 4095, 2)]
    [InlineData(-4, true, -1, 2)]
    [InlineData(100, true, 25, 7)]
    [InlineData(16380, false, 4095, 7)]
    [InlineData(-4, true, -1, 7)]
    public void Draw_preserves_partial_packet_and_separate_D8_cursor_even_when_link_rejected(int quarterDepth, bool linked, int bucket, ushort mode)
    {
        NativeBattleWaterExecutionFrame frame = NativeBattleCinematicBreakupAuxiliary.Emit(Fixture(mode), Fragment).WriteTemporaryByte(0x145, 1);
        int projected = 0;
        int published = 0;
        int appended = 0;
        NativeBattleCinematicBreakupAuxiliaryServices services = new((current, position) =>
        {
            projected++;
            Assert.Equal(new NativeBattleVector(2, 3, 4), position);
            return new(current, 0x12345678, quarterDepth, 0x80040000);
        }, (current, payload) =>
        {
            published++;
            Assert.Equal(Enumerable.Repeat(unchecked(quarterDepth << 2), 4), payload);
            return current;
        }, (current, output, ordering, batch) =>
        {
            appended++;
            Assert.Equal(Output, output);
            Assert.Equal(unchecked(0x1000u + (uint)(bucket * 4)), ordering);
            Assert.Equal(12, batch.AllocationBytes);
            Assert.Equal(!linked, batch.NativeUnlinkedAllocation);
            NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.Attempts);
            Assert.Equal(9, attempt.WriteMask.Count(value => value == NativeBattleGfPacketByteState.Known));
            Assert.Equal(0x6A030201u, BinaryPrimitives.ReadUInt32LittleEndian(attempt.Data.AsSpan().Slice(4)));
            Assert.Equal(0x12345678u, BinaryPrimitives.ReadUInt32LittleEndian(attempt.Data.AsSpan().Slice(8)));
            Assert.Equal(linked, attempt.Primitive is not null);
            return current;
        });
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicBreakupAuxiliary.AdvanceDraw(frame, services);
        Assert.Equal(1, projected);
        Assert.Equal(1, published);
        Assert.Equal(1, appended);
        Assert.Equal(Output + 12, WaterSchedulerFixture.ReadCoreDword(result, 0x2797768));
        Assert.Equal(0x12345678u, WaterSchedulerFixture.ReadCoreDword(result, 0x279770C));
        Assert.Equal(0x80040000u, result.ReadTemporaryDword(0xFC));
        Assert.Equal((ushort)21, Word(result, Pool + 12));
    }

    [Fact]
    public void Fixed_position_emitter_matches_record_source_without_requiring_a_published_fragment()
    {
        NativeBattleWaterExecutionFrame frame = Fixture();
        NativeBattleWaterExecutionFrame fromRecord = NativeBattleCinematicBreakupAuxiliary.Emit(frame, Fragment);
        NativeBattleWaterExecutionFrame fromValues = NativeBattleCinematicBreakupAuxiliary.Emit(frame, 256, 512, 768);
        Assert.Equal(fromRecord.Random, fromValues.Random);
        Assert.Equal(fromRecord.Arena.Read(0x10000, 28), fromValues.Arena.Read(0x10000, 28));
        Assert.Equal(Word(fromRecord, State + 0x1C), Word(fromValues, State + 0x1C));
    }

    [Fact]
    public void Expired_particle_clears_active_without_motion_projection_or_packet_writes()
    {
        NativeBattleWaterExecutionFrame frame = NativeBattleCinematicBreakupAuxiliary.Emit(Fixture(), Fragment);
        frame = frame with { Arena = frame.Arena.Write(0x10019, [0]) };
        NativeBattleCinematicBreakupAuxiliaryServices services = new((_, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), (_, _, _, _) => throw new InvalidOperationException());
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicBreakupAuxiliary.AdvanceDraw(frame, services);
        Assert.Equal((byte)0, result.Arena.Read(0x10018, 1)[0]);
        Assert.Equal((byte)255, result.Arena.Read(0x10019, 1)[0]);
        Assert.Equal(256u, Dword(result, Pool));
        Assert.Equal(Output, WaterSchedulerFixture.ReadCoreDword(result, 0x2797768));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(65535)]
    public void Unsupported_mode_is_rejected_by_each_auxiliary_entry_point(ushort mode)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(mode);
        NativeBattleCinematicBreakupAuxiliaryServices services = new((_, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), (_, _, _, _) => throw new InvalidOperationException());
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicBreakupAuxiliary.Initialize(frame, State, Payload));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicBreakupAuxiliary.Emit(frame, Fragment));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicBreakupAuxiliary.Emit(frame, 256, 512, 768));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicBreakupAuxiliary.AdvanceDraw(frame, services));
    }

    private static NativeBattleWaterExecutionFrame Fixture(ushort mode = 7)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryDword(0x84, State).WriteTemporaryDword(0x8C, Pool).WriteTemporaryDword(0x90, Payload).WriteTemporaryDword(0x14C, 0x1000);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797704, Pool);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797768, Output);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x279770C, 0x12345678);
        frame = PutWord(frame, Payload + 8, mode);
        frame = PutWord(frame, Payload + 0x2E, 2);
        frame = frame with { Arena = frame.Arena.Write(0x4032, [2]) };
        for (int index = 0; index < 6; index++)
        {
            frame = PutWord(frame, Payload + (uint)(0x34 + index * 4), (ushort)(10 + index));
        }
        frame = PutDword(frame, Fragment + 4, 256);
        frame = PutDword(frame, Fragment + 8, 512);
        frame = PutDword(frame, Fragment + 12, 768);
        frame = PutDword(frame, State + 12, Basis + 0x7000);
        return PutDword(frame, Basis + 0x7004, 0x00030201);
    }

    private static NativeBattleWaterExecutionFrame PutWord(NativeBattleWaterExecutionFrame frame, uint address, ushort value) => frame with { Arena = frame.Arena.Write(checked((int)(address - Basis)), BitConverter.GetBytes(value)) };
    private static NativeBattleWaterExecutionFrame PutDword(NativeBattleWaterExecutionFrame frame, uint address, uint value) => frame with { Arena = frame.Arena.Write(checked((int)(address - Basis)), BitConverter.GetBytes(value)) };
    private static ushort Word(NativeBattleWaterExecutionFrame frame, uint address) => BinaryPrimitives.ReadUInt16LittleEndian(frame.Arena.Read(checked((int)(address - Basis)), 2));
    private static uint Dword(NativeBattleWaterExecutionFrame frame, uint address) => BinaryPrimitives.ReadUInt32LittleEndian(frame.Arena.Read(checked((int)(address - Basis)), 4));
}
