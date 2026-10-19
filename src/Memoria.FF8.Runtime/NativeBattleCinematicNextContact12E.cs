using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public static class NativeBattleCinematicNextContact12E
{
    public static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame, NativeBattleWaterContextServices services)
    {
        uint manager = CoreDword(frame, 0x27973EC);
        uint action = CoreDword(frame, manager + 0xCC);
        uint owner = CoreDword(frame, 0x279744C);
        byte index = frame.Core.Read(owner + 0x3A, 1)[0];
        int count = unchecked((sbyte)Read(action + 0x10, 1)[0]);
        if (count > index)
        {
            uint contacts = BinaryPrimitives.ReadUInt32LittleEndian(Read(action + 8, 4));
            frame = services.ApplyContact506690(frame, unchecked(contacts + 24u * index));
            owner = CoreDword(frame, 0x279744C);
            byte current = frame.Core.Read(owner + 0x3A, 1)[0];
            frame = frame with { Core = frame.Core.Write(owner + 0x3A, new[] { unchecked((byte)(current + 1)) }) };
        }
        return NativeBattleScriptedCommandReads.Advance(frame, 2);

        byte[] Read(uint address, int length)
        {
            if (address >= NativeBattleEffectNodeMemory.ArenaAddress && (long)address - NativeBattleEffectNodeMemory.ArenaAddress <= NativeBattleEffectArena.Length - length)
            {
                return frame.Arena.Read(ArenaOffset(address, length), length);
            }
            byte[] bytes = services.ReadActionBytes(frame, address, length);
            return bytes is not null && bytes.Length == length ? bytes : throw new InvalidDataException("Native next contact requires its exact addressed action bytes.");
        }
    }
}
