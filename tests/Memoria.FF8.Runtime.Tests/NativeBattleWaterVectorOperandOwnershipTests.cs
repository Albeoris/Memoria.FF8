using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleWaterVectorOperandOwnershipTests
{
    [Fact]
    public void Vector_dispatch_consumes_only_its_instruction_and_selected_operands_in_a_mutable_resource()
    {
        NativeBattleWaterExecutionFrame frame = Fixture();
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x1004, 0xFFC) };
        NativeBattleWaterExecutionFrame result = NativeBattleWaterInstructionDispatcher.ExecuteVector(frame, new(0x1000, 0x1000, 0x3000), 0x400);
        Assert.Equal(7u << 8, WaterSchedulerFixture.ReadSlotDword(result, 0, 0x68));
        Assert.Equal(0x3004u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(0x1004, 1));
        Assert.Equal(frame.Random, result.Random);
    }

    [Fact]
    public void A_selected_unknown_operand_remains_a_required_native_read()
    {
        NativeBattleWaterExecutionFrame frame = Fixture();
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x1002, 2) };
        Assert.Throws<NotSupportedException>(() => NativeBattleWaterInstructionDispatcher.ExecuteVector(frame, new(0x1000, 0x1000, 0x3000), 0x400));
    }

    private static NativeBattleWaterExecutionFrame Fixture()
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create();
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797450, 0x3000);
        return frame with { Arena = frame.Arena.Write(0x1000, [0x0E, 0x80, 7, 0]) };
    }
}
