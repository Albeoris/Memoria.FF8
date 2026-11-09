using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;

namespace Memoria.FF8;

internal sealed record Ff7SemanticJointBinding(string SourceJoint, int TargetBone);
internal sealed record Ff7FootContactBinding(IReadOnlyList<string> SourceFeet, IReadOnlyList<int> TargetFeet);

/// <summary>An explicit named-joint profile; source rotations transfer through calibrated global bind bases, never Euler-array indices.</summary>
internal sealed class Ff7SemanticRetargeter
{
    private readonly FieldModel target;
    private readonly string sourceIdentity;
    private readonly IReadOnlyList<Matrix4x4> sourceRest;
    private readonly IReadOnlyList<Matrix4x4> targetRest;
    private readonly Matrix4x4 sourceBasis;
    private readonly Matrix4x4 calibration;
    private readonly Matrix4x4 inverseCalibration;
    private readonly Matrix4x4 calibratedAxes;
    private readonly Matrix4x4 inverseCalibratedAxes;
    private readonly Dictionary<int, int> joints;
    private readonly Vector3 sourceRootRest;
    private readonly Ff7FootContactBinding? footContact;

    public Ff7SemanticRetargeter(FieldModel target, PcFieldModelDescriptor descriptor, Ff7FieldModelResources original, Matrix4x4 targetToFf7, IReadOnlyList<Ff7SemanticJointBinding> bindings, Ff7FootContactBinding? footContact = null)
    {
        this.target = target;
        this.footContact = footContact;
        sourceIdentity = HierarchyIdentity(original.Hierarchy);
        calibration = targetToFf7;
        if (!Matrix4x4.Invert(calibration, out inverseCalibration))
        {
            throw new InvalidDataException("Retarget calibration is singular.");
        }
        Matrix4x4 axes = calibration;
        axes.Translation = Vector3.Zero;
        calibratedAxes = axes;
        if (!Matrix4x4.Decompose(calibratedAxes, out Vector3 scales, out _, out _) || MathF.Abs(MathF.Abs(scales.X) - MathF.Abs(scales.Y)) > 0.0001f || MathF.Abs(MathF.Abs(scales.X) - MathF.Abs(scales.Z)) > 0.0001f || !Matrix4x4.Invert(calibratedAxes, out inverseCalibratedAxes))
        {
            throw new NotSupportedException("Semantic retargeting requires a uniform-scale calibrated basis; nonuniform proportions require an explicit effector solver.");
        }
        sourceBasis = Matrix4x4.CreateRotationX(-MathF.PI / 2) * Matrix4x4.CreateScale(float.Parse(descriptor.Scale, CultureInfo.InvariantCulture) / 128);
        string idle = Path.GetFileNameWithoutExtension(descriptor.Animations[0]) + ".a";
        PcFieldAnimation neutral = original.Animations[idle];
        sourceRootRest = neutral.Frames[0].RootTranslation;
        sourceRest = Ff7FieldRenderer.Pose(original, neutral, 0).Select(matrix => matrix * sourceBasis).ToArray();
        targetRest = target.ReadBoneTransforms(0, 0);
        Dictionary<string, int> named = original.Hierarchy.Bones.ToDictionary(bone => bone.Name, bone => bone.Index, StringComparer.OrdinalIgnoreCase);
        joints = new();
        foreach (Ff7SemanticJointBinding binding in bindings)
        {
            if ((uint)binding.TargetBone >= target.Bones.Count || !named.TryGetValue(binding.SourceJoint, out int source) || !joints.TryAdd(binding.TargetBone, source))
            {
                throw new InvalidDataException("Retarget semantic bindings require existing named source joints and unique valid target bones.");
            }
        }
        if (footContact is not null && (footContact.SourceFeet.Count == 0 || footContact.TargetFeet.Count == 0 || footContact.SourceFeet.Any(name => !named.ContainsKey(name)) || footContact.TargetFeet.Any(bone => (uint)bone >= target.Bones.Count || !target.Vertices.Any(vertex => vertex.Bone == bone))))
        {
            throw new InvalidDataException("Foot contact bindings require named source feet and mesh-bearing target feet.");
        }
        foreach (int bone in target.Vertices.Select(vertex => vertex.Bone).Distinct())
        {
            int ancestor = bone;
            while (ancestor >= 0 && !joints.ContainsKey(ancestor))
            {
                ancestor = target.Bones[ancestor].Parent;
            }
            if (ancestor < 0)
            {
                throw new NotSupportedException($"Target mesh bone {bone} has no admitted semantic owner in its ancestry.");
            }
        }
    }

    public void ValidateSource(Ff7FieldModelResources original)
    {
        if (HierarchyIdentity(original.Hierarchy) != sourceIdentity)
        {
            throw new InvalidDataException("The original ordered named rig does not match the selected appearance profile.");
        }
    }

    public Vector3[] Pose(Ff7FieldModelResources original, PcFieldAnimation animation, int frame16)
    {
        if (HierarchyIdentity(original.Hierarchy) != sourceIdentity || animation.BoneCount != original.Hierarchy.DeclaredBoneCount || frame16 < 0 || frame16 / 16f >= animation.Frames.Count)
        {
            throw new InvalidDataException("Retarget source hierarchy, animation binding or original frame is incompatible with this profile.");
        }
        int first = frame16 >> 4;
        int second = Math.Min(first + 1, animation.Frames.Count - 1);
        float fraction = (frame16 & 15) / 16f;
        IReadOnlyList<Matrix4x4> firstPose = Ff7FieldRenderer.Pose(original, animation, first);
        IReadOnlyList<Matrix4x4> secondPose = Ff7FieldRenderer.Pose(original, animation, second);
        Matrix4x4[] current = new Matrix4x4[firstPose.Count];
        for (int index = 0; index < current.Length; index++)
        {
            Quaternion rotation = Quaternion.Slerp(Rotation(firstPose[index]), Rotation(secondPose[index]), fraction);
            Matrix4x4 matrix = Matrix4x4.CreateFromQuaternion(rotation);
            matrix.Translation = Vector3.Lerp(firstPose[index].Translation, secondPose[index].Translation, fraction);
            current[index] = matrix * sourceBasis;
        }
        Vector3 sourceRoot = Vector3.Lerp(animation.Frames[first].RootTranslation, animation.Frames[second].RootTranslation, fraction);
        Vector3 rootMotion = Vector3.TransformNormal((sourceRoot - sourceRootRest) * new Vector3(1, -1, 1), sourceBasis);
        rootMotion = Vector3.TransformNormal(rootMotion, inverseCalibration);
        Matrix4x4[] adjusted = new Matrix4x4[target.Bones.Count];
        for (int bone = 0; bone < adjusted.Length; bone++)
        {
            int parent = target.Bones[bone].Parent;
            Matrix4x4 restLocal = targetRest[bone];
            Matrix4x4 parentCurrent = Matrix4x4.Identity;
            if (parent >= 0)
            {
                if (!Matrix4x4.Invert(targetRest[parent], out Matrix4x4 inverseParent))
                {
                    throw new InvalidDataException("Target neutral parent transform is singular.");
                }
                restLocal *= inverseParent;
                parentCurrent = adjusted[parent];
            }
            Matrix4x4 localRotation = Matrix4x4.CreateFromQuaternion(Rotation(restLocal));
            if (joints.TryGetValue(bone, out int source))
            {
                Matrix4x4 restTargetRotation = Matrix4x4.CreateFromQuaternion(Rotation(targetRest[bone])) * calibratedAxes;
                Matrix4x4 inverseSourceRest = Matrix4x4.Transpose(Matrix4x4.CreateFromQuaternion(Rotation(sourceRest[source])));
                Matrix4x4 desiredGlobal = restTargetRotation * inverseSourceRest * Matrix4x4.CreateFromQuaternion(Rotation(current[source])) * inverseCalibratedAxes;
                Matrix4x4 inverseParentRotation = Matrix4x4.Transpose(Matrix4x4.CreateFromQuaternion(Rotation(parentCurrent)));
                localRotation = desiredGlobal * inverseParentRotation;
            }
            localRotation.Translation = restLocal.Translation + (parent < 0 ? rootMotion : Vector3.Zero);
            adjusted[bone] = localRotation * parentCurrent;
        }
        Vector3[] posed = target.PoseWithBoneTransforms(adjusted);
        if (footContact is not null)
        {
            HashSet<int> sourceFeet = original.Hierarchy.Bones.Where(bone => footContact.SourceFeet.Contains(bone.Name, StringComparer.OrdinalIgnoreCase)).Select(bone => bone.Index).ToHashSet();
            float sourceFloor = original.Geometry.Where(part => sourceFeet.Contains(part.BoneIndex)).SelectMany(part => part.Polygon.Vertices.Select(vertex => Vector3.Transform(vertex, current[part.BoneIndex]).Z)).Min();
            float targetFloor = target.Vertices.Select((vertex, index) => (vertex.Bone, Position: posed[index])).Where(vertex => footContact.TargetFeet.Contains(vertex.Bone)).Min(vertex => Vector3.Transform(vertex.Position, calibration).Z);
            Vector3 correction = Vector3.TransformNormal(new Vector3(0, 0, sourceFloor - targetFloor), inverseCalibration);
            for (int index = 0; index < posed.Length; index++)
            {
                posed[index] += correction;
            }
        }
        return posed;
    }

    private static Quaternion Rotation(Matrix4x4 matrix)
    {
        if (!Matrix4x4.Decompose(matrix, out _, out Quaternion rotation, out _))
        {
            throw new InvalidDataException("Retarget bind/pose transform cannot be decomposed.");
        }
        return Quaternion.Normalize(rotation);
    }

    internal static string HierarchyIdentity(HrcHierarchy hierarchy)
    {
        string text = hierarchy.DeclaredBoneCount.ToString(CultureInfo.InvariantCulture) + "\n" + string.Join("\n", hierarchy.Bones.Select(bone => $"{bone.Index}:{bone.Name.ToLowerInvariant()}:{bone.ParentName.ToLowerInvariant()}:{bone.Length.ToString("R", CultureInfo.InvariantCulture)}"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    }
}
