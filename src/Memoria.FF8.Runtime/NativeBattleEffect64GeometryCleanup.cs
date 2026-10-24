using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect64GeometryCleanup
{
    public static NativeBattleEffectArena Clear(uint geometryAddress, Func<uint, int, NativeBattleEffectArena> read, Action<uint, ushort> writeWord, NativeBattleEffectArena temporary8)
    {
        int objects = BinaryPrimitives.ReadInt32LittleEndian(read(geometryAddress, 4).Read(0, 4));
        for (int index = 0; index < objects; index++)
        {
            int relative = BinaryPrimitives.ReadInt32LittleEndian(read(checked(geometryAddress + 4 + (uint)(index * 4)), 4).Read(0, 4));
            uint cursor = checked(geometryAddress + (uint)relative);
            int groups = Signed(cursor);
            cursor += 2;
            for (int group = 0; group < groups; group++)
            {
                int vertices = Signed(cursor + 2);
                if (vertices < 0)
                {
                    throw new InvalidDataException("Geometry cleanup requires its current bounded vertex groups.");
                }
                cursor = checked(cursor + 4 + (uint)(vertices * 6));
            }
            cursor = checked(cursor + 3) & ~3u;
            ushort triangles = Word(cursor);
            ushort quads = Word(cursor + 2);
            cursor += 12;
            temporary8 = temporary8.Write(0, BitConverter.GetBytes(cursor)).Write(4, BitConverter.GetBytes(triangles)).Write(6, BitConverter.GetBytes(quads));
            temporary8 = temporary8.Write(4, BitConverter.GetBytes(unchecked((ushort)(triangles - 1))));
            for (int face = 0; face < triangles; face++)
            {
                writeWord(cursor + 14, (ushort)(Word(cursor + 14) & 0x7FFF));
                cursor += 16;
                temporary8 = temporary8.Write(4, BitConverter.GetBytes(unchecked((ushort)(triangles - face - 2))));
            }
            temporary8 = temporary8.Write(6, BitConverter.GetBytes(unchecked((ushort)(quads - 1))));
            for (int face = 0; face < quads; face++)
            {
                writeWord(cursor + 14, (ushort)(Word(cursor + 14) & 0x7FFF));
                cursor += 20;
                temporary8 = temporary8.Write(6, BitConverter.GetBytes(unchecked((ushort)(quads - face - 2))));
            }
        }
        return temporary8;

        ushort Word(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(read(address, 2).Read(0, 2));
        short Signed(uint address) => BinaryPrimitives.ReadInt16LittleEndian(read(address, 2).Read(0, 2));
    }
}
