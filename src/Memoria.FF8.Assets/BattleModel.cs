using System.Numerics;
using Albeoris.Games.FF8.Imaging;

namespace Memoria.FF8.Assets;

public readonly record struct BattleVertex(Vector3 Position, int Bone);
public sealed record BattleFace(int[] Vertices, Vector2[] Uv, int Texture, Rgba32 Color);
public sealed record BattleMesh(IReadOnlyList<BattleVertex> Vertices, IReadOnlyList<BattleFace> Faces);
public sealed record BattleBone(int Parent, float Length);
public sealed record BattlePose(Vector3 Translation, IReadOnlyList<Vector3> Rotations, IReadOnlyList<Vector3> Scales);
public sealed record BattleAnimation(IReadOnlyList<BattlePose> Frames);
public sealed record BattleModel(string Name, IReadOnlyList<BattleBone> Bones, IReadOnlyList<BattleMesh> Meshes, IReadOnlyList<PixelImage> Textures, IReadOnlyList<BattleAnimation> Animations, float Scale)
{
    public BattleSequenceSet? Sequences { get; init; }

    public IReadOnlyList<Vector3[]> Pose(int animation, float frame, bool loop = true)
    {
        if ((uint)animation >= Animations.Count)
        {
            throw new NotSupportedException($"Battle model {Name} has no animation {animation}.");
        }
        IReadOnlyList<BattlePose> frames = Animations[animation].Frames;
        float position = loop ? Math.Max(0, frame) : Math.Clamp(frame, 0, frames.Count - 1);
        int current = (int)MathF.Floor(position) % frames.Count;
        int next = loop ? (current + 1) % frames.Count : Math.Min(current + 1, frames.Count - 1);
        float blend = position - MathF.Floor(position);
        Matrix4x4[] transforms = Transforms(frames[current]);
        Matrix4x4[] nextTransforms = Transforms(frames[next]);
        return Meshes.Select(mesh => mesh.Vertices.Select(vertex => Vector3.Lerp(Vector3.Transform(vertex.Position, transforms[vertex.Bone]), Vector3.Transform(vertex.Position, nextTransforms[vertex.Bone]), blend) * Scale).ToArray()).ToArray();
    }

    private Matrix4x4[] Transforms(BattlePose pose)
    {
        Matrix4x4[] result = new Matrix4x4[Bones.Count];
        for (int bone = 0; bone < result.Length; bone++)
        {
            Vector3 angles = pose.Rotations[bone];
            Matrix4x4 local = Matrix4x4.CreateScale(pose.Scales[bone]) * Matrix4x4.CreateRotationZ(angles.Z) * Matrix4x4.CreateRotationX(-angles.X) * Matrix4x4.CreateRotationY(-angles.Y);
            int parent = Bones[bone].Parent;
            if (parent < 0)
            {
                local.Translation = new Vector3(-pose.Translation.X, -pose.Translation.Y, pose.Translation.Z);
                result[bone] = local;
            }
            else
            {
                local.Translation = new Vector3(0, 0, Bones[parent].Length);
                result[bone] = local * result[parent];
            }
        }
        return result;
    }
}

public sealed record BattleStageCamera(Vector3 NativeEye, Vector3 NativeTarget, int FocalLength);
public sealed record BattleStage(int Id, IReadOnlyList<BattleMesh> Meshes, IReadOnlyList<PixelImage> Textures)
{
    public IReadOnlyDictionary<int, BattleStageCamera> IntroductionCameras { get; init; } = new Dictionary<int, BattleStageCamera>();
}
public sealed record BattleEnemyPlacement(int Slot, int ModelId, Vector3 Position, int Level, bool Hidden, bool Untargetable)
{
    public int NativeModelId => ModelId + 16;
    public bool IsEnemyModel => NativeModelId >= 16;
    public bool Enabled { get; init; } = true;
    public bool InitiallySuspended { get; init; }
}
public sealed record BattleEncounter(int Id, int StageId, byte Flags, byte PrimaryCamera, byte AlternativeCamera, IReadOnlyList<BattleEnemyPlacement> Enemies)
{
    public IReadOnlyList<BattleEnemyPlacement> Slots { get; init; } = Enemies;
}



