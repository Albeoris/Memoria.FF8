namespace Memoria.FF8.Runtime;

public static class NativeBattlePandemona291ActorBones
{
    public static NativeBattleGraphicsModel Restore508C90(NativeBattleGraphicsModel model, NativeBattlePoseSource source)
    {
        source.Validate();
        NativeBattleSequenceState sequence = model.SequenceState ?? throw new NotSupportedException("Native Pandemona requires its current model sequence owner.");
        if (sequence.DecodedPose is not null || sequence.Pose is not null)
        {
            return NativeBattleBodyBoneMatrices.Publish508C90(model, source);
        }

        //508C90 consumes current skeleton fields even before an animation has been selected.
        NativeBattleEffectArena skeleton = NativeBattleEffectArena.Clear().Write(0, source.Skeleton.AsSpan());
        if (!source.SkeletonKnownBytes.IsDefaultOrEmpty)
        {
            for (int offset = 0; offset < source.SkeletonKnownBytes.Length; offset++)
            {
                if (!source.SkeletonKnownBytes[offset])
                {
                    skeleton = skeleton.MarkUnknown(offset, 1);
                }
            }
        }
        NativeBattleDecodedPose current = NativeBattleArenaPose.ReadCurrentPose(skeleton, 0, source.BoneCount);
        NativeBattleBodyBoneMatrices publication = new(NativeBattleBodyBoneMatrices.Identity(source), NativeBattlePoseMatrices.StageBoneMatrices(source, current));
        publication.Validate();
        return model with { CurrentBodyBoneMatrices = publication };
    }
}
