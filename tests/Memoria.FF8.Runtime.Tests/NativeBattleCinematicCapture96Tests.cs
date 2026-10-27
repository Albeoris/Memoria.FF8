using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicCapture96Tests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void CurrentDrawEnvironmentBankEmitsSevenLinkedPacketsWithExactMasksAndScratch(byte buffer)
    {
        NativeBattleWaterExecutionFrame frame = Fixture();
        List<(uint Address, uint Ordering, NativeBattleGfGeometryBatch Batch)> calls = [];
        NativeBattleCinematicCapture96Services services = new(_ => buffer, (_, selected) =>
        {
            Assert.Equal(buffer, selected);
            Assert.Single(calls);
            return buffer == 0 ? [0xF0, 0xFF, 1, 0] : [32, 0, 0, 1];
        }, (current, address, ordering, batch) =>
        {
            calls.Add((address, ordering, batch));
            return current;
        });
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicCapture96.Execute(frame, new(0x1000, 6, 0x80001000), services);
        Assert.Equal(new uint[] { 0x400, 0x40C, 0x424, 0x43C, 0x454, 0x46C, 0x484 }, calls.Select(call => call.Address));
        Assert.All(calls, call => Assert.Equal(0x124u, call.Ordering));
        Assert.Equal(144, calls.Sum(call => call.Batch.AllocationBytes));
        Assert.Equal(new uint[] { 0x01000000, 0xE6000000, 0 }, Assert.Single(calls[0].Batch.Attempts).Primitive!.Words);
        Assert.Equal(new uint[] { 0x01000000, 0xE6000001, 0 }, Assert.Single(calls[6].Batch.Attempts).Primitive!.Words);
        uint source = buffer == 0 ? 0x0001FFF0u : 0x01000020u;
        for (int index = 0; index < 5; index++)
        {
            NativeBattleGfGeometryAttempt move = Assert.Single(calls[index + 1].Batch.Attempts);
            Assert.Equal(new uint[] { 0x05000000, 0x01000000, 0x80000000, unchecked(source + (uint)(64 * index)), unchecked((uint)((-300 + 64 * index) & 0xFFFF)) | (500u << 16), 0x00E00040 }, move.Primitive!.Words);
            Assert.Equal(1, move.Primitive.Bucket);
        }
        foreach (var call in calls)
        {
            NativeBattleGfGeometryAttempt attempt = Assert.Single(call.Batch.Attempts);
            Assert.All(attempt.WriteMask.Take(3), state => Assert.Equal(NativeBattleGfPacketByteState.Untouched, state));
            int written = call.Batch.AllocationBytes == 12 ? 8 : 24;
            Assert.All(attempt.WriteMask.Skip(3).Take(written - 3), state => Assert.Equal(NativeBattleGfPacketByteState.Known, state));
            Assert.All(attempt.WriteMask.Skip(written), state => Assert.Equal(NativeBattleGfPacketByteState.Untouched, state));
        }
        Assert.Equal(12, Assert.Single(calls[0].Batch.Attempts).Data.Length);
        Assert.Equal(12, Assert.Single(calls[6].Batch.Attempts).Data.Length);
        Assert.Equal(0x490u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797690 + 0x7C));
        Assert.Equal(unchecked((uint)-300), result.ReadTemporaryDword(0xF0));
        Assert.Equal(500u, result.ReadTemporaryDword(0xF4));
        Assert.Equal(unchecked(source + 320), result.ReadTemporaryDword(0xF8));
        Assert.Equal(0x00E00040u, result.ReadTemporaryDword(0xFC));
        Assert.Equal(0x80001006u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Equal(frame.Arena.Read(0x4000, 16), result.Arena.Read(0x4000, 16));
    }

    [Fact]
    public void ServicesObserveNativePublicationOrderAndLiveScratchAndOwners()
    {
        NativeBattleWaterExecutionFrame frame = Fixture();
        int calls = 0;
        List<uint> orderings = [];
        List<uint> destinations = [];
        NativeBattleCinematicCapture96Services services = new(_ => 0, (current, _) =>
        {
            Assert.Equal(1, calls);
            Assert.Equal(7u, current.ReadTemporaryDword(0xF0));
            return [1, 0, 2, 0];
        }, (current, _, ordering, batch) =>
        {
            orderings.Add(ordering);
            calls++;
            if (calls == 1)
            {
                current = current.WriteTemporaryDword(0xF0, 7).WriteTemporaryDword(0xF4, 8).WriteTemporaryDword(0x14C, 0x200);
            }
            if (calls is >= 2 and <= 6)
            {
                destinations.Add(Assert.Single(batch.Attempts).Primitive!.Words[4]);
                current = current.WriteTemporaryDword(0xF0, 10000).WriteTemporaryDword(0xF4, 20000);
            }
            if (calls == 2)
            {
                current = WaterSchedulerFixture.CoreDword(current, 0x27973EC, 0x2797800);
                current = WaterSchedulerFixture.CoreDword(current, 0x2797450, 0x12340000);
            }
            return current;
        });
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicCapture96.Execute(frame, new(0x1000, 6, 0x80001000), services);
        Assert.Equal(new uint[] { 0x124, 0x204, 0x204, 0x204, 0x204, 0x204, 0x204 }, orderings);
        Assert.Equal(Enumerable.Range(0, 5).Select(index => (8u << 16) | (uint)(7 + 64 * index)), destinations);
        Assert.Equal(0x400u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797690 + 0x7C));
        Assert.Equal(0x490u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797800 + 0x7C));
        Assert.Equal(0x12340006u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
    }

    [Fact]
    public void EmptyNativeMoveBuilderStillLinksLengthZeroAndReservesItsSlot()
    {
        NativeBattleWaterExecutionFrame frame = Fixture();
        List<NativeBattleGfGeometryBatch> batches = [];
        NativeBattleCinematicCapture96Services services = new(_ => 0, (_, _) => [0, 0, 0, 0], (current, _, _, batch) =>
        {
            batches.Add(batch);
            return batches.Count == 2 ? current.WriteTemporaryDword(0xFC, 0) : current;
        });
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicCapture96.Execute(frame, new(0x1000, 6, 0x80001000), services);
        foreach (NativeBattleGfGeometryBatch batch in batches.Skip(2).Take(4))
        {
            Assert.Equal(24, batch.AllocationBytes);
            NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.Attempts);
            Assert.Equal(new uint[6], attempt.Primitive!.Words);
            Assert.Equal(24, attempt.Data.Length);
            Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[3]);
            Assert.All(attempt.WriteMask.Skip(4), state => Assert.Equal(NativeBattleGfPacketByteState.Untouched, state));
        }
        Assert.Equal(0x490u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797690 + 0x7C));
    }

    [Fact]
    public void SnapshotRetainsUnknownUnconsumedOperandAndTemporaryBytes()
    {
        NativeBattleWaterExecutionFrame frame = Fixture().WriteTemporaryWord(0x14A, 0xF096);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x1000, 2).MarkUnknown(0x1006, 2), TemporaryFrame = frame.TemporaryFrame.MarkUnknown(0xEE, 1) };
        NativeBattleWaterExecutionFrame restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(SnapshotJson(frame))!);
        NativeBattleCinematicCapture96Services services = new(_ => 0, (_, _) => [0, 0, 0, 0], (current, _, _, _) => current);
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicCapture96.Execute(restored, new(0x1000, 6, 0x80001000), services);
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(0x1006, 1));
        Assert.False(result.TemporaryFrame.IsKnown(0xEE, 1));
        Assert.Equal(SnapshotJson(NativeBattleCinematicCapture96.Execute(frame, new(0x1000, 6, 0x80001000), services)), SnapshotJson(result));
    }

    private static NativeBattleWaterExecutionFrame Fixture()
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryWord(0x14A, 0x96).WriteTemporaryDword(0x14C, 0x120);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797450, 0x80001000);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797690 + 0x7C, 0x400);
        byte[] program = new byte[6];
        BinaryPrimitives.WriteInt16LittleEndian(program.AsSpan(2), -300);
        BinaryPrimitives.WriteInt16LittleEndian(program.AsSpan(4), 500);
        return frame with { Arena = frame.Arena.Write(0x1000, program) };
    }

    private static string SnapshotJson<T>(T value) => JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(value));
}
