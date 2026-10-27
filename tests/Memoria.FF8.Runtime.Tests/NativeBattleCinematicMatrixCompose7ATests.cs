using System.Buffers.Binary;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicMatrixCompose7ATests
{
    private const uint Matrices = 0x27979E8;

    [Theory]
    [InlineData((short)0, false, (ushort)0)]
    [InlineData((short)-1, true, (ushort)0xFECD)]
    public void Parent_matrix_and_current_matrix_are_composed_before_aliasing_destination_and_padding_is_published_last(short selector, bool tagged, ushort padding)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create();
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797450, 0x80001000);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x27973B8, frame.TemporaryFrame.Address + 0x100);
        frame = frame.WriteTemporaryByte(0x14B, tagged ? (byte)0x80 : (byte)0);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x12, 7);
        frame = WaterSchedulerFixture.SlotWord(frame, 1, 0x12, 8);
        frame = WaterSchedulerFixture.CoreWord(frame, 0x2797206, 7);
        frame = WaterSchedulerFixture.CoreWord(frame, 0x2797208, 8);
        frame = WaterSchedulerFixture.CoreWord(frame, 0x2797692, 0xFECD);
        byte[] program = [0x7A, 0, 0, 0, 0, 0];
        BinaryPrimitives.WriteInt16LittleEndian(program.AsSpan(2), selector);
        frame = frame with { Arena = frame.Arena.Write(0x1000, program) };
        short[] turn = [0, -4096, 0, 4096, 0, 0, 0, 0, 4096];
        frame = Matrix(frame, Matrices, turn, 10, 20, 30);
        frame = Matrix(frame, Matrices + 64, turn, 10, 20, 30);
        frame = Matrix(frame, Matrices + 32, [4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 100, -200, 300);
        NativeBattleWaterExecutionFrame Run(NativeBattleWaterExecutionFrame current) => NativeBattleCinematicMatrixCompose7A.Execute(current, new(0x1000, 6, 0x80001000), (_, actual) =>
        {
            Assert.Equal(selector, actual);
            return NativeBattleEffectNodeMemory.ArenaAddress + 0x2100;
        });
        NativeBattleWaterExecutionFrame replayFrame = NativeBattleTestSnapshots.RestoreJson<NativeBattleWaterExecutionFrame>(NativeBattleTestSnapshots.Json(frame))!;
        NativeBattleWaterExecutionFrame result = Run(frame);
        Assert.Equal(turn, Enumerable.Range(0, 9).Select(index => BinaryPrimitives.ReadInt16LittleEndian(result.Core.Read(Matrices + 32 + (uint)(index * 2), 2))));
        Assert.Equal(210u, WaterSchedulerFixture.ReadCoreDword(result, Matrices + 52));
        Assert.Equal(120u, WaterSchedulerFixture.ReadCoreDword(result, Matrices + 56));
        Assert.Equal(330u, WaterSchedulerFixture.ReadCoreDword(result, Matrices + 60));
        Assert.Equal(padding, BinaryPrimitives.ReadUInt16LittleEndian(result.Core.Read(Matrices + 50, 2)));
        Assert.Equal(0x80001006u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Same(frame.TemporaryFrame, result.TemporaryFrame);
        Assert.Equal(NativeBattleTestSnapshots.Json(result), NativeBattleTestSnapshots.Json(Run(replayFrame)));
    }

    private static NativeBattleWaterExecutionFrame Matrix(NativeBattleWaterExecutionFrame frame, uint address, short[] rotation, int x, int y, int z)
    {
        byte[] raw = new byte[32];
        for (int index = 0; index < rotation.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(raw.AsSpan(index * 2), rotation[index]);
        }
        BinaryPrimitives.WriteInt32LittleEndian(raw.AsSpan(20), x);
        BinaryPrimitives.WriteInt32LittleEndian(raw.AsSpan(24), y);
        BinaryPrimitives.WriteInt32LittleEndian(raw.AsSpan(28), z);
        return frame with { Core = frame.Core.Write(address, raw).MarkUnknown(address + 18, 2) };
    }
}
