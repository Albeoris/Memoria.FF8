using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Runtime;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Assets;

public readonly record struct ModelBone(int Parent, float Length);
public readonly record struct ModelVertex(Vector3 Position, int Bone, int SkinBinding = -1);
public sealed record ModelFace(int[] Vertices, Vector2[] TextureCoordinates, int Texture, bool Quad)
{
    public int NativeTexture { get; init; } = -1;
    public Vector4[]? VertexColors { get; init; }
    public bool SemiTransparent { get; init; }
}
public sealed record AnimationFrame(Vector3 Translation, IReadOnlyList<Vector3> Rotations);
public sealed record ModelAnimation(IReadOnlyList<AnimationFrame> Frames);
public sealed record ModelControlBones(int First, int Second, int Additional, uint RawHeader);
/// <summary>Render samples use unwrapped animation phases and an exclusive terminal frame.</summary>
public readonly record struct FieldAnimationBlend(float PreviousFrame, float CurrentFrame, float Fraction, int FirstFrame = 0, int? TerminalFrame = null, bool Loop = false);
public sealed record FieldModel(string Name, IReadOnlyList<ModelBone> Bones, IReadOnlyList<ModelVertex> Vertices, IReadOnlyList<ModelFace> Faces, IReadOnlyList<PixelImage> Textures, IReadOnlyList<ModelAnimation> Animations)
{
    public ResourceSourceDescriptor? TextureSourceDescriptor { get; init; }
    public EditableModel? EditableOverride { get; init; }
    public IReadOnlyList<FieldSpringBoneJoint> SpringBones { get; init; } = [];
    /// <summary>The native main-character library entry that supplies this model's geometry.</summary>
    public string? MainCharacterSourceId { get; init; }
    internal FieldModel? NativeTextureSource { get; init; }
    public IReadOnlyList<PixelImage> NativeTextures => NativeTextureSource?.Textures ?? Textures;
    public IReadOnlyList<TextureSourceRegion> TextureRegions { get; init; } = [];
    public string? TextureResourceName { get; init; }
    public bool MainCharacterTextures { get; init; }
    public string? TextureSourceVariant { get; init; }

    public NativeModelPartsDefinition? NativeParts { get; init; }
    public NativeModelPartsDefinition? NativeRegistrationParts { get; init; }
    public NativeFieldEyeDefinition? NativeEyes { get; init; }

    public ModelControlBones ControlBones { get; init; } = new(-1, -1, -1, 0);

    public (PixelImage Image, Vector2 Scale) ResolveFaceTexture(ModelFace face, IReadOnlyList<PixelImage>? registeredTextures = null)
    {
        if (NativeTextureSource is null)
        {
            IReadOnlyList<PixelImage> textures = registeredTextures ?? Textures;
            return (textures[face.Texture], ResourceTextureList.PhysicalScale(textures, face.Texture));
        }
        // Imported UVs are relative to the embedded image canvas. Native replacements can have a different resolution.
        if ((registeredTextures ?? NativeTextures) is ResourceTextureList native && (uint)face.NativeTexture < native.Count && native.GetTexture(face.NativeTexture) is { IsModReplacement: true } replacement)
        {
            return (replacement.Image, new Vector2(replacement.Image.Width, replacement.Image.Height) / ResourceTextureList.LogicalSize(Textures, face.Texture));
        }
        return (Textures[face.Texture], ResourceTextureList.PhysicalScale(Textures, face.Texture));
    }

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
        if (EditableOverride is { } edited)
        {
            return ModelExchange.Pose(edited, animationId, frameNumber / edited.NativeFramesPerSecond).SelectMany(mesh => mesh).Select(position => ModelExchange.ToNative(position) + new Vector3(0, 0, rootTranslationZ)).ToArray();
        }
        Matrix4x4[] transforms = BuildTransforms(animationId, frameNumber, firstControlRotation, rootTranslationZ);
        return TransformVertices(transforms, skinTranslations);
    }

    /// <summary>Native global bone transforms for a presentation adapter. This does not mutate animation state.</summary>
    public IReadOnlyList<Matrix4x4> ReadBoneTransforms(int animationId, float frameNumber) => Array.AsReadOnly(BuildTransforms(animationId, frameNumber, null, 0));

    /// <summary>Poses original rigid vertices with adapter-supplied global transforms while retaining native skin ownership.</summary>
    public Vector3[] PoseWithBoneTransforms(IReadOnlyList<Matrix4x4> transforms)
    {
        if (transforms.Count != Bones.Count)
        {
            throw new InvalidDataException("Presentation bone transforms must retain the complete original indexed rig.");
        }
        return TransformVertices(transforms.ToArray(), null);
    }

    /// <summary>Blends two committed animation samples for presentation without advancing animation state.</summary>
    /// <remarks>Supply unwrapped phases across a loop boundary. The terminal frame is exclusive.</remarks>
    public Vector3[] PoseInterpolated(int animationId, float previousFrameNumber, float currentFrameNumber, float blend, Vector3? firstControlRotation = null, float rootTranslationZ = 0, IReadOnlyDictionary<int, Vector3>? skinTranslations = null, int firstFrame = 0, int? terminalFrame = null, bool loop = false)
    {
        if (!float.IsFinite(blend) || blend < 0 || blend > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(blend));
        }
        if (!float.IsFinite(previousFrameNumber) || !float.IsFinite(currentFrameNumber))
        {
            throw new ArgumentOutOfRangeException(nameof(previousFrameNumber));
        }
        if (EditableOverride is not null)
        {
            return Pose(animationId, float.Lerp(previousFrameNumber, currentFrameNumber, blend), firstControlRotation, rootTranslationZ, skinTranslations);
        }
        return TransformVertices(BuildInterpolatedTransforms(animationId, previousFrameNumber, currentFrameNumber, blend, firstControlRotation, rootTranslationZ, firstFrame, terminalFrame, loop), skinTranslations);
    }

    public int[] GetSpringBoneParents() => EditableOverride is { } edited ? edited.Bones.Select(bone => bone.Parent).ToArray() : Bones.Select(bone => bone.Parent).ToArray();

    public Matrix4x4[] ReadPresentationBoneTransforms(int animationId, float frameNumber, Vector3? firstControlRotation, float rootTranslationZ, FieldAnimationBlend? blend)
    {
        if (EditableOverride is { } edited)
        {
            float phase = blend is { } sample ? float.Lerp(sample.PreviousFrame, sample.CurrentFrame, sample.Fraction) : frameNumber;
            Matrix4x4[] transforms = ModelExchange.SampleTransforms(edited, animationId, phase / edited.NativeFramesPerSecond);
            for (int bone = 0; bone < transforms.Length; bone++)
            {
                transforms[bone] = ModelExchange.ToNativeTransform(transforms[bone]);
                transforms[bone].Translation += new Vector3(0, 0, rootTranslationZ);
            }
            return transforms;
        }
        return blend is { } interpolation
            ? BuildInterpolatedTransforms(animationId, interpolation.PreviousFrame, interpolation.CurrentFrame, interpolation.Fraction, firstControlRotation, rootTranslationZ, interpolation.FirstFrame, interpolation.TerminalFrame, interpolation.Loop)
            : BuildTransforms(animationId, frameNumber, firstControlRotation, rootTranslationZ);
    }

    public Vector3[] PosePresentationBones(Matrix4x4[] transforms, IReadOnlyDictionary<int, Vector3>? skinTranslations)
    {
        if (EditableOverride is { } edited)
        {
            return ModelExchange.PoseWithTransforms(edited, transforms.Select(ModelExchange.ConvertTransform).ToArray()).SelectMany(mesh => mesh).Select(ModelExchange.ToNative).ToArray();
        }
        return TransformVertices(transforms, skinTranslations);
    }

    public Vector3[]? PosePresentationNormals(Matrix4x4[] transforms)
    {
        return EditableOverride is { } edited ? ModelExchange.PoseNativeNormals(edited, transforms.Select(ModelExchange.ConvertTransform).ToArray()) : null;
    }

    private Matrix4x4[] BuildInterpolatedTransforms(int animationId, float previousFrameNumber, float currentFrameNumber, float blend, Vector3? firstControlRotation, float rootTranslationZ, int firstFrame, int? terminalFrame, bool loop)
    {
        if ((uint)animationId >= Animations.Count)
        {
            throw new InvalidDataException($"{Name}: unsupported animation {animationId}.");
        }
        int end = terminalFrame ?? Animations[animationId].Frames.Count;
        if (firstFrame < 0 || end <= firstFrame || end > Animations[animationId].Frames.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(terminalFrame));
        }
        float phase = blend == 0 ? previousFrameNumber : blend == 1 ? currentFrameNumber : float.Lerp(previousFrameNumber, currentFrameNumber, blend);
        if (loop)
        {
            int length = end - firstFrame;
            phase = firstFrame + ((phase - firstFrame) % length + length) % length;
        }
        else
        {
            phase = Math.Clamp(phase, firstFrame, end - 1);
        }
        int previousIndex = (int)MathF.Floor(phase);
        float fraction = phase - previousIndex;
        int nextIndex = previousIndex + 1;
        if (nextIndex >= end)
        {
            nextIndex = loop ? firstFrame : previousIndex;
        }
        if (fraction == 0 || previousIndex == nextIndex)
        {
            return BuildTransforms(ReadFrame(animationId, previousIndex), firstControlRotation, rootTranslationZ, presentation: true);
        }
        AnimationFrame previousFrame = ReadFrame(animationId, previousIndex);
        AnimationFrame currentFrame = ReadFrame(animationId, nextIndex);
        return BuildTransforms(previousFrame, firstControlRotation, rootTranslationZ, currentFrame, fraction, presentation: true);
    }

    private Vector3[] TransformVertices(Matrix4x4[] transforms, IReadOnlyDictionary<int, Vector3>? skinTranslations)
    {
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
        return BuildTransforms(frame, firstControlRotation, rootTranslationZ);
    }

    private Matrix4x4[] BuildTransforms(AnimationFrame frame, Vector3? firstControlRotation, float rootTranslationZ, AnimationFrame? nextFrame = null, float blend = 0, bool presentation = false)
    {
        int controlBone = firstControlRotation.HasValue ? FirstControlBone() : -1;
        Matrix4x4[] transforms = new Matrix4x4[Bones.Count];
        for (int bone = 0; bone < transforms.Length; bone++)
        {
            Vector3 angles = bone == controlBone ? firstControlRotation!.Value : frame.Rotations[bone];
            // System.Numerics uses row vectors: transpose the original column-vector Rx*Ry*Rz order.
            Matrix4x4 local = Matrix4x4.CreateRotationZ(angles.Z) * Matrix4x4.CreateRotationY(angles.Y) * Matrix4x4.CreateRotationX(angles.X);
            if (nextFrame is not null && bone != controlBone)
            {
                Vector3 nextAngles = nextFrame.Rotations[bone];
                Matrix4x4 nextLocal = Matrix4x4.CreateRotationZ(nextAngles.Z) * Matrix4x4.CreateRotationY(nextAngles.Y) * Matrix4x4.CreateRotationX(nextAngles.X);
                local = PoseInterpolation.Rotation(local, nextLocal, blend);
            }
            int parent = Bones[bone].Parent;
            if (parent < 0)
            {
                transforms[bone] = local * Matrix4x4.CreateRotationY(MathF.PI / 2);
                Vector3 translation = RootTranslation(frame, rootTranslationZ, presentation);
                if (nextFrame is not null)
                {
                    translation = Vector3.Lerp(translation, RootTranslation(nextFrame, rootTranslationZ, presentation), blend);
                }
                transforms[bone].Translation = translation;
            }
            else
            {
                local.Translation = new Vector3(0, 0, Bones[parent].Length);
                transforms[bone] = local * transforms[parent];
            }
        }
        return transforms;
    }

    private static Vector3 RootTranslation(AnimationFrame frame, float rootTranslationZ, bool presentation = false)
    {
        float translationZ = frame.Translation.Z + rootTranslationZ;
        if (frame.Translation.Z >= short.MinValue && frame.Translation.Z <= short.MaxValue && frame.Translation.Z == MathF.Truncate(frame.Translation.Z) && rootTranslationZ >= short.MinValue && rootTranslationZ <= short.MaxValue && rootTranslationZ == MathF.Truncate(rootTranslationZ))
        {
            // Native 0x533D59 adds backend WORD+98 before the root Z is read as a signed WORD.
            translationZ = unchecked((short)((int)frame.Translation.Z + (int)rootTranslationZ));
        }
        else if (presentation && frame.Translation.Z >= short.MinValue && frame.Translation.Z <= short.MaxValue && frame.Translation.Z == MathF.Truncate(frame.Translation.Z) && rootTranslationZ >= short.MinValue && rootTranslationZ <= short.MaxValue)
        {
            translationZ -= MathF.Floor((translationZ - short.MinValue) / 65536) * 65536;
        }
        return new Vector3(frame.Translation.X, frame.Translation.Y, translationZ);
    }
}
