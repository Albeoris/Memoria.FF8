using System.Collections.Immutable;

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
        if (model.EquipmentPresentationRole == NativeBattleEquipmentPresentationRole.FreeHands || model.LinkedTransform is null)
        {
            return model with { LinkedContactOffset = null };
        }
        NativeBattleSequenceState state = model.SequenceState ?? throw new InvalidDataException("A transferred weapon contact requires its current body pose clock.");
        NativeBattleDecodedHandContact? contact = state.DecodedPose?.HandContact;
        if (contact is null)
        {
            if (body.RetargetBinding is null)
            {
                return model with { LinkedContactOffset = null };
            }
            if (state.DecodedPose is not null)
            {
                return model;
            }
            byte animation = state.Pose ?? throw new InvalidDataException("A transferred weapon contact requires its current body animation.");
            byte frame = state.PoseFrame114 ?? throw new InvalidDataException("A transferred weapon contact requires its current body frame.");
            contact = body.Sample(animation, frame, state.PoseFlags109).HandContact ?? throw new InvalidDataException("A transferred weapon contact lost its sampled pose provenance.");
        }
        ImmutableArray<NativeBattleActionMatrix> recipientBones = NativeBattleBodyBoneMatrices.Require(model, body);
        if ((uint)contact.RecipientHand >= recipientBones.Length)
        {
            throw new InvalidDataException("A transferred weapon contact requires its admitted recipient hand.");
        }
        NativeBattleVector sourceHand = contact.SourcePosition;
        NativeBattleActionMatrix targetHand = recipientBones[contact.RecipientHand];
        NativeBattleVector offset = new(unchecked((short)(targetHand.X - sourceHand.X)), unchecked((short)(targetHand.Y - sourceHand.Y)), unchecked((short)(targetHand.Z - sourceHand.Z)));
        return model with { LinkedContactOffset = offset };
    }
}
