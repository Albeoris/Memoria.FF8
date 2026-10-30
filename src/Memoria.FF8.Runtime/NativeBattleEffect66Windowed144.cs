using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect66Windowed144Step(NativeBattleEffectMemory Memory, NativeBattleEffectArena Context144, NativeBattleGfGeometryBatch Batch);

public static class NativeBattleEffect66Windowed144
{
    public static NativeBattleEffect66Windowed144Step DrawLoaded(NativeBattleEffectMemory memory, NativeBattleStaticMeshResource mesh, NativeBattleCameraProjection projection, NativeBattleEffectArena context, NativeBattleSummonColors colors, int tableBaseBucket = 17, byte depthShift = 2, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        if (mesh.Address != 0xDE97A4 || mesh.Bytes.IsDefaultOrEmpty || mesh.Bytes.Length < 8 || BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan(4, 4)) != 14)
        {
            throw new InvalidDataException("Effect66 windowed drawing requires its selected physical source.");
        }
        NativeBattleWindowedMesh144Step step = NativeBattleWindowedMesh144.DrawLoaded(memory, mesh, projection, context, colors, tableBaseBucket, depthShift, pilotIdentity: pilotIdentity);
        return new(step.Memory, step.Context144, step.Batch);
    }
}
