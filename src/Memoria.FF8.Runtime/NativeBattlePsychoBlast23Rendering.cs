using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattlePsychoBlast23ActContext(NativeBattleActionMatrix Matrix, short DepthBias);
public sealed record NativeBattlePsychoBlast23NodeDraw(NativeBattleGfGeometryBatch Batch, NativeBattleEffectArena Temporary104, NativeBattleEffectArena MorphScratch, NativeBattleGeometryRegisterStep Registers, NativeBattleModelRendererRegisters FarControls);

public static class NativeBattlePsychoBlast23Rendering
{
    private static readonly NativeBattleActMeshRules Rules = new(NativeBattlePsychoBlast23Profile.MorphScratchAddress, NativeBattlePsychoBlast23Profile.MorphScratchLength, true, true);

    public static NativeBattlePsychoBlast23ActContext PrepareAct(NativeBattleModulePoolTask task, NativeBattleActionMatrix camera)
    {
        NativeBattleActMeshContext prepared = NativeBattleActMeshRendering.PrepareAct(task, camera);
        return new(prepared.Matrix, prepared.DepthBias);
    }

    public static bool Visible(NativeBattleGfAnimationNode node) => NativeBattleActMeshRendering.Visible(node);

    public static NativeBattlePsychoBlast23NodeDraw DrawNode(NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, NativeBattlePsychoBlast23ActContext root, NativeBattleEffectArena temporary, NativeBattleEffectArena morph, NativeBattleGeometryRegisterStep registers, NativeBattleModelRendererRegisters farControls, NativeBattleCameraProjection projection, short dqa, int dqb, short? zsf3, short? zsf4, NativeBattleSummonColors constants)
    {
        NativeBattleActMeshDraw drawn = NativeBattleActMeshRendering.DrawNode(source, node, new(root.Matrix, root.DepthBias), temporary, morph, registers, farControls, projection, dqa, dqb, zsf3, zsf4, constants, Rules);
        return new(drawn.Batch, drawn.Temporary104, drawn.MorphScratch, drawn.Registers, drawn.FarControls);
    }
}
