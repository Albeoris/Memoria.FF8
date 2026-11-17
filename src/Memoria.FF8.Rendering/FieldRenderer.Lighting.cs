using System.Numerics;
using Memoria.FF8.Assets;
using Memoria.FF8.Mods;

namespace Memoria.FF8.Rendering;

public sealed partial class FieldRenderer
{
    private readonly Dictionary<FieldModel, FieldModelNormals> normalTopology = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<int, LitActorPose> litActorPoses = [];
    private readonly Dictionary<int, FieldActorShadowState> lightingShadowStates = [];
    private readonly HashSet<int> shadowReceiverIds = [];
    private readonly List<FieldShadowTriangle> shadowCasters = [];
    private FieldLightingScene? effectiveLighting;

    /// <summary>Null restores legacy rendering; an active empty scene deliberately illuminates models with zero light.</summary>
    public FieldLightingScene? ActiveLighting { get; set; }
    public string? LightingUnavailableReason { get; private set; }
    public string? LightingShadowDiagnostic { get; private set; }
    public IReadOnlyDictionary<int, FieldActorShadowState> LightingShadowStates => lightingShadowStates;

    public Vector2? ProjectWorldPoint(Vector3 world)
    {
        ProjectedPoint point = ToViewport(camera.Project(world));
        return point.Depth > 1 && float.IsFinite(point.X) && float.IsFinite(point.Y) ? new Vector2(point.X, point.Y) : null;
    }

    public bool TryScreenRay(Vector2 screen, out Vector3 origin, out Vector3 direction)
    {
        Matrix4x4 view = new(camera.AxisX.X, camera.AxisY.X, camera.AxisZ.X, 0, camera.AxisX.Y, camera.AxisY.Y, camera.AxisZ.Y, 0, camera.AxisX.Z, camera.AxisY.Z, camera.AxisZ.Z, 0, camera.Translation.X, camera.Translation.Y, camera.Translation.Z, 1);
        if (!Matrix4x4.Invert(view, out Matrix4x4 inverse))
        {
            origin = direction = default;
            return false;
        }
        float x = ((screen.X - HorizontalOrigin) / scale - drawingOrigin.X - actorScrollOffset.X - camera.Pan.X) / camera.FocalLength;
        float y = ((screen.Y - offsetY) / verticalScale - drawingOrigin.Y - actorScrollOffset.Y - camera.Pan.Y) / camera.FocalLength;
        origin = inverse.Translation;
        direction = Vector3.Normalize(Vector3.TransformNormal(new Vector3(x, y, 1), inverse));
        return float.IsFinite(direction.LengthSquared());
    }

    public bool TryPickGround(Vector2 screen, out Vector3 position)
    {
        position = default;
        if (!TryScreenRay(screen, out Vector3 origin, out Vector3 direction))
        {
            return false;
        }
        float nearest = float.PositiveInfinity;
        foreach (WalkmeshTriangle triangle in field.Walkmesh.Triangles)
        {
            Vector3 edge1 = triangle.B - triangle.A;
            Vector3 edge2 = triangle.C - triangle.A;
            Vector3 cross = Vector3.Cross(direction, edge2);
            float determinant = Vector3.Dot(edge1, cross);
            if (Math.Abs(determinant) < 0.000001f)
            {
                continue;
            }
            Vector3 delta = origin - triangle.A;
            float u = Vector3.Dot(delta, cross) / determinant;
            Vector3 other = Vector3.Cross(delta, edge1);
            float v = Vector3.Dot(direction, other) / determinant;
            float distance = Vector3.Dot(edge2, other) / determinant;
            if (u >= 0 && v >= 0 && u + v <= 1 && distance > 0 && distance < nearest)
            {
                nearest = distance;
                position = origin + direction * distance;
            }
        }
        return float.IsFinite(nearest);
    }

    private void PrepareLighting(ActorVisual[] actors)
    {
        effectiveLighting = null;
        LightingUnavailableReason = null;
        LightingShadowDiagnostic = null;
        litActorPoses.Clear();
        lightingShadowStates.Clear();
        shadowReceiverIds.Clear();
        shadowCasters.Clear();
        IFieldLightingRenderer? backend = TriangleRenderer as IFieldLightingRenderer;
        if (backend?.FieldLightingSupported == true)
        {
            backend.SetFieldLighting(null);
        }
        if (ActiveLighting is null)
        {
            return;
        }
        if (backend is null || !backend.FieldLightingSupported)
        {
            LightingUnavailableReason = backend?.FieldLightingUnavailableReason ?? "This renderer does not support field spotlight shading.";
            return;
        }
        effectiveLighting = string.Equals(ActiveLighting.FieldName, field.Name, StringComparison.OrdinalIgnoreCase) ? ActiveLighting : FieldLightingScene.Empty(field.Name);
        backend.SetFieldLighting(effectiveLighting);
        FieldSpotLight? primary = effectiveLighting.Lights.FirstOrDefault(light => light.Id == effectiveLighting.PrimaryLightId);
        foreach (ActorVisual actor in actors.Where(actor => actor.Visible && (uint)actor.ModelId < field.Models.Count))
        {
            FieldModel model = field.Models[actor.ModelId];
            if (model.Bones.Count == 0 || model.Faces.Count == 0)
            {
                lightingShadowStates[actor.ObjectId] = FieldActorShadowState.IntentionalNone;
                continue;
            }
            Vector3[] pose = ActorPose(model, actor);
            Matrix4x4 rotation = Matrix4x4.CreateRotationZ((actor.Direction + 64) * MathF.Tau / 256);
            Vector3[] world = pose.Select(position => Vector3.Transform(position, rotation) + new Vector3(actor.X, actor.Y, actor.Z)).ToArray();
            if (!normalTopology.TryGetValue(model, out FieldModelNormals? topology))
            {
                topology = new FieldModelNormals(model, model.Pose(0, 0));
                normalTopology.Add(model, topology);
            }
            Vector3[]? imported = null;
            if (model.EditableOverride is { } edited)
            {
                float phase = actor.AnimationBlend is { } blend ? float.Lerp(blend.PreviousFrame, blend.CurrentFrame, blend.Fraction) : actor.AnimationFrame;
                imported = ModelExchange.PoseNativeNormals(edited, actor.AnimationId, phase / edited.NativeFramesPerSecond);
            }
            Vector3[][] normals = topology.Pose(pose, imported);
            foreach (Vector3[] corners in normals)
            {
                for (int corner = 0; corner < corners.Length; corner++)
                {
                    corners[corner] = Vector3.TransformNormal(corners[corner], rotation);
                }
            }
            LitActorPose litPose = new(world, normals);
            litActorPoses.Add(actor.ObjectId, litPose);
            FieldActorShadowState state = SelectShadowState(actor, primary, world, out int support);
            lightingShadowStates[actor.ObjectId] = state;
            if (state != FieldActorShadowState.Silhouette)
            {
                continue;
            }
            AddShadowReceivers(support, primary!);
            AddShadowCasters(actor, model, litPose);
        }
        if (shadowCasters.Count > 0 && !backend.PrepareFieldShadow(shadowCasters, out string? reason))
        {
            LightingShadowDiagnostic = reason;
            foreach (int actor in lightingShadowStates.Where(pair => pair.Value == FieldActorShadowState.Silhouette).Select(pair => pair.Key).ToArray())
            {
                lightingShadowStates[actor] = FieldActorShadowState.NativeFallback;
            }
            shadowReceiverIds.Clear();
        }
    }

    private FieldActorShadowState SelectShadowState(ActorVisual actor, FieldSpotLight? primary, Vector3[] world, out int support)
    {
        support = -1;
        if (actor.GroundShadowRadii601To608 is { } shape && (shape.Count != 8 || actor.GroundShadowRadius601To608 is not null))
        {
            throw new NotSupportedException("Native ground-shadow shape requires exactly bytes601..608 and one current shape authority.");
        }
        bool zeroRadius = actor.GroundShadowRadii601To608 is { } radii ? radii.All(radius => radius == 0) : actor.GroundShadowRadius601To608 is not > 0;
        if (!actor.GroundShadowEnabled || zeroRadius || actor.GroundShadowLevel609 is not > 0 || primary is null || !primary.Enabled || primary.Intensity <= 0 || primary.Color == Vector3.Zero || !world.Any(point => FieldLightingMath.Influence(primary, point) > 0))
        {
            return FieldActorShadowState.IntentionalNone;
        }
        Vector3 center = actor.GroundShadowCentre ?? new Vector3(actor.X, actor.Y, actor.Z);
        if (!field.Walkmesh.TryPosition(center.X, center.Y, center.Z, 1, out _, out support))
        {
            // A valid native support point is necessary even for the explicitly diagnosed native fallback.
            if (actor.GroundShadowCentre is null || primary.Position.Z <= center.Z)
            {
                return FieldActorShadowState.IntentionalNone;
            }
            LightingShadowDiagnostic = "Some actors have no matching support walkmesh triangle; their native shadow is gated by the Primary lamp.";
            return FieldActorShadowState.NativeFallback;
        }
        WalkmeshTriangle ground = field.Walkmesh.Triangles[support];
        Vector3 normal = Vector3.Cross(ground.B - ground.A, ground.C - ground.A);
        if (normal.Z < 0)
        {
            normal = -normal;
        }
        if (normal.LengthSquared() < 0.000001f || Vector3.Dot(primary.Position - ground.A, normal) <= 0)
        {
            return FieldActorShadowState.IntentionalNone;
        }
        return FieldActorShadowState.Silhouette;
    }

    private void AddShadowReceivers(int support, FieldSpotLight light)
    {
        Queue<int> pending = new();
        HashSet<int> visited = [];
        pending.Enqueue(support);
        while (pending.TryDequeue(out int id))
        {
            if ((uint)id >= field.Walkmesh.Triangles.Count || !visited.Add(id))
            {
                continue;
            }
            WalkmeshTriangle triangle = field.Walkmesh.Triangles[id];
            Vector3 minimum = Vector3.Min(triangle.A, Vector3.Min(triangle.B, triangle.C));
            Vector3 maximum = Vector3.Max(triangle.A, Vector3.Max(triangle.B, triangle.C));
            if (Vector3.DistanceSquared(Vector3.Clamp(light.Position, minimum, maximum), light.Position) >= light.Range * light.Range)
            {
                continue;
            }
            // Native adjacency is the authority for connected floors; no proximity weld can join stacked floors.
            shadowReceiverIds.Add(id);
            pending.Enqueue(triangle.NeighborA);
            pending.Enqueue(triangle.NeighborB);
            pending.Enqueue(triangle.NeighborC);
        }
    }

    private void AddShadowCasters(ActorVisual actor, FieldModel model, LitActorPose pose)
    {
        IReadOnlyList<Albeoris.Games.FF8.Imaging.PixelImage> textures = model.EditableOverride is not null ? model.Textures : actor.RegisteredTextures ?? model.Textures;
        foreach (ModelFace face in model.Faces)
        {
            if (face.SemiTransparent || actor.PrimitiveTransparencyEnabled || face.Vertices.Any(vertex => actor.AdditionalControlDisabled && model.Vertices[vertex].Bone == model.ControlBones.Additional))
            {
                continue;
            }
            var texture = textures[face.Texture];
            Vector2 size = new(texture.Width, texture.Height);
            Vector2 textureScale = ResourceTextureList.PhysicalScale(textures, face.Texture);
            TriangleVertex Vertex(int corner) => new(Vector2.Zero, 0, 1, face.TextureCoordinates[corner] * textureScale / size, new Vector4(1, 1, 1, actor.GroundShadowLevel609!.Value / 255f)) { WorldPosition = pose.World[face.Vertices[corner]] };
            TriangleMaterial material = new(texture, Sampler: TriangleSampler.PointClamp);
            shadowCasters.Add(new(Vertex(0), Vertex(1), Vertex(2), material));
            if (face.Quad)
            {
                shadowCasters.Add(new(Vertex(1), Vertex(3), Vertex(2), material));
            }
        }
    }

    private void QueueLightingReceivers(List<(float Depth, Action Draw)> translucent)
    {
        foreach (int id in shadowReceiverIds.Order())
        {
            WalkmeshTriangle receiver = field.Walkmesh.Triangles[id];
            ProjectedPoint a = ToViewport(camera.Project(receiver.A));
            ProjectedPoint b = ToViewport(camera.Project(receiver.B));
            ProjectedPoint c = ToViewport(camera.Project(receiver.C));
            if (a.Depth <= 1 || b.Depth <= 1 || c.Depth <= 1)
            {
                continue;
            }
            TriangleMaterial material = GpuMaterial(null, TriangleBlend.Alpha, false) with { PerspectiveDepth = true, FieldLighting = FieldLightingPass.GroundShadow };
            TriangleVertex va = GpuVertex(a, Vector2.Zero, Vector4.One) with { WorldPosition = receiver.A };
            TriangleVertex vb = GpuVertex(b, Vector2.Zero, Vector4.One) with { WorldPosition = receiver.B };
            TriangleVertex vc = GpuVertex(c, Vector2.Zero, Vector4.One) with { WorldPosition = receiver.C };
            translucent.Add((MathF.Floor((a.Depth + b.Depth + c.Depth) / 12) * 4, () => TriangleRenderer!.Draw(va, vb, vc, material)));
        }
    }

    private sealed record LitActorPose(Vector3[] World, Vector3[][] Normals);

    private void DrawLitModelTriangle(ActorVisual actor, ModelFace face, LitActorPose pose, int faceIndex, int a, int b, int c, Albeoris.Games.FF8.Imaging.PixelImage texture, Vector2 textureScale)
    {
        if (projected[face.Vertices[a]].Depth <= 1 || projected[face.Vertices[b]].Depth <= 1 || projected[face.Vertices[c]].Depth <= 1)
        {
            return;
        }
        TriangleMaterial material = GpuMaterial(texture, TriangleBlend.Replace, true) with
        {
            DepthComparison = TriangleDepthComparison.Less,
            PerspectiveDepth = true,
            CentroidTextureSampling = true,
            Sampler = modelTextureSampling == ModelTextureSampling.Nearest ? TriangleSampler.PointClamp : TriangleSampler.CutoutLinearClamp,
            FieldLighting = FieldLightingPass.Model
        };
        Vector4 tint = new((actor.PresentationPolygonColor ?? actor.PolygonColor!.Value) / 128, 1);
        TriangleVertex Vertex(int corner) => GpuVertex(projected[face.Vertices[corner]], face.TextureCoordinates[corner] * textureScale / new Vector2(texture.Width, texture.Height), tint * (face.VertexColors?[corner] ?? Vector4.One)) with { WorldPosition = pose.World[face.Vertices[corner]], WorldNormal = pose.Normals[faceIndex][corner] };
        TriangleRenderer!.Draw(Vertex(a), Vertex(b), Vertex(c), material);
    }
}
