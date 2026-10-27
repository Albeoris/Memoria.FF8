using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicWaveTests
{
    private const uint Arena = NativeBattleEffectNodeMemory.ArenaAddress;
    private const uint Manager = 0x2797690;

    [Fact]
    public void Wave_preserves_native_row_aliases_half_row_depth_history_and_reserved_packet_gaps()
    {
        NativeBattleWaterExecutionFrame source = Fixture();
        NativeBattleWaterExecutionFrame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleWaterExecutionFrame>(NativeBattleTestSnapshots.Json(source))!;
        (NativeBattleWaterExecutionFrame frame, NativeBattleGfGeometryBatch packets, int projections) = Run(source);
        Assert.Equal(528, projections);
        Assert.Equal(496, packets.Attempts.Count(attempt => attempt.Primitive is not null));
        Assert.Equal(0x6800, packets.AllocationBytes);
        Assert.Equal(Arena + 0x50A0, WaterSchedulerFixture.ReadCoreDword(frame, Manager + 0x74));
        Assert.Equal(0x7800u, WaterSchedulerFixture.ReadCoreDword(frame, Manager + 0x7C));
        Assert.Equal(Arena + 0x5000, WaterSchedulerFixture.ReadSlotDword(frame, 0, 0xBC));
        Assert.Equal((ushort)32, Word(frame, 0x5006));
        Assert.Equal((ushort)31, Word(frame, 0x509E));
        Assert.Throws<NotSupportedException>(() => frame.Arena.Read(0x5000, 4));
        NativeBattleGfGeometryAttempt first = packets.Attempts.First(attempt => attempt.Primitive is not null);
        Assert.Equal(272, first.Primitive!.Bucket);
        Assert.Equal(new[] { 0, 0, 0, 0 }, first.Primitive.VertexPayload4);
        Assert.Equal(0x3E010203u, Dword(first, 4));
        Assert.Equal((ushort)0x03FF, Word(first, 12));
        Assert.Equal((ushort)0x0300, Word(first, 48));
        Assert.Equal((ushort)0xF0, Word(first, 26));
        NativeBattleGfGeometryAttempt secondSector = Assert.Single(packets.Attempts, attempt => attempt.Primitive is not null && attempt.ByteOffsetRelative == 0x680);
        Assert.Equal((ushort)0xFFFE, Word(secondSector, 48));
        NativeBattleGfGeometryAttempt last = packets.Attempts.Last(attempt => attempt.Primitive is not null);
        Assert.Equal(104, last.Data.Length);
        Assert.Equal(13, last.Primitive!.Words.Length);
        Assert.All(last.WriteMask.Skip(52), state => Assert.Equal(NativeBattleGfPacketByteState.Untouched, state));
        Assert.Equal(17u, frame.ReadTemporaryDword(0x7C));
        foreach (int offset in new[] { 0x20, 0x30, 0x40, 0x50 })
        {
            Assert.Equal(0u, frame.ReadTemporaryDword(offset));
        }
        Assert.Same(source.Random, frame.Random);
        var independent = Run(restored);
        Assert.Equal(NativeBattleTestSnapshots.Json(frame), NativeBattleTestSnapshots.Json(independent.Frame));
        Assert.Equal(NativeBattleTestSnapshots.Json(packets), NativeBattleTestSnapshots.Json(independent.Packets));
    }

    [Fact]
    public void Existing_wave_state_does_not_read_the_initial_payload_or_allocate_again()
    {
        var first = Run(Fixture());
        NativeBattleWaterExecutionFrame input = first.Frame with { Arena = first.Frame.Arena.MarkUnknown(0x3000, 8) };
        var result = Run(input);
        Assert.Equal(WaterSchedulerFixture.ReadCoreDword(input, Manager + 0x74), WaterSchedulerFixture.ReadCoreDword(result.Frame, Manager + 0x74));
        Assert.Equal(0xE000u, WaterSchedulerFixture.ReadCoreDword(result.Frame, Manager + 0x7C));
        Assert.Equal(528, result.Projections);
    }

    private static (NativeBattleWaterExecutionFrame Frame, NativeBattleGfGeometryBatch Packets, int Projections) Run(NativeBattleWaterExecutionFrame source)
    {
        int count = 0;
        NativeBattleGfGeometryBatch? packets = null;
        NativeBattleCinematicWaveServices services = new((_, selector) =>
        {
            Assert.Equal((short)1, selector);
            return Arena + 0x2100;
        }, frame => frame, (frame, _) =>
        {
            int current = count++;
            if (current < 32)
            {
                Assert.Equal(source.ReadTemporaryDword(0xF0), frame.ReadTemporaryDword(0xF0));
            }
            return new(frame.WriteTemporaryDword(0xFC, unchecked(0x10000000u + (uint)current)), unchecked((uint)(current | current << 16)), 1000 + current);
        }, sector => (byte)(((sector - 1) & 3) << 6), address => checked((int)(address / 4)), (frame, _, batch) =>
        {
            packets = batch;
            return frame;
        });
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicWave.Draw(source, services);
        return (result, packets!, count);
    }

    private static NativeBattleWaterExecutionFrame Fixture()
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryDword(0x14C, 0x44);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x27973B8, frame.TemporaryFrame.Address + 0x100);
        frame = WaterSchedulerFixture.CoreDword(frame, Manager + 0x74, Arena + 0x5000);
        frame = WaterSchedulerFixture.CoreDword(frame, Manager + 0x7C, 0x1000);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xB8, Arena + 0x3000);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x90, 32);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x92, 0x60);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xCC, 0xFF010203);
        frame = WaterSchedulerFixture.SlotWord(frame, 1, 0x94, 0xFFFF);
        frame = WaterSchedulerFixture.SlotWord(frame, 1, 0x96, 3);
        frame = WaterSchedulerFixture.SlotWord(frame, 1, 0x98, 256);
        byte[] payload = new byte[8];
        ushort[] values = [0x3C, 0x100, 0x80, 1];
        for (int index = 0; index < values.Length; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(index * 2), values[index]);
        }
        return frame with { Arena = frame.Arena.Write(0x3000, payload).MarkUnknown(0x5000, 0x1200) };
    }

    private static ushort Word(NativeBattleWaterExecutionFrame frame, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(frame.Arena.Read(offset, 2));
    private static ushort Word(NativeBattleGfGeometryAttempt attempt, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(attempt.Data.AsSpan().Slice(offset, 2));
    private static uint Dword(NativeBattleGfGeometryAttempt attempt, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(attempt.Data.AsSpan().Slice(offset, 4));
}
