using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicMatrixAimCBTests
{
    private const uint Matrices = 0x27979E8;
    private static uint Address(int offset) => unchecked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)offset);
    private static readonly NativeBattleWaterScriptProgram Program = new(0x1000, 6, Address(0x1000));

    [Theory]
    [InlineData(true, false, 330)]
    [InlineData(true, true, 4426)]
    [InlineData(false, false, 4426)]
    [InlineData(false, true, 330)]
    public void Endpoint_translation_and_world_composition_are_independent_of_the_aim_rotation(bool fromCurrent, bool translateTo, int expectedZ)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(fromCurrent, translateTo);
        int resolutions = 0;
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicMatrixAimCB.Execute(frame, Program, (_, selector) =>
        {
            Assert.Equal((short)-7, selector);
            resolutions++;
            return Address(0x2100);
        });
        Assert.Equal(1, resolutions);
        Assert.Equal((ushort)7, BinaryPrimitives.ReadUInt16LittleEndian(result.Core.Read(0x2797206, 2)));
        short[] expected = fromCurrent ? [0, -4096, 0, -4096, 0, 0, 0, 0, -4096] : [0, -4096, 0, 4096, 0, 0, 0, 0, 4096];
        Assert.Equal(expected, Enumerable.Range(0, 9).Select(index => BinaryPrimitives.ReadInt16LittleEndian(result.Core.Read(Matrices + 32 + (uint)(index * 2), 2))));
        Assert.Equal(-190, BinaryPrimitives.ReadInt32LittleEndian(result.Core.Read(Matrices + 52, 4)));
        Assert.Equal(120, BinaryPrimitives.ReadInt32LittleEndian(result.Core.Read(Matrices + 56, 4)));
        Assert.Equal(expectedZ, BinaryPrimitives.ReadInt32LittleEndian(result.Core.Read(Matrices + 60, 4)));
        Assert.Equal((ushort)0, BinaryPrimitives.ReadUInt16LittleEndian(result.Core.Read(Matrices + 50, 2)));
        Assert.Equal(Address(0x1006), WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.All(Enumerable.Range(0xA0, 18), offset => Assert.False(result.TemporaryFrame.IsKnown(offset, 1)));
        Assert.Throws<NotSupportedException>(() => result.Core.Read(Matrices + 18, 1));
        Assert.Throws<NotSupportedException>(() => result.Core.Read(Matrices + 64, 1));
        Assert.Equal((ushort)0xA5A5, result.ReadTemporaryWord(0x13E));
    }

    [Fact]
    public void Actual_control_pointer_selects_translation_instead_of_an_unrelated_instruction_copy()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(true, false);
        uint otherContext = 0x2797300;
        frame = WaterSchedulerFixture.CoreDword(frame, 0x27973B8, otherContext);
        frame = frame with { Core = frame.Core.Write(otherContext + 0x4B, [0x10]) };
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicMatrixAimCB.Execute(frame, Program, (_, _) => Address(0x2100));
        Assert.Equal(4426, BinaryPrimitives.ReadInt32LittleEndian(result.Core.Read(Matrices + 60, 4)));
    }

    private static NativeBattleWaterExecutionFrame Fixture(bool fromCurrent, bool translateTo)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create();
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797450, Address(0x1000));
        frame = WaterSchedulerFixture.CoreDword(frame, 0x27973B8, frame.TemporaryFrame.Address + 0x100);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x12, 7);
        frame = frame.WriteTemporaryWord(0x14A, translateTo ? (ushort)0x10CB : (ushort)0x00CB);
        byte[] program = [0xCB, 0, 0, 0, 0, 0];
        BinaryPrimitives.WriteUInt16LittleEndian(program, translateTo ? (ushort)0x10CB : (ushort)0x00CB);
        BinaryPrimitives.WriteInt16LittleEndian(program.AsSpan(2), fromCurrent ? (short)0 : (short)-7);
        BinaryPrimitives.WriteInt16LittleEndian(program.AsSpan(4), fromCurrent ? (short)-7 : (short)0);
        byte[] global = new byte[32];
        short[] rotation = [0, -4096, 0, 4096, 0, 0, 0, 0, 4096];
        for (int index = 0; index < rotation.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(global.AsSpan(index * 2), rotation[index]);
        }
        BinaryPrimitives.WriteInt32LittleEndian(global.AsSpan(20), 10);
        BinaryPrimitives.WriteInt32LittleEndian(global.AsSpan(24), 20);
        BinaryPrimitives.WriteInt32LittleEndian(global.AsSpan(28), 30);
        frame = frame with { Core = frame.Core.Write(Matrices, global).MarkUnknown(Matrices + 18, 2).MarkUnknown(Matrices + 32, 33), Arena = frame.Arena.Write(0x1000, program).MarkUnknown(0x209A, 2).MarkUnknown(0x219A, 2) };
        frame = frame with { TemporaryFrame = frame.TemporaryFrame.MarkUnknown(0xA0, 18) };
        foreach (int index in new[] { 0, 1 })
        {
            frame = WaterSchedulerFixture.SlotWord(frame, index, 0x94, 100);
            frame = WaterSchedulerFixture.SlotWord(frame, index, 0x96, 200);
            frame = WaterSchedulerFixture.SlotWord(frame, index, 0x98, index == 0 ? (ushort)300 : (ushort)4396);
        }
        return frame;
    }
}
