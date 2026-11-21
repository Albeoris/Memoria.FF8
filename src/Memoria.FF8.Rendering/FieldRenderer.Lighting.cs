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
    private readonly List<WalkmeshTriangle> nativeShadowReceiverDiagnostics = [];
    private readonly List<FieldShadowReceiverGeometry> authoredShadowReceivers = [];
    private readonly List<FieldShadowTriangle> shadowCasters = [];
    private FieldLightingScene? effectiveLighting;

    /// <summary>Native lighting remains active until this field has at least one authored source, including disabled sources.</summary>
    public FieldLightingScene? ActiveLighting { get; set; }
    public bool UsesAuthoredLighting => effectiveLighting is not null;
    public string? LightingUnavailableReason { get; private set; }
    public string? LightingShadowDiagnostic { get; private set; }
    public IReadOnlyDictionary<int, FieldActorShadowState> LightingShadowStates => lightingShadowStates;
    public IReadOnlyList<WalkmeshTriangle> LightingNativeShadowReceivers => nativeShadowReceiverDiagnostics;

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
        nativeShadowReceiverDiagnostics.Clear();
        authoredShadowReceivers.Clear();
        shadowCasters.Clear();
        IFieldLightingRenderer? backend = TriangleRenderer as IFieldLightingRenderer;
        if (backend?.FieldLightingSupported == true)
        {
            backend.SetFieldLighting(null);
        }
        if (ActiveLighting is null || ActiveLighting.Lights.Count == 0 || !string.Equals(ActiveLighting.FieldName, field.Name, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        if (backend is null || !backend.FieldLightingSupported)
        {
            LightingUnavailableReason = backend?.FieldLightingUnavailableReason ?? "This renderer does not support field spotlight shading.";
            return;
        }
        effectiveLighting = ActiveLighting;
        backend.SetFieldLighting(effectiveLighting);
        FieldSpotLight? primary = effectiveLighting.Lights.FirstOrDefault(light => light.Id == effectiveLighting.PrimaryLightId);
        if (primary is { Enabled: true, Intensity: > 0 } && primary.Color != Vector3.Zero)
        {
            foreach (FieldShadowReceiver receiver in effectiveLighting.ShadowReceivers.Where(receiver => receiver.Enabled))
            {
                FieldShadowReceiverGeometry geometry = new(receiver);
                if (geometry.IsWithinReach(primary, receiver.Kind == "wall"))
                {
                    authoredShadowReceivers.Add(geometry);
                }
            }
        }
        HashSet<int> nativeFallbackActors = [];
        foreach (ActorVisual actor in actors)
        {
            lightingShadowStates[actor.ObjectId] = FieldActorShadowState.IntentionalNone;
        }
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
            if (!CanCastShadow(actor, primary, world))
            {
                continue;
            }
            FieldActorShadowState nativeState = SelectNativeReceiver(actor, primary!, out int support);
            if (nativeState != FieldActorShadowState.IntentionalNone)
            {
                nativeFallbackActors.Add(actor.ObjectId);
            }
            if (nativeState == FieldActorShadowState.Silhouette)
            {
                AddShadowReceivers(support, primary!);
            }
            else if (!authoredShadowReceivers.Any(receiver => receiver.CanReceiveShadow(primary!.Position, world)))
            {
                lightingShadowStates[actor.ObjectId] = nativeState;
                if (nativeState == FieldActorShadowState.NativeFallback)
                {
                    LightingShadowDiagnostic = "Some actors have no matching support walkmesh triangle; their native shadow is gated by the Primary lamp.";
                }
                continue;
            }
            lightingShadowStates[actor.ObjectId] = FieldActorShadowState.Silhouette;
            AddShadowCasters(actor, model, litPose);
        }
        if (shadowCasters.Count == 0)
        {
            shadowReceiverIds.Clear();
            authoredShadowReceivers.Clear();
            return;
        }
        if (!backend.PrepareFieldShadow(shadowCasters, out string? reason))
        {
            LightingShadowDiagnostic = reason;
            foreach (int actor in lightingShadowStates.Where(pair => pair.Value == FieldActorShadowState.Silhouette).Select(pair => pair.Key).ToArray())
            {
                lightingShadowStates[actor] = nativeFallbackActors.Contains(actor) ? FieldActorShadowState.NativeFallback : FieldActorShadowState.IntentionalNone;
            }
            shadowReceiverIds.Clear();
            authoredShadowReceivers.Clear();
        }
        foreach (int id in shadowReceiverIds.Order())
        {
            nativeShadowReceiverDiagnostics.Add(field.Walkmesh.Triangles[id]);
        }
    }

    private static bool CanCastShadow(ActorVisual actor, FieldSpotLight? primary, Vector3[] world)
    {
        if (actor.GroundShadowRadii601To608 is { } shape && (shape.Count != 8 || actor.GroundShadowRadius601To608 is not null))
        {
            throw new NotSupportedException("Native ground-shadow shape requires exactly bytes601..608 and one current shape authority.");
        }
        bool zeroRadius = actor.GroundShadowRadii601To608 is { } radii ? radii.All(radius => radius == 0) : actor.GroundShadowRadius601To608 is not > 0;
        return actor.GroundShadowEnabled && !zeroRadius && actor.GroundShadowLevel609 is > 0 && primary is { Enabled: true, Intensity: > 0 } && primary.Color != Vector3.Zero && world.Any(point => FieldLightingMath.Influence(primary, point) > 0);
    }

    private FieldActorShadowState SelectNativeReceiver(ActorVisual actor, FieldSpotLight primary, out int support)
    {
        Vector3 center = actor.GroundShadowCentre ?? new Vector3(actor.X, actor.Y, actor.Z);
        if (!field.Walkmesh.TryPosition(center.X, center.Y, center.Z, 1, out _, out support))
        {
            // A valid native support point is necessary even for the explicitly diagnosed native fallback.
            if (actor.GroundShadowCentre is null || primary.Position.Z <= center.Z)
            {
                return FieldActorShadowState.IntentionalNone;
            }
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
            if (Math.Abs(Vector3.Cross(triangle.B - triangle.A, triangle.C - triangle.A).Z) < 0.01f)
            {
                // Match the walkmesh support query's rejection of vertical or degenerate ground.
                continue;
            }
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
        foreach (ModelFace face in model.Faces)
        {
            if (face.SemiTransparent || actor.PrimitiveTransparencyEnabled || face.Vertices.Any(vertex => actor.AdditionalControlDisabled && model.Vertices[vertex].Bone == model.ControlBones.Additional))
            {
                continue;
            }
            var (texture, textureScale) = model.ResolveFaceTexture(face, actor.RegisteredTextures);
            Vector2 size = new(texture.Width, texture.Height);
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
        foreach (WalkmeshTriangle receiver in nativeShadowReceiverDiagnostics)
        {
            if (authoredShadowReceivers.Count == 0)
            {
                QueueLightingReceiverTriangle(translucent, receiver.A, receiver.B, receiver.C);
                continue;
            }
            QueueLightingReceiverPolygons(translucent, [receiver.A, receiver.B, receiver.C], authoredShadowReceivers.Count);
        }
        for (int index = 0; index < authoredShadowReceivers.Count; index++)
        {
            QueueLightingReceiverPolygons(translucent, authoredShadowReceivers[index].Corners, index);
        }
    }

    private void QueueLightingReceiverPolygons(List<(float Depth, Action Draw)> translucent, Vector3[] polygon, int overlapCount)
    {
        List<Vector3[]> polygons = [polygon];
        for (int index = 0; index < overlapCount && polygons.Count > 0; index++)
        {
            authoredShadowReceivers[index].SubtractFrom(polygons);
        }
        foreach (Vector3[] visible in polygons)
        {
            // Clip against the same near depth as FieldCamera.Project before perspective division.
            Vector3[] clipped = new Vector3[visible.Length + 1];
            int count = ClipReceiverNear(visible, clipped);
            for (int index = 1; index + 1 < count; index++)
            {
                if (Vector3.Cross(clipped[index] - clipped[0], clipped[index + 1] - clipped[0]).LengthSquared() > 0.000001f)
                {
                    QueueLightingReceiverTriangle(translucent, clipped[0], clipped[index], clipped[index + 1]);
                }
            }
        }
    }

    private int ClipReceiverNear(Vector3[] polygon, Span<Vector3> clipped)
    {
        const float near = 1.01f;
        int count = 0;
        Vector3 previous = polygon[^1];
        float previousDepth = Vector3.Dot(camera.AxisZ, previous) + camera.Translation.Z;
        foreach (Vector3 point in polygon)
        {
            float depth = Vector3.Dot(camera.AxisZ, point) + camera.Translation.Z;
            if ((previousDepth >= near) != (depth >= near))
            {
                clipped[count++] = Vector3.Lerp(previous, point, (near - previousDepth) / (depth - previousDepth));
            }
            if (depth >= near)
            {
                clipped[count++] = point;
            }
            previous = point;
            previousDepth = depth;
        }
        return count;
    }

    private void QueueLightingReceiverTriangle(List<(float Depth, Action Draw)> translucent, Vector3 worldA, Vector3 worldB, Vector3 worldC)
    {
        ProjectedPoint a = ToViewport(camera.Project(worldA));
        ProjectedPoint b = ToViewport(camera.Project(worldB));
        ProjectedPoint c = ToViewport(camera.Project(worldC));
        if (a.Depth <= 1 || b.Depth <= 1 || c.Depth <= 1)
        {
            return;
        }
        TriangleMaterial material = GpuMaterial(null, TriangleBlend.Alpha, false) with { PerspectiveDepth = true, FieldLighting = FieldLightingPass.GroundShadow };
        TriangleVertex va = GpuVertex(a, Vector2.Zero, Vector4.One) with { WorldPosition = worldA };
        TriangleVertex vb = GpuVertex(b, Vector2.Zero, Vector4.One) with { WorldPosition = worldB };
        TriangleVertex vc = GpuVertex(c, Vector2.Zero, Vector4.One) with { WorldPosition = worldC };
        translucent.Add((MathF.Floor((a.Depth + b.Depth + c.Depth) / 12) * 4, () => TriangleRenderer!.Draw(va, vb, vc, material)));
    }

    private sealed record LitActorPose(Vector3[] World, Vector3[][] Normals);

    private void DrawLitModelTriangle(ActorVisual actor, ModelFace face, LitActorPose pose, int faceIndex, int a, int b, int c, Albeoris.Games.FF8.Imaging.PixelImage texture, Vector2 textureScale)
    {
        if (projected[face.Vertices[a]].Depth <= 1 || projected[face.Vertices[b]].Depth <= 1 || projected[face.Vertices[c]].Depth <= 1)
        {
            return;
        }
        TriangleMaterial material = GpuMaterial(texture, TriangleBlend.Replace, true, ModelSampling) with
        {
            DepthComparison = TriangleDepthComparison.Less,
            PerspectiveDepth = true,
            FieldLighting = FieldLightingPass.Model
        };
        Vector4 tint = new((actor.PresentationPolygonColor ?? actor.PolygonColor!.Value) / 128, 1);
        TriangleVertex Vertex(int corner) => GpuVertex(projected[face.Vertices[corner]], face.TextureCoordinates[corner] * textureScale / new Vector2(texture.Width, texture.Height), tint * (face.VertexColors?[corner] ?? Vector4.One)) with { WorldPosition = pose.World[face.Vertices[corner]], WorldNormal = pose.Normals[faceIndex][corner] };
        TriangleRenderer!.Draw(Vertex(a), Vertex(b), Vertex(c), material);
    }
}
