using System.Collections.Immutable;
using System.Numerics;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattlePosedSemanticContact(long Callback, uint? AnimationDirectory, NativeBattleDecodedSemanticContact Pose, Vector3 WorldDelta);

public static class NativeBattleCinematicPropContact
{
    public static bool OwnsPairedTrack(NativeBattlePosedSemanticContact? contact, long callback, byte propAnimation, byte propFrame, byte propFrames)
    {
        return contact is { AnimationDirectory: 0x187C20C, Pose.Animation: 0 } && contact.Callback == callback && (contact.Pose.Flags & 1) == 0 && propAnimation == 0 && contact.Pose.Frame == propFrame && contact.Pose.Frames == propFrames;
    }

    public static ImmutableArray<NativeBattleActionMatrix> TranslateOriginal(ImmutableArray<NativeBattleActionMatrix> original, Vector3 worldDelta, NativeBattleCameraProjection projection)
    {
        NativeBattleActionMatrix view = projection.View ?? throw new InvalidDataException("A paired cinematic prop requires its actual draw camera basis.");
        var rotation = view.Rotation;
        int x = NativePrecision53.RoundInteger((rotation[0] * (double)worldDelta.X + rotation[1] * (double)worldDelta.Y + rotation[2] * (double)worldDelta.Z) / 4096);
        int y = NativePrecision53.RoundInteger((rotation[3] * (double)worldDelta.X + rotation[4] * (double)worldDelta.Y + rotation[5] * (double)worldDelta.Z) / 4096);
        int z = NativePrecision53.RoundInteger((rotation[6] * (double)worldDelta.X + rotation[7] * (double)worldDelta.Y + rotation[8] * (double)worldDelta.Z) / 4096);
        return original.Select(matrix => matrix with { X = checked(matrix.X + x), Y = checked(matrix.Y + y), Z = checked(matrix.Z + z) }).ToImmutableArray();
    }
}

public static partial class NativeBattlePoseMatrices
{
    internal static NativeBattlePosedSemanticContact? CaptureHandAContact502D40(NativeBattleGraphicsModel model, NativeBattlePoseSource source, NativeBattleCameraProjection projection, NativeBattleVector? actualCameraEye, long callback)
    {
        if (model.SequenceState?.DecodedPose?.HandAContact is not { } contact)
        {
            return null;
        }
        ImmutableArray<NativeBattleActionMatrix> bones = NativeBattleBodyBoneMatrices.Require(model, source);
        if ((uint)contact.RecipientBone >= bones.Length)
        {
            throw new InvalidDataException("A posed semantic contact leaves its actual recipient skeleton.");
        }
        NativeBattleActionMatrix basis = GeometryBasis502D40(model, projection.View ?? throw new InvalidDataException("A body contact requires its actual draw view."), actualCameraEye);
        NativeBattleFullMacVertex original = NativeBattleGfMatrices.TransformMac460840(basis, contact.SourcePosition);
        NativeBattleActionMatrix target = bones[contact.RecipientBone];
        Vector3 cameraDelta = new(target.X - original.X, target.Y - original.Y, target.Z - original.Z);
        return new(callback, model.ActiveAnimationDirectory, contact, NativeBattlePilotVisualGeometry.CameraToWorldDirection(cameraDelta, projection));
    }
}
