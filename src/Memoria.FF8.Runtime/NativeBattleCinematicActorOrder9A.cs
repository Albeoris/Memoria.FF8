using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public static class NativeBattleCinematicActorOrder9A
{
    public static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program)
    {
        int count = frame.ReadTemporaryWord(0x14A) >> 12;
        if (NativeBattleCinematicActorAccess.ReadContext(frame) is { } context)
        {
            return ExecuteHosted(frame, program, context, count);
        }
        uint attributes = CoreDword(frame, 0x279744C);
        frame = frame with { Core = frame.Core.Write(unchecked(attributes + 0x41), [unchecked((byte)count)]) };
        frame = NativeBattleScriptedCommandReads.Advance(frame, 2);
        for (int index = 0; index < count; index++)
        {
            byte value = NativeBattleScriptedCommandReads.Program(frame, program, 0, 1)[0];
            frame = frame with { Core = frame.Core.Write(unchecked(attributes + 0x48u + (uint)index), [value]) };
            frame = NativeBattleScriptedCommandReads.Advance(frame, 2);
        }
        return frame;
    }

    private static NativeBattleWaterExecutionFrame ExecuteHosted(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, NativeBattleCinematicActorContext context, int count)
    {
        ImmutableArray<byte>.Builder actors = ImmutableArray.CreateBuilder<byte>(count);
        frame = NativeBattleScriptedCommandReads.Advance(frame, 2);
        for (int index = 0; index < count; index++)
        {
            actors.Add(NativeBattleScriptedCommandReads.Program(frame, program, 0, 1)[0]);
            frame = NativeBattleScriptedCommandReads.Advance(frame, 2);
        }
        ImmutableArray<byte> ordered = actors.MoveToImmutable();
        NativeBattleCinematicActorContext updated = context.WithCurrentActors(ordered);
        uint header = NativeBattleScriptedEffectMemory.Address;
        NativeBattleScriptedEffectMemory core = frame.Core.Write(header + 0x41, [checked((byte)count)]);
        if (!ordered.IsEmpty)
        {
            core = core.Write(header + 0x48, ordered.AsSpan()[..Math.Min(ordered.Length, 6)]);
        }
        return frame with { Core = core with { PresentationActors = updated } };
    }
}
