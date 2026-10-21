using System.Buffers.Binary;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicPosePosition9Tests
{
    [Theory]
    [InlineData(0)]
    [InlineData(0x1000)]
    [InlineData(0x800)]
    [InlineData(0x400)]
    [InlineData(0x1C00)]
    [InlineData(0xE3FF)]
    public void Pose_position_reads_BYTE_actor_and_signed_current_translation_and_only_writes_three_WORDs(ushort flags)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create();
        uint directory = BinaryPrimitives.ReadUInt32LittleEndian(frame.Core.Read(0x279744C, 4));
        frame = WaterSchedulerFixture.CoreDword(frame, directory + 0x60 + 4 * 250, 0x1D97494);
        frame = frame with { Arena = frame.Arena.Write(0x201B, new byte[] { 250 }).Write(0x20A0, BitConverter.GetBytes(NativeBattleEffectNodeMemory.ArenaAddress + 0x1000)).Write(0x1000, BitConverter.GetBytes(flags)) };
        int[] positions = [unchecked((int)0xFFFEFFFF), int.MaxValue, int.MinValue];
        short[] translations = [-257, 32767, -32768];
        for (int lane = 0; lane < 3; lane++)
        {
            frame = frame with { Arena = frame.Arena.Write(0x205C + 4 * lane, BitConverter.GetBytes(positions[lane])) };
        }
        int reads = 0;
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicPosePosition9.Update(frame, pointer =>
        {
            reads++;
            Assert.Equal(0x1D97494u, pointer);
            return new(-255, new(translations[0], translations[1], translations[2]));
        });
        Assert.Equal(1, reads);
        NativeBattleEffectArena expected = frame.Arena;
        for (int lane = 0; lane < 3; lane++)
        {
            int value = positions[lane] >> 16;
            if ((flags & (0x1000 >> lane)) != 0)
            {
                value += translations[lane] * -255 / 256;
            }
            expected = expected.Write(0x2094 + 2 * lane, BitConverter.GetBytes(unchecked((ushort)value)));
        }
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(result.Arena));
        Assert.Equal(frame.Core, result.Core);
        Assert.Equal(frame.TemporaryFrame, result.TemporaryFrame);
        Assert.Equal(frame.Random, result.Random);
    }
}
