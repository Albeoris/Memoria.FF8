using System.Globalization;
using System.Numerics;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Resources;

public sealed record Ff7NativeFieldPose(IReadOnlyList<Matrix4x4> Bones, Vector3 RootTranslation);

public static class Ff7NativeFieldPoseMath
{
    public static Ff7NativeFieldPose Sample(Ff7FieldModelResources resources, PcFieldAnimation animation, int frame, float nativeTranslationScale = float.NaN)
    {
        return SampleFrames(resources, animation, frame, frame, 0, nativeTranslationScale);
    }

    public static Ff7NativeFieldPose SampleFrames(Ff7FieldModelResources resources, PcFieldAnimation animation, int first, int second, float fraction, float nativeTranslationScale = float.NaN)
    {
        if (animation.RotationOrder != 0x020001 || (uint)first >= animation.Frames.Count || (uint)second >= animation.Frames.Count || !float.IsFinite(fraction) || fraction < 0 || fraction > 1)
        {
            throw new InvalidDataException("Native field poses require bounded original frames, interpolation and the admitted rotation order.");
        }
        Matrix4x4 Interpolate(Matrix4x4 a, Matrix4x4 b)
        {
            return Matrix4x4.CreateFromQuaternion(Quaternion.Slerp(Quaternion.CreateFromRotationMatrix(a), Quaternion.CreateFromRotationMatrix(b), fraction));
        }
        Vector3 translation = first == second ? animation.ReadNativeRootTranslation(first) : Vector3.Lerp(animation.ReadNativeRootTranslation(first), animation.ReadNativeRootTranslation(second), fraction);
        Matrix4x4 rootRotation = Rotate(animation.ReadNativeRootRotation(first));
        if (first != second)
        {
            rootRotation = Interpolate(rootRotation, Rotate(animation.ReadNativeRootRotation(second)));
        }
        Matrix4x4 root = rootRotation * Matrix4x4.CreateTranslation(translation * new Vector3(1, -1, 1));
        Matrix4x4[] transforms = new Matrix4x4[resources.Hierarchy.Bones.Count];
        Dictionary<string, int> indices = resources.Hierarchy.Bones.ToDictionary(bone => bone.Name, bone => bone.Index, StringComparer.OrdinalIgnoreCase);
        foreach (HrcBone bone in resources.Hierarchy.Bones)
        {
            Matrix4x4 parent = root;
            if (!bone.ParentName.Equals("root", StringComparison.OrdinalIgnoreCase))
            {
                HrcBone parentBone = resources.Hierarchy.Bones[indices[bone.ParentName]];
                parent = Matrix4x4.CreateTranslation(0, 0, -parentBone.Length) * transforms[parentBone.Index];
            }
            Vector3 rotation = resources.Hierarchy.IsRootOnly ? Vector3.Zero : animation.ReadNativeBoneRotation(first, bone.Index, nativeTranslationScale);
            Matrix4x4 local = Rotate(rotation);
            if (first != second && !resources.Hierarchy.IsRootOnly)
            {
                local = Interpolate(local, Rotate(animation.ReadNativeBoneRotation(second, bone.Index, nativeTranslationScale)));
            }
            transforms[bone.Index] = local * parent;
        }
        return new(Array.AsReadOnly(transforms), translation);
    }

    public static Matrix4x4 ModelBasis(PcFieldModelDescriptor descriptor)
    {
        float scale = float.Parse(descriptor.Scale, CultureInfo.InvariantCulture) / 4096 * 30.43090057373047f;
        if (!float.IsFinite(scale) || scale <= 0)
        {
            throw new InvalidDataException("FFVII model scale must be finite and positive.");
        }
        return Matrix4x4.CreateRotationX(-MathF.PI / 2) * Matrix4x4.CreateScale(scale);
    }

    public static Matrix4x4 Rotate(Vector3 degrees)
    {
        if (float.IsInfinity(degrees.X) || float.IsInfinity(degrees.Y) || float.IsInfinity(degrees.Z))
        {
            throw new InvalidDataException("Native field Euler rotations cannot consume infinite angles.");
        }
        // The native x87 comparison skips both zero and unordered axes before constructing their matrices.
        // Preserve NaN as that branch marker until this consumer, independently for each Euler axis.
        Vector3 radians = degrees * (MathF.PI / 180);
        Matrix4x4 z = float.IsNaN(degrees.Z) ? Matrix4x4.Identity : Matrix4x4.CreateRotationZ(radians.Z);
        Matrix4x4 x = float.IsNaN(degrees.X) ? Matrix4x4.Identity : Matrix4x4.CreateRotationX(radians.X);
        Matrix4x4 y = float.IsNaN(degrees.Y) ? Matrix4x4.Identity : Matrix4x4.CreateRotationY(radians.Y);
        return z * x * y;
    }
}
