using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicRangeBranch94Tests
{
    private static readonly NativeBattleWaterSlotBank Bank = new(0x1000, 0x3000, 8, 0x2000);
    private static readonly NativeBattleWaterScriptProgram Program = new(0x1000, 10, NativeBattleEffectNodeMemory.ArenaAddress + 0x1000);

    [Theory]
    [InlineData(100, 200, 110, 220, 10, 20, true)]
    [InlineData(100, 200, 90, 180, 10, 20, true)]
    [InlineData(100, 200, 111, 200, 10, 20, false)]
    [InlineData(100, 200, 100, 179, 10, 20, false)]
    [InlineData(0, 0, 0, 0, -1, 20, false)]
    [InlineData(0, 0, 0, 0, 20, -1, false)]
    [InlineData(32767, -32768, -32768, 32767, 32767, 32767, false)]
    [InlineData(-32768, 32767, -1, 0, 32767, 32767, true)]
    public void Signed_XZ_bounds_are_inclusive_and_use_promoted_arithmetic(short x, short z, short selectedX, short selectedZ, short toleranceX, short toleranceZ, bool inside)
    {
        NativeBattleWaterExecutionFrame frame = Create(x, z, selectedX, selectedZ, toleranceX, toleranceZ, -16);
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicRangeBranch94.Execute(frame, Bank, Program);
        Assert.Equal(unchecked(Program.Address + (inside ? (uint)-16 : 10u)), WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Same(frame.Arena, result.Arena);
        Assert.Same(frame.TemporaryFrame, result.TemporaryFrame);
        Assert.Equal(frame.Random, result.Random);
    }

    [Fact]
    public void Signed_parent_selector_uses_the_current_bank_lookup_and_survives_JSON_next()
    {
        NativeBattleWaterExecutionFrame frame = Create(1, 2, 1, 2, 0, 0, 20);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x14, 7);
        frame = frame with { Arena = frame.Arena.Write(0x1002, BitConverter.GetBytes((short)-1)) };
        NativeBattleWaterExecutionFrame restored = JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(JsonSerializer.Serialize(frame))!;
        NativeBattleWaterExecutionFrame expected = NativeBattleCinematicRangeBranch94.Execute(frame, Bank, Program);
        NativeBattleWaterExecutionFrame actual = NativeBattleCinematicRangeBranch94.Execute(restored, Bank, Program);
        Assert.Equal(Program.Address + 20, WaterSchedulerFixture.ReadCoreDword(actual, 0x2797450));
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
    }

    [Fact]
    public void Rejected_X_does_not_read_Z_or_branch_displacement_but_still_reads_both_tolerances()
    {
        NativeBattleWaterExecutionFrame frame = Create(0, 0, 100, 0, 1, 1, 4);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x2098, 2).MarkUnknown(0x2198, 2).MarkUnknown(0x1008, 2) };
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicRangeBranch94.Execute(frame, Bank, Program);
        Assert.Equal(Program.Address + 10, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        NativeBattleWaterExecutionFrame missingTolerance = frame with { Arena = frame.Arena.MarkUnknown(0x1006, 2) };
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicRangeBranch94.Execute(missingTolerance, Bank, Program));
    }

    [Fact]
    public void Accepted_branch_requires_its_displacement_and_current_selected_XZ_owners()
    {
        NativeBattleWaterExecutionFrame frame = Create(0, 0, 0, 0, 0, 0, 4);
        foreach (int offset in new[] { 0x1008, 0x2094, 0x2098, 0x2194, 0x2198 })
        {
            NativeBattleWaterExecutionFrame missing = frame with { Arena = frame.Arena.MarkUnknown(offset, 2) };
            Assert.Throws<NotSupportedException>(() => NativeBattleCinematicRangeBranch94.Execute(missing, Bank, Program));
        }
    }

    private static NativeBattleWaterExecutionFrame Create(short x, short z, short selectedX, short selectedZ, short toleranceX, short toleranceZ, short displacement)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create();
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797450, Program.Address);
        frame = frame with { Core = frame.Core.Write(0x2797454, new byte[] { 1, 0xFF }) };
        frame = WaterSchedulerFixture.SlotWord(frame, 1, 0x12, 7);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x94, unchecked((ushort)x));
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x98, unchecked((ushort)z));
        frame = WaterSchedulerFixture.SlotWord(frame, 1, 0x94, unchecked((ushort)selectedX));
        frame = WaterSchedulerFixture.SlotWord(frame, 1, 0x98, unchecked((ushort)selectedZ));
        byte[] instruction = new byte[10];
        BitConverter.GetBytes((short)0x94).CopyTo(instruction, 0);
        BitConverter.GetBytes((short)7).CopyTo(instruction, 2);
        BitConverter.GetBytes(toleranceX).CopyTo(instruction, 4);
        BitConverter.GetBytes(toleranceZ).CopyTo(instruction, 6);
        BitConverter.GetBytes(displacement).CopyTo(instruction, 8);
        return frame with { Arena = frame.Arena.Write(0x1000, instruction).MarkUnknown(0x2096, 2).MarkUnknown(0x2196, 2) };
    }
}
