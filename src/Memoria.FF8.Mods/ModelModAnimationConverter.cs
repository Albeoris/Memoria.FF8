using System.Numerics;
using Memoria.FF8.Assets;

namespace Memoria.FF8.Mods;

/// <summary>Updates gameplay timing and root/control tracks alongside editable render tracks.</summary>
internal static class ModelModAnimationConverter
{
    private static readonly Matrix4x4 ToGltf = Matrix4x4.CreateRotationX(-MathF.PI / 2);
    private static readonly Matrix4x4 ToNative = Matrix4x4.CreateRotationX(MathF.PI / 2);

    public static BattleModel ToBattle(EditableModel edited, BattleModel model)
    {
        EditableModel original = ModelExchange.FromBattle(model with { EditableOverride = null });
        BattleAnimation[] animations = edited.Animations.Select((animation, index) =>
        {
            if (index < model.Animations.Count && Same(animation, original.Animations[index]))
            {
                return model.Animations[index];
            }
            List<BattlePose> frames = [];
            for (int frame = 0; frame < FrameCount(animation, edited.NativeFramesPerSecond); frame++)
            {
                Matrix4x4[] locals = Locals(edited, animation, frame / edited.NativeFramesPerSecond);
                Vector3[] rotations = new Vector3[model.Bones.Count];
                Vector3[] scales = new Vector3[model.Bones.Count];
                Vector3 translation = Vector3.Zero;
                for (int bone = 0; bone < model.Bones.Count; bone++)
                {
                    Matrix4x4 native = ToGltf * locals[Joint(edited, bone)] * ToNative;
                    if (!Matrix4x4.Decompose(native, out scales[bone], out Quaternion rotation, out Vector3 position))
                    {
                        throw new NotSupportedException("An animation transform cannot be decomposed into native joint tracks.");
                    }
                    Matrix4x4 matrix = Matrix4x4.CreateFromQuaternion(rotation);
                    rotations[bone] = new(MathF.Asin(Math.Clamp(matrix.M32, -1, 1)), -MathF.Atan2(matrix.M31, matrix.M33), MathF.Atan2(matrix.M12, matrix.M22));
                    if (model.Bones[bone].Parent < 0)
                    {
                        translation = new Vector3(-position.X, -position.Y, position.Z) / model.Scale;
                    }
                    else if (Vector3.Distance(position, new(0, 0, model.Bones[model.Bones[bone].Parent].Length * model.Scale)) > 0.001f)
                    {
                        throw new NotSupportedException("Native battle clips cannot animate non-root joint translations.");
                    }
                }
                frames.Add(new(translation, rotations, scales));
            }
            return new BattleAnimation(frames);
        }).ToArray();
        return model with { Animations = animations, NativePoseSource = model.NativePoseSource is { } source ? ModelModNativeAnimationWriter.Rewrite(source, model.Animations, animations) : null };
    }

    public static FieldModel ToField(EditableModel edited, FieldModel model)
    {
        EditableModel original = ModelExchange.FromField(model with { EditableOverride = null }, edited.NativeFramesPerSecond);
        ModelAnimation[] animations = edited.Animations.Select((animation, index) =>
        {
            if (index < model.Animations.Count && Same(animation, original.Animations[index]))
            {
                return model.Animations[index];
            }
            List<AnimationFrame> frames = [];
            for (int frame = 0; frame < FrameCount(animation, edited.NativeFramesPerSecond); frame++)
            {
                Matrix4x4[] locals = Locals(edited, animation, frame / edited.NativeFramesPerSecond);
                Vector3[] rotations = new Vector3[model.Bones.Count];
                Vector3 translation = Vector3.Zero;
                for (int bone = 0; bone < model.Bones.Count; bone++)
                {
                    Matrix4x4 native = ToGltf * locals[Joint(edited, bone)] * ToNative;
                    Vector3 position = native.Translation;
                    native.Translation = Vector3.Zero;
                    if (model.Bones[bone].Parent < 0)
                    {
                        translation = position;
                        native *= Matrix4x4.CreateRotationY(-MathF.PI / 2);
                    }
                    else if (Vector3.Distance(position, new(0, 0, model.Bones[model.Bones[bone].Parent].Length)) > 0.001f)
                    {
                        throw new NotSupportedException("Native field clips cannot animate non-root joint translations.");
                    }
                    if (!Matrix4x4.Decompose(native, out Vector3 scale, out Quaternion rotation, out _) || Vector3.Distance(scale, Vector3.One) > 0.001f)
                    {
                        throw new NotSupportedException("Native field clips cannot represent animated scale or shear.");
                    }
                    Matrix4x4 matrix = Matrix4x4.CreateFromQuaternion(rotation);
                    rotations[bone] = new(MathF.Atan2(-matrix.M32, matrix.M33), MathF.Asin(Math.Clamp(matrix.M31, -1, 1)), MathF.Atan2(-matrix.M21, matrix.M11));
                }
                frames.Add(new(translation, rotations));
            }
            return new ModelAnimation(frames);
        }).ToArray();
        return model with { Animations = animations };
    }

    private static int Joint(EditableModel model, int native)
    {
        int[] joints = Enumerable.Range(0, model.Bones.Count).Where(index => model.Bones[index].IsJoint).ToArray();
        int match = Array.FindIndex(joints, index => model.Bones[index].NativeIndex == native || model.Bones[index].Name == $"bone_{native}");
        if (match >= 0)
        {
            return joints[match];
        }
        if (native < joints.Length)
        {
            return joints[native];
        }
        throw new NotSupportedException("The imported animation is missing a native joint.");
    }

    private static int FrameCount(EditableAnimation animation, float framesPerSecond)
    {
        double count = Math.Round(animation.Duration * framesPerSecond) + 1;
        if (count < 1 || count > 65535 || Math.Abs((count - 1) / framesPerSecond - animation.Duration) > 0.001)
        {
            throw new NotSupportedException("Clip duration must align with the target native frame grid and contain at most 65535 frames.");
        }
        return (int)count;
    }

    private static bool Same(EditableAnimation a, EditableAnimation b) => a.Channels.Count == b.Channels.Count && a.Channels.Zip(b.Channels).All(pair => pair.First.Bone == pair.Second.Bone && pair.First.Path == pair.Second.Path && pair.First.Interpolation == pair.Second.Interpolation && pair.First.Times.SequenceEqual(pair.Second.Times) && pair.First.Values.SequenceEqual(pair.Second.Values));

    private static Matrix4x4[] Locals(EditableModel model, EditableAnimation animation, float seconds)
    {
        EditableBone[] bones = model.Bones.ToArray();
        foreach (EditableAnimationChannel channel in animation.Channels)
        {
            Vector4 value = Sample(channel, seconds);
            EditableBone bone = bones[channel.Bone];
            bones[channel.Bone] = channel.Path switch
            {
                "translation" => bone with { Translation = new(value.X, value.Y, value.Z) },
                "rotation" => bone with { Rotation = Quaternion.Normalize(new(value.X, value.Y, value.Z, value.W)) },
                "scale" => bone with { Scale = new(value.X, value.Y, value.Z) },
                _ => throw new NotSupportedException("The clip contains an unsupported animation channel.")
            };
        }
        return bones.Select(bone => Matrix4x4.CreateScale(bone.Scale) * Matrix4x4.CreateFromQuaternion(bone.Rotation) * Matrix4x4.CreateTranslation(bone.Translation)).ToArray();
    }

    private static Vector4 Sample(EditableAnimationChannel channel, float seconds)
    {
        int previous = 0;
        while (previous + 1 < channel.Times.Length && channel.Times[previous + 1] <= seconds)
        {
            previous++;
        }
        int next = Math.Min(previous + 1, channel.Times.Length - 1);
        float t = next == previous ? 0 : Math.Clamp((seconds - channel.Times[previous]) / (channel.Times[next] - channel.Times[previous]), 0, 1);
        if (channel.Interpolation == "STEP")
        {
            return channel.Values[previous];
        }
        if (channel.Interpolation == "CUBICSPLINE")
        {
            float duration = channel.Times[next] - channel.Times[previous];
            float square = t * t;
            float cube = square * t;
            return (2 * cube - 3 * square + 1) * channel.Values[previous * 3 + 1] + (cube - 2 * square + t) * duration * channel.Values[previous * 3 + 2] + (-2 * cube + 3 * square) * channel.Values[next * 3 + 1] + (cube - square) * duration * channel.Values[next * 3];
        }
        if (channel.Path == "rotation")
        {
            Vector4 a = channel.Values[previous];
            Vector4 b = channel.Values[next];
            Quaternion rotation = Quaternion.Slerp(new(a.X, a.Y, a.Z, a.W), new(b.X, b.Y, b.Z, b.W), t);
            return new(rotation.X, rotation.Y, rotation.Z, rotation.W);
        }
        return Vector4.Lerp(channel.Values[previous], channel.Values[next], t);
    }
}
