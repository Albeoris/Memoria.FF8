namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect283MeshContext108Step(NativeBattleEffectMemory Memory, NativeBattleEffectArena Context108, NativeBattleGfGeometryBatch Batch);

public static class NativeBattleEffect283MeshContext108
{
    public static NativeBattleEffect283MeshContext108Step DrawLoaded(NativeBattleEffectMemory memory, NativeBattleStaticMeshResource mesh, NativeBattleCameraProjection projection, NativeBattleEffectArena context, NativeBattleSummonColors constants, int tableBaseBucket, byte depthShift, Func<int, (NativeBattleVector Vertex, short? W)> readSelectedVertex, Action<NativeBattleGfGeometryAttempt, int>? observeAttempt = null)
    {
        NativeBattleWindowedMeshContext108Step result = NativeBattleWindowedMeshContext108.DrawLoaded(memory, mesh, projection, context, constants, tableBaseBucket, depthShift, 96, readSelectedVertex, observeAttempt);
        return new(result.Memory, result.Context108, result.Batch);
    }
}
