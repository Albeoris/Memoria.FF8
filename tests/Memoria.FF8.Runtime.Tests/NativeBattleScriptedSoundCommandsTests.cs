using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleScriptedSoundCommandsTests
{
    [Theory]
    [InlineData(0x00C3)]
    [InlineData(0x7EC3)]
    [InlineData(0x80C3)]
    [InlineData(0xFEC3)]
    public void Bufferless_sound_control_reads_no_operand_and_only_advances_current_PC(ushort instruction)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryWord(0x14A, instruction);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797450, 0x80001000);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x1000, 8) };
        NativeBattleWaterExecutionFrame result = NativeBattleScriptedSoundCommands.ExecuteC3(frame, NativeGlobalSoundBackend.Bufferless);
        Assert.Same(frame.Arena, result.Arena);
        Assert.Equal(frame.TemporaryFrame, result.TemporaryFrame);
        Assert.Equal(0x80001002u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Throws<NotSupportedException>(() => NativeBattleScriptedSoundCommands.ExecuteC3(frame, (NativeGlobalSoundBackend)1));
    }
}
