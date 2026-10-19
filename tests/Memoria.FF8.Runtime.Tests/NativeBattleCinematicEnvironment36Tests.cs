using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicEnvironment36Tests
{
    [Theory]
    [InlineData(0, 0, 1, 0)]
    [InlineData(1, 1, 0, 1)]
    [InlineData(0, -1, 1, 1)]
    public void UsesAlternateEnvironmentSignedParityAndRestoreBeforeCapture(byte buffer, sbyte parity, byte alternate, byte clear)
    {
        NativeBattleWaterExecutionFrame frame = Fixture().WriteTemporaryByte(0x141, unchecked((byte)parity));
        ImmutableArray<byte> source = Environment(clear);
        int calls = 0;
        NativeBattleCinematicEnvironment36Services services = new(_ => buffer, (_, bank) =>
        {
            Assert.Equal(alternate, bank);
            return source;
        }, (current, address, ordering, batch) =>
        {
            calls++;
            Assert.Equal(unchecked(NativeBattleEffectNodeMemory.ArenaAddress + 0x4000u + (uint)(parity * 128)), address);
            Assert.Equal(0x1000u, ordering);
            Assert.Equal(128, batch.AllocationBytes);
            Assert.Equal(2, batch.Attempts.Length);
            NativeBattleGfGeometryAttempt restore = batch.Attempts[0];
            NativeBattleGfGeometryAttempt capture = batch.Attempts[1];
            Assert.Equal(64, restore.ByteOffsetRelative);
            Assert.Equal(0, capture.ByteOffsetRelative);
            Assert.Equal(0x3FFC / 4, restore.Primitive!.Bucket);
            Assert.Equal(0x3FC0 / 4, capture.Primitive!.Bucket);
            Assert.Equal(clear == 0 ? 0x05000000u : 0x08000000u, restore.Primitive.Words[0]);
            Assert.Equal(0x08000000u, capture.Primitive.Words[0]);
            Assert.Equal(0x02000000u, capture.Primitive.Words[6]);
            Assert.Equal(0x0190FFEFu, capture.Primitive.Words[7]);
            Assert.Equal(0x00E00140u, capture.Primitive.Words[8]);
            Assert.Equal(0xE1000600u, capture.Primitive.Words[4]);
            int restoreWritten = clear == 0 ? 24 : 36;
            CheckMask(restore, restoreWritten);
            CheckMask(capture, 36);
            byte[] scratch = current.Arena.Read(0x5000, 28);
            Assert.Equal(unchecked((ushort)-17), BinaryPrimitives.ReadUInt16LittleEndian(scratch));
            Assert.Equal(400, BinaryPrimitives.ReadUInt16LittleEndian(scratch.AsSpan(2)));
            Assert.Equal(320, BinaryPrimitives.ReadUInt16LittleEndian(scratch.AsSpan(4)));
            Assert.Equal(224, BinaryPrimitives.ReadUInt16LittleEndian(scratch.AsSpan(6)));
            Assert.Equal(scratch.Take(4), scratch.Skip(8).Take(4));
            Assert.Equal(new byte[] { 1, 1, 1, 0, 0, 0 }, scratch.Skip(0x16));
            return current;
        });
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicEnvironment36.Draw(frame, services);
        Assert.Equal(1, calls);
        Assert.Equal(NativeBattleEffectNodeMemory.ArenaAddress + 0x5000u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797690 + 0x74));
        Assert.Equal(0x777u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797690 + 0x7C));
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(0x501C, 1));
    }

    [Fact]
    public void NullStorageAllocatesExactly256WithoutClearingOrAdvancingPacketCursor()
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.SlotDword(Fixture(), 0, 0xBC, 0);
        NativeBattleCinematicEnvironment36Services services = new(_ => 1, (_, _) => Environment(0), (current, address, _, _) =>
        {
            Assert.Equal(NativeBattleEffectNodeMemory.ArenaAddress + 0x5000u, address);
            Assert.Equal(address, WaterSchedulerFixture.ReadSlotDword(current, 0, 0xBC));
            Assert.Equal(address + 0x100, WaterSchedulerFixture.ReadCoreDword(current, 0x2797690 + 0x74));
            Assert.Throws<NotSupportedException>(() => current.Arena.Read(0x5000, 1));
            Assert.Equal(28, current.Arena.Read(0x5100, 28).Length);
            return current;
        });
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicEnvironment36.Draw(frame, services);
        Assert.Equal(0x777u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797690 + 0x7C));
    }

    [Fact]
    public void SnapshotRetainsUnwrittenPacketAndScratchBytes()
    {
        NativeBattleWaterExecutionFrame frame = Fixture();
        frame = frame with { TemporaryFrame = frame.TemporaryFrame with { Known = frame.TemporaryFrame.Known.SetItem(0xEE, false) } };
        NativeBattleWaterExecutionFrame restored = JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(JsonSerializer.Serialize(frame))!;
        NativeBattleCinematicEnvironment36Services services = new(_ => 0, (_, _) => Environment(0), (current, _, _, _) => current);
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicEnvironment36.Draw(restored, services);
        Assert.False(result.TemporaryFrame.Known[0xEE]);
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(0x4000, 1));
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(0x501C, 1));
        Assert.Equal(JsonSerializer.Serialize(NativeBattleCinematicEnvironment36.Draw(frame, services)), JsonSerializer.Serialize(result));
    }

    private static void CheckMask(NativeBattleGfGeometryAttempt attempt, int written)
    {
        Assert.All(attempt.WriteMask.Take(3), state => Assert.Equal(NativeBattleGfPacketByteState.Untouched, state));
        Assert.All(attempt.WriteMask.Skip(3).Take(written - 3), state => Assert.Equal(NativeBattleGfPacketByteState.Known, state));
        Assert.All(attempt.WriteMask.Skip(written), state => Assert.Equal(NativeBattleGfPacketByteState.Untouched, state));
    }

    private static ImmutableArray<byte> Environment(byte clear)
    {
        byte[] source = new byte[28];
        BinaryPrimitives.WriteInt16LittleEndian(source, 10);
        BinaryPrimitives.WriteInt16LittleEndian(source.AsSpan(2), 20);
        BinaryPrimitives.WriteInt16LittleEndian(source.AsSpan(4), 320);
        BinaryPrimitives.WriteInt16LittleEndian(source.AsSpan(6), 240);
        source[0x18] = clear;
        source[0x19] = 7;
        source[0x1A] = 8;
        source[0x1B] = 9;
        return source.ToImmutableArray();
    }

    private static NativeBattleWaterExecutionFrame Fixture()
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryByte(0x141, 0).WriteTemporaryDword(0x14C, 0x1000);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797690 + 0x74, NativeBattleEffectNodeMemory.ArenaAddress + 0x5000);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797690 + 0x7C, 0x777);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xBC, NativeBattleEffectNodeMemory.ArenaAddress + 0x4000);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xB8, NativeBattleEffectNodeMemory.ArenaAddress + 0x3000);
        byte[] payload = new byte[8];
        BinaryPrimitives.WriteUInt16LittleEndian(payload, 0xFFFD);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(2), -17);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(4), 400);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(6), 320);
        return frame with { Arena = frame.Arena.Write(0x3000, payload).MarkUnknown(0x4000, 0x1200) };
    }
}
