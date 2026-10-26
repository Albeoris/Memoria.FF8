namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicActorMeshMask8DTests
{
    [Theory]
    [InlineData(0x008D, 0x0003, 0xA5000004u, 0xA5000007u)]
    [InlineData(0x808D, 0x0003, 0xA5000007u, 0xA5000004u)]
    [InlineData(0x008D, -32768, 0x12345678u, 0xFFFFD678u)]
    [InlineData(0x808D, -32768, 0xFFFFFFFFu, 0x00007FFFu)]
    [InlineData(0x108D, -1, 0x00000000u, 0xFFFFFFFFu)]
    [InlineData(0x908D, -1, 0xFFFFFFFFu, 0x00000000u)]
    public void Signed_mask_changes_only_current_selected_physical_actor(int instruction, int operand, uint mask, uint expected)
    {
        const uint selectedActor = 0x1D97494;
        NativeBattleWaterExecutionFrame frame = Fixture(unchecked((ushort)instruction), unchecked((short)operand));
        uint header = WaterSchedulerFixture.ReadCoreDword(frame, 0x279744C);
        frame = WaterSchedulerFixture.CoreDword(frame, header + 0x60 + 3 * 4, selectedActor);
        uint published = 0;
        int reads = 0;
        int writes = 0;
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicActorMeshMask8D.Execute(frame, new(0x1000, 4, 0x1000), actor =>
        {
            reads++;
            Assert.Equal(selectedActor, actor);
            return mask;
        }, (actor, value) =>
        {
            writes++;
            Assert.Equal(selectedActor, actor);
            published = value;
        });
        Assert.Equal(expected, published);
        Assert.Equal(1, reads);
        Assert.Equal(1, writes);
        Assert.Equal(0x1004u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Same(frame.Arena, result.Arena);
        Assert.Equal(frame.Random, result.Random);
        Assert.Equal(frame.TemporaryFrame, result.TemporaryFrame);
    }

    [Fact]
    public void Unselected_instruction_fails_before_reading_actor_or_operand()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x008C, 1);
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicActorMeshMask8D.Execute(frame, new(0x1000, 2, 0x1000), _ => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException()));
    }

    private static NativeBattleWaterExecutionFrame Fixture(ushort instruction, short operand)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.CoreDword(WaterSchedulerFixture.Create(), 0x2797450, 0x1000).WriteTemporaryWord(0x14A, instruction);
        return frame with { Arena = frame.Arena.Write(0x1002, BitConverter.GetBytes(operand)).Write(0x201B, [3]) };
    }
}
