using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleAcid317ActMeshStep(NativeBattleGfGeometryBatch Batch, NativeBattleEffectArena Temporary88, NativeBattleEffectArena Arena, NativeBattleGeometryRegisterStep Registers, NativeBattleModelRendererRegisters FarControls);

public static class NativeBattleAcid317ActMesh
{
    public static bool Visible(NativeBattleGfAnimationNode node) => NativeBattleBillboardActContext12.Visible(node);

    public static NativeBattleAcid317ActMeshStep Draw(NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, NativeBattleEffectArena context12, NativeBattlePackedMatrix32 camera, NativeBattlePackedMatrix32 normalizedView, NativeBattleEffectArena temporary88, NativeBattleEffectArena arena, NativeBattleGeometryRegisterStep registers, NativeBattleModelRendererRegisters farControls, NativeBattleCameraProjection projection, short dqa, int dqb, short? zsf3, short? zsf4, NativeBattleSummonColors constants, short? localVectorW = null)
    {
        NativeBattleBillboardActContext12Step step = NativeBattleBillboardActContext12.Draw(source, node, context12, camera, normalizedView, temporary88, arena, registers, farControls, projection, dqa, dqb, zsf3, zsf4, constants, NativeBattleAcid317Profile.ArenaAddress + NativeBattleAcid317Profile.MorphOffset, NativeBattleAcid317Profile.MorphBytes, localVectorW);
        return new(step.Batch, step.Temporary88, step.Arena, step.Registers, step.FarControls);
    }
}
