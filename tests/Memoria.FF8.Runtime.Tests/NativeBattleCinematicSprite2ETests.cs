using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicSprite2ETests
{
    [Theory]
    [InlineData(7, 5, 97, 103, 198, 202, 1, 7, 254, 2)]
    [InlineData(-7, -5, 103, 97, 202, 198, 1, 251, 254, 250)]
    [InlineData(0, 0, 100, 100, 200, 200, 1, 1, 254, 254)]
    public void EmitsUnclippedQuadWithSignedHalfSizesAndByteWrappedUvs(short width, short height, ushort left, ushort right, ushort top, ushort bottom, byte u, byte rightU, byte v, byte bottomV)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(width, height);
        int appends = 0;
        NativeBattleCinematicSprite2EServices services = new(current => new(current.WriteTemporaryWord(0xD0, 100).WriteTemporaryWord(0xD2, 200).WriteTemporaryDword(0xFC, 0xFFFFFFFF), 79), (current, output, ordering, batch) =>
        {
            appends++;
            Assert.Equal(0x400u, output);
            Assert.Equal(0x1010u, ordering);
            Assert.Equal(40, batch.AllocationBytes);
            NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.Attempts);
            Assert.Equal(0x2C332211u, attempt.Primitive!.Words[1]);
            Assert.Equal(Pack(left, top), attempt.Primitive.Words[2]);
            Assert.Equal(Pack(right, top), attempt.Primitive.Words[4]);
            Assert.Equal(Pack(left, bottom), attempt.Primitive.Words[6]);
            Assert.Equal(Pack(right, bottom), attempt.Primitive.Words[8]);
            Assert.Equal((uint)(u | (v << 8) | (0xABCDu << 16)), attempt.Primitive.Words[3]);
            Assert.Equal((uint)(rightU | (v << 8) | (0x1234u << 16)), attempt.Primitive.Words[5]);
            Assert.Equal((uint)(u | (bottomV << 8)), attempt.Primitive.Words[7]);
            Assert.Equal((uint)(rightU | (bottomV << 8)), attempt.Primitive.Words[9]);
            Assert.Empty(attempt.Primitive.VertexPayload4);
            foreach (int offset in new[] { 0, 1, 2, 0x1E, 0x1F, 0x26, 0x27 })
            {
                Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[offset]);
            }
            Assert.Equal(9, attempt.Data[3]);
            Assert.Equal(33, attempt.WriteMask.Count(state => state == NativeBattleGfPacketByteState.Known));
            return current;
        });
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicSprite2E.Draw(frame, services);
        Assert.Equal(1, appends);
        Assert.Equal(0x428u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797690 + 0x7C));
        Assert.Equal(0xFFFFFFFFu, result.ReadTemporaryDword(0xFC));
    }

    [Fact]
    public void CapturesEntryCursorPayloadAndMaterialButReloadsProjectedOwnersAndFinalManager()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(7, 5);
        NativeBattleCinematicSprite2EServices services = new(current =>
        {
            current = WaterSchedulerFixture.SlotDword(current, 0, 0xB8, uint.MaxValue);
            current = WaterSchedulerFixture.SlotDword(current, 0, 0xCC, 0);
            current = WaterSchedulerFixture.CoreDword(current, 0x2797690 + 0x7C, 0x900);
            current = current with { Arena = current.Arena.Write(0x3004, new byte[] { 0xFD, 0xFF, 1, 0 }) };
            return new(current.WriteTemporaryWord(0xD0, 0x8000).WriteTemporaryWord(0xD2, 0xFFFF).WriteTemporaryDword(0x14C, 0x2000), -1);
        }, (current, output, ordering, batch) =>
        {
            Assert.Equal(0x400u, output);
            Assert.Equal(0x1FFCu, ordering);
            NativeBattleGeometryPrimitive primitive = Assert.Single(batch.Attempts).Primitive!;
            Assert.Equal(0x2C332211u, primitive.Words[1]);
            Assert.Equal(Pack(0x8001, 0xFFFF), primitive.Words[2]);
            Assert.Equal(Pack(0x7FFF, 0xFFFF), primitive.Words[4]);
            current = WaterSchedulerFixture.CoreDword(current, 0x27973EC, 0x2797800);
            return current;
        });
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicSprite2E.Draw(frame, services);
        Assert.Equal(0x900u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797690 + 0x7C));
        Assert.Equal(0x428u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797800 + 0x7C));
    }

    [Fact]
    public void SnapshotPreservesUnconsumedPayloadAndTemporaryPadding()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(3, 3);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x3008, 8), TemporaryFrame = frame.TemporaryFrame with { Known = frame.TemporaryFrame.Known.SetItem(0xEE, false) } };
        NativeBattleWaterExecutionFrame restored = JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(JsonSerializer.Serialize(frame))!;
        NativeBattleCinematicSprite2EServices services = new(current => new(current.WriteTemporaryWord(0xD0, 0).WriteTemporaryWord(0xD2, 0), 0), (current, _, _, _) => current);
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicSprite2E.Draw(restored, services);
        Assert.False(result.TemporaryFrame.Known[0xEE]);
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(0x3008, 1));
        Assert.Equal(JsonSerializer.Serialize(NativeBattleCinematicSprite2E.Draw(frame, services)), JsonSerializer.Serialize(result));
    }

    private static uint Pack(ushort x, ushort y) => (uint)x | ((uint)y << 16);

    private static NativeBattleWaterExecutionFrame Fixture(short width, short height)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryDword(0x14C, 0x1000);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797690 + 0x7C, 0x400);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xB8, NativeBattleEffectNodeMemory.ArenaAddress + 0x3000);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xCC, 0x00332211);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x92, 0x1234);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x9A, 0xABCD);
        byte[] payload = new byte[8];
        BinaryPrimitives.WriteInt16LittleEndian(payload, 0x101);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(2), -2);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(4), width);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(6), height);
        return frame with { Arena = frame.Arena.Write(0x3000, payload) };
    }
}
