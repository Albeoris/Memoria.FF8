namespace Memoria.FF8.Runtime;

public sealed record NativeBattleMiniPulseCannon286MeshContext108Step(NativeBattleEffectMemory Memory, NativeBattleEffectArena Context108, NativeBattleGfGeometryBatch Batch);

public static class NativeBattleMiniPulseCannon286MeshContext108
{
    public static NativeBattleMiniPulseCannon286MeshContext108Step DrawLoaded(NativeBattleEffectMemory memory, NativeBattleStaticMeshResource mesh, NativeBattleCameraProjection projection, NativeBattleEffectArena context, NativeBattleSummonColors constants, int tableBaseBucket, byte depthShift, Func<int, (NativeBattleVector Vertex, short? W)> readSelectedVertex) => DrawLoaded(memory, mesh, projection, context, constants, tableBaseBucket, depthShift, readSelectedVertex, null);

    public static NativeBattleMiniPulseCannon286MeshContext108Step DrawLoaded(NativeBattleEffectMemory memory, NativeBattleStaticMeshResource mesh, NativeBattleCameraProjection projection, NativeBattleEffectArena context, NativeBattleSummonColors constants, int tableBaseBucket, byte depthShift, Func<int, (NativeBattleVector Vertex, short? W)> readSelectedVertex, NativeBattlePilotVisualIdentity? pilotIdentity)
    {
        NativeBattleWindowedMeshContext108Step result = NativeBattleWindowedMeshContext108.DrawLoaded(memory, mesh, projection, context, constants, tableBaseBucket, depthShift, 144, readSelectedVertex, pilotIdentity: pilotIdentity);
        return new(result.Memory, result.Context108, result.Batch);
    }
}
