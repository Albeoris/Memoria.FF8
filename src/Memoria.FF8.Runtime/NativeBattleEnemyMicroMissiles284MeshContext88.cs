using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEnemyMicroMissiles284MeshContext88Step(NativeBattleEffectMemory Memory, NativeBattleEffectArena Context88, NativeBattleGfGeometryBatch Batch);

public static class NativeBattleEnemyMicroMissiles284MeshContext88
{
    public static NativeBattleEnemyMicroMissiles284MeshContext88Step DrawLoaded(NativeBattleEffectMemory memory, NativeBattleStaticMeshResource mesh, NativeBattleCameraProjection projection, NativeBattleEffectArena context, NativeBattleSummonColors constants, int tableBaseBucket, byte depthShift, Func<int, (NativeBattleVector Vertex, short? W)> readSelectedVertex, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        uint flags = BinaryPrimitives.ReadUInt32LittleEndian(context.Read(0x1C, 4));
        if (BinaryPrimitives.ReadUInt32LittleEndian(context.Read(0, 4)) != mesh.Address || flags is not (0x2030 or 0x20F0) || depthShift > 31)
        {
            throw new InvalidDataException("Enemy Micro Missiles mesh requires its actual addressed caller context.");
        }
        int vertices = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(4));
        if (vertices is < 1 or > 64)
        {
            throw new InvalidDataException("Enemy Micro Missiles geometry leaves its current selected-row extent.");
        }
        int cursor = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(mesh.Bytes.AsSpan()));
        int[] lengths = [12, 12, 20, 24, 20, 24, 28, 36];
        for (int group = 0; group < 8; group++)
        {
            int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(cursor, 4));
            if (count < 0 || count != 0 && group is not (0 or 1 or 2 or 3 or 4 or 5 or 7))
            {
                throw new NotSupportedException("Enemy Micro Missiles programme requires its selected F3, F4, FT3, FT4, G3, G4 or GT4 group.");
            }
            cursor = checked(cursor + 4 + count * lengths[group]);
            if (cursor > mesh.Bytes.Length)
            {
                throw new InvalidDataException("Enemy Micro Missiles mesh directory leaves its current source.");
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
                throw new InvalidDataException("Enemy Micro Missiles packed vertex requires its matching current XYZ read.");
            }
            return current.W;
        }
        NativeBattleDefaultActMeshStep drawn = NativeBattleDefaultActMesh.Draw(memory, mesh.Bytes, mesh.Address, 0, context, projection, constants, ReadVertex, ReadW, tableBaseBucket, depthShift, pilotIdentity: pilotIdentity);
        return new(drawn.Memory, drawn.Context88, drawn.Batch);
    }
}
