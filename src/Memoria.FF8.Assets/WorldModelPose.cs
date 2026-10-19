using System.Numerics;

namespace Memoria.FF8.Assets;

/// <summary>World character hierarchy from native 0x6528D0, 0x653DA0 and 0x654060; independent of field control bones.</summary>
public static class WorldModelPose
{
    public static Vector3[] Pose(FieldModel model, int animationId, int frameIndex, float nativeHeading)
    {
        AnimationFrame frame = ReadFrame(model, animationId, frameIndex, nameof(frameIndex));
        return Pose(model, frame, frame, 0, nativeHeading);
    }

    /// <summary>Samples between two committed native animation frames without advancing animation state.</summary>
    public static Vector3[] PoseInterpolated(FieldModel model, int animationId, int previousFrameIndex, int currentFrameIndex, float blend, float nativeHeading)
    {
        if (!float.IsFinite(blend) || blend < 0 || blend > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(blend));
        }
        AnimationFrame previousFrame = ReadFrame(model, animationId, previousFrameIndex, nameof(previousFrameIndex));
        AnimationFrame currentFrame = ReadFrame(model, animationId, currentFrameIndex, nameof(currentFrameIndex));
        if (blend == 1)
        {
            return Pose(model, currentFrame, currentFrame, 0, nativeHeading);
        }
        if (previousFrameIndex == currentFrameIndex)
        {
            blend = 0;
        }
        return Pose(model, previousFrame, currentFrame, blend, nativeHeading);
    }

    private static AnimationFrame ReadFrame(FieldModel model, int animationId, int frameIndex, string frameParameter)
    {
        if ((uint)animationId >= model.Animations.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(animationId));
        }
        ModelAnimation animation = model.Animations[animationId];
        if ((uint)frameIndex >= animation.Frames.Count)
        {
            throw new ArgumentOutOfRangeException(frameParameter);
        }
        return animation.Frames[frameIndex];
    }

    private static Vector3[] Pose(FieldModel model, AnimationFrame previousFrame, AnimationFrame currentFrame, float blend, float nativeHeading)
    {
        Vector3 translation = blend == 0 ? previousFrame.Translation : Vector3.Lerp(previousFrame.Translation, currentFrame.Translation, blend);
        Matrix4x4[] transforms = new Matrix4x4[model.Bones.Count];
        Matrix4x4 virtualRoot = Matrix4x4.CreateRotationZ(MathF.PI / 2);
        Matrix4x4 body = Matrix4x4.CreateRotationY(nativeHeading - MathF.PI);
        for (int bone = 0; bone < transforms.Length; bone++)
        {
            Matrix4x4 local = LocalRotation(previousFrame.Rotations[bone]);
            if (blend != 0)
            {
                Quaternion previousRotation = Quaternion.CreateFromRotationMatrix(local);
                Quaternion currentRotation = Quaternion.CreateFromRotationMatrix(LocalRotation(currentFrame.Rotations[bone]));
                local = Matrix4x4.CreateFromQuaternion(Quaternion.Slerp(previousRotation, currentRotation, blend));
            }
            int parent = model.Bones[bone].Parent;
            if (parent < 0)
            {
                transforms[bone] = local * virtualRoot;
                transforms[bone].Translation = new Vector3(translation.X, -translation.Z, translation.Y);
            }
            else
            {
                local.Translation = new Vector3(0, 0, model.Bones[parent].Length);
                transforms[bone] = local * transforms[parent];
            }
        }
        Vector3[] positions = new Vector3[model.Vertices.Count];
        for (int index = 0; index < positions.Length; index++)
        {
            ModelVertex vertex = model.Vertices[index];
            Vector3 native = Vector3.Transform(Vector3.Transform(vertex.Position, transforms[vertex.Bone]), body);
            positions[index] = new Vector3(native.X, -native.Y, -native.Z);
        }
        return positions;
    }

    private static Matrix4x4 LocalRotation(Vector3 angles)
    {
        angles = new Vector3(PackedAngle(angles.X), PackedAngle(angles.Y), PackedAngle(angles.Z));
        return Matrix4x4.CreateRotationZ(angles.Z) * Matrix4x4.CreateRotationY(angles.Y) * Matrix4x4.CreateRotationX(angles.X);
    }

    private static float PackedAngle(float angle)
    {
        return ((int)MathF.Round(angle * 4096 / MathF.Tau) & 4092) * (MathF.Tau / 4096);
    }
}
