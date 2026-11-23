using System.Numerics;
using System.Runtime.CompilerServices;
using Memoria.FF8.Assets;

namespace Memoria.FF8.Rendering;

public enum FieldSceneMaterialMode
{
    Unlit,
    UnlitShadowReceiver,
    FieldLit
}

public enum FieldSceneGeometryRole
{
    PhysicalSurface,
    PaintedBackdrop,
    OccluderProxy,
    ReceiverProxy,
    ReferenceOnly
}

public enum FieldSceneRenderAlphaMode
{
    Source,
    Opaque,
    Mask,
    Blend,
    OpaqueCoreAndBlend
}

public enum FieldSceneFrameFit
{
    Contain,
    Crop,
    Expand
}

public sealed record FieldSceneRenderMaterial(FieldSceneMaterialMode Mode, bool CastShadows = false, bool ReceiveShadows = false, float? ShadowAlphaCutoff = null)
{
    public FieldSceneRenderAlphaMode AlphaMode { get; init; }
    public float? AlphaCutoff { get; init; }
    public bool? DoubleSided { get; init; }
}

public sealed record FieldSceneRenderInstance(FieldSceneAsset Asset, Matrix4x4 AssetToField, int AnimationIndex = -1, float AnimationTime = 0, IReadOnlyDictionary<string, FieldSceneRenderMaterial>? Materials = null, IReadOnlyDictionary<string, FieldSceneGeometryRole>? Roles = null);

/// <summary>Camera-space axes are right, down and forward. VerticalScale is tan(yfov/2) for perspective and the vertical half extent for orthographic.</summary>
public sealed record FieldSceneRenderCamera(Matrix4x4 FieldToCamera, bool Orthographic, float VerticalScale, float? AspectRatio = null, float NearPlane = 1, float? FarPlane = null)
{
    public FieldSceneFrameFit FrameFit { get; init; }
    /// <summary>Image centre offset as a fraction of the authored frame's dimensions. Positive values move the point right and down.</summary>
    public Vector2 PrincipalOffset { get; init; }
    /// <summary>The native scroll recorded at calibration, in drawing units, excluding frame shake.</summary>
    public Vector2? NativeScrollBaseline { get; init; }
}

public readonly record struct FieldNativeRenderView(FieldCamera Camera, Vector2 DrawingOrigin, Vector2 ActorOffset, Vector4 Viewport, Vector2 FocalPixels, Vector2 PrincipalPixels);

public sealed partial class FieldRenderer
{
    private static readonly ConditionalWeakTable<FieldSceneAsset, ScenePose> bindScenePoses = new();
    private readonly List<SceneTriangle> customSceneTriangles = [];
    private readonly List<Vector3> customSceneReceivers = [];
    private readonly List<FieldShadowTriangle> customSceneCasters = [];
    private FieldSceneRenderCamera? renderedSceneCamera;
    private Vector4 sceneViewport;
    private Vector4 sceneProjectionFrame;

    public IReadOnlyList<FieldSceneRenderInstance> CustomSceneInstances { get; set; } = [];
    public FieldSceneRenderCamera? CustomSceneCamera { get; set; }
    public bool CustomSceneOwnsNativeShadows { get; set; }
    public FieldNativeRenderView NativeView { get; private set; }

    private bool OrthographicScene => renderedSceneCamera?.Orthographic == true;
    private float SceneNearPlane => renderedSceneCamera?.NearPlane ?? 1;
    private Vector4 WorldClip => new(sceneViewport.X, sceneViewport.Y, sceneViewport.X + sceneViewport.Z, sceneViewport.Y + sceneViewport.W);

    private void PrepareSceneView(FieldCamera nativeCamera, bool backgroundVisible)
    {
        Vector2 principal = new(HorizontalOrigin + (drawingOrigin.X + actorScrollOffset.X + nativeCamera.Pan.X) * scale, offsetY + (drawingOrigin.Y + actorScrollOffset.Y + nativeCamera.Pan.Y) * verticalScale);
        Vector4 nativeViewport = new(clipLeft, clipTop, clipRight - clipLeft, clipBottom - clipTop);
        NativeView = new(nativeCamera, drawingOrigin, actorScrollOffset, nativeViewport, nativeCamera.FocalLength * new Vector2(scale, verticalScale), principal);
        sceneViewport = nativeViewport;
        sceneProjectionFrame = nativeViewport;
        renderedSceneCamera = !backgroundVisible && InspectionCamera is null && TriangleRenderer is not null ? CustomSceneCamera : null;
        if (renderedSceneCamera is { } authored)
        {
            sceneViewport = new(0, 0, OutputWidth, OutputHeight);
            sceneProjectionFrame = sceneViewport;
            if (authored.AspectRatio is float aspect && authored.FrameFit != FieldSceneFrameFit.Expand)
            {
                bool contain = authored.FrameFit == FieldSceneFrameFit.Contain;
                float width = contain ? Math.Min(OutputWidth, OutputHeight * aspect) : Math.Max(OutputWidth, OutputHeight * aspect);
                float height = contain ? Math.Min(OutputHeight, OutputWidth / aspect) : Math.Max(OutputHeight, OutputWidth / aspect);
                sceneProjectionFrame = new((OutputWidth - width) / 2, (OutputHeight - height) / 2, width, height);
                if (contain)
                {
                    sceneViewport = sceneProjectionFrame;
                }
            }
        }
    }

    private Vector3 SceneCameraPoint(Vector3 world)
    {
        return renderedSceneCamera is { } authored
            ? Vector3.Transform(world, authored.FieldToCamera)
            : new Vector3(Vector3.Dot(camera.AxisX, world), Vector3.Dot(camera.AxisY, world), Vector3.Dot(camera.AxisZ, world)) + camera.Translation;
    }

    private ProjectedPoint ProjectSceneCameraPoint(Vector3 point)
    {
        if (renderedSceneCamera is { } authored)
        {
            float divisor = authored.Orthographic ? 1 : Math.Max(authored.NearPlane, point.Z);
            float factor = sceneProjectionFrame.W / (2 * authored.VerticalScale * divisor);
            Vector2 centre = ScenePrincipalPoint(authored);
            return new(centre.X + point.X * factor, centre.Y + point.Y * factor, point.Z);
        }
        float depth = Math.Max(1, point.Z);
        return ToViewport(new ProjectedPoint(160 + camera.Pan.X + camera.FocalLength * point.X / depth, 120 + camera.Pan.Y + camera.FocalLength * point.Y / depth, point.Z));
    }

    private ProjectedPoint ProjectFieldWorld(Vector3 world)
    {
        return renderedSceneCamera is null ? ToViewport(camera.Project(world)) : ProjectSceneCameraPoint(SceneCameraPoint(world));
    }

    private bool OutsideWorldDepth(float depth)
    {
        return depth <= SceneNearPlane || (renderedSceneCamera?.FarPlane is float far && depth > far);
    }

    private bool TrySceneScreenRay(Vector2 screen, out Vector3 origin, out Vector3 direction)
    {
        origin = direction = default;
        if (renderedSceneCamera is not { } authored || !Matrix4x4.Invert(authored.FieldToCamera, out Matrix4x4 inverse))
        {
            return false;
        }
        Vector2 position = (screen - ScenePrincipalPoint(authored)) * (2 * authored.VerticalScale / sceneProjectionFrame.W);
        origin = authored.Orthographic ? Vector3.Transform(new Vector3(position, 0), inverse) : inverse.Translation;
        direction = Vector3.Normalize(Vector3.TransformNormal(authored.Orthographic ? Vector3.UnitZ : new Vector3(position, 1), inverse));
        return float.IsFinite(direction.LengthSquared());
    }

    private Vector2 ScenePrincipalPoint(FieldSceneRenderCamera authored)
    {
        Vector2 centre = new Vector2(sceneProjectionFrame.X, sceneProjectionFrame.Y) + (new Vector2(0.5f) + authored.PrincipalOffset) * new Vector2(sceneProjectionFrame.Z, sceneProjectionFrame.W);
        if (authored.NativeScrollBaseline is Vector2 baseline)
        {
            centre += (actorScrollOffset - baseline) * new Vector2(scale, verticalScale);
        }
        return centre;
    }

    private void PrepareCustomScene()
    {
        customSceneTriangles.Clear();
        customSceneReceivers.Clear();
        customSceneCasters.Clear();
        if (TriangleRenderer is null)
        {
            return;
        }
        foreach (FieldSceneRenderInstance instance in CustomSceneInstances)
        {
            FieldSceneAsset asset = instance.Asset;
            ScenePose pose = instance.AnimationIndex < 0
                ? bindScenePoses.GetValue(asset, static source => SampleScenePose(source, -1, 0))
                : SampleScenePose(asset, instance.AnimationIndex, instance.AnimationTime);
            if (!Matrix4x4.Invert(instance.AssetToField, out Matrix4x4 inverse))
            {
                throw new InvalidDataException("Field scene placement must be an invertible transform.");
            }
            Matrix4x4 placementsNormals = Matrix4x4.Transpose(inverse);
            for (int index = 0; index < asset.Primitives.Count; index++)
            {
                FieldScenePrimitive primitive = asset.Primitives[index];
                FieldSceneGeometryRole role = instance.Roles?.GetValueOrDefault(primitive.Id) ?? instance.Roles?.GetValueOrDefault(primitive.NodeId) ?? FieldSceneGeometryRole.PhysicalSurface;
                if (role == FieldSceneGeometryRole.ReferenceOnly)
                {
                    continue;
                }
                EditableMesh mesh = asset.Geometry.Meshes[index];
                FieldSceneMaterial material = asset.Materials[primitive.MaterialIndex];
                FieldSceneRenderMaterial policy = instance.Materials?.GetValueOrDefault(material.Id) ?? new(material.Unlit ? FieldSceneMaterialMode.Unlit : FieldSceneMaterialMode.FieldLit);
                var texture = material.Texture >= 0 ? asset.Geometry.Textures[material.Texture] : null;
                ScenePipeline pipeline = new(material, policy, role, texture);
                SceneVertex[] vertices = new SceneVertex[mesh.Vertices.Count];
                Vector3 minimum = new(float.PositiveInfinity);
                Vector3 maximum = new(float.NegativeInfinity);
                bool receiver = ReceivesSceneShadows(pipeline);
                for (int vertex = 0; vertex < vertices.Length; vertex++)
                {
                    Vector3 point = Vector3.Transform(pose.Positions[index][vertex], instance.AssetToField);
                    Vector3 normal = Vector3.TransformNormal(pose.Normals[index][vertex], placementsNormals);
                    vertices[vertex] = new(point, SceneCameraPoint(point), normal, mesh.Vertices[vertex].Uv, mesh.Vertices[vertex].Color * material.BaseColorFactor);
                    if (receiver)
                    {
                        minimum = Vector3.Min(minimum, point);
                        maximum = Vector3.Max(maximum, point);
                    }
                }
                if (receiver)
                {
                    AddSceneReceiverBounds(minimum, maximum);
                }
                Matrix4x4 meshNode = mesh.Skinned ? Matrix4x4.Identity : mesh.Node >= 0 ? pose.Transforms[mesh.Node] : mesh.Transform;
                bool mirror = (meshNode * instance.AssetToField).GetDeterminant() < 0;
                bool casts = TryShadowMaterial(pipeline, out TriangleMaterial shadow);
                for (int triangle = 0; triangle < mesh.Indices.Count; triangle += 3)
                {
                    SceneVertex a = vertices[mesh.Indices[triangle]];
                    SceneVertex b = vertices[mesh.Indices[triangle + (mirror ? 2 : 1)]];
                    SceneVertex c = vertices[mesh.Indices[triangle + (mirror ? 1 : 2)]];
                    if (a.Normal == Vector3.Zero || b.Normal == Vector3.Zero || c.Normal == Vector3.Zero)
                    {
                        Vector3 normal = Vector3.Cross(b.World - a.World, c.World - a.World);
                        a = a with { Normal = normal };
                        b = b with { Normal = normal };
                        c = c with { Normal = normal };
                    }
                    if (casts)
                    {
                        customSceneCasters.Add(new(SceneShadowVertex(a), SceneShadowVertex(b), SceneShadowVertex(c), shadow));
                    }
                    QueueClippedSceneTriangle(a, b, c, pipeline);
                }
            }
        }
    }

    private static ScenePose SampleScenePose(FieldSceneAsset asset, int clip, float time)
    {
        Matrix4x4[] transforms = asset.SampleTransforms(clip, time);
        return new(transforms, ModelExchange.PoseWithTransforms(asset.Geometry, transforms), asset.PoseNormals(transforms));
    }

    private void AddSceneReceiverBounds(Vector3 minimum, Vector3 maximum)
    {
        for (int corner = 0; corner < 8; corner++)
        {
            customSceneReceivers.Add(new((corner & 1) == 0 ? minimum.X : maximum.X, (corner & 2) == 0 ? minimum.Y : maximum.Y, (corner & 4) == 0 ? minimum.Z : maximum.Z));
        }
    }

    private static bool ReceivesSceneShadows(ScenePipeline pipeline)
    {
        return pipeline.Role == FieldSceneGeometryRole.ReceiverProxy || (pipeline.Role is FieldSceneGeometryRole.PhysicalSurface or FieldSceneGeometryRole.PaintedBackdrop && pipeline.Policy.Mode != FieldSceneMaterialMode.Unlit && pipeline.Policy.ReceiveShadows);
    }

    private static bool TryShadowMaterial(ScenePipeline pipeline, out TriangleMaterial shadow)
    {
        SceneAlphaCoverage coverage = pipeline.AlphaMode == FieldSceneRenderAlphaMode.Opaque ? SceneAlphaCoverage.Opaque : SceneAlphaCoverage.Mask;
        float? cutoff = pipeline.AlphaMode is FieldSceneRenderAlphaMode.Blend or FieldSceneRenderAlphaMode.OpaqueCoreAndBlend ? pipeline.Policy.ShadowAlphaCutoff : pipeline.AlphaCutoff;
        shadow = new TriangleMaterial(pipeline.Texture, MinimumAlpha: cutoff ?? 0, Sampler: TriangleSampler.PointClamp) { SceneCoverage = coverage, SceneSampler = pipeline.Material.Sampler };
        return pipeline.Policy.CastShadows && (coverage == SceneAlphaCoverage.Opaque || cutoff.HasValue);
    }

    private static TriangleVertex SceneShadowVertex(SceneVertex vertex)
    {
        return new(Vector2.Zero, 0, 1, vertex.Uv, vertex.Color) { WorldPosition = vertex.World, WorldNormal = vertex.Normal };
    }

    private void QueueClippedSceneTriangle(SceneVertex a, SceneVertex b, SceneVertex c, ScenePipeline pipeline)
    {
        Span<SceneVertex> input = stackalloc SceneVertex[5];
        Span<SceneVertex> clipped = stackalloc SceneVertex[5];
        input[0] = a;
        input[1] = b;
        input[2] = c;
        int count = ClipSceneDepth(input[..3], clipped, SceneNearPlane, true);
        if (count < 3)
        {
            return;
        }
        if (renderedSceneCamera?.FarPlane is float far)
        {
            count = ClipSceneDepth(clipped[..count], input, far, false);
            input[..count].CopyTo(clipped);
        }
        for (int index = 1; index + 1 < count; index++)
        {
            TriangleVertex va = ProjectCustomVertex(clipped[0]);
            TriangleVertex vb = ProjectCustomVertex(clipped[index]);
            TriangleVertex vc = ProjectCustomVertex(clipped[index + 1]);
            Vector2 ab = vb.Position - va.Position;
            Vector2 ac = vc.Position - va.Position;
            float area = ab.X * ac.Y - ab.Y * ac.X;
            if (area == 0 || !float.IsFinite(area) || (!pipeline.DoubleSided && area > 0))
            {
                continue;
            }
            if (area > 0)
            {
                va = va with { WorldNormal = -va.WorldNormal };
                vb = vb with { WorldNormal = -vb.WorldNormal };
                vc = vc with { WorldNormal = -vc.WorldNormal };
            }
            customSceneTriangles.Add(new(va, vb, vc, pipeline));
        }
    }

    private static int ClipSceneDepth(ReadOnlySpan<SceneVertex> input, Span<SceneVertex> output, float plane, bool near)
    {
        int count = 0;
        SceneVertex previous = input[^1];
        bool previousInside = near ? previous.Camera.Z >= plane : previous.Camera.Z <= plane;
        foreach (SceneVertex current in input)
        {
            bool inside = near ? current.Camera.Z >= plane : current.Camera.Z <= plane;
            if (inside != previousInside)
            {
                float t = (plane - previous.Camera.Z) / (current.Camera.Z - previous.Camera.Z);
                Vector3 point = Vector3.Lerp(previous.Camera, current.Camera, t);
                point.Z = plane;
                output[count++] = new(Vector3.Lerp(previous.World, current.World, t), point, Vector3.Lerp(previous.Normal, current.Normal, t), Vector2.Lerp(previous.Uv, current.Uv, t), Vector4.Lerp(previous.Color, current.Color, t));
            }
            if (inside)
            {
                output[count++] = current;
            }
            previous = current;
            previousInside = inside;
        }
        return count;
    }

    private TriangleVertex ProjectCustomVertex(SceneVertex vertex)
    {
        ProjectedPoint point = ProjectSceneCameraPoint(vertex.Camera);
        float reciprocal = OrthographicScene ? 1 : 1 / point.Depth;
        return new(new(point.X, point.Y), point.Depth, reciprocal, vertex.Uv, vertex.Color * reciprocal)
        {
            WorldPosition = vertex.World,
            WorldNormal = vertex.Normal
        };
    }

    private void DrawCustomScene(List<(float Depth, Action Draw)> translucent, bool backdrop)
    {
        foreach (SceneTriangle triangle in customSceneTriangles)
        {
            ScenePipeline pipeline = triangle.Pipeline;
            bool background = pipeline.Role == FieldSceneGeometryRole.PaintedBackdrop;
            if (background != backdrop)
            {
                continue;
            }
            FieldLightingPass lighting = effectiveLighting is null ? FieldLightingPass.None : pipeline.Role switch
            {
                FieldSceneGeometryRole.OccluderProxy => FieldLightingPass.None,
                FieldSceneGeometryRole.ReceiverProxy => FieldLightingPass.GroundShadow,
                _ when pipeline.Policy.Mode == FieldSceneMaterialMode.FieldLit => pipeline.Policy.ReceiveShadows ? FieldLightingPass.Model : FieldLightingPass.ModelWithoutShadow,
                _ when ReceivesSceneShadows(pipeline) => FieldLightingPass.UnlitShadowReceiver,
                _ => FieldLightingPass.None
            };
            SceneAlphaCoverage coverage = pipeline.AlphaMode switch
            {
                FieldSceneRenderAlphaMode.Mask => SceneAlphaCoverage.Mask,
                FieldSceneRenderAlphaMode.Blend => SceneAlphaCoverage.Blend,
                FieldSceneRenderAlphaMode.OpaqueCoreAndBlend => SceneAlphaCoverage.OpaqueCore,
                _ => SceneAlphaCoverage.Opaque
            };
            bool receiverProxy = pipeline.Role == FieldSceneGeometryRole.ReceiverProxy;
            if (receiverProxy && lighting == FieldLightingPass.None)
            {
                continue;
            }
            TriangleMaterial material = GpuMaterial(pipeline.Texture, TriangleBlend.Replace, !receiverProxy && !background) with
            {
                ClipRectangle = WorldClip,
                PerspectiveDepth = !OrthographicScene,
                SceneCoverage = coverage,
                SceneSampler = pipeline.Material.Sampler,
                MinimumAlpha = pipeline.AlphaCutoff,
                ColorWrite = pipeline.Role != FieldSceneGeometryRole.OccluderProxy,
                FieldLighting = lighting
            };
            float sortDepth = (triangle.A.Depth + triangle.B.Depth + triangle.C.Depth) / 3;
            if (receiverProxy)
            {
                TriangleMaterial proxy = material with { Blend = TriangleBlend.Alpha, DepthComparison = TriangleDepthComparison.Equal };
                translucent.Add((sortDepth, () => TriangleRenderer!.Draw(triangle.A, triangle.B, triangle.C, proxy)));
                continue;
            }
            if (coverage != SceneAlphaCoverage.Blend)
            {
                TriangleRenderer!.Draw(triangle.A, triangle.B, triangle.C, material);
            }
            if (coverage is SceneAlphaCoverage.OpaqueCore or SceneAlphaCoverage.Blend && material.ColorWrite)
            {
                TriangleMaterial fractional = material with { SceneCoverage = coverage == SceneAlphaCoverage.Blend ? coverage : SceneAlphaCoverage.Fractional, Blend = TriangleBlend.Alpha, DepthWrite = false };
                translucent.Add((sortDepth, () => TriangleRenderer!.Draw(triangle.A, triangle.B, triangle.C, fractional)));
            }
        }
    }

    private readonly record struct SceneVertex(Vector3 World, Vector3 Camera, Vector3 Normal, Vector2 Uv, Vector4 Color);
    private readonly record struct SceneTriangle(TriangleVertex A, TriangleVertex B, TriangleVertex C, ScenePipeline Pipeline);
    private sealed record ScenePose(Matrix4x4[] Transforms, IReadOnlyList<Vector3[]> Positions, IReadOnlyList<Vector3[]> Normals);
    private sealed record ScenePipeline(FieldSceneMaterial Material, FieldSceneRenderMaterial Policy, FieldSceneGeometryRole Role, Albeoris.Games.FF8.Imaging.PixelImage? Texture)
    {
        public float AlphaCutoff => Policy.AlphaCutoff ?? Material.AlphaCutoff;
        public bool DoubleSided => Policy.DoubleSided ?? Material.DoubleSided;
        public FieldSceneRenderAlphaMode AlphaMode => Policy.AlphaMode != FieldSceneRenderAlphaMode.Source ? Policy.AlphaMode : Material.AlphaMode switch
        {
            FieldSceneAlphaMode.Mask => FieldSceneRenderAlphaMode.Mask,
            FieldSceneAlphaMode.Blend => FieldSceneRenderAlphaMode.OpaqueCoreAndBlend,
            _ => FieldSceneRenderAlphaMode.Opaque
        };
    }
}
