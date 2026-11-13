using System.Numerics;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF8.Assets;

namespace Memoria.FF8.Rendering;

public readonly record struct Ff7FieldPosePhase(int Frame16, int FirstFrame = 0, int LastFrame = -1, bool Loop = false);
public sealed record Ff7FieldPose(IReadOnlyList<Matrix4x4> Bones, Vector3 RootTranslation);

internal readonly record struct Ff7FieldPoseFrames(int First, int Second, float Fraction)
{
    public static Ff7FieldPoseFrames Resolve(PcFieldAnimation animation, Ff7FieldPosePhase phase)
    {
        int frame = phase.Frame16 >> 4;
        if (animation.RotationOrder != 0x020001 || (uint)frame >= animation.Frames.Count)
        {
            throw new NotSupportedException("FF7 pose requires the admitted Z-X-Y rotation order, bounded native HRC channels and an original frame within the clip.");
        }
        int last = phase.LastFrame < 0 ? animation.Frames.Count - 1 : Math.Min(phase.LastFrame, animation.Frames.Count - 1);
        float fraction = (phase.Frame16 & 15) / 16f;
        int next = frame;
        if (fraction != 0 && frame < last)
        {
            next++;
        }
        else if (fraction != 0 && frame == last && phase.Loop)
        {
            next = Math.Clamp(phase.FirstFrame, 0, last);
        }
        return new(frame, next, fraction);
    }

    public Vector3 Translation(PcFieldAnimation animation)
    {
        Vector3 first = animation.Frames[First].RootTranslation;
        return First == Second ? first : Vector3.Lerp(first, animation.Frames[Second].RootTranslation, Fraction);
    }

    public Matrix4x4 Root(PcFieldAnimation animation)
    {
        Matrix4x4 first = Ff7FieldRenderer.Rotate(animation.Frames[First].RootRotation);
        Matrix4x4 rotation = First == Second ? first : PoseInterpolation.Rotation(first, Ff7FieldRenderer.Rotate(animation.Frames[Second].RootRotation), Fraction);
        return rotation * Matrix4x4.CreateTranslation(Translation(animation) * new Vector3(1, -1, 1));
    }
}

public static class Ff7FieldPoseSampler
{
    public static Ff7FieldPose Sample(Ff7FieldModelResources resources, PcFieldAnimation animation, Ff7FieldPosePhase phase, float nativeTranslationScale = float.NaN)
    {
        return SampleFrames(resources, animation, Ff7FieldPoseFrames.Resolve(animation, phase), nativeTranslationScale);
    }

    internal static Ff7FieldPose SampleFrames(Ff7FieldModelResources resources, PcFieldAnimation animation, Ff7FieldPoseFrames frames, float nativeTranslationScale = float.NaN)
    {
        if (resources.KnownPoseBinding is { } binding && ReferenceEquals(animation, binding.Motion))
        {
            return new(Ff7KnownFieldPoseRetargeter.Pose(resources, binding, frames), frames.Translation(animation));
        }
        Matrix4x4 root = frames.Root(animation);
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
            Vector3 rotation = resources.Hierarchy.IsRootOnly ? Vector3.Zero : animation.ReadNativeBoneRotation(frames.First, bone.Index, nativeTranslationScale);
            Matrix4x4 local = Ff7FieldRenderer.Rotate(rotation);
            if (frames.First != frames.Second && !resources.Hierarchy.IsRootOnly)
            {
                Vector3 next = animation.ReadNativeBoneRotation(frames.Second, bone.Index, nativeTranslationScale);
                local = PoseInterpolation.Rotation(local, Ff7FieldRenderer.Rotate(next), frames.Fraction);
            }
            transforms[bone.Index] = local * parent;
        }
        return new(Array.AsReadOnly(transforms), frames.Translation(animation));
    }
}
