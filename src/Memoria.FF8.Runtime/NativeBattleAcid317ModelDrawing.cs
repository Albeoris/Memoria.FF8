using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleAcid317ModelStep(NativeBattleEffectMemory Memory, NativeBattleEffectArena Temporary132, NativeBattleEffectArena VertexScratch, NativeBattleGfGeometryBatch Batch);

public static class NativeBattleAcid317ModelDrawing
{
    public static NativeBattleAcid317ModelStep DrawBody(NativeBattleEffectMemory memory, NativeBattleEffectArena temporary132, NativeBattleEffectArena vertexScratch, Func<int, int, byte[]> readGeometry, uint geometryAddress, ImmutableArray<NativeBattlePackedMatrix32> bones, NativeBattlePackedMatrix32 camera, NativeBattleCameraProjection projection, NativeBattleScriptedGraphicsMemory controls, uint scratchAddress, int scratchLength, uint cacheAddress, int cacheLength, int tableBaseBucket = 17, byte depthShift = 0)
    {
        NativeBattleDeformedBodyStep step = NativeBattleDeformedBodyDrawing.DrawBody(memory, temporary132, vertexScratch, readGeometry, geometryAddress, bones, camera, projection, controls, scratchAddress, scratchLength, cacheAddress, cacheLength, tableBaseBucket, depthShift);
        return new(step.Memory, step.Temporary132, step.VertexScratch, step.Batch);
    }
}
