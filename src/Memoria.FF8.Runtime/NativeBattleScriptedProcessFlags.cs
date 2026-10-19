using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleScriptedProcessFlagsStep(NativeBattleWaterExecutionFrame Frame, NativeBattleStageState State);

public static class NativeBattleScriptedProcessFlags
{
    public static NativeBattleScriptedProcessFlagsStep Execute(NativeBattleWaterExecutionFrame frame, NativeBattleStageState stage)
    {
        if (stage.Groups.IsDefault || stage.Groups.Length != 4 || stage.Groups.Any(group => group is null))
        {
            throw new InvalidDataException("Native stage flag command requires its four current group rows.");
        }
        ushort instruction = frame.ReadTemporaryWord(0x14A);
        if ((instruction & 0x1FF) != 0x6D)
        {
            throw new NotSupportedException("Native stage flag command requires its selected instruction.");
        }
        ImmutableArray<NativeBattleStageGroup> groups = stage.Groups.Select(group => group with { Flags = (instruction & 0x8000) == 0 ? (byte)(group.Flags | 2) : (byte)(group.Flags & 0xFD) }).ToImmutableArray();
        return new(NativeBattleScriptedCommandReads.Advance(frame, 2), stage with { Groups = groups });
    }
}
