using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicPointCloud27Tests
{
    [Fact]
    public void InitializesOriginalRecordsWithLiteralCrtSequenceAndRetainsUnwrittenBytes()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0, 0);
        byte[] payload = new byte[12];
        Word(payload, 0, 1);
        Word(payload, 2, 10);
        Word(payload, 4, 20);
        Word(payload, 6, 30);
        Word(payload, 8, 8);
        Word(payload, 10, 256);
        frame = frame with { Arena = frame.Arena.Write(0x3000, payload).Write(0x5000, new byte[] { 7, 0 }).MarkUnknown(0x5012, 2) };
        int appends = 0;
        NativeBattleCinematicPointCloud27Services services = new(current => current, (current, address) =>
        {
            Assert.Equal(Address(0x5004), address);
            Assert.Equal(new byte[] { 0, 0, 0xF5, 0xFF, 5, 0 }, current.Arena.Read(0x5004, 6));
            Assert.Equal(new byte[] { 1, 0 }, current.Arena.Read(0x500A, 2));
            Assert.Equal(0x68131C36u, Dword(current, 0x500C));
            Assert.Equal(new byte[] { 57, 0 }, current.Arena.Read(0x5010, 2));
            Assert.Equal(8, BinaryPrimitives.ReadUInt16LittleEndian(current.Arena.Read(0x5000, 2)));
            Assert.Equal(Address(0x5014), WaterSchedulerFixture.ReadCoreDword(current, 0x2797690 + 0x74));
            return new(current, 0x12345678, -5);
        }, (current, output, ordering, batch) =>
        {
            appends++;
            Assert.Equal(0x400u, output);
            Assert.Equal(0xFF8u, ordering);
            NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.Attempts);
            Assert.Equal(new uint[] { 0x02000000, 0x68131C36, 0x12345678 }, attempt.Primitive!.Words);
            Assert.All(attempt.WriteMask.Take(3), state => Assert.Equal(NativeBattleGfPacketByteState.Untouched, state));
            Assert.All(attempt.WriteMask.Skip(3), state => Assert.Equal(NativeBattleGfPacketByteState.Known, state));
            Assert.Equal(12, batch.AllocationBytes);
            Assert.Equal(new byte[] { 56, 0 }, current.Arena.Read(0x5010, 2));
            return current;
        });
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicPointCloud27.Draw(frame, services);
        Assert.Equal(1, appends);
        Assert.Equal(Address(0x5000), WaterSchedulerFixture.ReadSlotDword(result, 0, 0xBC));
        Assert.Equal(0x40Cu, WaterSchedulerFixture.ReadCoreDword(result, 0x2797690 + 0x7C));
        Assert.Equal(0xFFFFFFFBu, Dword(result, 0x5018));
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(0x5012, 1));
        Assert.Equal(256u, result.ReadTemporaryDword(0xF0));
    }

    [Fact]
    public void ProjectsAllPointsBeforeBlinkAndKeepsTheFrameWhichTransitionsToHidden()
    {
        NativeBattleWaterExecutionFrame frame = Warm(3);
        frame = frame with { Arena = frame.Arena.Write(0x401A, new byte[] { 1 }).Write(0x4020, new byte[] { 1, 0 }).Write(0x402A, new byte[] { 1 }).Write(0x4030, new byte[] { 0, 1 }) };
        int projected = 0;
        List<uint> orderings = [];
        NativeBattleCinematicPointCloud27Services services = new(current => current, (current, address) =>
        {
            Assert.Empty(orderings);
            Assert.Equal(Address(0x4004 + 16 * projected), address);
            int index = projected++;
            return new(current, unchecked(0x00100020u + (uint)index), index == 0 ? -1 : 0x10004);
        }, (current, _, ordering, _) =>
        {
            Assert.Equal(3, projected);
            orderings.Add(ordering);
            return current;
        });
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicPointCloud27.Draw(frame, services);
        Assert.Equal(new uint[] { 0xFFC, 0x1004 }, orderings);
        Assert.Equal(new byte[] { 0, 0 }, result.Arena.Read(0x4020, 2));
        Assert.Equal(new byte[] { 36, 0 }, result.Arena.Read(0x4030, 2));
        Assert.Equal(0x418u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797690 + 0x7C));
        Assert.Equal(Address(0x5000), WaterSchedulerFixture.ReadCoreDword(result, 0x2797690 + 0x74));
        Assert.Equal(2, BinaryPrimitives.ReadUInt16LittleEndian(result.Arena.Read(0x4000, 2)));
    }

    [Fact]
    public void ZeroHiddenCounterUnderflowsAndFixedDepthUsesCurrentSlotWithoutCueDivision()
    {
        NativeBattleWaterExecutionFrame frame = Warm(2);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0xC0, 0xFFFF);
        frame = frame with { Arena = frame.Arena.Write(0x401A, new byte[] { 1 }).Write(0x4020, new byte[] { 0, 0 }) };
        int appends = 0;
        NativeBattleCinematicPointCloud27Services services = new(current => current, (current, _) => new(current, 0xFFFFFFFF, int.MinValue), (current, _, ordering, _) =>
        {
            Assert.Equal(0x4FFCu, ordering);
            appends++;
            return current;
        });
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicPointCloud27.Draw(frame, services);
        Assert.Equal(1, appends);
        Assert.Equal(new byte[] { 0, 255 }, result.Arena.Read(0x4020, 2));
        Assert.Equal(1u, result.Random.Seed);
    }

    [Fact]
    public void NegativeRetainedCountOnlyUpdatesAgeAndAnchorWithoutReadingPayloadOrRecords()
    {
        NativeBattleWaterExecutionFrame frame = Warm(-1);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x20B8, 4).MarkUnknown(0x20CC, 4).MarkUnknown(0x4004, 16) };
        int anchors = 0;
        NativeBattleCinematicPointCloud27Services services = new(current =>
        {
            anchors++;
            return current;
        }, (_, _) => throw new InvalidOperationException(), (_, _, _, _) => throw new InvalidOperationException());
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicPointCloud27.Draw(frame, services);
        Assert.Equal(1, anchors);
        Assert.Equal(2, BinaryPrimitives.ReadUInt16LittleEndian(result.Arena.Read(0x4000, 2)));
        Assert.Equal(0x400u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797690 + 0x7C));
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(0x4004, 1));
    }

    [Fact]
    public void SnapshotPreservesPaddingAndCanonicalRandomThroughTheSameNextDraw()
    {
        NativeBattleWaterExecutionFrame frame = Warm(1);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x400B, 1).MarkUnknown(0x4012, 2), TemporaryFrame = frame.TemporaryFrame with { Known = frame.TemporaryFrame.Known.SetItem(0xEE, false) } };
        NativeBattleWaterExecutionFrame restored = JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(JsonSerializer.Serialize(frame))!;
        NativeBattleCinematicPointCloud27Services services = new(current => current, (current, _) => new(current, 0, 0), (current, _, _, _) => current);
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicPointCloud27.Draw(restored, services);
        Assert.False(result.TemporaryFrame.Known[0xEE]);
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(0x400B, 1));
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(0x4012, 1));
        Assert.Equal(JsonSerializer.Serialize(NativeBattleCinematicPointCloud27.Draw(frame, services)), JsonSerializer.Serialize(result));
    }

    [Theory]
    [InlineData(false, false, -1)]
    [InlineData(true, false, -1)]
    [InlineData(false, true, -1)]
    [InlineData(true, true, -1)]
    [InlineData(false, false, 0)]
    [InlineData(true, false, 0)]
    [InlineData(false, false, 1)]
    [InlineData(true, false, 1)]
    public void Batched_points_preserve_blink_random_unknowns_and_last_successful_packet_publication(bool commonPackets, bool hidden, int failingAppend)
    {
        NativeBattleWaterExecutionFrame original = Warm(3);
        for (int point = 0; point < 3; point++)
        {
            int record = 0x4004 + point * 16;
            original = original with { Arena = original.Arena.Write(record + 6, [1]).Write(record + 12, hidden ? [0, 1] : [1, 0]).MarkUnknown(record + 14, 2) };
        }
        original = original with { Arena = original.Arena.WriteResourcePointer(0x7000, new(null, 123)).MarkUnknown(0x6000, 36) };
        original = WaterSchedulerFixture.CoreDword(original, 0x2797690 + 0x7C, commonPackets ? 0x40u : Address(0x6000));
        NativeBattlePrimitiveProcess sourceProcess = NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400() with { ByteCursor = 0x40 };
        string originalBytes = JsonSerializer.Serialize(original);
        var scalar = Run(false);
        var batched = Run(true);
        Assert.Equal(JsonSerializer.Serialize(scalar.Frame), JsonSerializer.Serialize(batched.Frame));
        Assert.Equal(JsonSerializer.Serialize(scalar.Published), JsonSerializer.Serialize(batched.Published));
        Assert.Equal(JsonSerializer.Serialize(scalar.Process), JsonSerializer.Serialize(batched.Process));
        Assert.Equal(JsonSerializer.Serialize(scalar.Ordering), JsonSerializer.Serialize(batched.Ordering));
        Assert.Equal(originalBytes, JsonSerializer.Serialize(original));
        Assert.Null(batched.Published.Transaction);
        Assert.Null(batched.Process.CommonTransaction);
        Assert.Equal(original.Arena.ReadResourcePointer(0x7000), batched.Published.ReadResourcePointer(0x7000));
        Assert.Throws<NotSupportedException>(() => batched.Published.Read(0x4012, 1));
        if (failingAppend < 0)
        {
            Assert.NotNull(batched.Frame);
            Assert.Null(batched.Frame.Arena.Transaction);
            Assert.NotEqual(original.Random, batched.Frame.Random);
            Assert.Equal(commonPackets ? 0x40u + (hidden ? 0u : 36u) : Address(0x6000 + (hidden ? 0 : 36)), WaterSchedulerFixture.ReadCoreDword(batched.Frame, 0x2797690 + 0x7C));
            Assert.Equal(hidden ? 0 : 3, batched.Appends);
        }
        else if (failingAppend == 0)
        {
            Assert.Equal(JsonSerializer.Serialize(original.Arena), JsonSerializer.Serialize(batched.Published));
        }

        (NativeBattleWaterExecutionFrame? Frame, NativeBattleEffectArena Published, NativeBattlePrimitiveProcess Process, NativeBattleLogicalOrdering Ordering, int Appends) Run(bool batch)
        {
            using NativeBattleEffectArenaTransaction? arena = batch ? new(original.Arena) : null;
            NativeBattleLogicalOrdering seed = NativeBattleLogicalOrdering.Clear();
            using NativeBattleLogicalOrderingTransaction? links = batch ? new(seed) : null;
            NativeBattleLogicalOrdering ordering = links?.View ?? seed;
            NativeBattlePrimitiveProcess process = sourceProcess;
            NativeBattleCommonPacketTransaction? common = null;
            NativeBattleEffectArena published = original.Arena;
            int appends = 0;
            NativeBattleCinematicPointCloud27Services services = new(current => current, (current, address) => new(current, address, 0x40), (current, output, _, packets) =>
            {
                if (arena is not null && ReferenceEquals(current.Arena.Transaction, arena))
                {
                    current = current with { Arena = arena.Freeze() };
                }
                NativeBattlePrimitiveProcess previousProcess = process;
                if (batch && commonPackets && common is null)
                {
                    common = new(process);
                    process = common.View;
                }
                common?.BeginCheckpoint();
                try
                {
                    if (appends++ == failingAppend)
                    {
                        packets = packets with { Attempts = [.. packets.Attempts, new(0, [], [], null)] };
                    }
                    if (commonPackets)
                    {
                        NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(process, ordering, packets);
                        process = written.Process;
                        ordering = written.Ordering;
                        published = current.Arena;
                    }
                    else
                    {
                        NativeBattleEffectMemory memory = new(current.Arena, 0, 0, [], 0, 0, 0, 0);
                        NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(memory, checked((int)(output - NativeBattleEffectNodeMemory.ArenaAddress)), ordering, packets);
                        published = written.Memory.Arena!;
                        ordering = written.Ordering;
                    }
                    common?.CommitCheckpoint();
                    return current with { Arena = published };
                }
                catch
                {
                    common?.RollbackCheckpoint();
                    process = previousProcess;
                    throw;
                }
            });
            NativeBattleWaterExecutionFrame? drawn = null;
            try
            {
                drawn = NativeBattleCinematicPointCloud27.Draw(original with { Arena = arena?.View ?? original.Arena }, services);
                Assert.Equal(-1, failingAppend);
                if (arena is not null && ReferenceEquals(drawn.Arena.Transaction, arena))
                {
                    drawn = drawn with { Arena = arena.Freeze() };
                }
                ordering = links?.Freeze() ?? ordering;
            }
            catch (InvalidDataException) when (failingAppend >= 0)
            {
                ordering = seed;
            }
            finally
            {
                try
                {
                    if (common is not null && ReferenceEquals(process.CommonTransaction, common))
                    {
                        process = common.Freeze(process);
                    }
                }
                finally
                {
                    common?.Dispose();
                }
            }
            return (drawn, published, process, ordering, appends);
        }
    }

    private static NativeBattleWaterExecutionFrame Warm(short count)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(Address(0x4000), 0);
        byte[] storage = new byte[4 + Math.Max(0, (int)count) * 16];
        Word(storage, 0, 1);
        Word(storage, 2, count);
        for (int index = 0; index < count; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(storage.AsSpan(4 + index * 16 + 8), 0x68112233);
        }
        return frame with { Arena = frame.Arena.Write(0x4000, storage) };
    }

    private static NativeBattleWaterExecutionFrame Fixture(uint storage, ushort fixedDepth)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryDword(0x14C, 0x1000);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797690 + 0x74, Address(0x5000));
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797690 + 0x7C, 0x400);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xBC, storage);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xB8, Address(0x3000));
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xCC, 0x00102030);
        return WaterSchedulerFixture.SlotWord(frame, 0, 0xC0, fixedDepth);
    }

    private static uint Address(int offset) => unchecked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)offset);
    private static uint Dword(NativeBattleWaterExecutionFrame frame, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(frame.Arena.Read(offset, 4));
    private static void Word(byte[] data, int offset, short value) => BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(offset), value);
}
