using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

internal static class NativeBattleScriptedCommandReads
{
    public static byte[] Address(NativeBattleWaterExecutionFrame frame, uint address, int length, Func<NativeBattleWaterExecutionFrame, uint, int, byte[]> external)
    {
        if (address >= NativeBattleEffectNodeMemory.ArenaAddress && (long)address - NativeBattleEffectNodeMemory.ArenaAddress <= NativeBattleEffectArena.Length - length)
        {
            return frame.Arena.Read(ArenaOffset(address, length), length);
        }
        if (address >= NativeBattleScriptedEffectMemory.Address && (long)address - NativeBattleScriptedEffectMemory.Address <= NativeBattleScriptedEffectMemory.Length - length)
        {
            return frame.Core.Read(address, length);
        }
        byte[] bytes = external(frame, address, length);
        return bytes is not null && bytes.Length == length ? bytes : throw new InvalidDataException("Native cinematic command requires its exact current addressed bytes.");
    }

    public static byte[] Program(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, uint delta, int length)
    {
        uint relative = unchecked(CoreDword(frame, 0x2797450) + delta - program.Address);
        if (program.ArenaOffset < 0 || program.Length < length || program.ArenaOffset > NativeBattleEffectArena.Length - program.Length || relative > (uint)(program.Length - length))
        {
            throw new InvalidDataException("Native cinematic operand leaves its current program owner.");
        }
        return frame.Arena.Read(checked(program.ArenaOffset + (int)relative), length);
    }

    public static NativeBattleWaterExecutionFrame Advance(NativeBattleWaterExecutionFrame frame, uint amount) => WriteCoreDword(frame, 0x2797450, unchecked(CoreDword(frame, 0x2797450) + amount));
}
