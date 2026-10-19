using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicEncounterBranchC5Tests
{
    [Theory]
    [InlineData(339, 339, -12, 0xFF4u)]
    [InlineData(0, 0, 0, 0x1000u)]
    [InlineData(32767, 32767, 32767, 0x8FFFu)]
    public void Matching_encounter_branches_from_instruction_start_and_survives_snapshot(int encounter, short operand, short displacement, uint expected)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(operand, displacement);
        NativeBattleWaterExecutionFrame restored = JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(JsonSerializer.Serialize(frame))!;
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicEncounterBranchC5.Execute(frame, new(0x1000, 6, 0x1000), (ushort)encounter);
        Assert.Equal(expected, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(NativeBattleCinematicEncounterBranchC5.Execute(restored, new(0x1000, 6, 0x1000), (ushort)encounter)));
        Assert.Equal(frame.Arena, result.Arena);
    }

    [Theory]
    [InlineData(340, 339)]
    [InlineData(65535, -1)]
    [InlineData(32768, -32768)]
    public void Mismatch_does_not_read_unknown_skipped_displacement_or_sign_extend_encounter(int encounter, short operand)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(operand, 12);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x1004, 2) };
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicEncounterBranchC5.Execute(frame, new(0x1000, 6, 0x1000), (ushort)encounter);
        Assert.Equal(0x1006u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Equal(frame.Arena, result.Arena);
    }

    private static NativeBattleWaterExecutionFrame Fixture(short operand, short displacement)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.CoreDword(WaterSchedulerFixture.Create(), 0x2797450, 0x1000);
        return frame with { Arena = frame.Arena.Write(0x1002, BitConverter.GetBytes(operand)).Write(0x1004, BitConverter.GetBytes(displacement)) };
    }
}
