namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleEffectActorGeometry? ReadNativeForeignTarget(int index)
    {
        if (presentationForeignActors?.TryGetValue(index, out NativeBattleForeignActor? foreign) != true)
        {
            return null;
        }
        NativeBattleEffectActorGeometry anchors = foreign!.Geometry?.Anchors ?? throw new NotSupportedException("The host actor cannot supply posed skeleton bounds.");
        NativeBattleActionMatrix delta = NativeBattleForeignPlacement.Delta(foreign, NativeGraphics!.Models[index]);
        return anchors with { Anchor240 = NativeBattleGfMatrices.Transform460840(delta, anchors.Anchor240), Anchor241 = NativeBattleGfMatrices.Transform460840(delta, anchors.Anchor241) };
    }

    private NativeBattleForeignModelGeometry? ReadNativeForeignModel(int index)
    {
        if (presentationForeignActors?.TryGetValue(index, out NativeBattleForeignActor? foreign) != true)
        {
            return null;
        }
        NativeBattleForeignModelGeometry geometry = foreign!.Geometry ?? throw new NotSupportedException("The host actor cannot supply posed triangles and their original materials.");
        return geometry with { World = NativeBattleGfMatrices.Compose56C2F0(ReadEffectPresentationForeignPlacement(index), geometry.World) };
    }

    internal NativeBattleActionMatrix ReadEffectPresentationForeignPlacement(int index)
    {
        NativeBattleForeignActor actor = presentationForeignActors?[index] ?? throw new InvalidOperationException("The selected actor is not owned by the host.");
        return NativeBattleForeignPlacement.Delta(actor, NativeGraphics!.Models[index]);
    }
}
