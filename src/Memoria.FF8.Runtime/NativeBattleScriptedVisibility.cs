using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleScriptedVisibilityServices(Func<NativeBattleWaterExecutionFrame, int, ushort> ReadFlags, Func<NativeBattleWaterExecutionFrame, int, ushort, NativeBattleWaterExecutionFrame> WriteFlags);

public static class NativeBattleScriptedVisibility
{
    public static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, NativeBattleScriptedVisibilityServices services)
    {
        if ((frame.ReadTemporaryWord(0x14A) & 0x1FF) != 0x13D)
        {
            throw new NotSupportedException("Native visibility command requires its selected instruction.");
        }
        uint manager = CoreDword(frame, 0x27973EC);
        uint root = CoreDword(frame, manager + 0x90);
        bool party = frame.Arena.Read(ArenaOffset(unchecked(root + 0x1B), 1), 1)[0] < 3;
        short operand = BinaryPrimitives.ReadInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, 2, 2));
        bool hide = operand is 1 or 3 or 5;
        bool all = operand is 4 or 5;
        bool selectedParty = operand is 2 or 3 ? !party : party;
        int first = all || selectedParty ? 0 : 3;
        int count = all ? 7 : selectedParty ? 3 : 4;
        for (int index = first; index < first + count; index++)
        {
            if (!hide)
            {
                uint scene = CoreDword(frame, 0x279744C);
                if ((CoreWord(frame, unchecked(scene + 0x4Eu + (uint)(2 * index))) & 4) != 0)
                {
                    continue;
                }
            }
            ushort flags = services.ReadFlags(frame, index);
            frame = services.WriteFlags(frame, index, hide ? (ushort)(flags | 4) : (ushort)(flags & 0xFFFB));
        }
        return NativeBattleScriptedCommandReads.Advance(frame, 4);
    }
}
