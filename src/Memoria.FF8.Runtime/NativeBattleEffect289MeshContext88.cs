using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect289MeshContext88Step(NativeBattleEffectMemory Memory, NativeBattleEffectArena Context88, NativeBattleGfGeometryBatch Batch);

public static class NativeBattleEffect289MeshContext88
{
    public static NativeBattleEffect289MeshContext88Step DrawLoaded(NativeBattleEffectMemory memory, NativeBattleStaticMeshResource mesh, NativeBattleCameraProjection projection, NativeBattleEffectArena context88, NativeBattleSummonColors constants, Func<int, (NativeBattleVector Vertex, short? W)> readSelectedVertex, int tableBaseBucket = 17, byte depthShift = 2)
    {
        uint flags = BinaryPrimitives.ReadUInt32LittleEndian(context88.Read(0x1C, 4));
        int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(4));
        if (BinaryPrimitives.ReadUInt32LittleEndian(context88.Read(0, 4)) != mesh.Address || flags is not (0x2030 or 0x20F0) || count is < 1 or > 68 || depthShift > 31)
        {
            throw new InvalidDataException("Effect289 programme requires its actual selected context and vertex extent.");
        }
        int cursor = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan());
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        for (int group = 0; group < 8; group++)
        {
            int faces = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(cursor));
            if (faces < 0 || faces != 0 && group is not (4 or 5 or 7))
            {
                throw new NotSupportedException("Effect289 programme requires its selected Gouraud polygon groups.");
            }
            cursor = checked(cursor + 4 + faces * strides[group]);
            if (cursor > mesh.Bytes.Length)
            {
                throw new InvalidDataException("Effect289 programme polygons leave their original source.");
            }
        }
        (NativeBattleVector Vertex, short? W) current = default;
        int currentOffset = -1;
        NativeBattleVector ReadVertex(int offset)
        {
            if (offset < 0 || (offset & 7) != 0 || offset >= count * 8)
            {
                throw new InvalidDataException("Effect289 programme vertex leaves its current selected rows.");
            }
            currentOffset = offset;
            current = readSelectedVertex(offset);
            return current.Vertex;
        }
        short? ReadW(int offset)
        {
            if (offset != currentOffset)
            {
                throw new InvalidDataException("Effect289 programme requires matching current XYZ and W reads.");
            }
            return current.W;
        }
        NativeBattleDefaultActMeshStep drawn = NativeBattleDefaultActMesh.Draw(memory, mesh.Bytes, mesh.Address, 0, context88, projection, constants, ReadVertex, ReadW, tableBaseBucket, depthShift);
        return new(drawn.Memory, drawn.Context88, drawn.Batch);
    }
}
