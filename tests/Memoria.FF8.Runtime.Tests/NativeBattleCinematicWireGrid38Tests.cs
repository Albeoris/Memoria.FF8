using System.Buffers.Binary;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicWireGrid38Tests
{
    [Fact]
    public void Closes_sixteen_vertices_and_emits_both_line_passes_with_original_angular_depth_metadata()
    {
        NativeBattleWaterExecutionFrame frame = Fixture();
        int projections = 0;
        int packets = 0;
        List<short> fades = [];
        NativeBattleCinematicWireGrid38Services services = new((_, selector) =>
        {
            Assert.Equal(0x8001, selector);
            return Address(0x3800);
        }, current => current, current =>
        {
            Assert.Equal(4096u, current.ReadTemporaryDword(0xC0));
            Assert.Equal(4096u, current.ReadTemporaryDword(0xC4));
            return current;
        }, (current, output) =>
        {
            Assert.Equal(projections % 17 % 16, current.ReadTemporaryWord(0xD0));
            Assert.Equal(100 + projections % 17 % 16, current.ReadTemporaryWord(0xD4));
            Assert.Equal(projections < 17 ? unchecked((ushort)-2) : (ushort)2, current.ReadTemporaryWord(0xD2));
            Assert.False(current.TemporaryFrame.IsKnown(0xD6, 1));
            int ordinal = projections++;
            current = ArenaDword(current, output, (uint)ordinal);
            return new(current.WriteTemporaryDword(0xFC, 0x1234), 100 + ordinal);
        }, (current, color, fade) =>
        {
            fades.Add(fade);
            return new(current, color);
        }, (current, output, ordering, batch) =>
        {
            NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.Attempts);
            NativeBattleGeometryPrimitive primitive = Assert.IsType<NativeBattleGeometryPrimitive>(attempt.Primitive);
            int column = packets % 16;
            Assert.Equal(Address(0x6000 + packets * 28), output);
            Assert.Equal(new int[] { 100 + column, 101 + column, 0, 0 }, primitive.VertexPayload4);
            Assert.Equal((uint)(0x1000 + ((100 + column) & ~3)), ordering);
            Assert.Equal((uint)column, primitive.Words[4]);
            Assert.Equal((uint)(column + (packets < 16 ? 1 : 17)), primitive.Words[6]);
            Assert.Equal(0xE1000220u, primitive.Words[1]);
            Assert.Equal(0u, primitive.Words[2]);
            Assert.Equal(0x52030201u + (uint)(column % 2 * 0x030303), primitive.Words[3]);
            Assert.Equal(28, batch.AllocationBytes);
            Assert.Equal(25, attempt.WriteMask.Count(value => value == NativeBattleGfPacketByteState.Known));
            packets++;
            return current;
        });
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicWireGrid38.Draw(frame, services);
        Assert.Equal(34, projections);
        Assert.Equal(32, packets);
        Assert.Equal(48, fades.Count(value => value == 64));
        Assert.Equal(16, fades.Count(value => value == 0));
        Assert.Equal(Address(0x6380), WaterSchedulerFixture.ReadCoreDword(result, 0x2797690 + 0x7C));
        Assert.Equal(Address(0x404C), WaterSchedulerFixture.ReadCoreDword(result, 0x2797690 + 0x74));
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(0x4076, 2));
        NativeBattleWaterExecutionFrame clone = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(result)))!);
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(result.Arena)), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(clone.Arena)));
    }

    [Theory]
    [InlineData(0, 0, 17)]
    [InlineData(1, -1, 34)]
    public void Zero_rows_and_negative_color_threshold_keep_projection_without_reading_palette(int rows, short threshold, int expected)
    {
        NativeBattleWaterExecutionFrame frame = ArenaWord(Fixture(), Address(0x3006), (ushort)rows);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x94, unchecked((ushort)threshold));
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x3080, 8) };
        int projections = 0;
        NativeBattleCinematicWireGrid38Services services = new((_, _) => Address(0x3800), current => current, current => current, (current, output) =>
        {
            projections++;
            return new(ArenaDword(current, output, 0), -1);
        }, (_, _, _) => throw new InvalidOperationException("No palette color is consumed."), (_, _, _, _) => throw new InvalidOperationException("No line is emitted."));
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicWireGrid38.Draw(frame, services);
        Assert.Equal(expected, projections);
        Assert.Equal(Address(0x6000), WaterSchedulerFixture.ReadCoreDword(result, 0x2797690 + 0x7C));
    }

    private static NativeBattleWaterExecutionFrame Fixture()
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryDword(0x14C, 0x1000);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797690 + 0x74, Address(0x4000));
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797690 + 0x7C, Address(0x6000));
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xB8, Address(0x3000));
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xBC, 0);
        foreach ((int offset, ushort value) in new (int, ushort)[] { (0x8C, 256), (0x8E, 4), (0x90, 0), (0x94, 16), (0x96, 0), (0x98, 0) })
        {
            frame = WaterSchedulerFixture.SlotWord(frame, 0, offset, value);
        }
        foreach ((int offset, ushort value) in new (int, ushort)[] { (0, 0x80), (2, 1), (4, 0x8001), (6, 1), (8, 2) })
        {
            frame = ArenaWord(frame, Address(0x3000 + offset), value);
        }
        for (int index = 0; index < 16; index++)
        {
            frame = ArenaWord(frame, Address(0x300A + index * 2), (ushort)index);
            frame = ArenaWord(frame, Address(0x3900 + index * 8), (ushort)index);
            frame = ArenaWord(frame, Address(0x3904 + index * 8), (ushort)(100 + index));
        }
        frame = ArenaDword(frame, Address(0x3814), 0x100);
        frame = ArenaDword(frame, Address(0x3080), 0x030201);
        frame = ArenaDword(frame, Address(0x3084), 0x060504);
        frame.TemporaryFrame.MarkUnknown(0xD6, 2);
        return frame with { Arena = frame.Arena.MarkUnknown(0x302A, 2).MarkUnknown(0x4076, 2) };
    }

    private static uint Address(int offset) => unchecked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)offset);

    private static NativeBattleWaterExecutionFrame ArenaWord(NativeBattleWaterExecutionFrame frame, uint address, ushort value)
    {
        return frame with { Arena = frame.Arena.Write(checked((int)(address - NativeBattleEffectNodeMemory.ArenaAddress)), BitConverter.GetBytes(value)) };
    }

    private static NativeBattleWaterExecutionFrame ArenaDword(NativeBattleWaterExecutionFrame frame, uint address, uint value)
    {
        return frame with { Arena = frame.Arena.Write(checked((int)(address - NativeBattleEffectNodeMemory.ArenaAddress)), BitConverter.GetBytes(value)) };
    }
}
