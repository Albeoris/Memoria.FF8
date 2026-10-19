using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

/// <summary>Selects the current node's actor byte from the action, an inline byte, or the cached participant.</summary>
public static class NativeBattleCinematicActorSelector64
{
    public static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, Func<NativeBattleWaterExecutionFrame, uint, int, byte[]> readExternal)
    {
        ushort instruction = frame.ReadTemporaryWord(0x14A);
        if ((instruction & 0x1FF) != 0x64)
        {
            throw new NotSupportedException("Native actor selector requires its selected instruction.");
        }
        byte actor;
        uint size = 2;
        if ((instruction & 0x8000) != 0)
        {
            uint manager = CoreDword(frame, 0x27973EC);
            uint parameters = CoreDword(frame, unchecked(manager + 0xC0));
            actor = NativeBattleScriptedCommandReads.Address(frame, parameters, 1, readExternal)[0];
        }
        else if ((instruction & 0x1000) != 0)
        {
            actor = NativeBattleScriptedCommandReads.Program(frame, program, 2, 1)[0];
            size = 4;
        }
        else
        {
            uint owner = CoreDword(frame, 0x279744C);
            actor = frame.Core.Read(unchecked(owner + 0x48), 1)[0];
        }
        frame = frame with { Arena = frame.Arena.Write(SlotOffset(frame, 0x1B, 1), [actor]) };
        return NativeBattleScriptedCommandReads.Advance(frame, size);
    }
}
