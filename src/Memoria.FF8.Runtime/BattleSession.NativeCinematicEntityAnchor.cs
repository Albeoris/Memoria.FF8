using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleCinematicEntityAnchor ReadNativeCinematicEntityAnchor(NativeBattleWaterExecutionFrame frame, NativeBattleGraphicsState graphics, BattleModelOwner actorIndex, short selector)
    {
        if (ReadNativeForeignPose(graphics, actorIndex) is { } pose)
        {
            NativeBattleWaterActorAnchorResult foreign = CreateNativeActorAnchorServices().Evaluate(frame, pose, selector, 4096, pose.World);
            return new(foreign.Frame, foreign.Anchor);
        }
        NativeBattleGraphicsModel actor = graphics.ActorModels.Get(actorIndex);
        NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, actorIndex).PoseSource ?? throw new NotSupportedException("Native cinematic entity anchor requires its actual current rig.");
        NativeBattleActionMatrix basis = actor.GeometryWorldMatrix ?? throw new NotSupportedException("Native cinematic entity anchor requires its current entity matrix.");
        NativeBattleWaterActorAnchorResult result = CreateNativeActorAnchorServices().EvaluateB66B80(frame, source.Skeleton, NativeBattleBodyBoneMatrices.Require(actor, source), selector, 4096, basis);
        return new(result.Frame, result.Anchor);
    }
}
