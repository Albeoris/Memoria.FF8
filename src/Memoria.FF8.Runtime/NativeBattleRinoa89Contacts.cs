using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static class NativeBattleRinoa89Contacts
{
    public static NativeBattleRinoa89Frame Apply6FC410(NativeBattleRinoa89Frame frame, NativeBattleActionPacketsState packets, Func<NativeBattleRinoa89Frame, uint, NativeBattleRinoa89Frame> apply506690)
    {
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Native6FC410 requires its actual selected action parameters.");
        int count = packets.Headers[parameters.HeaderIndex7C].ReadByte(16) >> 3;
        for (int index = 0; index < count; index++)
        {
            uint pointer = BinaryPrimitives.ReadUInt32LittleEndian(frame.Staging.Read(0x2556620, 4));
            if (pointer < 0x1D28344 || (pointer - 0x1D28344) % 24 != 0)
            {
                throw new InvalidDataException("Native6FC410 requires its actual physical hit cursor.");
            }
            _ = packets.ReadContact(checked((int)((pointer - 0x1D28344) / 24)));
            frame = apply506690(frame, pointer);
            uint current = BinaryPrimitives.ReadUInt32LittleEndian(frame.Staging.Read(0x2556620, 4));
            frame = frame with { Staging = frame.Staging.Write(0x2556620, BitConverter.GetBytes(unchecked(current + 24))) };
        }
        return frame;
    }
}
