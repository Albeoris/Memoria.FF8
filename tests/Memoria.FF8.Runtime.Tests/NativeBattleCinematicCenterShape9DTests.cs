using System.Buffers.Binary;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicCenterShape9DTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Recenters_signed_bounds_after_updating_scaled_fixed_origin_and_reloads_mutated_count(bool shorten)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(2);
        frame = Coordinates(frame, 0x3020, -11, -8, short.MinValue);
        frame = Coordinates(frame, 0x3028, 6, 3, short.MaxValue);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x3026, 2).MarkUnknown(0x302E, 2) };
        int updates = 0;
        NativeBattleWaterExecutionFrame Run(NativeBattleWaterExecutionFrame current) => NativeBattleCinematicCenterShape9D.Execute(current, new(0x1000, 10, 0x1000), (_, selector) =>
        {
            Assert.Equal(-2, selector);
            return Address(0x3000);
        }, active =>
        {
            updates++;
            Assert.Equal(unchecked((uint)(-3 << 16)) + 123u, WaterSchedulerFixture.ReadSlotDword(active, 0, 0x5C));
            Assert.Equal(65536u + 123u, WaterSchedulerFixture.ReadSlotDword(active, 0, 0x60));
            Assert.Equal(unchecked((uint)(-127 << 16)) + 123u, WaterSchedulerFixture.ReadSlotDword(active, 0, 0x64));
            Assert.Equal(-11, Read(active, 0x3020));
            return shorten ? Dword(active, 0x3018, 1) : active;
        });
        NativeBattleWaterExecutionFrame replayFrame = NativeBattleTestSnapshots.RestoreJson<NativeBattleWaterExecutionFrame>(NativeBattleTestSnapshots.Json(frame))!;
        NativeBattleWaterExecutionFrame result = Run(frame);
        Assert.Equal(1, updates);
        Assert.Equal(new short[] { -8, -5, -32767 }, new[] { Read(result, 0x3020), Read(result, 0x3022), Read(result, 0x3024) });
        Assert.Equal(shorten ? new short[] { 6, 3, 32767 } : new short[] { 9, 6, -32768 }, new[] { Read(result, 0x3028), Read(result, 0x302A), Read(result, 0x302C) });
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(0x3026, 2));
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(0x302E, 2));
        Assert.Equal(0x100Au, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Equal(NativeBattleTestSnapshots.Json(result), NativeBattleTestSnapshots.Json(Run(replayFrame)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Nonpositive_count_preserves_unknown_vertices_and_still_updates_origin_with_native_empty_bounds(int count)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(count);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x3020, 16) };
        int calls = 0;
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicCenterShape9D.Execute(frame, new(0x1000, 10, 0x1000), (_, _) => Address(0x3000), active =>
        {
            calls++;
            Assert.Equal(123u, WaterSchedulerFixture.ReadSlotDword(active, 0, 0x5C));
            Assert.Equal(123u, WaterSchedulerFixture.ReadSlotDword(active, 0, 0x60));
            Assert.Equal(123u, WaterSchedulerFixture.ReadSlotDword(active, 0, 0x64));
            return active;
        });
        Assert.Equal(1, calls);
        Assert.Equal(0x100Au, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(0x3020, 2));
    }

    private static NativeBattleWaterExecutionFrame Fixture(int count)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create();
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797450, 0x1000);
        byte[] program = new byte[10];
        short[] words = [0x9D, -2, 256, -128, 32767];
        for (int index = 0; index < words.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(program.AsSpan(index * 2), words[index]);
        }
        frame = frame with { Arena = frame.Arena.Write(0x1000, program) };
        frame = Dword(Dword(frame, 0x3014, 0x20), 0x3018, unchecked((uint)count));
        for (int lane = 0; lane < 3; lane++)
        {
            frame = WaterSchedulerFixture.SlotDword(frame, 0, 0x5C + lane * 4, 123);
        }
        return frame;
    }

    private static NativeBattleWaterExecutionFrame Coordinates(NativeBattleWaterExecutionFrame frame, int offset, short x, short y, short z)
    {
        byte[] bytes = new byte[6];
        BinaryPrimitives.WriteInt16LittleEndian(bytes, x);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(2), y);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(4), z);
        return frame with { Arena = frame.Arena.Write(offset, bytes) };
    }

    private static NativeBattleWaterExecutionFrame Dword(NativeBattleWaterExecutionFrame frame, int offset, uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return frame with { Arena = frame.Arena.Write(offset, bytes) };
    }

    private static short Read(NativeBattleWaterExecutionFrame frame, int offset) => BinaryPrimitives.ReadInt16LittleEndian(frame.Arena.Read(offset, 2));
    private static uint Address(int offset) => unchecked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)offset);
}
