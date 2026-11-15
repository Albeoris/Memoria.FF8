using System.Numerics;
using System.Globalization;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;

namespace Memoria.FF8.Rendering;

public sealed record Ff7WorldModelTriangle(Ff7WorldVertex A, Ff7WorldVertex B, Ff7WorldVertex C, TriangleMaterial Material, bool Cull);

/// <summary>Original world character geometry and animation, using the shared pose and material contracts.</summary>
public sealed class Ff7WorldModelAdapter(Ff7WorldCharacterDescriptor descriptor, Ff7FieldModelResources resources)
{
    private readonly Dictionary<PcTexture, PixelImage> images = new(ReferenceEqualityComparer.Instance);

    public IReadOnlyList<Ff7WorldModelTriangle> Create(Vector3 nativePosition, int direction, int animationIndex, int animationFrame, IFf7ActorAppearance? appearance = null, int storyCharacter = -1, Ff7FieldPoseBlend? animationBlend = null, Vector3? nativeTilt = null, int pitchUnits = 256)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(animationIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(animationIndex, descriptor.Animations.Count);
        string animationName = descriptor.Animations[animationIndex];
        PcFieldAnimation animation = resources.Animations[animationName];
        if (appearance is not null)
        {
            PcFieldModelDescriptor fieldDescriptor = new(descriptor.HrcName, 0, descriptor.HrcName, descriptor.ScaleWord.ToString(CultureInfo.InvariantCulture), Array.Empty<PcFieldLight>(), 0, descriptor.Animations);
            Ff7ActorVisual actor = new(0, 0, 0, 0, 0, 0, animationIndex, checked(animationFrame * 16)) { PartyCharacter = storyCharacter, PoseDomain = Ff7ActorPoseDomain.World, AnimationBlend = animationBlend };
            if (appearance.TryCreate(actor, fieldDescriptor, resources, out IReadOnlyList<Ff7AppearanceTriangle> mapped))
            {
                float fieldScale = descriptor.ScaleWord / 4096f * 30.43090057373047f;
                Matrix4x4 transform = Matrix4x4.CreateRotationX(-MathF.PI / 2) * Matrix4x4.CreateScale(descriptor.Scale / fieldScale) * Rotation(direction, nativeTilt ?? Vector3.Zero, pitchUnits) * Matrix4x4.CreateTranslation(nativePosition);
                Ff7WorldVertex Convert(Ff7WorldVertex vertex) => vertex with { Position = Vector3.Transform(vertex.Position, transform), Normal = vertex.Normal is Vector3 normal ? Vector3.TransformNormal(normal, transform) : null };
                return mapped.Select(triangle => new Ff7WorldModelTriangle(Convert(triangle.A), Convert(triangle.B), Convert(triangle.C), triangle.Material with { DepthComparison = TriangleDepthComparison.GreaterEqual, DepthMapping = TriangleDepthMapping.Positive, DepthScale = 1, CentroidTextureSampling = true }, false)).ToArray();
            }
        }
        Matrix4x4 normalBasis = Matrix4x4.CreateRotationX(MathF.PI) * Rotation(direction, nativeTilt ?? Vector3.Zero, pitchUnits);
        Matrix4x4 basis = Matrix4x4.CreateScale(descriptor.Scale) * normalBasis * Matrix4x4.CreateTranslation(nativePosition);
        return CreateOriginal(animation, animationFrame, descriptor.Scale, basis, normalBasis, animationBlend);
    }

    public static Matrix4x4 Rotation(int direction, Vector3 nativeTilt, int pitchUnits = 256)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pitchUnits);
        return Matrix4x4.CreateRotationX(nativeTilt.X * MathF.Tau / pitchUnits) * Matrix4x4.CreateRotationZ(nativeTilt.Z * MathF.Tau / 4096) * Matrix4x4.CreateRotationY((direction + nativeTilt.Y) * MathF.Tau / 4096);
    }

    public IReadOnlyList<Ff7WorldModelTriangle> CreateOriginal(int animationIndex, int animationFrame, float scale, Matrix4x4 basis, Matrix4x4 normalBasis)
    {
        return CreateOriginal(resources.Animations[descriptor.Animations[animationIndex]], animationFrame, scale, basis, normalBasis);
    }

    public IReadOnlyList<Ff7WorldModelTriangle> CreateScene(int owner, int storyCharacter, int animationIndex, Ff7FieldPosePhase phase, float scale, Matrix4x4 basis, Matrix4x4 normalBasis, IFf7ActorAppearance? appearance)
    {
        if (appearance is not null && storyCharacter >= 0)
        {
            PcFieldModelDescriptor fieldDescriptor = new(descriptor.HrcName, 0, descriptor.HrcName, descriptor.ScaleWord.ToString(CultureInfo.InvariantCulture), [], 0, descriptor.Animations);
            Ff7ActorVisual actor = new(owner, owner, 0, 0, 0, 0, animationIndex, phase.Frame16) { PartyCharacter = storyCharacter, PoseDomain = Ff7ActorPoseDomain.Scene, FirstAnimationFrame = phase.FirstFrame, LastAnimationFrame = phase.LastFrame, AnimationLoop = phase.Loop, AnimationBlend = phase.Blend };
            if (appearance.TryCreate(actor, fieldDescriptor, resources, out IReadOnlyList<Ff7AppearanceTriangle> mapped))
            {
                if (!Matrix4x4.Invert(Ff7NativeFieldPoseMath.ModelBasis(fieldDescriptor), out Matrix4x4 inverseFieldBasis))
                {
                    throw new InvalidDataException("Mapped native scene geometry requires an invertible original model basis.");
                }
                Matrix4x4 transform = inverseFieldBasis * basis;
                Ff7WorldVertex Convert(Ff7WorldVertex vertex) => vertex with { Position = Vector3.Transform(vertex.Position, transform), Normal = vertex.Normal is Vector3 normal ? Vector3.TransformNormal(normal, transform) : null };
                return mapped.Select(triangle => new Ff7WorldModelTriangle(Convert(triangle.A), Convert(triangle.B), Convert(triangle.C), triangle.Material with { DepthComparison = TriangleDepthComparison.GreaterEqual, DepthMapping = TriangleDepthMapping.Positive, DepthScale = 1, CentroidTextureSampling = true }, false)).ToArray();
            }
        }
        return CreateOriginal(resources.Animations[descriptor.Animations[animationIndex]], 0, scale, basis, normalBasis, phase: phase);
    }

    private IReadOnlyList<Ff7WorldModelTriangle> CreateOriginal(PcFieldAnimation animation, int animationFrame, float scale, Matrix4x4 basis, Matrix4x4 normalBasis, Ff7FieldPoseBlend? animationBlend = null, Ff7FieldPosePhase? phase = null)
    {
        IReadOnlyList<Matrix4x4> pose = Ff7FieldPoseSampler.Sample(resources, animation, phase ?? new(checked(animationFrame * 16)) { Blend = animationBlend }, scale).Bones;
        Ff7ModelMaterialState materialState = new() { Blend = descriptor.Transparent ? TriangleBlend.Alpha : TriangleBlend.Replace, BlendMode = descriptor.Transparent ? 0 : 4, DepthWrite = !descriptor.Transparent };
        List<Ff7WorldModelTriangle> triangles = new();
        foreach (Ff7BoneGeometry part in resources.Geometry)
        {
            PcPolygonModel model = part.Polygon;
            Matrix4x4 transform = pose[part.BoneIndex] * basis;
            Matrix4x4 normalTransform = pose[part.BoneIndex] * normalBasis;
            if (model.RenderStates.Count != model.Groups.Count)
            {
                throw new InvalidDataException("FFVII world model groups require their original material states.");
            }
            for (int groupIndex = 0; groupIndex < model.Groups.Count; groupIndex++)
            {
                PcModelGroup group = model.Groups[groupIndex];
                PcRenderState state = PcRenderState.Read(model.RenderStates[groupIndex].Span);
                materialState.Apply(state);
                PixelImage? image = null;
                if (group.TexturesUsed != 0)
                {
                    PcTexture texture = part.Textures[group.TextureNumber];
                    if (!images.TryGetValue(texture, out image))
                    {
                        image = Ff7FieldRenderer.DecodeTexture(texture);
                        images.Add(texture, image);
                    }
                }
                int[] normals = Ff7FieldLighting.BindNormals(model, group);
                TriangleMaterial material = new(image, materialState.Blend, materialState.DepthTest ? TriangleDepthComparison.GreaterEqual : TriangleDepthComparison.Always, materialState.DepthWrite, materialState.AlphaReference, materialState.Sampler)
                {
                    DepthMapping = TriangleDepthMapping.Positive,
                    DepthScale = 1,
                    CentroidTextureSampling = true,
                    PreserveTargetAlpha = false
                };
                Ff7WorldVertex Vertex(int local)
                {
                    int index = group.VertexStart + local;
                    uint color = index < model.VertexColors.Count ? model.VertexColors[index] : uint.MaxValue;
                    int red = (int)(color >> 16 & 255);
                    int green = (int)(color >> 8 & 255);
                    int blue = (int)(color & 255);
                    if (descriptor.ColorOffset is { } offset)
                    {
                        red = Math.Clamp(red + offset.Red, 0, 255);
                        green = Math.Clamp(green + offset.Green, 0, 255);
                        blue = Math.Clamp(blue + offset.Blue, 0, 255);
                    }
                    float vertexAlpha = descriptor.Transparent ? 128 : state.VertexAlpha;
                    Vector4 rgba = new(red / 255f, green / 255f, blue / 255f, (color >> 24) / 255f * vertexAlpha / 255f);
                    Vector3? normal = normals[local] < 0 ? null : Vector3.TransformNormal(model.Normals[normals[local]], normalTransform);
                    return new(Vector3.Transform(model.Vertices[index], transform), image is null ? Vector2.Zero : model.TextureCoordinates[group.TextureCoordinateStart + local], rgba) { Normal = normal };
                }
                for (int index = group.PolygonStart; index < group.PolygonStart + group.PolygonCount; index++)
                {
                    PcModelPolygon polygon = model.Polygons[index];
                    Ff7WorldVertex a = Vertex(polygon.A);
                    Ff7WorldVertex b = Vertex(polygon.B);
                    Ff7WorldVertex c = Vertex(polygon.C);
                    if (!materialState.Gouraud)
                    {
                        b = b with { Color = a.Color, Normal = a.Normal };
                        c = c with { Color = a.Color, Normal = a.Normal };
                    }
                    triangles.Add(new(a, b, c, material, materialState.Cull));
                }
            }
        }
        return triangles.AsReadOnly();
    }

    public static int Draw(ITriangleRenderer renderer, IReadOnlyList<Ff7WorldModelTriangle> triangles, Ff7WorldView view, bool underwater = false, int nativeColorWeight = 255)
    {
        int submitted = 0;
        foreach (Ff7WorldModelTriangle triangle in triangles)
        {
            CameraSpaceVertex Vertex(Ff7WorldVertex vertex)
            {
                Vector4 color = vertex.Normal is Vector3 normal ? Ff7WorldLighting.Character(Vector3.TransformNormal(normal, view.NativeRotation), vertex.Color, underwater) : vertex.Color;
                if (nativeColorWeight != 255)
                {
                    color = Ff7WorldLighting.BlendModelColor(color, nativeColorWeight);
                }
                return new(view.ToCamera(vertex.Position), vertex.Uv, color);
            }
            submitted += CameraSpaceTriangleSubmission.Draw(renderer, Vertex(triangle.A), Vertex(triangle.B), Vertex(triangle.C), view.Width, view.Height, view.Focal, triangle.Material, triangle.Cull);
        }
        return submitted;
    }
}
