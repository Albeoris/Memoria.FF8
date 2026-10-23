namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEnemyMicroMissiles284Controller
{
    public static NativeBattleEnemyMicroMissiles284PointStep ReadAnchor67E550(NativeBattleEnemyMicroMissiles284Frame frame, uint model, int selector, NativeBattleEffectArena retainedPoint8, NativeBattleEnemyMicroMissiles284Inputs inputs)
    {
        (frame, uint temporary) = Allocate(frame, 20);
        NativeBattleBoneAnchor67E550Step result = NativeBattleBoneAnchor67E550.Query(selector, Context(frame, temporary, 20), retainedPoint8, value => inputs.ReadBoneAlias(frame, model, value), bone => inputs.ReadBoneParent(frame, model, bone), bone => inputs.ReadCurrentBoneMatrix(frame, model, bone), () => inputs.ReadModelWorld(frame, model));
        if (result.Result == 0)
        {
            frame = PublishContext(frame, temporary, result.Temporary20, 20);
        }
        return new(Release(frame, 20), result.Point8, result.Result);
    }
}
