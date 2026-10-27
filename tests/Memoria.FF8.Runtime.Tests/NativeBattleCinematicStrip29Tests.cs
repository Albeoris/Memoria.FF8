using System.Buffers.Binary;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicStrip29Tests
{
    [Theory]
    [InlineData(false, 0xFFC)]
    [InlineData(true, 0x11000)]
    public void Projects_the_complete_sine_strip_and_links_both_halves_with_native_depth_and_masks(bool fixedDepth, uint expectedOrdering)
    {
        NativeBattleWaterExecutionFrame initial = WaterSchedulerFixture.Create().WriteTemporaryDword(0x14C, 0x1000);
        initial = WaterSchedulerFixture.CoreDword(initial, 0x2797690 + 0x74, Address(0x4000));
        initial = WaterSchedulerFixture.CoreDword(initial, 0x2797690 + 0x7C, Address(0x6000));
        initial = WaterSchedulerFixture.SlotDword(initial, 0, 0xB8, Address(0x3000));
        initial = WaterSchedulerFixture.SlotDword(initial, 0, 0xCC, 0x02030201);
        initial = WaterSchedulerFixture.SlotDword(initial, 0, 0xC0, 0x10003);
        initial = WaterSchedulerFixture.SlotWord(initial, 0, 0xDE, fixedDepth ? (ushort)0x20 : (ushort)0);
        initial = WaterSchedulerFixture.SlotWord(initial, 0, 0x94, 256);
        initial = WaterSchedulerFixture.SlotWord(initial, 0, 0x96, 64);
        initial = WaterSchedulerFixture.SlotWord(initial, 0, 0x98, 1024);
        initial = initial with { Arena = initial.Arena.Write(0x3000, new byte[] { 0xFE, 0xFF }).MarkUnknown(0x4006, 2) };
        initial.TemporaryFrame.MarkUnknown(0xC6, 1);
        int projected = 0;
        int packets = 0;
        List<string> calls = [];
        NativeBattleCinematicStrip29Services services = new((frame, texture) =>
        {
            Assert.Equal(-2, texture);
            calls.Add("page");
            return frame.WriteTemporaryDword(0xFC, 0x1234);
        }, (frame, texture) =>
        {
            Assert.Equal(-2, texture);
            Assert.Equal(0x1234u, frame.ReadTemporaryDword(0x68));
            calls.Add("clut");
            return 0xABCD;
        }, frame =>
        {
            calls.Add("basis");
            return frame;
        }, (frame, point) =>
        {
            Assert.False(frame.TemporaryFrame.IsKnown(0xC6, 1));
            short[] columns = [-2032, 0, 2032];
            short[] heights = [256, 0, -256, 0];
            Assert.Equal(columns[projected % 3], point.X);
            Assert.Equal(heights[(projected / 3) % 4], point.Y);
            if (projected == 0)
            {
                Assert.Equal(2048, point.Z);
            }
            if (projected == 380)
            {
                Assert.Equal(-1984, point.Z);
            }
            projected++;
            return new(frame.WriteTemporaryDword(0xFC, 0xCAFEBABE), unchecked((uint)(0x10000 + projected - 1)), -projected);
        }, (frame, output, ordering, batch) =>
        {
            Assert.Equal(381, projected);
            Assert.Equal(Address(0x6000 + packets * 40), output);
            Assert.Equal(40, batch.AllocationBytes);
            NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.Attempts);
            Assert.Equal(33, attempt.WriteMask.Count(value => value == NativeBattleGfPacketByteState.Known));
            Assert.Empty(attempt.Primitive!.VertexPayload4);
            foreach (int offset in new[] { 0, 1, 2, 30, 31, 38, 39 })
            {
                Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[offset]);
            }
            if (packets < 2)
            {
                Assert.Equal(expectedOrdering, ordering);
                Assert.Equal(packets == 0 ? 0x10000u : 0x10002u, attempt.Primitive.Words[2]);
                Assert.Equal(0x10001u, attempt.Primitive.Words[4]);
                Assert.Equal(packets == 0 ? 0x10003u : 0x10005u, attempt.Primitive.Words[6]);
                Assert.Equal(0x10004u, attempt.Primitive.Words[8]);
                Assert.Equal(0x2E030201u, attempt.Primitive.Words[1]);
                Assert.Equal(0xABCD0100u, attempt.Primitive.Words[3]);
                Assert.Equal(0x1234017Fu, attempt.Primitive.Words[5]);
                Assert.Equal(3, attempt.Data[29]);
                Assert.Equal(3, attempt.Data[37]);
            }
            if (packets == 251)
            {
                Assert.Equal(251, attempt.Data[13]);
                Assert.Equal(253, attempt.Data[29]);
            }
            packets++;
            return frame;
        });
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicStrip29.Draw(initial, services);
        Assert.Equal(new[] { "page", "clut", "basis" }, calls);
        Assert.Equal(252, packets);
        Assert.Equal(Address(0x8760), WaterSchedulerFixture.ReadCoreDword(result, 0x2797690 + 0x7C));
        Assert.Equal(Address(0x4000), WaterSchedulerFixture.ReadCoreDword(result, 0x2797690 + 0x74));
        Assert.Equal(unchecked((ushort)-2016), result.ReadTemporaryWord(0xC4));
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(0x4006, 2));
        NativeBattleWaterExecutionFrame restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(result)))!);
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(result)), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(restored)));
    }

    private static uint Address(int offset) => unchecked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)offset);
}
