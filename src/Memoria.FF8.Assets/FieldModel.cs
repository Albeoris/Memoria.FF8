using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public readonly record struct ModelBone(int Parent, float Length);
public readonly record struct ModelVertex(Vector3 Position, int Bone, int SkinBinding = -1);
public sealed record ModelFace(int[] Vertices, Vector2[] TextureCoordinates, int Texture, bool Quad)
{
    public bool SemiTransparent { get; init; }
}
public sealed record AnimationFrame(Vector3 Translation, IReadOnlyList<Vector3> Rotations);
public sealed record ModelAnimation(IReadOnlyList<AnimationFrame> Frames);
public sealed record ModelControlBones(int First, int Second, int Additional, uint RawHeader);
public sealed record FieldModel(string Name, IReadOnlyList<ModelBone> Bones, IReadOnlyList<ModelVertex> Vertices, IReadOnlyList<ModelFace> Faces, IReadOnlyList<PixelImage> Textures, IReadOnlyList<ModelAnimation> Animations)
{
    public NativeModelPartsDefinition? NativeParts { get; init; }
    public NativeFieldEyeDefinition? NativeEyes { get; init; }

    public ModelControlBones ControlBones { get; init; } = new(-1, -1, -1, 0);

    public Vector3 ReadFirstControlRotation(int animationId, float frameNumber)
    {
        int bone = FirstControlBone();
        return ReadFrame(animationId, frameNumber).Rotations[bone];
    }

    public Vector3 GetFirstControlBoneAnchor(int animationId, float frameNumber, float rootTranslationZ = 0, Vector3? firstControlRotation = null)
    {
        int bone = FirstControlBone();
        return BuildTransforms(animationId, frameNumber, firstControlRotation, rootTranslationZ)[bone].Translation;
    }

    public float GetFirstControlBoneHeight(int animationId, float frameNumber, float rootTranslationZ = 0, Vector3? firstControlRotation = null)
    {
        return GetFirstControlBoneAnchor(animationId, frameNumber, rootTranslationZ, firstControlRotation).Z;
    }

    public Vector3[] Pose(int animationId, float frameNumber, Vector3? firstControlRotation = null, float rootTranslationZ = 0, IReadOnlyDictionary<int, Vector3>? skinTranslations = null)
    {
        Matrix4x4[] transforms = BuildTransforms(animationId, frameNumber, firstControlRotation, rootTranslationZ);
        Vector3[] positions = new Vector3[Vertices.Count];
        for (int i = 0; i < positions.Length; i++)
        {
            ModelVertex vertex = Vertices[i];
            positions[i] = Vector3.Transform(vertex.Position, transforms[vertex.Bone]);
            if (skinTranslations is not null && skinTranslations.TryGetValue(vertex.SkinBinding, out Vector3 translation))
            {
                positions[i] += Vector3.TransformNormal(translation, transforms[vertex.Bone]);
            }
        }
        return positions;
    }

    private int FirstControlBone()
    {
        if ((uint)ControlBones.First >= Bones.Count)
        {
            throw new InvalidDataException($"{Name}: declared first control bone {ControlBones.First} is absent.");
        }
        return ControlBones.First;
    }

    private AnimationFrame ReadFrame(int animationId, float frameNumber)
    {
        if ((uint)animationId >= Animations.Count)
        {
            throw new InvalidDataException($"{Name}: unsupported animation {animationId}.");
        }
        ModelAnimation animation = Animations[animationId];
        int index = Math.Clamp((int)frameNumber, 0, animation.Frames.Count - 1);
        return animation.Frames[index];
    }

    private Matrix4x4[] BuildTransforms(int animationId, float frameNumber, Vector3? firstControlRotation, float rootTranslationZ)
    {
        AnimationFrame frame = ReadFrame(animationId, frameNumber);
        int controlBone = firstControlRotation.HasValue ? FirstControlBone() : -1;
        Matrix4x4[] transforms = new Matrix4x4[Bones.Count];
        for (int bone = 0; bone < transforms.Length; bone++)
        {
            Vector3 angles = bone == controlBone ? firstControlRotation!.Value : frame.Rotations[bone];
            // System.Numerics uses row vectors: transpose the original column-vector Rx*Ry*Rz order.
            Matrix4x4 local = Matrix4x4.CreateRotationZ(angles.Z) * Matrix4x4.CreateRotationY(angles.Y) * Matrix4x4.CreateRotationX(angles.X);
            int parent = Bones[bone].Parent;
            if (parent < 0)
            {
                transforms[bone] = local * Matrix4x4.CreateRotationY(MathF.PI / 2);
                float translationZ = frame.Translation.Z + rootTranslationZ;
                if (frame.Translation.Z >= short.MinValue && frame.Translation.Z <= short.MaxValue && frame.Translation.Z == MathF.Truncate(frame.Translation.Z) && rootTranslationZ >= short.MinValue && rootTranslationZ <= short.MaxValue && rootTranslationZ == MathF.Truncate(rootTranslationZ))
                {
                    // Native 0x533D59 adds backend WORD+98 before the root Z is read as a signed WORD.
                    translationZ = unchecked((short)((int)frame.Translation.Z + (int)rootTranslationZ));
                }
                transforms[bone].Translation = new Vector3(frame.Translation.X, frame.Translation.Y, translationZ);
            }
            else
            {
                local.Translation = new Vector3(0, 0, Bones[parent].Length);
                transforms[bone] = local * transforms[parent];
            }
        }
        return transforms;
    }
}
