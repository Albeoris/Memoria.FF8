using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect312Contacts
{
    public static int Find(uint parametersAddress, int actor, Func<uint, int, NativeBattleEffectArena> read, NativeBattleActionPacketsState? packets = null)
    {
        uint header = BinaryPrimitives.ReadUInt32LittleEndian(read(checked(parametersAddress + 4), 4).Read(0, 4));
        int count = packets?.PresentationRoster is null
            ? read(checked(header + 16), 1).Read(0, 1)[0]
            : NativeBattleCinematicContacts.ReadCount(header, (address, length) => read(address, length).Read(0, length), packets, signedNative: false);
        if (count == 0)
        {
            return -1;
        }
        uint first = BinaryPrimitives.ReadUInt32LittleEndian(read(checked(header + 8), 4).Read(0, 4));
        for (int index = 0; index < count; index++)
        {
            uint contact = packets?.PresentationRoster is null ? checked(first + (uint)(index * 24)) : NativeBattleCinematicContacts.Offset(first, index, packets);
            if (read(contact, 1).Read(0, 1)[0] == actor)
            {
                return index;
            }
        }
        return -1;
    }
}
