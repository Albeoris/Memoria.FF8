using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleBrothersTargetCommandTests
{
    private const uint Header = 0x2797690;
    private static readonly NativeBattleWaterScriptProgram Program = new(0x1000, 4, 0x1000);

    [Fact]
    public void Other_encounters_advance_without_instruction_bone_header_or_flag_reads()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0xD5);
        frame = frame with { TemporaryFrame = frame.TemporaryFrame with { Known = frame.TemporaryFrame.Known.SetItem(0x14A, false).SetItem(0x14B, false) }, Core = frame.Core.MarkUnknown(0x279744C, 4).MarkUnknown(0x27973E8, 4), Arena = frame.Arena.MarkUnknown(0x1000, 4) };
        NativeBattleWaterExecutionFrame result = NativeBattleBrothersTargetCommand.Execute(frame, Program, Services(94));
        Assert.Equal(0x1004u, Pc(result));
    }

    [Theory]
    [InlineData(0x20D5)]
    [InlineData(0xF0D5)]
    public void Other_modes_are_literal_returns(ushort instruction)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(instruction);
        frame = frame with { Core = frame.Core.MarkUnknown(0x279744C, 4).MarkUnknown(0x27973E8, 4) };
        Assert.Same(frame, NativeBattleBrothersTargetCommand.Execute(frame, Program, Services(63)));
    }

    [Fact]
    public void Model3_target_clears_only_retained_flag_and_advances_without_operand()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0xD5);
        frame = frame with { Core = frame.Core.Write(Header + 0x48, new byte[] { 3 }), Arena = frame.Arena.MarkUnknown(0x1002, 2) };
        byte flag = 9;
        byte[] header = frame.Core.Read(Header, 0x180);
        NativeBattleWaterExecutionFrame result = NativeBattleBrothersTargetCommand.Execute(frame, Program, Services(63) with { WriteFlag = value => flag = value });
        Assert.Equal((byte)0, flag);
        Assert.Equal(header, result.Core.Read(Header, 0x180));
        Assert.Equal(0x1004u, Pc(result));
    }

    [Fact]
    public void Mode0_publishes_physical_model3_target_and_preserves_other_header_bytes()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0xD5);
        frame = frame with { Core = frame.Core.Write(Header, Enumerable.Repeat((byte)0xA5, 0x180).ToArray()), Arena = frame.Arena.MarkUnknown(0x1002, 2) };
        byte flag = 0;
        NativeBattleGraphicsModel model = new(3, 0x9876) { Transform = new(new(0, 0, 0), new(-123, 32767, -32768), 0, 0, 4096) };
        NativeBattleWaterExecutionFrame result = NativeBattleBrothersTargetCommand.Execute(frame, Program, Services(63) with { WriteFlag = value => flag = value, ReadModel3 = _ => model });
        byte[] expected = frame.Core.Read(Header, 0x180);
        expected[0x41] = 4;
        expected[0x4B] = 3;
        BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(0x54), 0x9876);
        BinaryPrimitives.WriteUInt32LittleEndian(expected.AsSpan(0x6C), 0x1D97494);
        BinaryPrimitives.WriteInt16LittleEndian(expected.AsSpan(0x9C), -123);
        BinaryPrimitives.WriteInt16LittleEndian(expected.AsSpan(0x9E), 32767);
        BinaryPrimitives.WriteInt16LittleEndian(expected.AsSpan(0xA0), -32768);
        Assert.Equal((byte)1, flag);
        Assert.Equal(expected, result.Core.Read(Header, 0x180));
        Assert.Equal(0x1004u, Pc(result));
    }

    [Theory]
    [InlineData(0, 255, -12, 0x1004u)]
    [InlineData(1, 2, -12, 0x1004u)]
    [InlineData(1, 3, -12, 0xFF4u)]
    [InlineData(1, 3, 12, 0x100Cu)]
    public void Mode1_branches_from_same_PC_only_for_retained_model3(byte flag, byte actor, short displacement, uint expectedPc)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x10D5);
        frame = frame with { Arena = frame.Arena.Write(0x201B, new byte[] { actor }).Write(0x1002, BitConverter.GetBytes(displacement)), Core = frame.Core.MarkUnknown(Header, 0x180) };
        if (flag == 0)
        {
            frame = frame with { Arena = frame.Arena.MarkUnknown(0x201B, 1) };
        }
        if (flag == 0 || actor != 3)
        {
            frame = frame with { Arena = frame.Arena.MarkUnknown(0x1002, 2) };
        }
        NativeBattleBrothersTargetServices services = Services(63) with { ReadFlag = () => flag };
        NativeBattleWaterExecutionFrame result = NativeBattleBrothersTargetCommand.Execute(frame, Program, services);
        Assert.Equal(expectedPc, Pc(result));
        NativeBattleWaterExecutionFrame restored = JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(JsonSerializer.Serialize(frame))!;
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(NativeBattleBrothersTargetCommand.Execute(restored, Program, services)));
    }

    [Fact]
    public void Cold_flag_is_zero_and_missing_warm_owner_stays_unknown()
    {
        Assert.Equal((byte)0, NativeBattleEffectMemory.FromColdProcess().ScriptedTarget2796D40);
        NativeBattleEffectMemory cold = NativeBattleEffectMemory.FromColdProcess();
        Assert.Null((cold with { ScriptedTarget2796D40 = null }).ScriptedTarget2796D40);
    }

    private static NativeBattleWaterExecutionFrame Fixture(ushort instruction)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryWord(0x14A, instruction);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797450, 0x1000);
        return WaterSchedulerFixture.CoreDword(frame, 0x279744C, Header);
    }

    private static NativeBattleBrothersTargetServices Services(ushort encounter) => new(encounter, () => throw new InvalidOperationException("Unexpected flag read."), _ => throw new InvalidOperationException("Unexpected flag write."), _ => throw new InvalidOperationException("Unexpected model read."));
    private static uint Pc(NativeBattleWaterExecutionFrame frame) => WaterSchedulerFixture.ReadCoreDword(frame, 0x2797450);
}
