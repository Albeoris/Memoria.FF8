using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static class NativeBattleBoomerangSword304StaticMeshContext88
{
    public static NativeBattleStaticMeshContext88Step DrawLoaded(NativeBattleEffectMemory memory, NativeBattleStaticMeshResource mesh, NativeBattleCameraProjection projection, NativeBattleEffectArena context, NativeBattleSummonColors constants, int tableBaseBucket, byte depthShift, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        uint flags = BinaryPrimitives.ReadUInt32LittleEndian(context.Read(28, 4));
        bool main = mesh.Address == 0x102EA98;
        bool trail = mesh.Address == 0x102F2B4;
        int vertices = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(4));
        if ((!main && !trail) || main && (flags != 0 || vertices != 52) || trail && (flags != 0xC0 || vertices != 14))
        {
            throw new InvalidDataException("Boomerang Sword requires its actual inline static mesh context.");
        }
        int cursor = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan());
        int[] lengths = [12, 12, 20, 24, 20, 24, 28, 36];
        for (int group = 0; group < 8; group++)
        {
            int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(cursor, 4));
            if (count < 0 || count != 0 && (main ? group is not (2 or 3) : group != 4))
            {
                throw new NotSupportedException("Boomerang Sword static mesh requires its selected FT3, FT4 or G3 group.");
            }
            cursor = checked(cursor + 4 + count * lengths[group]);
            if (cursor > mesh.Bytes.Length)
            {
                throw new InvalidDataException("Boomerang Sword static mesh directory leaves its original source.");
            }
        }
        return NativeBattleStaticMeshContext88.DrawLoaded(memory, mesh, projection, context, constants, tableBaseBucket, depthShift, pilotIdentity: pilotIdentity);
    }
}
