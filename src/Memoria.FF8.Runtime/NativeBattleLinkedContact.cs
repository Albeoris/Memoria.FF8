namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleGraphicsModel
{
    public NativeBattleVector? LinkedContactOffset { get; init; }

    public NativeBattleLinkedModelTransform? ReadEffectiveLinkedTransform()
    {
        if (LinkedTransform is not { } transform || LinkedContactOffset is not { } delta)
        {
            return LinkedTransform;
        }
        NativeBattleVector position = transform.Position;
        return transform with { Position = new(unchecked((short)(position.X + delta.X)), unchecked((short)(position.Y + delta.Y)), unchecked((short)(position.Z + delta.Z))) };
    }
}

public static class NativeBattleLinkedContact
{
    public static NativeBattleGraphicsModel Publish(NativeBattleGraphicsModel model, NativeBattlePoseSource body)
    {
        if (model.LinkedTransform is null || body.RetargetBinding is not { } binding)
        {
            return model with { LinkedContactOffset = null };
        }
        NativeBattleSequenceState state = model.SequenceState ?? throw new InvalidDataException("A transferred weapon contact requires its current body pose clock.");
        byte animation = state.Pose ?? throw new InvalidDataException("A transferred weapon contact requires its current body animation.");
        byte frame = state.PoseFrame114 ?? throw new InvalidDataException("A transferred weapon contact requires its current body frame.");
        System.Collections.Immutable.ImmutableArray<NativeBattleActionMatrix> recipientBones = NativeBattleBodyBoneMatrices.Require(model, body);
        if ((uint)binding.SourceHand >= binding.SourceMotion.BoneCount || (uint)binding.RecipientHand >= recipientBones.Length)
        {
            throw new InvalidDataException("A transferred weapon contact requires its admitted semantic hands.");
        }
        NativeBattleVector sourceHand;
        if ((state.PoseFlags109 & 1) == 0 && !binding.SourceHandFrames.IsDefaultOrEmpty)
        {
            if (animation >= binding.SourceHandFrames.Length || frame == 0 || frame > binding.SourceHandFrames[animation].Length)
            {
                throw new InvalidDataException("A transferred weapon contact frame exceeds its source animation.");
            }
            sourceHand = binding.SourceHandFrames[animation][frame - 1];
        }
        else
        {
            NativeBattleDecodedPose donor = binding.SourceMotion.Sample(animation, frame, state.PoseFlags109);
            NativeBattleActionMatrix hand = NativeBattlePoseMatrices.StageBoneMatrices(binding.SourceMotion, donor)[binding.SourceHand];
            sourceHand = new(checked((short)hand.X), checked((short)hand.Y), checked((short)hand.Z));
        }
        NativeBattleActionMatrix targetHand = recipientBones[binding.RecipientHand];
        NativeBattleVector offset = new(checked((short)(targetHand.X - sourceHand.X)), checked((short)(targetHand.Y - sourceHand.Y)), checked((short)(targetHand.Z - sourceHand.Z)));
        return model with { LinkedContactOffset = offset };
    }
}
