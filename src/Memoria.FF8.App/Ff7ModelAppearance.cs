using System.Numerics;
using Memoria.FF7.Formats;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using FieldCharacterCatalog = Memoria.FF8.Assets.FieldCharacterCatalog;
using Ff7FieldModelResources = Memoria.FF7.Resources.Ff7FieldModelResources;

namespace Memoria.FF8;

/// <summary>An explicit appearance profile. Bindings affect poses only; FF7 retains frame and event completion ownership.</summary>
internal sealed partial class Ff7ModelAppearance : IFf7ActorAppearance
{
    private readonly Dictionary<string, Ff7ModelAppearance> variants = new(StringComparer.OrdinalIgnoreCase);
    private readonly GameAssets assets;
    private readonly FieldCharacterCatalog catalog;
    private readonly string sourceId;
    private readonly string nativeHrc;
    private readonly Matrix4x4 targetToFf7;
    private float? calibrationDescriptorScale;
    private readonly IReadOnlyDictionary<string, string> clips;
    private readonly FieldModel geometry;
    private readonly Ff7SemanticRetargeter? retargeter;
    private readonly HashSet<string> loopingClips;
    private readonly Dictionary<string, FieldModel> loadedClips = new(StringComparer.Ordinal);

    public Ff7ModelAppearance(GameAssets assets, FieldCharacterCatalog catalog, string sourceId, string nativeHrc, Matrix4x4 targetToFf7, IReadOnlyDictionary<string, string> animationBindings, Ff7SemanticRetargeter? retargeter = null, IEnumerable<string>? loopingAnimations = null)
    {
        this.assets = assets ?? throw new ArgumentNullException(nameof(assets));
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.sourceId = sourceId;
        this.nativeHrc = nativeHrc;
        this.targetToFf7 = targetToFf7;
        this.retargeter = retargeter;
        loopingClips = new(loopingAnimations ?? [], StringComparer.OrdinalIgnoreCase);
        clips = new Dictionary<string, string>(animationBindings, StringComparer.OrdinalIgnoreCase);
        if (!Matrix4x4.Invert(targetToFf7, out _) || !Finite(targetToFf7))
        {
            throw new ArgumentException("A field appearance requires an explicit finite invertible source-to-FF7 basis/scale calibration.", nameof(targetToFf7));
        }
        FieldCharacterSource source = catalog.GetSource(sourceId);
        HashSet<string> compatible = catalog.CompatibleAnimations(sourceId).Select(animation => animation.Id).ToHashSet(StringComparer.Ordinal);
        if (clips.Values.Any(identity => !compatible.Contains(identity)))
        {
            throw new InvalidDataException("Appearance bindings must select witnessed compatible target field animations.");
        }
        geometry = assets.LoadFieldModelSource(source.Entry);
    }

    public bool TryCreate(Ff7ActorVisual actor, PcFieldModelDescriptor descriptor, Ff7FieldModelResources original, out IReadOnlyList<Ff7AppearanceTriangle> triangles)
    {
        triangles = [];
        if (!Path.GetFileName(descriptor.Hrc).Equals(nativeHrc, StringComparison.OrdinalIgnoreCase))
        {
            if (variants.TryGetValue(Path.GetFileName(descriptor.Hrc), out Ff7ModelAppearance? variant))
            {
                return variant.TryCreate(actor, descriptor, original, out triangles);
            }
            if (actor.PartyCharacter == 0)
            {
                throw new NotSupportedException("The story Cloud actor requires an explicit compatible source rig in the selected appearance profile.");
            }
            return false;
        }
        if ((uint)actor.AnimationIndex >= descriptor.Animations.Count || actor.AnimationFrame16 < 0)
        {
            throw new InvalidDataException("Mapped FF7 actor has an invalid native clip/phase.");
        }
        string originalName = Path.GetFileNameWithoutExtension(descriptor.Animations[actor.AnimationIndex]) + ".a";
        PcFieldAnimation sourceClip = original.Animations[originalName];
        float sourceFrame = actor.AnimationFrame16 / 16f;
        if (sourceFrame >= sourceClip.Frames.Count)
        {
            throw new InvalidDataException("Mapped FF7 phase lies beyond its original clip; the simulation must own looping/completion.");
        }
        retargeter?.ValidateSource(original);
        FieldModel target = geometry;
        Vector3[] posed;
        if (clips.TryGetValue(originalName, out string? targetIdentity))
        {
            if (!loadedClips.TryGetValue(targetIdentity, out FieldModel? selected))
            {
                ModelAnimation animation = assets.LoadFieldCharacterAnimation(catalog, sourceId, targetIdentity);
                selected = geometry with { Animations = new[] { animation } };
                loadedClips.Add(targetIdentity, selected);
            }
            target = selected;
            int targetFrames = target.Animations[0].Frames.Count;
            bool loop = loopingClips.Contains(originalName);
            float targetFrame = MapAnimationPhase(sourceFrame, sourceClip.Frames.Count, targetFrames, loop);
            posed = target.PoseInterpolated(0, targetFrame, targetFrame, 0, loop: loop);
        }
        else if (retargeter is not null)
        {
            posed = retargeter.Pose(original, sourceClip, actor.AnimationFrame16);
        }
        else
        {
            throw new NotSupportedException($"FF7 appearance motion '{originalName}' has no admitted target field animation binding or semantic retarget profile.");
        }
        float descriptorRatio = 1;
        if (calibrationDescriptorScale is float referenceScale)
        {
            descriptorRatio = float.Parse(descriptor.Scale, System.Globalization.CultureInfo.InvariantCulture) / referenceScale;
            if (!float.IsFinite(descriptorRatio) || descriptorRatio <= 0)
            {
                throw new InvalidDataException("Mapped FF7 descriptor scale must remain finite and positive.");
            }
        }
        Matrix4x4 world = targetToFf7 * Matrix4x4.CreateScale(descriptorRatio) * Matrix4x4.CreateRotationZ(actor.Direction * MathF.Tau / 256) * Matrix4x4.CreateTranslation(actor.X4096 / 4096f, actor.Y4096 / 4096f, actor.Z4096 / 4096f);
        List<Ff7AppearanceTriangle> output = [];
        foreach (ModelFace face in target.Faces)
        {
            if ((uint)face.Texture >= target.Textures.Count)
            {
                throw new InvalidDataException("Mapped target field face references an absent texture.");
            }
            var image = target.Textures[face.Texture];
            Ff7WorldVertex Vertex(int corner)
            {
                Vector2 uv = face.TextureCoordinates[corner] / ResourceTextureList.LogicalSize(target.Textures, face.Texture);
                return new(Vector3.Transform(posed[face.Vertices[corner]], world), uv, face.VertexColors?[corner] ?? Vector4.One);
            }
            TriangleMaterial material = new(image, face.SemiTransparent ? TriangleBlend.HalfAdd : TriangleBlend.Replace, TriangleDepthComparison.LessEqual, true, Sampler: TriangleSampler.PointClamp) { PreserveTargetAlpha = false };
            AddTriangle(Vertex(0), Vertex(1), Vertex(2), material);
            if (face.Quad)
            {
                AddTriangle(Vertex(1), Vertex(3), Vertex(2), material);
            }
        }
        // Preserve the target field appearance: MCH geometry has no normal channel and its textures/vertex colors already own its shading.
        void AddTriangle(Ff7WorldVertex a, Ff7WorldVertex b, Ff7WorldVertex c, TriangleMaterial material)
        {
            Vector3 normal = Vector3.Cross(b.Position - a.Position, c.Position - a.Position);
            if (normal.LengthSquared() == 0)
            {
                return;
            }
            output.Add(new(a, b, c, material));
        }
        triangles = output.AsReadOnly();
        return true;
    }

    /// <summary>Explicit appearance policy: match the original neutral visible height and foot anchor, retaining the caller's admitted target basis.</summary>
    public static Matrix4x4 MatchOriginalNeutralBounds(FieldModel target, PcFieldModelDescriptor descriptor, Ff7FieldModelResources original, Matrix4x4 targetBasis)
    {
        string idle = Path.GetFileNameWithoutExtension(descriptor.Animations[0]) + ".a";
        IReadOnlyList<Matrix4x4> pose = Ff7FieldRenderer.Pose(original, original.Animations[idle], 0);
        Matrix4x4 originalBasis = Ff7FieldRenderer.ModelBasis(descriptor);
        Vector3[] originalVertices = original.Geometry.SelectMany(part => part.Polygon.Vertices.Select(vertex => Vector3.Transform(vertex, pose[part.BoneIndex] * originalBasis))).ToArray();
        Vector3[] targetVertices = target.Pose(0, 0).Select(vertex => Vector3.Transform(vertex, targetBasis)).ToArray();
        float sourceMin = originalVertices.Min(vertex => vertex.Z);
        float sourceHeight = originalVertices.Max(vertex => vertex.Z) - sourceMin;
        float targetMin = targetVertices.Min(vertex => vertex.Z);
        float targetHeight = targetVertices.Max(vertex => vertex.Z) - targetMin;
        float ratio = sourceHeight / targetHeight;
        Matrix4x4 calibration = targetBasis * Matrix4x4.CreateScale(ratio) * Matrix4x4.CreateTranslation(0, 0, sourceMin - targetMin * ratio);
        if (!float.IsFinite(ratio) || ratio <= 0 || !Finite(calibration) || !Matrix4x4.Invert(calibration, out _))
        {
            throw new InvalidDataException("Neutral appearance bounds cannot establish a finite height/foot-anchor calibration.");
        }
        return calibration;
    }

    internal static float MapAnimationPhase(float sourceFrame, int sourceFrames, int targetFrames, bool loop)
    {
        if (!float.IsFinite(sourceFrame) || sourceFrame < 0 || sourceFrames <= 0 || targetFrames <= 0 || sourceFrame >= sourceFrames)
        {
            throw new InvalidDataException("Appearance phase must remain within its native source clip.");
        }
        return sourceFrames <= 1 ? 0 : loop ? sourceFrame * targetFrames / sourceFrames : Math.Min(sourceFrame, sourceFrames - 1) * (targetFrames - 1f) / (sourceFrames - 1);
    }

    private static bool Finite(Matrix4x4 matrix) => new[] { matrix.M11, matrix.M12, matrix.M13, matrix.M14, matrix.M21, matrix.M22, matrix.M23, matrix.M24, matrix.M31, matrix.M32, matrix.M33, matrix.M34, matrix.M41, matrix.M42, matrix.M43, matrix.M44 }.All(float.IsFinite);
}
