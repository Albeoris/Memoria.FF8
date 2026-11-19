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

    public NativeBattleVector ReadSourceHand(byte animation, byte counter, byte flags)
    {
        if ((flags & 1) == 0 && !SourceHandFrames.IsDefaultOrEmpty)
        {
            if (animation >= SourceHandFrames.Length || counter == 0 || counter > SourceHandFrames[animation].Length)
            {
                int frames = animation < SourceHandFrames.Length ? SourceHandFrames[animation].Length : 0;
                throw new InvalidDataException($"A transferred weapon contact frame exceeds its source animation: pose {animation}, frame {counter}, source frames {frames}, flags {flags}.");
            }
            return SourceHandFrames[animation][counter - 1];
        }
        NativeBattleDecodedPose pose = SourceMotion.Sample(animation, counter, flags);
        ImmutableArray<NativeBattleActionMatrix> matrices = NativeBattlePoseMatrices.StageBoneMatrices(SourceMotion, pose);
        if ((uint)SourceHand >= matrices.Length)
        {
            throw new InvalidDataException("A transferred weapon contact requires its admitted source hand.");
        }
        NativeBattleActionMatrix hand = matrices[SourceHand];
        return new(unchecked((short)hand.X), unchecked((short)hand.Y), unchecked((short)hand.Z));
    }
}

public sealed record NativeBattleContactVertex(int Bone, NativeBattleVector Position);
public sealed record NativeBattleFootContactCalibration(ImmutableArray<NativeBattleContactVertex> Source, ImmutableArray<NativeBattleContactVertex> Recipient);
public sealed record NativeBattleTwoHandContactCalibration(int SourcePrimary, int SourceSecondary, int RecipientPrimary, int RecipientUpperArm, int RecipientForearm, int RecipientSecondary, float MaximumNativeError)
{
    public int? RecipientPrimaryUpperArm { get; init; }
    public int? RecipientPrimaryForearm { get; init; }
}
public enum NativeBattleRootTranslationMode
{
    RecipientNeutral,
    SourceAbsolute
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
    private readonly NativeBattleFootContactCalibration? feet;
    private readonly float sourceFloor;
    private readonly float recipientFloor;
    private readonly NativeBattleTwoHandContactCalibration? hands;
    private readonly string? primaryContactJoint;
    private readonly NativeBattleRootTranslationMode translationMode;

    public NativeBattlePoseRetargeter(NativeBattlePoseSource sourceNeutral, NativeBattlePoseSource recipientNeutral, IReadOnlyList<NativeBattleSemanticBonePair> pairs, float rootMotionScale, NativeBattleFootContactCalibration? footContact = null, NativeBattleTwoHandContactCalibration? twoHandContact = null, string? primaryContactJoint = "hand-a", NativeBattleRootTranslationMode translationMode = NativeBattleRootTranslationMode.RecipientNeutral)
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
        this.primaryContactJoint = primaryContactJoint;
        if (!Enum.IsDefined(translationMode))
        {
            throw new InvalidDataException("A native motion compilation requires a supported root translation coordinate contract.");
        }
        this.translationMode = translationMode;
        foreach (NativeBattleSemanticBonePair pair in pairs)
        {
            ValidateAxes(pair.SourceBindAxes);
            ValidateAxes(pair.RecipientBindAxes);
        }
        feet = footContact;
        if (feet is not null)
        {
            if (feet.Source.IsDefaultOrEmpty || feet.Recipient.IsDefaultOrEmpty || feet.Source.Any(vertex => (uint)vertex.Bone >= source.BoneCount) || feet.Recipient.Any(vertex => (uint)vertex.Bone >= recipient.BoneCount))
            {
                throw new InvalidDataException("A foot contact calibration requires actual source and recipient mesh vertices on admitted foot bones.");
            }
            sourceFloor = FootFloor(source, sourcePose, feet.Source);
            recipientFloor = FootFloor(recipient, recipientRest, feet.Recipient);
        }
        hands = twoHandContact;
        if (hands is not null && ((uint)hands.SourcePrimary >= source.BoneCount || (uint)hands.SourceSecondary >= source.BoneCount || (uint)hands.RecipientPrimary >= recipient.BoneCount || (uint)hands.RecipientUpperArm >= recipient.BoneCount || (uint)hands.RecipientForearm >= recipient.BoneCount || (uint)hands.RecipientSecondary >= recipient.BoneCount || Parent(recipient, hands.RecipientForearm) != hands.RecipientUpperArm || Parent(recipient, hands.RecipientSecondary) != hands.RecipientForearm || !float.IsFinite(hands.MaximumNativeError) || hands.MaximumNativeError <= 0))
        {
            throw new InvalidDataException("A two-hand contact requires admitted source hands, a direct recipient arm chain and a positive finite native error tolerance.");
        }
        if (hands is not null && (hands.RecipientPrimaryUpperArm is not null || hands.RecipientPrimaryForearm is not null))
        {
            if (hands.RecipientPrimaryUpperArm is not int primaryUpper || hands.RecipientPrimaryForearm is not int primaryForearm || (uint)primaryUpper >= recipient.BoneCount || (uint)primaryForearm >= recipient.BoneCount || Parent(recipient, primaryForearm) != primaryUpper || Parent(recipient, hands.RecipientPrimary) != primaryForearm)
            {
                throw new InvalidDataException("A coupled hand contact requires both direct recipient arm chains.");
            }
        }
    }

    public NativeBattlePoseSource Compile(NativeBattlePoseSource motion, ImmutableArray<ImmutableArray<bool>> secondaryContactFrames = default)
    {
        motion.Validate();
        if (!motion.Skeleton.AsSpan().SequenceEqual(source.Skeleton.AsSpan()))
        {
            throw new InvalidDataException("The motion bank must be bound to the calibrated source skeleton.");
        }
        ImmutableArray<ImmutableArray<NativeBattleDecodedPose>> decoded = NativeBattlePoseDecoder.DecodeFrames(motion.Animations.AsSpan(), motion.BoneCount);
        if (!secondaryContactFrames.IsDefault && (secondaryContactFrames.Length != decoded.Length || secondaryContactFrames.Where((frames, index) => frames.IsDefault || frames.Length != decoded[index].Length).Any()))
        {
            throw new InvalidDataException("Source hand contacts must cover exactly the original animation frames.");
        }
        ImmutableArray<NativeBattleDecodedPose>[] converted = decoded.Select((animation, animationIndex) => animation.Select((pose, frameIndex) =>
        {
            try
            {
                return Transfer(pose, secondaryContactFrames.IsDefault || secondaryContactFrames[animationIndex][frameIndex]);
            }
            catch (NotSupportedException exception)
            {
                throw new NotSupportedException($"Native retarget animation {animationIndex}, frame {frameIndex + 1}: {exception.Message}", exception);
            }
        }).ToImmutableArray()).ToArray();
        byte[] aliases = Enumerable.Repeat(byte.MaxValue, source.BoneCount).ToArray();
        foreach (NativeBattleSemanticBonePair pair in bindings.Values)
        {
            aliases[pair.Source] = checked((byte)pair.Recipient);
        }
        NativeBattleSemanticBonePair? primary = bindings.Values.SingleOrDefault(pair => pair.Joint == primaryContactJoint);
        NativeBattlePoseRetargetBinding? contact = primary is null ? null : new(motion, primary.Source, primary.Recipient)
        {
            SourceHandFrames = decoded.Select(animation => animation.Select(pose =>
            {
                NativeBattleActionMatrix hand = NativeBattlePoseMatrices.StageBoneMatrices(motion, pose)[primary.Source];
                return new NativeBattleVector(unchecked((short)hand.X), unchecked((short)hand.Y), unchecked((short)hand.Z));
            }).ToImmutableArray()).ToImmutableArray()
        };
        return recipient with { Animations = NativeBattleAnimationEncoder.Encode(converted), ActionBoneAliases = aliases.ToImmutableArray(), RetargetBinding = contact, SecondaryHandContactFrames = secondaryContactFrames.IsDefault ? [] : secondaryContactFrames };
    }

    public NativeBattleDecodedPose Transfer(NativeBattleDecodedPose pose, bool secondaryContact = true)
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
        short Coordinate(short position, short origin, short target) => translationMode == NativeBattleRootTranslationMode.SourceAbsolute ? checked((short)MathF.Round(position * rootScale)) : checked((short)(target + MathF.Round((position - origin) * rootScale)));
        NativeBattleVector translation = new(Coordinate(pose.Translation.X, sourceOrigin.X, recipientRest.Translation.X), Coordinate(pose.Translation.Y, sourceOrigin.Y, recipientRest.Translation.Y), Coordinate(pose.Translation.Z, sourceOrigin.Z, recipientRest.Translation.Z));
        NativeBattleDecodedPose result = new(translation, bones.MoveToImmutable(), recipientRest.HasScale);
        if (hands is not null && secondaryContact)
        {
            result = ApplyTwoHandContact(pose, result, hands);
        }
        if (feet is not null)
        {
            int sourceScale = BinaryPrimitives.ReadInt16LittleEndian(source.Skeleton.AsSpan()[2..]);
            int targetScale = BinaryPrimitives.ReadInt16LittleEndian(recipient.Skeleton.AsSpan()[2..]);
            if (sourceScale <= 0 || targetScale <= 0)
            {
                throw new NotSupportedException("Foot contact requires positive source and recipient native skeleton scales.");
            }
            float currentSourceFloor = FootFloor(source, pose, feet.Source);
            float desiredFloor = recipientFloor + (currentSourceFloor - sourceFloor) * rootScale * targetScale / sourceScale;
            float currentFloor = FootFloor(recipient, result, feet.Recipient);
            short correctedY = checked((short)(result.Translation.Y + MathF.Round((desiredFloor - currentFloor) * 256 / targetScale)));
            result = result with { Translation = result.Translation with { Y = correctedY } };
        }
        return result;
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
        short Angle(float value) => checked((short)MathF.Round(value * (4096 / MathF.Tau)));
        short x = Angle(MathF.Atan2(-row.M32, MathF.Sqrt(row.M31 * row.M31 + row.M33 * row.M33)));
        // A quantized quarter turn has one coupled Y/Z angle; floating-point remnants cannot select separate axes.
        bool singular = Math.Abs(x) == 1024;
        float y = singular ? MathF.Atan2(-row.M13, row.M11) : MathF.Atan2(row.M31, row.M33);
        float z = singular ? 0 : MathF.Atan2(row.M12, row.M22);
        return new(x, Angle(y), Angle(z));
    }

    private static int Parent(NativeBattlePoseSource rig, int bone) => BinaryPrimitives.ReadInt16LittleEndian(rig.Skeleton.AsSpan().Slice(16 + bone * 48, 2));

    private static void ValidateAxes(Matrix4x4 axes)
    {
        if (!Matrix4x4.Decompose(axes, out Vector3 scale, out Quaternion rotation, out Vector3 translation) || !float.IsFinite(rotation.LengthSquared()) || Vector3.DistanceSquared(scale, Vector3.One) > 0.000001f || translation.LengthSquared() > 0.000001f || MathF.Abs(axes.GetDeterminant() - 1) > 0.0001f)
        {
            throw new InvalidDataException("Semantic bind axes require a finite orthonormal positive rotation.");
        }
    }

    private static float FootFloor(NativeBattlePoseSource rig, NativeBattleDecodedPose pose, ImmutableArray<NativeBattleContactVertex> vertices)
    {
        ImmutableArray<NativeBattleActionMatrix> matrices = NativeBattlePoseMatrices.StageBoneMatrices(rig, pose);
        int floor = int.MinValue;
        foreach (NativeBattleContactVertex vertex in vertices)
        {
            NativeBattleActionMatrix bone = matrices[vertex.Bone];
            NativeBattleVector rotated = NativeBattleGfMatrices.RotateShort56C4F0(bone, vertex.Position);
            floor = Math.Max(floor, rotated.Y + bone.Y);
        }
        return floor;
    }

    private NativeBattleDecodedPose ApplyTwoHandContact(NativeBattleDecodedPose sourcePose, NativeBattleDecodedPose targetPose, NativeBattleTwoHandContactCalibration contact)
    {
        ImmutableArray<NativeBattleActionMatrix> donor = NativeBattlePoseMatrices.StageBoneMatrices(source, sourcePose);
        ImmutableArray<NativeBattleActionMatrix> target = NativeBattlePoseMatrices.StageBoneMatrices(recipient, targetPose);
        Vector3 Point(NativeBattleActionMatrix matrix) => new(matrix.X, matrix.Y, matrix.Z);
        Vector3 gap = Point(donor[contact.SourceSecondary]) - Point(donor[contact.SourcePrimary]);
        Vector3 primary = Point(target[contact.RecipientPrimary]);
        if (contact.RecipientPrimaryUpperArm is int primaryUpper && contact.RecipientPrimaryForearm is int primaryForearm)
        {
            primary = NativeBattleCoupledHandContact.SolvePrimary(primary, Point(target[primaryUpper]), LimbLength(targetPose, primaryUpper), LimbLength(targetPose, primaryForearm), Point(target[contact.RecipientUpperArm]), LimbLength(targetPose, contact.RecipientUpperArm), LimbLength(targetPose, contact.RecipientForearm), gap);
            if (primary != Point(target[contact.RecipientPrimary]))
            {
                targetPose = ApplyArmContact(targetPose, contact with { RecipientUpperArm = primaryUpper, RecipientForearm = primaryForearm, RecipientSecondary = contact.RecipientPrimary }, primary);
            }
        }
        targetPose = ApplyArmContact(targetPose, contact, primary + gap);
        target = NativeBattlePoseMatrices.StageBoneMatrices(recipient, targetPose);
        Matrix4x4[] handRotations = GlobalRotations(recipient, targetPose);
        targetPose = RefineArmContact(targetPose, contact, gap, true);
        targetPose = RestoreHandRotation(targetPose, contact, handRotations[contact.RecipientSecondary]);
        if (contact.RecipientPrimaryForearm is int primaryParent)
        {
            targetPose = RestoreHandRotation(targetPose, contact with { RecipientSecondary = contact.RecipientPrimary, RecipientForearm = primaryParent }, handRotations[contact.RecipientPrimary]);
        }
        target = NativeBattlePoseMatrices.StageBoneMatrices(recipient, targetPose);
        float error = Vector3.Distance(Point(target[contact.RecipientSecondary]) - Point(target[contact.RecipientPrimary]), gap);
        if (!float.IsFinite(error) || error > contact.MaximumNativeError)
        {
            throw new NotSupportedException($"The native coupled hand gap error {error:R} exceeds its profile tolerance {contact.MaximumNativeError:R} native units.");
        }
        return targetPose;
    }

    private NativeBattleDecodedPose ApplyArmContact(NativeBattleDecodedPose targetPose, NativeBattleTwoHandContactCalibration contact, Vector3 desired)
    {
        ImmutableArray<NativeBattleActionMatrix> target = NativeBattlePoseMatrices.StageBoneMatrices(recipient, targetPose);
        Vector3 Point(NativeBattleActionMatrix matrix) => new(matrix.X, matrix.Y, matrix.Z);
        Matrix4x4[] rotations = GlobalRotations(recipient, targetPose);
        Vector3 pole = Vector3.TransformNormal(Vector3.UnitX, rotations[contact.RecipientUpperArm]);
        Vector3 toward = desired - Point(target[contact.RecipientUpperArm]);
        if (toward.LengthSquared() > 0 && Vector3.Cross(pole, toward).LengthSquared() < 0.00000001f)
        {
            pole = Vector3.TransformNormal(Vector3.UnitY, rotations[contact.RecipientUpperArm]);
        }
        NativeBattleTwoBoneContactSolution solution = NativeBattleTwoBoneContact.Solve(Point(target[contact.RecipientUpperArm]), Point(target[contact.RecipientForearm]), Point(target[contact.RecipientSecondary]), desired, pole, LimbLength(targetPose, contact.RecipientUpperArm), LimbLength(targetPose, contact.RecipientForearm));
        Matrix4x4 handRotation = rotations[contact.RecipientSecondary];
        ImmutableArray<NativeBattleBonePose>.Builder bones = targetPose.Bones.ToBuilder();
        Matrix4x4 upper = Aim(rotations[contact.RecipientUpperArm], solution.Elbow - Point(target[contact.RecipientUpperArm]), Length(recipient, contact.RecipientUpperArm));
        int upperParent = Parent(recipient, contact.RecipientUpperArm);
        bones[contact.RecipientUpperArm] = bones[contact.RecipientUpperArm] with { Rotation = Angles(upper * Matrix4x4.Transpose(upperParent < 0 ? Matrix4x4.Identity : rotations[upperParent])) };
        targetPose = targetPose with { Bones = bones.ToImmutable() };
        target = NativeBattlePoseMatrices.StageBoneMatrices(recipient, targetPose);
        rotations = GlobalRotations(recipient, targetPose);
        Matrix4x4 lower = Aim(rotations[contact.RecipientForearm], desired - Point(target[contact.RecipientForearm]), Length(recipient, contact.RecipientForearm));
        bones[contact.RecipientForearm] = bones[contact.RecipientForearm] with { Rotation = Angles(lower * Matrix4x4.Transpose(rotations[contact.RecipientUpperArm])) };
        targetPose = targetPose with { Bones = bones.ToImmutable() };
        rotations = GlobalRotations(recipient, targetPose);
        bones[contact.RecipientSecondary] = bones[contact.RecipientSecondary] with { Rotation = Angles(handRotation * Matrix4x4.Transpose(rotations[contact.RecipientForearm])) };
        targetPose = targetPose with { Bones = bones.ToImmutable() };
        target = NativeBattlePoseMatrices.StageBoneMatrices(recipient, targetPose);
        targetPose = RefineArmContact(targetPose, contact, desired);
        targetPose = RestoreHandRotation(targetPose, contact, handRotation);
        target = NativeBattlePoseMatrices.StageBoneMatrices(recipient, targetPose);
        float error = Vector3.Distance(Point(target[contact.RecipientSecondary]), desired);
        if (!float.IsFinite(error) || contact.RecipientPrimaryUpperArm is null && error > contact.MaximumNativeError)
        {
            throw new NotSupportedException($"The native secondary hand contact error {error:R} exceeds its profile tolerance {contact.MaximumNativeError:R} native units.");
        }
        return targetPose;
    }

    private NativeBattleDecodedPose RestoreHandRotation(NativeBattleDecodedPose pose, NativeBattleTwoHandContactCalibration contact, Matrix4x4 rotation)
    {
        Matrix4x4[] rotations = GlobalRotations(recipient, pose);
        var bones = pose.Bones.ToBuilder();
        bones[contact.RecipientSecondary] = bones[contact.RecipientSecondary] with { Rotation = Angles(rotation * Matrix4x4.Transpose(rotations[contact.RecipientForearm])) };
        return pose with { Bones = bones.ToImmutable() };
    }

    private NativeBattleDecodedPose RefineArmContact(NativeBattleDecodedPose pose, NativeBattleTwoHandContactCalibration contact, Vector3 desired, bool coupled = false)
    {
        float Error(NativeBattleDecodedPose candidate)
        {
            var matrices = NativeBattlePoseMatrices.StageBoneMatrices(recipient, candidate);
            NativeBattleActionMatrix hand = matrices[contact.RecipientSecondary];
            Vector3 point = new(hand.X, hand.Y, hand.Z);
            if (coupled)
            {
                NativeBattleActionMatrix primary = matrices[contact.RecipientPrimary];
                point -= new Vector3(primary.X, primary.Y, primary.Z);
            }
            return Vector3.DistanceSquared(point, desired);
        }
        int[] joints = coupled && contact.RecipientPrimaryUpperArm is int primaryUpper && contact.RecipientPrimaryForearm is int primaryForearm
            ? [contact.RecipientUpperArm, contact.RecipientForearm, primaryUpper, primaryForearm]
            : [contact.RecipientUpperArm, contact.RecipientForearm];
        float shortest = joints.Min(joint => LimbLength(pose, joint));
        int radiusLimit = (int)MathF.Min(2048, MathF.Max(1, MathF.Ceiling(4096 / (MathF.Tau * shortest))));
        float error = Error(pose);
        float maximum = contact.MaximumNativeError * contact.MaximumNativeError;
        while (error > maximum)
        {
            NativeBattleDecodedPose best = pose;
            float bestError = error;
            // A small native-scale matrix can retain the same integer endpoint over several 12-bit angle steps.
            // Search at most one positional quantum per shortest limb, stopping at the first improving radius.
            for (int radius = 1; radius <= radiusLimit && bestError >= error; radius++)
            {
                foreach (int joint in joints)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        for (int y = -1; y <= 1; y++)
                        {
                            for (int z = -1; z <= 1; z++)
                            {
                                if (x == 0 && y == 0 && z == 0)
                                {
                                    continue;
                                }
                                NativeBattleVector original = pose.Bones[joint].Rotation;
                                NativeBattleVector changed = new((short)((original.X + x * radius) & 4095), (short)((original.Y + y * radius) & 4095), (short)((original.Z + z * radius) & 4095));
                                var bones = pose.Bones.ToBuilder();
                                bones[joint] = bones[joint] with { Rotation = changed };
                                NativeBattleDecodedPose candidate = pose with { Bones = bones.ToImmutable() };
                                float candidateError = Error(candidate);
                                if (candidateError < bestError)
                                {
                                    best = candidate;
                                    bestError = candidateError;
                                }
                            }
                        }
                    }
                }
            }
            if (bestError >= error)
            {
                break;
            }
            pose = best;
            error = bestError;
        }
        return pose;
    }

    private static Matrix4x4 Aim(Matrix4x4 current, Vector3 desired, short length)
    {
        if (length == 0 || desired.LengthSquared() == 0)
        {
            throw new NotSupportedException("A native hand contact requires a nonzero limb and direction.");
        }
        Vector3 first = Vector3.Normalize(Vector3.TransformNormal(Vector3.UnitZ, current) * Math.Sign(length));
        Vector3 second = Vector3.Normalize(desired);
        Vector3 cross = Vector3.Cross(first, second);
        float sine = cross.Length();
        float cosine = Math.Clamp(Vector3.Dot(first, second), -1, 1);
        if (sine < 0.0000001f)
        {
            return cosine >= 0 ? current : current * Matrix4x4.CreateFromAxisAngle(Vector3.Normalize(Vector3.TransformNormal(Vector3.UnitX, current)), MathF.PI);
        }
        return current * Matrix4x4.CreateFromAxisAngle(cross / sine, MathF.Atan2(sine, cosine));
    }

    private static short Length(NativeBattlePoseSource rig, int bone) => BinaryPrimitives.ReadInt16LittleEndian(rig.Skeleton.AsSpan().Slice(18 + bone * 48, 2));

    private float LimbLength(NativeBattleDecodedPose pose, int bone)
    {
        float scale = BinaryPrimitives.ReadInt16LittleEndian(recipient.Skeleton.AsSpan()[2..]) / 4096f;
        if (pose.HasScale)
        {
            for (int ancestor = bone; ancestor >= 0; ancestor = Parent(recipient, ancestor))
            {
                NativeBattleVector local = pose.Bones[ancestor].Scale;
                if (local.X <= 0 || local.X != local.Y || local.X != local.Z)
                {
                    throw new NotSupportedException("Two-hand contact requires positive uniform recipient arm scales.");
                }
                scale *= local.X / 1024f;
            }
        }
        return MathF.Abs(Length(recipient, bone)) * scale;
    }

}
