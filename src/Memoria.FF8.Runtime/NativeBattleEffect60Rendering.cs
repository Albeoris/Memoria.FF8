namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect60Rendering
{
    private static readonly NativeBattleActMeshRules Rules = new(0x2606C78, 968, false, false);

    public static NativeBattleActMeshContext PrepareAct(NativeBattleModulePoolTask task, NativeBattleActionMatrix camera) => NativeBattleActMeshRendering.PrepareAct(task, camera);
    public static bool Visible(NativeBattleGfAnimationNode node) => NativeBattleActMeshRendering.Visible(node);

    public static NativeBattleActMeshDraw DrawNode(NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, NativeBattleActMeshContext root, NativeBattleEffectArena temporary, NativeBattleEffectArena morph, NativeBattleGeometryRegisterStep registers, NativeBattleModelRendererRegisters farControls, NativeBattleCameraProjection projection, short dqa, int dqb, short? zsf3, short? zsf4, NativeBattleSummonColors constants)
    {
        return NativeBattleActMeshRendering.DrawNode(source, node, root, temporary, morph, registers, farControls, projection, dqa, dqb, zsf3, zsf4, constants, Rules);
    }
}
