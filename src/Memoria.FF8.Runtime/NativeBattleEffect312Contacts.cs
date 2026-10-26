using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect312Contacts
{
    public static int Find(uint parametersAddress, int actor, Func<uint, int, NativeBattleEffectArena> read)
    {
        uint header = BinaryPrimitives.ReadUInt32LittleEndian(read(checked(parametersAddress + 4), 4).Read(0, 4));
        int count = read(checked(header + 16), 1).Read(0, 1)[0];
        if (count == 0)
        {
            return -1;
        }
        uint first = BinaryPrimitives.ReadUInt32LittleEndian(read(checked(header + 8), 4).Read(0, 4));
        for (int index = 0; index < count; index++)
        {
            if (read(checked(first + (uint)(index * 24)), 1).Read(0, 1)[0] == actor)
            {
                return index;
            }
        }
        return -1;
    }
}
