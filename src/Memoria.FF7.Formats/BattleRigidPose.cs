using System.Numerics;

namespace Memoria.FF7.Formats;

public static class BattleRigidPose
{
    public static Vector3 BonePosition(BattleModelData model, BattleClipFrame frame, Vector3 actorPosition, float facingRadians, int bone)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)bone, (uint)model.Bones.Count);
        Span<Matrix4x4> transforms = stackalloc Matrix4x4[model.Bones.Count];
        Matrix4x4 world = Matrix4x4.CreateRotationY(facingRadians) * Matrix4x4.CreateTranslation(actorPosition);
        if (bone == 0)
        {
            return (FrameTransform(frame) * world).Translation;
        }
        WriteTransforms(model, frame, world, transforms);
        return transforms[bone - 1].Translation;
    }

    public static void WriteTransforms(BattleModelData model, BattleClipFrame frame, Matrix4x4 world, Span<Matrix4x4> transforms)
    {
        IReadOnlyList<BattleBone> bones = model.Bones;
        if (frame.Rotations.Count != bones.Count + 1 || transforms.Length != bones.Count)
        {
            throw new InvalidDataException("FFVII battle clip has a different joint count from its skeleton.");
        }

        Matrix4x4 root = FrameTransform(frame) * world;
        for (int index = 0; index < bones.Count; index++)
        {
            int parent = bones[index].Parent;
            Matrix4x4 attachment = parent < 0 ? root : Matrix4x4.CreateTranslation(0, 0, bones[parent].Length) * transforms[parent];
            transforms[index] = Rotation(frame.Rotations[index + 1]) * attachment;
        }
    }

    public static Vector3 WeaponPosition(BattleClipFrame frame, Vector3 actorPosition, float facingRadians, Vector3 point)
    {
        Matrix4x4 world = Matrix4x4.CreateRotationY(facingRadians) * Matrix4x4.CreateTranslation(actorPosition);
        return Vector3.Transform(point, FrameTransform(frame) * world);
    }

    public static Matrix4x4 FrameTransform(BattleClipFrame frame)
    {
        return Rotation(frame.Rotations[0]) * Matrix4x4.CreateTranslation(frame.X, frame.Y, frame.Z);
    }

    private static Matrix4x4 Rotation(BattleRotationWords words)
    {
        const float radiansPerWord = MathF.Tau / 4096;
        return Matrix4x4.CreateRotationZ(words.Z * radiansPerWord) * Matrix4x4.CreateRotationX(words.X * radiansPerWord) * Matrix4x4.CreateRotationY(words.Y * radiansPerWord);
    }
}
