using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicColumns2DTests
{
    [Fact]
    public void ColdInitializationPublishesAll320SparseRecordsAndTexturePageGroups()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0, true);
        byte[] payload = Payload();
        BinaryPrimitives.WriteUInt16LittleEndian(payload, 704);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(2), 256);
        frame = frame with { Arena = frame.Arena.Write(0x3000, payload).MarkUnknown(0x5002, 14).MarkUnknown(0x5012, 2).MarkUnknown(0x5024, 12) };
        List<NativeBattleGeometryPrimitive> packets = [];
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicColumns2D.Draw(frame, (current, _, ordering, batch) =>
        {
            Assert.Equal(0x1000u, ordering);
            packets.Add(Assert.Single(batch.Attempts).Primitive!);
            return current;
        });
        Assert.Equal(320, packets.Count);
        Assert.Equal(0x11Bu, WaterSchedulerFixture.ReadSlotDword(result, 0, 0xC0));
        Assert.Equal(Address(0x5000), WaterSchedulerFixture.ReadSlotDword(result, 0, 0xBC));
        Assert.Equal(Address(0x7810), WaterSchedulerFixture.ReadCoreDword(result, 0x2797690 + 0x74));
        Assert.Equal(0x480BB881u, result.Random.Seed);
        Assert.Equal(0x3C808080u, result.ReadTemporaryDword(0x70));
        for (int group = 0; group < 5; group++)
        {
            NativeBattleGeometryPrimitive first = packets[group * 64];
            Assert.Equal(0x11Bu + (uint)group, first.Words[6] >> 16);
            Assert.Equal((uint)(group * 64), first.Words[2]);
        }
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(0x5002, 1));
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(0x5012, 1));
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(0x5024, 1));
        Assert.Equal(0, Word(result, 0x5000));
        Assert.Equal(0x4500u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797690 + 0x7C));
    }

    [Fact]
    public void PausedVisibleColumnEmitsQuadThenModeLineAndPreservesPacketPadding()
    {
        NativeBattleWaterExecutionFrame frame = Warm(true, 1);
        List<NativeBattleGfGeometryAttempt> attempts = [];
        List<uint> outputs = [];
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicColumns2D.Draw(frame, (current, output, _, batch) =>
        {
            outputs.Add(output);
            attempts.Add(Assert.Single(batch.Attempts));
            return current;
        });
        Assert.Equal(new uint[] { 0x400, 0x434 }, outputs);
        Assert.Equal(2, attempts.Count);
        NativeBattleGeometryPrimitive quad = attempts[0].Primitive!;
        Assert.Equal(0x0C000000u, quad.Words[0]);
        Assert.Equal(0x3C6060A0u, quad.Words[1]);
        Assert.Equal(0x000A0000u, quad.Words[2]);
        Assert.Equal(0x000A0001u, quad.Words[5]);
        Assert.Equal(0x00140000u, quad.Words[8]);
        Assert.Equal(0x00140001u, quad.Words[11]);
        Assert.Equal(0x00E001u, quad.Words[12]);
        foreach (int offset in new[] { 0, 1, 2, 14, 15, 38, 39, 50, 51 })
        {
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempts[0].WriteMask[offset]);
        }
        Assert.Equal(new uint[] { 0x06000000, 0xE1000060, 0, 0x52000000, 0xFF8A0000, 0x00FFFF00, 0x000A0000 }, attempts[1].Primitive!.Words);
        Assert.All(attempts[1].WriteMask.Skip(3), state => Assert.Equal(NativeBattleGfPacketByteState.Known, state));
        Assert.Equal(4, Word(result, 0x4000));
        Assert.Equal(0x450u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797690 + 0x7C));
    }

    [Fact]
    public void UnpausedThresholdTransitionUpdatesMotionBeforeTheSameFrameLine()
    {
        NativeBattleWaterExecutionFrame frame = Warm(false, 0);
        byte[] record = new byte[32];
        BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(4), 250);
        BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(6), 10);
        BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(8), 25500);
        BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(14), 3);
        frame = frame with { Arena = frame.Arena.Write(0x4010, record) };
        byte[] payload = Payload();
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(20), 100);
        frame = frame with { Arena = frame.Arena.Write(0x3000, payload) };
        int packets = 0;
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicColumns2D.Draw(frame, (current, _, _, _) =>
        {
            packets++;
            return current;
        });
        Assert.Equal(2, packets);
        Assert.Equal(260, Word(result, 0x4014));
        Assert.Equal(25760u, Dword(result, 0x4018));
        Assert.Equal(1, Word(result, 0x4010));
        Assert.Equal(263, Word(result, 0x401C));
        Assert.Equal(263u, Dword(result, 0x4020));
        Assert.Equal(5, Word(result, 0x4000));
    }

    [Fact]
    public void DeadPausedColumnsDoNotReadTheirUnknownLowStatusOrMotion()
    {
        NativeBattleWaterExecutionFrame frame = Warm(true, 0x8000);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x4010, 1).MarkUnknown(0x4012, 30) };
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicColumns2D.Draw(frame, (_, _, _, _) => throw new InvalidOperationException());
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(0x4010, 1));
        Assert.Equal(0x400u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797690 + 0x7C));
        Assert.Equal(4, Word(result, 0x4000));
    }

    [Fact]
    public void LivePauseAfterTheFirstPacketStopsFurtherUpdatesAndFinalAgeIncrement()
    {
        NativeBattleWaterExecutionFrame frame = Warm(false, 0);
        int packets = 0;
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicColumns2D.Draw(frame, (current, _, _, _) =>
        {
            packets++;
            return current.WriteTemporaryByte(0x145, 1);
        });
        Assert.Equal(1, packets);
        Assert.Equal(4, Word(result, 0x4000));
    }

    [Fact]
    public void SnapshotPreservesTheSameNextFrameAndUnconsumedPadding()
    {
        NativeBattleWaterExecutionFrame frame = Warm(true, 0);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x4012, 2), TemporaryFrame = frame.TemporaryFrame.MarkUnknown(0xEE, 1) };
        NativeBattleWaterExecutionFrame restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(SnapshotJson(frame))!);
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicColumns2D.Draw(restored, (current, _, _, _) => current);
        Assert.False(result.TemporaryFrame.IsKnown(0xEE, 1));
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(0x4012, 1));
        Assert.Equal(SnapshotJson(NativeBattleCinematicColumns2D.Draw(frame, (current, _, _, _) => current)), SnapshotJson(result));
    }

    private static NativeBattleWaterExecutionFrame Warm(bool paused, ushort status)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(Address(0x4000), paused);
        byte[] state = new byte[0x2810];
        BinaryPrimitives.WriteUInt16LittleEndian(state, 4);
        for (int index = 0; index < 320; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(state.AsSpan(16 + index * 32), 0x8000);
        }
        BinaryPrimitives.WriteUInt16LittleEndian(state.AsSpan(16), status);
        BinaryPrimitives.WriteUInt32LittleEndian(state.AsSpan(24), 20 * 256);
        BinaryPrimitives.WriteUInt32LittleEndian(state.AsSpan(32), 10 * 256);
        return frame with { Arena = frame.Arena.Write(0x4000, state) };
    }

    private static NativeBattleWaterExecutionFrame Fixture(uint storage, bool paused)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryDword(0x14C, 0x1000).WriteTemporaryByte(0x145, paused ? (byte)1 : (byte)0);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797690 + 0x74, Address(0x5000));
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797690 + 0x7C, 0x400);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xBC, storage);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xB8, Address(0x3000));
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xC0, 0x100);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x92, 0x20);
        return frame with { Arena = frame.Arena.Write(0x3000, Payload()) };
    }

    private static byte[] Payload()
    {
        byte[] payload = new byte[22];
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(20), 0x7FFF);
        return payload;
    }

    private static uint Address(int offset) => NativeBattleEffectNodeMemory.ArenaAddress + (uint)offset;
    private static ushort Word(NativeBattleWaterExecutionFrame frame, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(frame.Arena.Read(offset, 2));
    private static uint Dword(NativeBattleWaterExecutionFrame frame, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(frame.Arena.Read(offset, 4));

    private static string SnapshotJson<T>(T value) => JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(value));
}
