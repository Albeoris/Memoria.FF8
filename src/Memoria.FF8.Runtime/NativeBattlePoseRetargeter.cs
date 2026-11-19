using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Numerics;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleSemanticBonePair(string Joint, int Source, int Recipient)
{
    public Matrix4x4 RecipientBindAxes { get; init; } = Matrix4x4.Identity;
    public Matrix4x4 SourceBindAxes { get; init; } = Matrix4x4.Identity;
}

public sealed record NativeBattlePoseRetargetBinding(NativeBattlePoseSource SourceMotion, int SourceHand, int RecipientHand)
{
    public ImmutableArray<ImmutableArray<NativeBattleVector>> SourceHandFrames { get; init; } = [];
}

/// <summary>Compiles calibrated semantic motion into recipient-rig native animation bytes before battle execution.</summary>
public sealed class NativeBattlePoseRetargeter
{
    private readonly NativeBattlePoseSource source;
    private readonly NativeBattlePoseSource recipient;
    private readonly NativeBattleDecodedPose recipientRest;
    private readonly IReadOnlyDictionary<int, NativeBattleSemanticBonePair> bindings;
    private readonly float rootScale;
    private readonly NativeBattleVector sourceOrigin;

    public NativeBattlePoseRetargeter(NativeBattlePoseSource sourceNeutral, NativeBattlePoseSource recipientNeutral, IReadOnlyList<NativeBattleSemanticBonePair> pairs, float rootMotionScale)
    {
        sourceNeutral.Validate();
        recipientNeutral.Validate();
        source = sourceNeutral;
        recipient = recipientNeutral;
        NativeBattleDecodedPose sourcePose = source.Sample(0, 1, 0);
        sourceOrigin = sourcePose.Translation;
        recipientRest = recipient.Sample(0, 1, 0);
        if (pairs.Count == 0 || pairs.Select(pair => pair.Joint).Distinct(StringComparer.Ordinal).Count() != pairs.Count || pairs.Select(pair => pair.Recipient).Distinct().Count() != pairs.Count || pairs.Select(pair => pair.Source).Distinct().Count() != pairs.Count || pairs.Any(pair => string.IsNullOrWhiteSpace(pair.Joint) || (uint)pair.Source >= source.BoneCount || (uint)pair.Recipient >= recipient.BoneCount))
        {
            throw new InvalidDataException("Semantic retargeting requires unique, named, valid source and recipient joints.");
        }
        bindings = pairs.ToDictionary(pair => pair.Recipient);
        if (!bindings.TryGetValue(0, out NativeBattleSemanticBonePair? root) || root.Source != 0)
        {
            throw new InvalidDataException("Semantic retargeting requires the actual root-to-root binding.");
        }
        if (!float.IsFinite(rootMotionScale) || rootMotionScale <= 0)
        {
            throw new InvalidDataException("A semantic profile requires a finite positive calibrated root-motion scale.");
        }
        rootScale = rootMotionScale;
        foreach (NativeBattleSemanticBonePair pair in pairs)
        {
            ValidateAxes(pair.SourceBindAxes);
            ValidateAxes(pair.RecipientBindAxes);
        }
    }

    public NativeBattlePoseSource Compile(NativeBattlePoseSource motion)
    {
        motion.Validate();
        if (!motion.Skeleton.AsSpan().SequenceEqual(source.Skeleton.AsSpan()))
        {
            throw new InvalidDataException("The motion bank must be bound to the calibrated source skeleton.");
        }
        ImmutableArray<ImmutableArray<NativeBattleDecodedPose>> decoded = NativeBattlePoseDecoder.DecodeFrames(motion.Animations.AsSpan(), motion.BoneCount);
        ImmutableArray<NativeBattleDecodedPose>[] converted = decoded.Select(animation => animation.Select(Transfer).ToImmutableArray()).ToArray();
        byte[] aliases = Enumerable.Repeat(byte.MaxValue, source.BoneCount).ToArray();
        foreach (NativeBattleSemanticBonePair pair in bindings.Values)
        {
            aliases[pair.Source] = checked((byte)pair.Recipient);
        }
        NativeBattleSemanticBonePair? primary = bindings.Values.SingleOrDefault(pair => pair.Joint == "hand-a");
        NativeBattlePoseRetargetBinding? contact = primary is null ? null : new(motion, primary.Source, primary.Recipient)
        {
            SourceHandFrames = decoded.Select(animation => animation.Select(pose =>
            {
                NativeBattleActionMatrix hand = NativeBattlePoseMatrices.StageBoneMatrices(motion, pose)[primary.Source];
                return new NativeBattleVector(checked((short)hand.X), checked((short)hand.Y), checked((short)hand.Z));
            }).ToImmutableArray()).ToImmutableArray()
        };
        return recipient with { Animations = NativeBattleAnimationEncoder.Encode(converted), ActionBoneAliases = aliases.ToImmutableArray(), RetargetBinding = contact };
    }

    public NativeBattleDecodedPose Transfer(NativeBattleDecodedPose pose)
    {
        if (pose.Bones.Length != source.BoneCount)
        {
            throw new InvalidDataException("The motion pose does not match its calibrated source skeleton.");
        }
        Matrix4x4[] sourceCurrent = GlobalRotations(source, pose);
        Matrix4x4[] targetCurrent = new Matrix4x4[recipient.BoneCount];
        ImmutableArray<NativeBattleBonePose>.Builder bones = ImmutableArray.CreateBuilder<NativeBattleBonePose>(recipient.BoneCount);
        for (int bone = 0; bone < recipient.BoneCount; bone++)
        {
            int parent = Parent(recipient, bone);
            Matrix4x4 parentCurrent = parent < 0 ? Matrix4x4.Identity : targetCurrent[parent];
            Matrix4x4 inverseParent = Matrix4x4.Transpose(parentCurrent);
            Matrix4x4 local = LocalRotation(recipientRest.Bones[bone].Rotation);
            if (bindings.TryGetValue(bone, out NativeBattleSemanticBonePair? pair))
            {
                local = pair.RecipientBindAxes * Matrix4x4.Transpose(pair.SourceBindAxes) * sourceCurrent[pair.Source] * inverseParent;
            }
            targetCurrent[bone] = local * parentCurrent;
            bones.Add(new(Angles(local), recipientRest.Bones[bone].Scale));
        }
        short Coordinate(short position, short origin, short target) => checked((short)Math.Clamp(target + MathF.Round((position - origin) * rootScale), short.MinValue, short.MaxValue));
        NativeBattleVector translation = new(Coordinate(pose.Translation.X, sourceOrigin.X, recipientRest.Translation.X), Coordinate(pose.Translation.Y, sourceOrigin.Y, recipientRest.Translation.Y), Coordinate(pose.Translation.Z, sourceOrigin.Z, recipientRest.Translation.Z));
        return new(translation, bones.MoveToImmutable(), recipientRest.HasScale);
    }

    private static Matrix4x4[] GlobalRotations(NativeBattlePoseSource rig, NativeBattleDecodedPose pose)
    {
        Matrix4x4[] result = new Matrix4x4[rig.BoneCount];
        for (int bone = 0; bone < result.Length; bone++)
        {
            int parent = Parent(rig, bone);
            result[bone] = LocalRotation(pose.Bones[bone].Rotation) * (parent < 0 ? Matrix4x4.Identity : result[parent]);
        }
        return result;
    }

    private static Matrix4x4 LocalRotation(NativeBattleVector angles)
    {
        const float radians = MathF.Tau / 4096;
        return Matrix4x4.CreateRotationZ(angles.Z * radians) * Matrix4x4.CreateRotationX(angles.X * radians) * Matrix4x4.CreateRotationY(angles.Y * radians);
    }

    private static NativeBattleVector Angles(Matrix4x4 row)
    {
        float x = MathF.Asin(Math.Clamp(-row.M32, -1, 1));
        float cosine = MathF.Cos(x);
        float y = MathF.Abs(cosine) > 0.00001f ? MathF.Atan2(row.M31, row.M33) : MathF.Atan2(-row.M13, row.M11);
        float z = MathF.Abs(cosine) > 0.00001f ? MathF.Atan2(row.M12, row.M22) : 0;
        short Angle(float value) => checked((short)MathF.Round(value * (4096 / MathF.Tau)));
        return new(Angle(x), Angle(y), Angle(z));
    }

    private static int Parent(NativeBattlePoseSource rig, int bone) => BinaryPrimitives.ReadInt16LittleEndian(rig.Skeleton.AsSpan().Slice(16 + bone * 48, 2));

    private static void ValidateAxes(Matrix4x4 axes)
    {
        if (!Matrix4x4.Decompose(axes, out Vector3 scale, out Quaternion rotation, out Vector3 translation) || !float.IsFinite(rotation.LengthSquared()) || Vector3.DistanceSquared(scale, Vector3.One) > 0.000001f || translation.LengthSquared() > 0.000001f || MathF.Abs(axes.GetDeterminant() - 1) > 0.0001f)
        {
            throw new InvalidDataException("Semantic bind axes require a finite orthonormal positive rotation.");
        }
    }

}
