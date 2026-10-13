using System.Numerics;

namespace Memoria.FF8.Assets;

/// <summary>World character hierarchy from native 0x6528D0, 0x653DA0 and 0x654060; independent of field control bones.</summary>
public static class WorldModelPose
{
    public static Vector3[] Pose(FieldModel model, int animationId, int frameIndex, float nativeHeading)
    {
        if ((uint)animationId >= model.Animations.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(animationId));
        }
        ModelAnimation animation = model.Animations[animationId];
        if ((uint)frameIndex >= animation.Frames.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(frameIndex));
        }
        AnimationFrame frame = animation.Frames[frameIndex];
        Matrix4x4[] transforms = new Matrix4x4[model.Bones.Count];
        Matrix4x4 virtualRoot = Matrix4x4.CreateRotationZ(MathF.PI / 2);
        Matrix4x4 body = Matrix4x4.CreateRotationY(nativeHeading - MathF.PI);
        for (int bone = 0; bone < transforms.Length; bone++)
        {
            Vector3 angles = frame.Rotations[bone];
            static float PackedAngle(float angle) => ((int)MathF.Round(angle * 4096 / MathF.Tau) & 4092) * (MathF.Tau / 4096);
            angles = new Vector3(PackedAngle(angles.X), PackedAngle(angles.Y), PackedAngle(angles.Z));
            Matrix4x4 local = Matrix4x4.CreateRotationZ(angles.Z) * Matrix4x4.CreateRotationY(angles.Y) * Matrix4x4.CreateRotationX(angles.X);
            int parent = model.Bones[bone].Parent;
            if (parent < 0)
            {
                transforms[bone] = local * virtualRoot;
                transforms[bone].Translation = new Vector3(frame.Translation.X, -frame.Translation.Z, frame.Translation.Y);
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
}
