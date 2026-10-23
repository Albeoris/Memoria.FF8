using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static class NativeBattleSnipeLaser302StaticMeshContext88
{
    public static NativeBattleStaticMeshContext88Step DrawLoaded(NativeBattleEffectMemory memory, NativeBattleStaticMeshResource mesh, NativeBattleCameraProjection projection, NativeBattleEffectArena context, NativeBattleSummonColors constants, int tableBaseBucket, byte depthShift)
    {
        uint flags = BinaryPrimitives.ReadUInt32LittleEndian(context.Read(28, 4));
        if (mesh.Address != 0x103462C || BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(4)) != 220 || flags is not (0x30 or 0xF0))
        {
            throw new InvalidDataException("Snipe Laser requires its actual inline static mesh context.");
        }
        int cursor = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan());
        int[] lengths = [12, 12, 20, 24, 20, 24, 28, 36];
        for (int group = 0; group < 8; group++)
        {
            int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(cursor, 4));
            if (count < 0 || count != 0 && group is not (3 or 5 or 7))
            {
                throw new NotSupportedException("Snipe Laser static mesh requires its selected FT4, G4 or GT4 group.");
            }
            cursor = checked(cursor + 4 + count * lengths[group]);
            if (cursor > mesh.Bytes.Length)
            {
                throw new InvalidDataException("Snipe Laser static mesh directory leaves its original source.");
            }
        }
        return NativeBattleStaticMeshContext88.DrawLoaded(memory, mesh, projection, context, constants, tableBaseBucket, depthShift);
    }
}
