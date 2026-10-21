namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicActorCommands77Tests
{
    [Theory]
    [InlineData(0x00BE, 2, -32768, true)]
    [InlineData(0x08BE, 130, 32767, true)]
    [InlineData(0x00BE, 0, -32768, false)]
    [InlineData(0x00BE, 1, 32767, false)]
    [InlineData(0x10BE, 2, -7, false)]
    [InlineData(0xF0BE, 2, 7, false)]
    public void Actor_branch_uses_high_nibble_BYTE_selector_and_signed_WORD_relative_to_opcode(ushort instruction, byte flags, short delta, bool loaded)
    {
        const uint basis = NativeBattleEffectNodeMemory.ArenaAddress;
        NativeBattleWaterExecutionFrame frame = Frame(instruction);
        frame = frame with { Arena = frame.Arena.Write(0x1002, BitConverter.GetBytes(delta)) };
        int reads = 0;
        if ((instruction & 0xF000) != 0)
        {
            frame = frame with { Core = frame.Core.MarkUnknown(0x279744C, 4) };
        }
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicActorCommands77.BranchBE(frame, new(0x1000, 4, basis + 0x1000), pointer =>
        {
            reads++;
            Assert.Equal(0x1D97494u, pointer);
            return flags;
        });
        Assert.Equal((instruction & 0xF000) == 0 ? 1 : 0, reads);
        Assert.Equal(unchecked(basis + 0x1000u + (loaded ? 4u : (uint)(int)delta)), WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Equal(frame.Arena, result.Arena);
        Assert.Equal(frame.Random, result.Random);
    }

    [Fact]
    public void Loaded_branch_does_not_read_the_unused_displacement()
    {
        NativeBattleWaterExecutionFrame frame = Frame(0xBE);
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicActorCommands77.BranchBE(frame, new(0, 0, 0), _ => 2);
        Assert.Equal(NativeBattleEffectNodeMemory.ArenaAddress + 0x1004, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(15)]
    public void Actor_service_reads_actor_only_for_mode_two_and_advances_after_the_service(int mode)
    {
        NativeBattleWaterExecutionFrame frame = Frame(checked((ushort)(0xC1 | mode << 12)));
        if (mode != 2)
        {
            frame = frame with { Core = frame.Core.MarkUnknown(0x279744C, 4) };
        }
        List<int> calls = [];
        NativeBattleWaterExecutionFrame Record(NativeBattleWaterExecutionFrame current, int service)
        {
            Assert.Equal(NativeBattleEffectNodeMemory.ArenaAddress + 0x1000, WaterSchedulerFixture.ReadCoreDword(current, 0x2797450));
            calls.Add(service);
            return current.WriteTemporaryWord(0x10, checked((ushort)service));
        }
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicActorCommands77.ExecuteC1(frame, new(current => Record(current, 0), current => Record(current, 1), (current, pointer) =>
        {
            Assert.Equal(0x1D97494u, pointer);
            return Record(current, 2);
        }));
        Assert.Equal(mode <= 2 ? new[] { mode } : [], calls);
        Assert.Equal(NativeBattleEffectNodeMemory.ArenaAddress + 0x1002, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Equal(mode <= 2 ? checked((ushort)mode) : (ushort)0xA5A5, result.ReadTemporaryWord(0x10));
        Assert.Equal(frame.Arena, result.Arena);
    }

    private static NativeBattleWaterExecutionFrame Frame(ushort instruction)
    {
        const uint directory = NativeBattleScriptedEffectMemory.Address;
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryWord(0x14A, instruction);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797450, NativeBattleEffectNodeMemory.ArenaAddress + 0x1000);
        frame = WaterSchedulerFixture.CoreDword(frame, directory + 0x60 + 4 * 250, 0x1D97494);
        return frame with { Arena = frame.Arena.Write(0x201B, new byte[] { 250 }) };
    }
}
