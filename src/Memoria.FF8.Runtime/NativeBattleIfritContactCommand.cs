using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

/// <summary>The generic cinematic contact command, selecting all contacts or the first current-slot match.</summary>
public static class NativeBattleIfritContactCommand
{
    public static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame, NativeBattleWaterContextServices services)
    {
        ushort instruction = frame.ReadTemporaryWord(0x14A);
        if ((instruction & 0x0FFF) != 0x022)
        {
            throw new NotSupportedException("Native cinematic contact command requires its selected instruction.");
        }
        uint manager = CoreDword(frame, 0x27973EC);
        uint action = CoreDword(frame, unchecked(manager + 0xCC));
        uint contacts = BinaryPrimitives.ReadUInt32LittleEndian(ReadAction(unchecked(action + 8), 4));
        int count = unchecked((sbyte)ReadAction(unchecked(action + 0x10), 1)[0]);
        if ((instruction & 0xF000) == 0x8000)
        {
            for (int index = 0; index < count; index++)
            {
                frame = services.ApplyContact506690(frame, unchecked(contacts + (uint)(0x18 * index)));
            }
        }
        else
        {
            byte wanted = ReadSlot(frame, 0x1B, 1)[0];
            for (int index = 0; index < count; index++)
            {
                uint contact = unchecked(contacts + (uint)(0x18 * index));
                if (ReadAction(contact, 1)[0] == wanted)
                {
                    frame = services.ApplyContact506690(frame, contact);
                    break;
                }
            }
        }
        return WriteCoreDword(frame, 0x2797450, unchecked(CoreDword(frame, 0x2797450) + 2));

        byte[] ReadAction(uint address, int length)
        {
            if (address >= NativeBattleEffectNodeMemory.ArenaAddress && (long)address - NativeBattleEffectNodeMemory.ArenaAddress <= NativeBattleEffectArena.Length - length)
            {
                return frame.Arena.Read(ArenaOffset(address, length), length);
            }
            byte[] bytes = services.ReadActionBytes(frame, address, length);
            return bytes is not null && bytes.Length == length ? bytes : throw new InvalidDataException("Native cinematic contact read requires its exact addressed action-packet extent.");
        }
    }
}
