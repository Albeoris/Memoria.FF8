using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEvilEye282MeshContext88Step(NativeBattleEffectMemory Memory, NativeBattleEffectArena Context88, NativeBattleGfGeometryBatch Batch);

public static class NativeBattleEvilEye282MeshContext88
{
    public static NativeBattleEvilEye282MeshContext88Step DrawLoaded(NativeBattleEffectMemory memory, NativeBattleStaticMeshResource mesh, NativeBattleCameraProjection projection, NativeBattleEffectArena context, NativeBattleSummonColors constants, int tableBaseBucket, byte depthShift, Func<int, (NativeBattleVector Vertex, short? W)> readSelectedVertex)
    {
        return DrawLoaded(memory, mesh, projection, context, constants, tableBaseBucket, depthShift, readSelectedVertex, null);
    }

    public static NativeBattleEvilEye282MeshContext88Step DrawLoaded(NativeBattleEffectMemory memory, NativeBattleStaticMeshResource mesh, NativeBattleCameraProjection projection, NativeBattleEffectArena context, NativeBattleSummonColors constants, int tableBaseBucket, byte depthShift, Func<int, (NativeBattleVector Vertex, short? W)> readSelectedVertex, NativeBattlePilotVisualIdentity? pilotIdentity)
    {
        uint flags = BinaryPrimitives.ReadUInt32LittleEndian(context.Read(0x1C, 4));
        if (BinaryPrimitives.ReadUInt32LittleEndian(context.Read(0, 4)) != mesh.Address || flags is not (0x2030 or 0x20F0) || depthShift > 31)
        {
            throw new InvalidDataException("Evil-Eye mesh requires its actual addressed caller context.");
        }
        int cursor = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(mesh.Bytes.AsSpan()));
        int[] lengths = [12, 12, 20, 24, 20, 24, 28, 36];
        for (int group = 0; group < 8; group++)
        {
            int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(cursor, 4));
            if (count < 0 || count != 0 && group is not (4 or 5 or 6 or 7))
            {
                throw new NotSupportedException("Evil-Eye programme requires its selected G3, G4, GT3 or GT4 group.");
            }
            cursor = checked(cursor + 4 + count * lengths[group]);
            if (cursor > mesh.Bytes.Length)
            {
                throw new InvalidDataException("Evil-Eye mesh directory leaves its current source.");
            }
        }
        (NativeBattleVector Vertex, short? W) current = default;
        int currentOffset = -1;
        NativeBattleVector ReadVertex(int offset)
        {
            current = readSelectedVertex(offset);
            currentOffset = offset;
            return current.Vertex;
        }
        short? ReadW(int offset)
        {
            if (offset != currentOffset)
            {
                throw new InvalidDataException("Evil-Eye packed vertex requires its matching current XYZ read.");
            }
            return current.W;
        }
        NativeBattleDefaultActMeshStep drawn = NativeBattleDefaultActMesh.Draw(memory, mesh.Bytes, mesh.Address, 0, context, projection, constants, ReadVertex, ReadW, tableBaseBucket, depthShift, pilotIdentity: pilotIdentity);
        return new(drawn.Memory, drawn.Context88, drawn.Batch);
    }
}
