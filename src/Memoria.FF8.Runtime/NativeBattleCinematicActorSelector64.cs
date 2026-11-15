using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

/// <summary>Selects the current node's actor byte from the action, an inline byte, or the cached participant.</summary>
public static class NativeBattleCinematicActorSelector64
{
    public static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, BattleModelOwner source, NativeBattleModelAliases aliases)
    {
        ushort instruction = frame.ReadTemporaryWord(0x14A);
        if ((instruction & 0x1FF) != 0x64)
        {
            throw new NotSupportedException("Native actor selector requires its selected instruction.");
        }
        BattleModelOwner actor;
        uint size = 2;
        if ((instruction & 0x8000) != 0)
        {
            actor = source;
        }
        else if ((instruction & 0x1000) != 0)
        {
            actor = aliases.DecodeLiteral(NativeBattleScriptedCommandReads.Program(frame, program, 2, 1)[0]);
            size = 4;
        }
        else
        {
            uint owner = CoreDword(frame, 0x279744C);
            actor = NativeBattleCinematicActorAccess.ReadCurrentActor(frame, 0, owner);
        }
        frame = NativeBattleCinematicActorAccess.WriteSlotActor(frame, actor, aliases);
        return NativeBattleScriptedCommandReads.Advance(frame, size);
    }
}
