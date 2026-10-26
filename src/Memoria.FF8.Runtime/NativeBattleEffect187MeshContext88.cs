using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect187MeshContext88
{
    public static NativeBattleDefaultActMeshStep DrawLoaded(NativeBattleEffectMemory memory, NativeBattleStaticMeshResource mesh, NativeBattleCameraProjection projection, NativeBattleEffectArena context88, NativeBattleSummonColors constants, byte depthShift, Func<int, NativeBattleEffectArena>? readPackedVertex = null, int tableBaseBucket = 17)
    {
        uint flags = BinaryPrimitives.ReadUInt32LittleEndian(context88.Read(0x1C, 4));
        if (BinaryPrimitives.ReadUInt32LittleEndian(context88.Read(0, 4)) != mesh.Address || flags is not (0x33 or 0xF3 or 0x2033 or 0x20F3) || depthShift > 31)
        {
            throw new NotSupportedException("Effect187 mesh requires its actual addressed context and reached flags.");
        }
        if ((flags & 0x2000) != 0 && readPackedVertex is null)
        {
            throw new NotSupportedException("Effect187 selected vertices require their actual physical owner.");
        }
        context88 = context88.Write(0x18, BitConverter.GetBytes(0u));
        return NativeBattleDefaultActMesh.Draw(memory, mesh.Bytes, mesh.Address, 0, context88, projection, constants, tableBaseBucket: tableBaseBucket, depthShift: depthShift, readPackedVertex: readPackedVertex);
    }
}
