using System.Numerics;

namespace Memoria.FF7.Formats;

public readonly record struct BattleActorPoseTransform(float AngleX = 0, float AngleY = 0, float AngleZ = 0, float X = 0, float Y = 0, float Z = 0);

public static class BattleRigidPose
{
    public static Vector3 BonePosition(BattleModelData model, BattleClipFrame frame, Vector3 actorPosition, float facingRadians, int bone, BattleActorPoseTransform? extra = null)
    {
        return BonePoint(model, frame, actorPosition, facingRadians, bone, Vector3.Zero, extra);
    }

    public static Vector3 BonePoint(BattleModelData model, BattleClipFrame frame, Vector3 actorPosition, float facingRadians, int bone, Vector3 localPoint, BattleActorPoseTransform? extra = null)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)bone, (uint)model.Bones.Count);
        Span<Matrix4x4> transforms = stackalloc Matrix4x4[model.Bones.Count];
        Matrix4x4 world = Matrix4x4.CreateRotationY(facingRadians) * Matrix4x4.CreateTranslation(actorPosition);
        if (bone == 0)
        {
            return Vector3.Transform(localPoint, FrameTransform(frame, extra) * world);
        }
        WriteTransforms(model, frame, world, transforms, extra);
        return Vector3.Transform(localPoint, transforms[bone - 1]);
    }

    public static void WriteTransforms(BattleModelData model, BattleClipFrame frame, Matrix4x4 world, Span<Matrix4x4> transforms, BattleActorPoseTransform? extra = null)
    {
        IReadOnlyList<BattleBone> bones = model.Bones;
        if (frame.Rotations.Count != bones.Count + 1 || transforms.Length != bones.Count)
        {
            throw new InvalidDataException("FFVII battle clip has a different joint count from its skeleton.");
        }

        Matrix4x4 root = FrameTransform(frame, extra) * world;
        for (int index = 0; index < bones.Count; index++)
        {
            int parent = bones[index].Parent;
            Matrix4x4 attachment = parent < 0 ? root : Matrix4x4.CreateTranslation(extra?.X ?? 0, extra?.Y ?? 0, bones[parent].Length + (extra?.Z ?? 0)) * transforms[parent];
            transforms[index] = Rotation(frame.Rotations[index + 1], extra) * attachment;
        }
    }

    public static Vector3 WeaponPosition(BattleClipFrame frame, Vector3 actorPosition, float facingRadians, Vector3 point, BattleActorPoseTransform? extra = null)
    {
        Matrix4x4 world = Matrix4x4.CreateRotationY(facingRadians) * Matrix4x4.CreateTranslation(actorPosition);
        return Vector3.Transform(point, FrameTransform(frame, extra) * world);
    }

    public static Matrix4x4 FrameTransform(BattleClipFrame frame, BattleActorPoseTransform? extra = null)
    {
        return Rotation(frame.Rotations[0], extra) * Matrix4x4.CreateTranslation(frame.X + (extra?.X ?? 0), frame.Y + (extra?.Y ?? 0), frame.Z + (extra?.Z ?? 0));
    }

    public static Matrix4x4 Rotation(BattleRotationWords words, BattleActorPoseTransform? extra = null)
    {
        const float radiansPerWord = MathF.Tau / 4096;
        const float degreesPerWord = 360f / 4096;
        float x = words.X + (extra?.AngleX ?? 0) / degreesPerWord;
        float y = words.Y + (extra?.AngleY ?? 0) / degreesPerWord;
        float z = words.Z + (extra?.AngleZ ?? 0) / degreesPerWord;
        return Matrix4x4.CreateRotationZ(z * radiansPerWord) * Matrix4x4.CreateRotationX(x * radiansPerWord) * Matrix4x4.CreateRotationY(y * radiansPerWord);
    }
}
