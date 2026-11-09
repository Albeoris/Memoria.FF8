using System.Numerics;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;

namespace Memoria.FF8.Rendering;

internal static class Ff7KnownFieldPoseRetargeter
{
    public static IReadOnlyList<Matrix4x4> Pose(Ff7FieldModelResources target, Ff7KnownFieldPoseBinding binding, int frameIndex)
    {
        if (target.Hierarchy.Bones.Count != binding.TargetHierarchy.Bones.Count || target.Hierarchy.Bones.Where((bone, index) => bone.Index != binding.TargetHierarchy.Bones[index].Index || bone.Name != binding.TargetHierarchy.Bones[index].Name || bone.ParentName != binding.TargetHierarchy.Bones[index].ParentName || bone.Length != binding.TargetHierarchy.Bones[index].Length || !bone.ResourceDescriptors.SequenceEqual(binding.TargetHierarchy.Bones[index].ResourceDescriptors)).Any())
        {
            throw new InvalidDataException("Known field pose binding does not match the selected target hierarchy.");
        }
        Ff7FieldModelResources source = new("", binding.SourceHierarchy, [], new Dictionary<string, PcFieldAnimation>());
        IReadOnlyList<Matrix4x4> sourceRest = Ff7FieldRenderer.Pose(source, binding.SourceBind, 0);
        IReadOnlyList<Matrix4x4> sourceCurrent = Ff7FieldRenderer.Pose(source, binding.Motion, frameIndex);
        IReadOnlyList<Matrix4x4> targetRest = Ff7FieldRenderer.Pose(target, binding.TargetBind, 0);
        PcFieldAnimationFrame frame = binding.Motion.Frames[frameIndex];
        Matrix4x4 root = Ff7FieldRenderer.Rotate(frame.RootRotation) * Matrix4x4.CreateTranslation(frame.RootTranslation * new Vector3(1, -1, 1));
        Matrix4x4 bindRoot = Ff7FieldRenderer.Rotate(binding.TargetBind.Frames[0].RootRotation) * Matrix4x4.CreateTranslation(binding.TargetBind.Frames[0].RootTranslation * new Vector3(1, -1, 1));
        Dictionary<string, int> indices = target.Hierarchy.Bones.ToDictionary(bone => bone.Name, bone => bone.Index, StringComparer.OrdinalIgnoreCase);
        Matrix4x4[] result = new Matrix4x4[target.Hierarchy.Bones.Count];
        foreach (HrcBone bone in target.Hierarchy.Bones)
        {
            bool parentIsRoot = bone.ParentName.Equals("root", StringComparison.OrdinalIgnoreCase);
            int parentIndex = parentIsRoot ? -1 : indices[bone.ParentName];
            Matrix4x4 parent = parentIsRoot ? root : result[parentIndex];
            Matrix4x4 restParent = parentIsRoot ? bindRoot : targetRest[parentIndex];
            if (!Matrix4x4.Invert(restParent, out Matrix4x4 inverseRestParent))
            {
                throw new InvalidDataException("Known field pose target bind parent is singular.");
            }
            Matrix4x4 local = Rotation(targetRest[bone.Index] * inverseRestParent);
            int sourceIndex = binding.TargetToSource[bone.Index];
            if (sourceIndex >= 0)
            {
                local = SemanticJointRotation.Transfer(Rotation(targetRest[bone.Index]), Rotation(sourceRest[sourceIndex]), Rotation(sourceCurrent[sourceIndex]), Rotation(parent), Matrix4x4.Identity, Matrix4x4.Identity);
            }
            Matrix4x4 offset = parentIsRoot ? Matrix4x4.Identity : Matrix4x4.CreateTranslation(0, 0, -target.Hierarchy.Bones[parentIndex].Length);
            result[bone.Index] = local * offset * parent;
        }
        return Array.AsReadOnly(result);
    }

    private static Matrix4x4 Rotation(Matrix4x4 matrix)
    {
        if (!Matrix4x4.Decompose(matrix, out _, out Quaternion rotation, out _))
        {
            throw new InvalidDataException("Known field pose bind/current matrix is not decomposable.");
        }
        return Matrix4x4.CreateFromQuaternion(Quaternion.Normalize(rotation));
    }

}
