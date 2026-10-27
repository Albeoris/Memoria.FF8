using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicBlitB8Tests
{
    [Theory]
    [InlineData((short)-16)]
    [InlineData((short)16)]
    public void Relative_B6_uses_signed_offset_and_native_rectangle_without_touching_the_copy_ring(short offset)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0, 1);
        uint pc = Address(0x1000);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797450, pc);
        frame = frame with { Arena = frame.Arena.Write(0x1002, BitConverter.GetBytes(offset)), Core = frame.Core.MarkUnknown(0x2798BF4, 1) };
        for (int lane = 0; lane < 6; lane++)
        {
            frame = frame with { Arena = frame.Arena.Write(0x1000 + offset + lane * 2, BitConverter.GetBytes(unchecked((short)(-32768 + lane)))) };
        }
        NativeBattleWaterExecutionFrame Run(NativeBattleWaterExecutionFrame current) => NativeBattleCinematicBlitB8.ExecuteRelativeB6(current, new(0x1000, 8, pc), (queued, copy) =>
        {
            Assert.Equal(new NativeBattleEffectVramCopy(-32768, -32767, -32766, -32765, -32764, -32763), copy);
            return WaterSchedulerFixture.CoreDword(queued, 0x2797450, pc + 100);
        });
        NativeBattleWaterExecutionFrame restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(SnapshotJson(frame))!);
        NativeBattleWaterExecutionFrame result = Run(frame);
        Assert.Equal(pc + 104, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Same(frame.Arena, result.Arena);
        Assert.Same(frame.TemporaryFrame, result.TemporaryFrame);
        Assert.Same(frame.Random, result.Random);
        Assert.Equal(SnapshotJson(result), SnapshotJson(Run(restored)));
    }

    [Theory]
    [InlineData(3, 2, 5, unchecked((short)0x800C))]
    [InlineData(-3, 2, 13, unchecked((short)0x8012))]
    public void Selects_signed_remainder_frame_copies_current_rectangle_and_wraps_the_shared_ring(short phase, short period, short x, short y)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(phase, period);
        NativeBattleCinematicResources resources = StaticResources();
        int copies = 0;
        NativeBattleWaterExecutionFrame Run(NativeBattleWaterExecutionFrame current) => NativeBattleCinematicBlitB8.Execute(current, new(0x1000, 8, 0x1000), resources, (_, selector) =>
        {
            Assert.Equal(-2, selector);
            return Address(0x3000);
        }, (copyFrame, copy) =>
        {
            copies++;
            Assert.Equal(new NativeBattleEffectVramCopy(x, y, 17, 19, 3, unchecked((short)0x800A)), copy);
            Assert.Equal((byte)255, copyFrame.Core.Read(0x2798BF4, 1)[0]);
            Assert.Equal(copyFrame.ReadTemporary(0xA0, 8), copyFrame.Core.Read(0x2797960, 8));
            return copyFrame;
        });
        NativeBattleWaterExecutionFrame restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(SnapshotJson(frame))!);
        NativeBattleWaterExecutionFrame result = Run(frame);
        Assert.Equal((byte)0, result.Core.Read(0x2798BF4, 1)[0]);
        Assert.Equal(0x1008u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Same(frame.Random, result.Random);
        Assert.Same(frame.Arena, result.Arena);
        Assert.False(result.TemporaryFrame.IsKnown(0xA8, 1));
        Assert.Throws<NotSupportedException>(() => result.Core.Read(0x279795F, 1));
        Assert.Equal(SnapshotJson(result), SnapshotJson(Run(restored)));
        Assert.Equal(2, copies);
    }

    [Fact]
    public void Zero_period_faults_before_frame_kind_or_copy_ring_are_read()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(1, 0);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x3050, 1), Core = frame.Core.MarkUnknown(0x2798BF4, 1) };
        Assert.Throws<DivideByZeroException>(() => NativeBattleCinematicBlitB8.Execute(frame, new(0x1000, 8, 0x1000), StaticResources(), (_, _) => Address(0x3000), (_, _) => throw new InvalidOperationException("No copy is enqueued.")));
    }

    [Fact]
    public void Shared_copy_helper_increments_the_live_counter_after_the_enqueue_service()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0, 1);
        for (int lane = 0; lane < 4; lane++)
        {
            frame = frame.WriteTemporaryWord(0xA0 + lane * 2, (ushort)(lane + 1));
        }
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicBlitB8.CopyB66FE0(frame, -1, -2, (current, copy) =>
        {
            Assert.Equal(new NativeBattleEffectVramCopy(1, 2, 3, 4, -1, -2), copy);
            return current with { Core = current.Core.Write(0x2798BF4, [7]) };
        });
        Assert.Equal((byte)8, result.Core.Read(0x2798BF4, 1)[0]);
    }

    private static NativeBattleWaterExecutionFrame Fixture(short phase, short period)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create();
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797450, 0x1000);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x94, unchecked((ushort)phase));
        byte[] program = new byte[8];
        BinaryPrimitives.WriteUInt16LittleEndian(program, 0xF0B8);
        BinaryPrimitives.WriteInt16LittleEndian(program.AsSpan(2), -2);
        BinaryPrimitives.WriteInt16LittleEndian(program.AsSpan(4), 1);
        BinaryPrimitives.WriteInt16LittleEndian(program.AsSpan(6), period);
        frame = frame with { Arena = frame.Arena.Write(0x1000, program).Write(0x3010, BitConverter.GetBytes(0x40u)).Write(0x3041, [0x10]).Write(0x3050, [2, 7, 11, 17, 19, 3, 5, 9, 13]) };
        frame = frame with { Core = frame.Core.Write(0x2798BF4, [255]).MarkUnknown(0x279795F, 1), TemporaryFrame = frame.TemporaryFrame.MarkUnknown(0xA8, 1) };
        return frame;
    }

    private static NativeBattleCinematicResources StaticResources()
    {
        byte[] data = new byte[24];
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(4), 0xFFFC);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(16), 0x7FFF);
        return new(0x1870FC0, new("static", data.ToImmutableArray()), null!, null!);
    }

    private static uint Address(int offset) => unchecked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)offset);

    private static string SnapshotJson<T>(T value) => JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(value));
}
