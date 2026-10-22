namespace Memoria.FF8.Runtime;

public sealed record NativeBattleAnchorDepthAct16Step(NativeBattleEffectMemory Memory, NativeBattleEffectArena Temporary88, NativeBattleGfGeometryBatch Batch);

public static class NativeBattleAnchorDepthAct16
{
    public static bool Visible(NativeBattleGfAnimationNode node) => NativeBattleActMeshRendering.Visible(node);

    public static NativeBattleAnchorDepthAct16Step Draw(NativeBattleEffectMemory memory, NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, NativeBattleEffectArena context16, NativeBattlePackedMatrix32 camera, NativeBattleEffectArena temporary88, NativeBattleCameraProjection projection, NativeBattleSummonColors constants, uint morphAddress, int morphLength, short? localVectorW = null)
    {
        NativeBattleEffectArena arena = memory.Arena ?? throw new NotSupportedException("Anchor ACT requires its current shared morph arena.");
        NativeBattleGeometryRegisterStep registers = new(memory.GeometryRegisters ?? NativeBattleGeometryRegisters.Unknown(), memory.ScriptedColorRegisters ?? NativeBattleScriptedColorRegisters.Unknown());
        NativeBattleAnchorActPreparationStep prepared = NativeBattleAnchorActPreparation.Prepare(source, node, context16, camera, temporary88, arena, morphAddress, morphLength, registers, constants, NativeBattleAnchorActContextPolicy.AnchorDepth16, localVectorW);
        memory = memory with { Arena = prepared.Arena, GeometryRegisters = prepared.Registers.Geometry, ScriptedColorRegisters = prepared.Registers.Colors };
        NativeBattleDefaultActMeshStep drawn = NativeBattleDefaultActMesh.Draw(memory, source, prepared.GeometryOffset, prepared.Temporary88, projection, constants, offset => prepared.ReadVertex(offset).Vertex, offset => prepared.ReadVertex(offset).W);
        return new(drawn.Memory, drawn.Context88, drawn.Batch);
    }
}
