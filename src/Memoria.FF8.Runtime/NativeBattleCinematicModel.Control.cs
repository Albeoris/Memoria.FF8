using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleCinematicModel
{
    public static NativeBattleWaterExecutionFrame Control41(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program)
    {
        uint model = BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, 0xBC, 4));
        ushort operand = BinaryPrimitives.ReadUInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, 2, 2));
        int mode = frame.ReadTemporaryWord(0x14A) >> 12;
        if (mode == 0 || mode == 8)
        {
            int offset = ArenaOffset(unchecked(model + 2), 2);
            ushort flags = BinaryPrimitives.ReadUInt16LittleEndian(frame.Arena.Read(offset, 2));
            flags = mode == 0 ? (ushort)(flags & ~operand) : (ushort)(flags | operand);
            frame = frame with { Arena = frame.Arena.Write(offset, BitConverter.GetBytes(flags)) };
            return NativeBattleScriptedCommandReads.Advance(frame, 4);
        }
        if (mode == 1)
        {
            int offset = ArenaOffset(unchecked(model + 2), 1);
            frame = frame with { Arena = frame.Arena.Write(offset, new byte[] { (byte)(frame.Arena.Read(offset, 1)[0] | 4) }) };
            uint address = unchecked(CoreDword(frame, 0x2797450) + 2);
            frame = frame with { Arena = frame.Arena.Write(ArenaOffset(unchecked(model + 0x7C), 4), BitConverter.GetBytes(address)) };
            return NativeBattleScriptedCommandReads.Advance(frame, 6);
        }
        return frame;
    }
}
