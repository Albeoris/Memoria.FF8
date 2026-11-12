namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleEffectActorGeometry? ReadNativeForeignTarget(int index)
    {
        if (presentationForeignActors?.TryGetValue(index, out NativeBattleForeignActor? foreign) != true)
        {
            return null;
        }
        return foreign!.Geometry?.Anchors ?? throw new NotSupportedException("The host actor cannot supply posed skeleton bounds.");
    }

    private NativeBattleForeignModelGeometry? ReadNativeForeignModel(int index)
    {
        if (presentationForeignActors?.TryGetValue(index, out NativeBattleForeignActor? foreign) != true)
        {
            return null;
        }
        return foreign!.Geometry ?? throw new NotSupportedException("The host actor cannot supply posed triangles and their original materials.");
    }
}
