using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicLocalClipTests
{
    [Theory]
    [InlineData(8, 2, 50, 16)]
    [InlineData(4, 2, -50, 16)]
    [InlineData(12, 2, 50, 16)]
    [InlineData(8, 5000, 10, -25536)]
    public void Local_clip_wraps_word_threshold_preserves_padding_and_publishes_actual_work_source(ushort flags, short localY, short sourceY, short expected)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x6000);
        byte[] point = new byte[8];
        BinaryPrimitives.WriteInt16LittleEndian(point, 123);
        BinaryPrimitives.WriteInt16LittleEndian(point.AsSpan(2), sourceY);
        BinaryPrimitives.WriteInt16LittleEndian(point.AsSpan(4), -456);
        frame = frame with { Arena = frame.Arena.Write(0x5000, point).Write(0x6000, Enumerable.Repeat((byte)0xA5, 8).ToArray()) };
        NativeBattleWaterExecutionFrame result = NativeBattleScriptedMeshPreparation.ApplyLocalClip(frame, new(new(100, localY, 200), flags), 1);
        Assert.Equal(0x20E5AB8u, result.ReadTemporaryDword(0x74));
        Assert.Equal((short)123, BinaryPrimitives.ReadInt16LittleEndian(result.Arena.Read(0x6000, 2)));
        Assert.Equal(expected, BinaryPrimitives.ReadInt16LittleEndian(result.Arena.Read(0x6002, 2)));
        Assert.Equal((short)-456, BinaryPrimitives.ReadInt16LittleEndian(result.Arena.Read(0x6004, 2)));
        Assert.Equal(new byte[] { 0xA5, 0xA5 }, result.Arena.Read(0x6006, 2));
    }

    [Fact]
    public void Local_clip_reads_each_word_after_prior_writes_when_source_and_destination_overlap()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x4FFE);
        frame = frame with { Arena = frame.Arena.Write(0x5000, new byte[] { 10, 0, 100, 0, 30, 0, 0xA5, 0xA5 }) };
        NativeBattleWaterExecutionFrame result = NativeBattleScriptedMeshPreparation.ApplyLocalClip(frame, new(new(0, 2, 0), 8), 1);
        Assert.Equal((short)16, BinaryPrimitives.ReadInt16LittleEndian(result.Arena.Read(0x4FFE, 2)));
        Assert.Equal((short)16, BinaryPrimitives.ReadInt16LittleEndian(result.Arena.Read(0x5000, 2)));
        Assert.Equal((short)30, BinaryPrimitives.ReadInt16LittleEndian(result.Arena.Read(0x5002, 2)));
    }

    [Theory]
    [InlineData(0, 1, false)]
    [InlineData(8, 0, true)]
    [InlineData(4, -1, true)]
    public void No_clamp_flags_leave_source_untouched_but_nonpositive_counts_still_publish_work(ushort flags, int count, bool changesSource)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x6000);
        NativeBattleWaterExecutionFrame result = NativeBattleScriptedMeshPreparation.ApplyLocalClip(frame, new(new(0, 0, 0), flags), count);
        Assert.Equal(changesSource ? 0x20E5AB8u : 0x20E4AB8u, result.ReadTemporaryDword(0x74));
        Assert.Same(frame.Arena, result.Arena);
    }

    private static NativeBattleWaterExecutionFrame Fixture(int destination) => WaterSchedulerFixture.Create().WriteTemporaryDword(0x74, NativeBattleEffectNodeMemory.ArenaAddress + 0x5000).WriteTemporaryDword(0x7C, NativeBattleEffectNodeMemory.ArenaAddress + (uint)destination);
}
