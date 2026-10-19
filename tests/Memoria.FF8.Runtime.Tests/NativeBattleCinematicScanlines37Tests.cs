using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicScanlines37Tests
{
    [Fact]
    public void IdentityRowMapEmitsTwoNativeTriangleLayersAndPreservesTheirUnwrittenClut()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(256);
        frame = frame with { Arena = frame.Arena.Write(0x4008, new byte[] { 0, 1, 0, 0xFF }) };
        int scaled = 0;
        List<NativeBattleGfGeometryAttempt> packets = [];
        NativeBattleCinematicScanlines37Services services = new((current, command, factor) =>
        {
            Assert.Empty(packets);
            Assert.Equal((short)256, factor);
            Assert.Equal(scaled++ == 0 ? 0x26804040u : 0x26404060u, command);
            return new(current, command);
        }, (current, _, ordering, batch) =>
        {
            Assert.Equal(2, scaled);
            Assert.Equal(0x1000u, ordering);
            packets.Add(Assert.Single(batch.Attempts));
            return current;
        });
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicScanlines37.Draw(frame, services);
        Assert.Equal(432, packets.Count);
        Assert.Equal(0x00000001u, packets[0].Primitive!.Words[2]);
        Assert.Equal(0u, packets[1].Primitive!.Words[2]);
        Assert.Equal(0x0001FFFFu, packets[2].Primitive!.Words[2]);
        Assert.Equal(0x0001FFFFu, packets[3].Primitive!.Words[2]);
        Assert.Equal(0x011B00FFu, packets[0].Primitive!.Words[5]);
        Assert.Equal(0x000001FFu, packets[0].Primitive!.Words[7]);
        foreach (NativeBattleGfGeometryAttempt packet in packets)
        {
            Assert.Equal(32, packet.Data.Length);
            foreach (int offset in new[] { 0, 1, 2, 14, 15, 30, 31 })
            {
                Assert.Equal(NativeBattleGfPacketByteState.Untouched, packet.WriteMask[offset]);
            }
        }
        Assert.Equal(0x3A00u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797690 + 0x7C));
        Assert.Equal(Address(0x5000), WaterSchedulerFixture.ReadCoreDword(result, 0x2797690 + 0x74));
        Assert.All(result.Arena.Read(0x5200, 216), value => Assert.Equal((byte)1, value));
        Assert.Equal(1u, result.Random.Seed);
    }

    [Theory]
    [InlineData(0, 160u, 0x00D000A0u)]
    [InlineData(-32768, 0xFFFF80A0u, 0x00CF80A0u)]
    public void ZeroVerticalScaleSuppressesRowsAndBlackBordersKeepPackedDwordBorrow(short side, uint secondRight, uint secondBottom)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.SlotWord(Fixture(0), 0, 0x94, unchecked((ushort)side));
        List<NativeBattleGeometryPrimitive> packets = [];
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicScanlines37.Draw(frame, Services((current, _, _, batch) =>
        {
            packets.Add(Assert.Single(batch.Attempts).Primitive!);
            return current;
        }));
        Assert.Equal(2, packets.Count);
        Assert.All(packets, packet => Assert.Equal(0x28000000u, packet.Words[1]));
        Assert.Equal(secondRight, packets[1].Words[3]);
        Assert.Equal(secondBottom, packets[1].Words[5]);
        Assert.Equal(0x430u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797690 + 0x7C));
    }

    [Fact]
    public void NegativeVerticalScaleDeduplicatesTheCentralRowAndOptionalBandUsesItsOwnNativeMask()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(-1);
        frame = frame with { Arena = frame.Arena.Write(0x4000, new byte[] { 0xFF, 0x6B }).Write(0x3006, new byte[] { 1, 0 }) };
        List<NativeBattleGfGeometryAttempt> packets = [];
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicScanlines37.Draw(frame, Services((current, _, _, batch) =>
        {
            packets.Add(Assert.Single(batch.Attempts));
            return current;
        }));
        Assert.Equal(3, packets.Count);
        Assert.Equal(32, packets[0].Data.Length);
        Assert.Equal(32, packets[1].Data.Length);
        Assert.Equal(40, packets[2].Data.Length);
        Assert.Equal(0x2E70B0B0u, packets[2].Primitive!.Words[1]);
        Assert.Equal(0x006CFFF0u, packets[2].Primitive!.Words[2]);
        Assert.Equal(0x006C0130u, packets[2].Primitive!.Words[4]);
        Assert.Equal(0x00006C00u, packets[2].Primitive!.Words[3]);
        foreach (int offset in new[] { 14, 15, 30, 31, 38, 39 })
        {
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, packets[2].WriteMask[offset]);
        }
        Assert.Equal(108u, result.ReadTemporaryDword(0x80));
        Assert.Equal(0x468u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797690 + 0x7C));
    }

    [Fact]
    public void ColdStateUsesUnsignedDword60000RandomOperandAndRetainsItsPadding()
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.SlotDword(Fixture(0), 0, 0xBC, 0);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x5006, 2) };
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicScanlines37.Draw(frame, Services((_, _, _, _) => throw new InvalidOperationException()));
        Assert.Equal(Address(0x5000), WaterSchedulerFixture.ReadSlotDword(result, 0, 0xBC));
        Assert.Equal(Address(0x51B8), WaterSchedulerFixture.ReadCoreDword(result, 0x2797690 + 0x74));
        Assert.Equal(new byte[] { 0x26, 0xE9, 0x7B, 0x6D, 0xE1, 0xE9 }, result.Arena.Read(0x5000, 6));
        Assert.Equal(0x0C15DA12u, result.Random.Seed);
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(0x5006, 1));
    }

    [Fact]
    public void SnapshotKeepsActualWorkspaceGapAndTheSameNextDraw()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(256);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x4006, 2).MarkUnknown(0x51B0, 16), TemporaryFrame = frame.TemporaryFrame with { Known = frame.TemporaryFrame.Known.SetItem(0xEE, false) } };
        NativeBattleWaterExecutionFrame restored = JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(JsonSerializer.Serialize(frame))!;
        NativeBattleCinematicScanlines37Services services = Services((current, _, _, _) => current);
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicScanlines37.Draw(restored, services);
        Assert.False(result.TemporaryFrame.Known[0xEE]);
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(0x51B0, 1));
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(0x4006, 1));
        Assert.Equal(JsonSerializer.Serialize(NativeBattleCinematicScanlines37.Draw(frame, services)), JsonSerializer.Serialize(result));
    }

    private static NativeBattleCinematicScanlines37Services Services(Func<NativeBattleWaterExecutionFrame, uint, uint, NativeBattleGfGeometryBatch, NativeBattleWaterExecutionFrame> append) => new((current, command, _) => new(current, command), append);

    private static NativeBattleWaterExecutionFrame Fixture(short vertical)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryDword(0x14C, 0x1000);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797690 + 0x74, Address(0x5000));
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797690 + 0x7C, 0x400);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xBC, Address(0x4000));
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xB8, Address(0x3000));
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xCC, 0x02000000);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x8C, 320);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x8E, 256);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x94, 160);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x96, unchecked((ushort)vertical));
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x98, 1);
        byte[] payload = new byte[12];
        BinaryPrimitives.WriteUInt16LittleEndian(payload, 704);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(2), 256);
        return frame with { Arena = frame.Arena.Write(0x3000, payload) };
    }

    private static uint Address(int offset) => NativeBattleEffectNodeMemory.ArenaAddress + (uint)offset;
}
