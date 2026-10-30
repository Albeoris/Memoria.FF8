using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleOilBlast137MeshStep(NativeBattleEffectMemory Memory, NativeBattleEffectArena Context84, NativeBattleGfGeometryBatch Batch);

public static class NativeBattleOilBlast137Mesh
{
    public static NativeBattleOilBlast137MeshStep Draw(NativeBattleEffectMemory memory, NativeBattleStaticMeshResource source, NativeBattleEffectArena context84, NativeBattleCameraProjection projection, int tableBaseBucket = 17, byte depthShift = 2)
    {
        return DrawCore(memory, source, context84, projection, tableBaseBucket, depthShift, null);
    }

    public static NativeBattleOilBlast137MeshStep DrawPresentation(NativeBattleEffectMemory memory, NativeBattleStaticMeshResource source, NativeBattleEffectArena context84, NativeBattleCameraProjection projection, int tableBaseBucket = 17, byte depthShift = 2, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        return DrawCore(memory, source, context84, projection, tableBaseBucket, depthShift, pilotIdentity);
    }

    private static NativeBattleOilBlast137MeshStep DrawCore(NativeBattleEffectMemory memory, NativeBattleStaticMeshResource source, NativeBattleEffectArena context84, NativeBattleCameraProjection projection, int tableBaseBucket, byte depthShift, NativeBattlePilotVisualIdentity? pilotIdentity)
    {
        context84 = context84.Write(0x18, BitConverter.GetBytes(checked(source.Address + 8)));
        NativeBattleWindowedMeshStep drawn = NativeBattleWindowedMesh.Draw(memory, source, context84, projection, NativeBattleWindowedMeshLayout.OilBlast137, ReadVertex, tableBaseBucket, depthShift, pilotIdentity);
        return new(drawn.Memory, drawn.Context, drawn.Batch);

        (NativeBattleVector Vertex, short? W) ReadVertex(uint address)
        {
            long offset = (long)address - source.Address;
            if (source.Bytes.IsDefault || offset < 8 || offset > source.Bytes.Length - 8)
            {
                throw new InvalidDataException("Oil Blast vertex read leaves its original addressed source.");
            }
            ReadOnlySpan<byte> bytes = source.Bytes.AsSpan().Slice(checked((int)offset), 8);
            return (new(BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes[2..]), BinaryPrimitives.ReadInt16LittleEndian(bytes[4..])), BinaryPrimitives.ReadInt16LittleEndian(bytes[6..]));
        }
    }
}
