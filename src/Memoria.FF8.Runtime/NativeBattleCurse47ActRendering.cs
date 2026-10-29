namespace Memoria.FF8.Runtime;

public static partial class NativeBattleCurse47ActRendering
{
    public const uint MorphAddress = 0x264E778;
    public const int MorphLength = 840;
    private static readonly NativeBattleActMeshRules Rules = new(MorphAddress, MorphLength, false, false);

    public static bool Visible(NativeBattleGfAnimationNode node) => NativeBattleActMeshRendering.Visible(node);

    public static NativeBattleActMeshDraw DrawNode(NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, NativeBattleActMeshContext root, NativeBattleEffectArena temporary, NativeBattleEffectArena morph, NativeBattleGeometryRegisterStep registers, NativeBattleModelRendererRegisters farControls, NativeBattleCameraProjection projection, short dqa, int dqb, short? zsf3, short? zsf4, NativeBattleSummonColors constants, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        return NativeBattleActMeshRendering.DrawNode(source, node, root, temporary, morph, registers, farControls, projection, dqa, dqb, zsf3, zsf4, constants, Rules, pilotIdentity: pilotIdentity);
    }
}
