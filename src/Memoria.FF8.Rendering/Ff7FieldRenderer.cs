using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;

namespace Memoria.FF8.Rendering;

public enum Ff7ActorPoseDomain
{
    Field,
    World,
    Scene
}

public sealed record Ff7ActorVisual(int EntityId, int ModelIndex, int X4096, int Y4096, int Z4096, byte Direction, int AnimationIndex, int AnimationFrame16, bool Visible = true)
{
    public int PartyCharacter { get; init; } = -1;
    public Ff7ActorPoseDomain PoseDomain { get; init; }
    public int PhysicalSlot { get; init; }
    public byte NativeTransparencyMode { get; init; }
    public int FirstAnimationFrame { get; init; }
    public int LastAnimationFrame { get; init; } = -1;
    public bool AnimationLoop { get; init; }
    public Ff7FieldPoseBlend? AnimationBlend { get; init; }
    public Ff7FieldPosePhase PosePhase => new(AnimationFrame16, FirstAnimationFrame, LastAnimationFrame, AnimationLoop) { Blend = AnimationBlend };
    public bool AutomaticBlinkDisabled { get; init; }
    public byte NativeEyeLeft { get; init; }
    public byte NativeEyeRight { get; init; }
    public Ff7ModelRenderOverrides? ModelEffects { get; init; }
}
public sealed record Ff7ModelRenderOverrides(Func<int, bool> PartVisible, Func<Vector3, Vector3, uint, uint>? VertexColor, bool SuppressNativeLighting, bool RequiresOriginalTopology = true)
{
    public Func<int, int, uint, uint>? SourceVertexColor { get; init; }
    public Func<int, uint, uint>? AppearanceVertexColor { get; init; }
}
public sealed record Ff7FieldWaterSplashVisual(int TextureIndex, Vector2 Position, float Depth, float HalfSize);
public sealed record Ff7FieldPointerVisual(PixelImage Image, Vector2 Position, bool FlipHorizontal, bool FlipVertical);
public sealed record Ff7FieldRenderFrame(int CameraIndex, Vector2 CameraOffsetPixels, IReadOnlyList<Ff7ActorVisual> Actors, IReadOnlyList<ushort> BackgroundParameters, bool MovieActive = false, PcFieldCamera? CameraOverride = null, bool DrawBackgroundDuringMovie = false, IReadOnlyList<ushort[]>? PaletteRows = null, long PaletteVersion = 0)
{
    public bool FieldHidden { get; init; }
    public ushort Layer2Depth { get; init; } = 4095;
    public ushort Layer3Depth { get; init; } = 1;
    public short Layer2ScrollX16 { get; init; }
    public short Layer2ScrollY16 { get; init; }
    public short Layer3ScrollX16 { get; init; }
    public short Layer3ScrollY16 { get; init; }
    public Vector2 BackgroundCameraShakePixels { get; init; }
    public uint? FieldColorArgb { get; init; }
    public uint? QuadColorArgb { get; init; }
    public TriangleBlend QuadBlend { get; init; } = TriangleBlend.Alpha;
    public Ff7FieldPointerVisual? PlayerPointer { get; init; }
    public IReadOnlyList<Ff7FieldWaterSplashVisual> WaterSplashes { get; init; } = [];
}

public readonly record struct Ff7WorldVertex(Vector3 Position, Vector2 Uv, Vector4 Color)
{
    public Vector3? Normal { get; init; }
    public int AppearanceVertexIndex { get; init; } = -1;
}
public sealed record Ff7AppearanceTriangle(Ff7WorldVertex A, Ff7WorldVertex B, Ff7WorldVertex C, TriangleMaterial Material);
public interface IFf7ActorAppearance
{
    bool SupportsPartVisibility => false;
    bool SupportsVertexColorBindings => false;
    bool TrySampleVertices(Ff7ActorVisual pose, PcFieldModelDescriptor descriptor, Ff7FieldModelResources original, out IReadOnlyList<Vector3> worldVertices)
    {
        worldVertices = [];
        return false;
    }
    bool TrySampleVertices(Ff7ActorVisual pose, PcFieldModelDescriptor descriptor, Ff7FieldModelResources original, Vector3 modelLocalScale, out IReadOnlyList<Vector3> worldVertices)
    {
        if (modelLocalScale == Vector3.One)
        {
            return TrySampleVertices(pose, descriptor, original, out worldVertices);
        }
        worldVertices = [];
        return false;
    }
    bool TryCreate(Ff7ActorVisual actor, PcFieldModelDescriptor descriptor, Ff7FieldModelResources original, out IReadOnlyList<Ff7AppearanceTriangle> triangles);
}

/// <summary>Explicit selected PC projection contract. These values must come from the admitted resource/execution profile.</summary>
public readonly record struct Ff7ProjectionDepth(float NearPlane, float FarPlane)
{
    public float Project(float cameraZ)
    {
        return Ff7FieldProjectionMath.ProjectDepth(cameraZ, NearPlane, FarPlane);
    }

    public float ProjectBackground(ushort depth)
    {
        _ = Project(NearPlane);
        if (depth < 1)
        {
            return 0.0001f;
        }
        if (depth <= NearPlane / 4)
        {
            return depth / 1000f;
        }
        if (depth >= 4095)
        {
            return 0.9999f;
        }
        float projected = Project(depth * 4f);
        return projected is >= 0 and <= 1 ? projected : depth / 4096f;
    }
}

/// <summary>Native FF7 field resources submitted to the shared triangle backend. It never advances simulation or animation waits.</summary>
public sealed partial class Ff7FieldRenderer
{
    private readonly Ff7FieldResources field;
    private readonly IReadOnlyDictionary<int, Ff7FieldModelResources> models;
    private readonly Ff7ProjectionDepth projection;
    private readonly IFf7ActorAppearance? appearance;
    private PcFieldTile[]? backgroundTiles;
    private Dictionary<PcFieldTile, BackgroundAtlasRegion> backgroundAtlas = [];
    private long paletteVersion;
    private IReadOnlyList<ushort[]>? paletteRows;
    private readonly Dictionary<(PcPolygonModel Model, int Group), int[]> modelNormalBindings = [];
    private readonly Dictionary<PcTexture, PixelImage> modelImages = new(ReferenceEqualityComparer.Instance);

    public Ff7FieldRenderer(Ff7FieldResources field, IReadOnlyDictionary<int, Ff7FieldModelResources> models, Ff7ProjectionDepth projection, IFf7ActorAppearance? appearance = null)
    {
        this.field = field ?? throw new ArgumentNullException(nameof(field));
        this.models = models ?? throw new ArgumentNullException(nameof(models));
        this.projection = projection;
        this.appearance = appearance;
        _ = projection.Project(projection.NearPlane);
    }

    public void Render(ITriangleRenderer renderer, int width, int height, Ff7FieldRenderFrame frame)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(frame);
        if (width < 1 || height < 1 || width > 8192 || height > 8192 || frame.BackgroundParameters.Count != 256 || (uint)frame.CameraIndex >= field.Cameras.Count)
        {
            throw new InvalidDataException("FF7 field render dimensions, camera or background parameter state is invalid.");
        }
        frame = PrepareViewport(width, height, frame);
        bool drawBackground = !frame.FieldHidden && (!frame.MovieActive || frame.DrawBackgroundDuringMovie);
        if (drawBackground)
        {
            PrepareBackground(frame);
        }
        renderer.Begin(width, height, new Vector4(0, 0, 0, frame.MovieActive ? 0 : 1));
        try
        {
            if (frame.FieldHidden)
            {
                return;
            }
            if (drawBackground)
            {
                DrawBackground(renderer, width, height, frame, false);
            }
            Ff7ModelMaterialState modelState = new();
            TriangleMaterial? lastActorMaterial = null;
            foreach (Ff7ActorVisual actor in frame.Actors.Where(actor => actor.Visible && actor.NativeTransparencyMode == 0).OrderBy(actor => actor.PhysicalSlot))
            {
                DrawActor(renderer, width, height, frame, actor, modelState, ref lastActorMaterial);
            }
            DrawWaterSplashes(renderer, width, height, frame, modelState, lastActorMaterial);
            if (drawBackground)
            {
                DrawBackground(renderer, width, height, frame, true);
            }
            foreach (Ff7ActorVisual actor in frame.Actors.Where(actor => actor.Visible && actor.NativeTransparencyMode != 0).OrderBy(actor => actor.PhysicalSlot))
            {
                DrawActor(renderer, width, height, frame, actor, modelState, ref lastActorMaterial);
            }
            if (frame.QuadColorArgb is uint quad && (quad >> 24) != 0)
            {
                Vector4 clip = FieldClip(width, height);
                Vector4 color = new((quad >> 16 & 255) / 255f, (quad >> 8 & 255) / 255f, (quad & 255) / 255f, (quad >> 24) / 255f);
                if (frame.QuadBlend is not (TriangleBlend.Alpha or TriangleBlend.Add))
                {
                    throw new NotSupportedException("The native FFVII fade quad requires an admitted blend equation.");
                }
                TriangleMaterial material = new(null, frame.QuadBlend, TriangleDepthComparison.Always, false) { ClipRectangle = clip, PreserveTargetAlpha = frame.QuadBlend == TriangleBlend.Add };
                TrianglePrimitives.Quad(renderer, new Vector2(clip.X, clip.Y), new Vector2(clip.Z - clip.X, clip.W - clip.Y), BitConverter.Int32BitsToSingle(0x358637BD), color, material, Vector2.One);
            }
            if (frame.PlayerPointer is { } pointer)
            {
                Vector2 uvStart = new(pointer.FlipHorizontal ? 0.125f : 0, pointer.FlipVertical ? BitConverter.Int32BitsToSingle(0x3F6F1AA0) : 0);
                Vector2 uvEnd = new(pointer.FlipHorizontal ? 0 : 0.125f, pointer.FlipVertical ? 0 : BitConverter.Int32BitsToSingle(0x3F6F1AA0));
                TriangleMaterial material = new(pointer.Image, TriangleBlend.Alpha, TriangleDepthComparison.Always, false) { ClipRectangle = new Vector4(0, 0, width, height), PreserveTargetAlpha = false };
                TrianglePrimitives.Quad(renderer, FieldPoint(pointer.Position, width, height), new Vector2(15 * height / 240f), BitConverter.Int32BitsToSingle(0x3727C5AC), Vector4.One, material, uvEnd, uvStart);
            }
        }
        finally
        {
            renderer.End();
        }
    }

    public Vector3 Project(Vector3 world, PcFieldCamera camera, Vector2 cameraOffsetPixels)
    {
        return PcFieldProjection.Project(world, camera, new Vector2(160 + cameraOffsetPixels.X, 120 + cameraOffsetPixels.Y));
    }

    public static Matrix4x4 ModelBasis(PcFieldModelDescriptor descriptor)
    {
        return Ff7NativeFieldPoseMath.ModelBasis(descriptor);
    }

    public static IReadOnlyList<Matrix4x4> Pose(Ff7FieldModelResources resources, PcFieldAnimation animation, int frameIndex, float nativeTranslationScale = float.NaN)
    {
        return Ff7FieldPoseSampler.Sample(resources, animation, new(checked(frameIndex * 16)), nativeTranslationScale).Bones;
    }

    private void PrepareBackground(Ff7FieldRenderFrame frame)
    {
        IReadOnlyList<ushort[]>? liveRows = frame.PaletteRows is { Count: > 0 } ? frame.PaletteRows : null;
        bool samePalette = backgroundTiles is not null && ReferenceEquals(paletteRows, liveRows) && (liveRows is null || paletteVersion == frame.PaletteVersion);
        backgroundTiles ??= field.Background.Layers.SelectMany(layer => layer).OrderBy(tile => tile.UsesFxPage).ToArray();
        if (samePalette && Array.TrueForAll(backgroundTiles, tile => !IsVisible(tile, frame) || backgroundAtlas.ContainsKey(tile)))
        {
            return;
        }

        PcFieldPalettes palettes = field.Palettes;
        if (liveRows is not null)
        {
            if (liveRows.Count != palettes.Palettes.Count || liveRows.Any(row => row.Length != palettes.ColorsPerPalette))
            {
                throw new InvalidDataException("FFVII presented palette dimensions differ from the selected field.");
            }
            palettes = palettes with { Palettes = liveRows };
        }

        // Pack tiles in submission order within each pass so adjacent tiles can
        // batch without changing native equal-depth or blend ordering.
        // Decode a conditional tile only when its native visibility first admits it.
        PcFieldTile[] tiles = Array.FindAll(backgroundTiles, tile => IsVisible(tile, frame) || samePalette && backgroundAtlas.ContainsKey(tile));
        PixelImage[] images = Array.ConvertAll(tiles, tile => DecodeTile(field.Background, palettes, tile, liveRows is not null));
        Dictionary<PixelImage, BackgroundAtlasRegion> regions = BackgroundTextureAtlas.Build(images);
        Dictionary<PcFieldTile, BackgroundAtlasRegion> atlas = new(tiles.Length);
        for (int index = 0; index < tiles.Length; index++)
        {
            atlas.Add(tiles[index], regions[images[index]]);
        }

        backgroundAtlas = atlas;
        paletteRows = liveRows;
        paletteVersion = frame.PaletteVersion;
    }

    private static bool IsVisible(PcFieldTile tile, Ff7FieldRenderFrame frame)
    {
        return tile.Layer == 0 || tile.State == 0 || (frame.BackgroundParameters[tile.Parameter] & tile.State) != 0;
    }

    private void DrawBackground(ITriangleRenderer renderer, int width, int height, Ff7FieldRenderFrame frame, bool blended)
    {
        foreach (PcFieldTile tile in field.Background.Layers.SelectMany(layer => layer))
        {
            if (tile.UsesFxPage != blended || !IsVisible(tile, frame))
            {
                continue;
            }
            BackgroundAtlasRegion region = backgroundAtlas[tile];
            float z = tile.Layer switch
            {
                0 => 0.9997f,
                2 => frame.Layer2Depth >= 4095 ? 0.9998f : projection.ProjectBackground(frame.Layer2Depth),
                3 => projection.ProjectBackground(frame.Layer3Depth),
                _ => tile.Z / 10000000f
            };
            if (!float.IsFinite(z) || z < 0 || z > 1)
            {
                throw new InvalidDataException($"FF7 tile {tile.Layer}/{tile.Index} has invalid persisted normalized depth.");
            }
            TriangleBlend blend = blended ? tile.BlendMode switch
            {
                0 => TriangleBlend.HalfAdd,
                1 => TriangleBlend.Add,
                2 => TriangleBlend.Subtract,
                3 => TriangleBlend.QuarterAdd,
                _ => throw new NotSupportedException($"FF7 tile blend {tile.BlendMode} is unsupported.")
            } : TriangleBlend.Replace;
            TriangleMaterial material = TriangleSamplingProfile.PixelExact.Apply(new(region.Image, blend, TriangleDepthComparison.LessEqual, !blended) { PreserveTargetAlpha = false, ClipRectangle = FieldClip(width, height) });
            Vector2 origin = BackgroundTileOrigin(tile, frame);
            TrianglePrimitives.Quad(renderer, FieldPoint(origin, width, height), new Vector2(tile.PixelSize * (height / 240f)), z, FieldColor(frame), material, region.End, region.Start, pointSampleRectangle: true);
        }
    }

    private void DrawActor(ITriangleRenderer renderer, int width, int height, Ff7FieldRenderFrame frame, Ff7ActorVisual actor, Ff7ModelMaterialState currentState, ref TriangleMaterial? lastActorMaterial)
    {
        PcFieldCamera selectedCamera = frame.CameraOverride ?? field.Cameras[frame.CameraIndex];
        if (selectedCamera.AxisZ == Vector3.Zero && selectedCamera.Translation.Z <= 0)
        {
            return;
        }
        if ((uint)actor.ModelIndex >= field.Models.Models.Count || !models.TryGetValue(actor.ModelIndex, out Ff7FieldModelResources? resources))
        {
            throw new NotSupportedException($"FF7 entity {actor.EntityId} has no decoded model descriptor {actor.ModelIndex}.");
        }
        PcFieldModelDescriptor descriptor = field.Models.Models[actor.ModelIndex];
        Ff7FieldLighting lighting = new(descriptor);
        if ((uint)actor.AnimationIndex >= descriptor.Animations.Count || actor.AnimationFrame16 < 0)
        {
            throw new InvalidDataException($"FF7 entity {actor.EntityId} has invalid animation state.");
        }
        string animationName = Path.GetFileNameWithoutExtension(descriptor.Animations[actor.AnimationIndex]) + ".a";
        if (appearance is not null && appearance.TryCreate(actor, descriptor, resources, out IReadOnlyList<Ff7AppearanceTriangle> replacement))
        {
            if (actor.ModelEffects is { } indexedEffects && (!appearance.SupportsPartVisibility || indexedEffects.SourceVertexColor is not null && indexedEffects.AppearanceVertexColor is null || indexedEffects.AppearanceVertexColor is not null && !appearance.SupportsVertexColorBindings || indexedEffects.RequiresOriginalTopology && (indexedEffects.VertexColor is not null || indexedEffects.SuppressNativeLighting)))
            {
                throw new NotSupportedException("FFVII topology-owned model effects require an explicit replacement-appearance binding.");
            }
            foreach (Ff7AppearanceTriangle triangle in replacement)
            {
                TriangleVertex Vertex(Ff7WorldVertex vertex)
                {
                    Vector3 point = Project(vertex.Position, frame.CameraOverride ?? field.Cameras[frame.CameraIndex], frame.CameraOffsetPixels);
                    uint original = Argb(vertex.Color);
                    if (actor.ModelEffects?.VertexColor is { } effectColor)
                    {
                        original = effectColor(vertex.Position, vertex.Normal ?? Vector3.Zero, original);
                    }
                    if (actor.ModelEffects?.AppearanceVertexColor is { } appearanceColor)
                    {
                        if (vertex.AppearanceVertexIndex < 0)
                        {
                            throw new InvalidDataException("Mapped vertex colors require the sampled appearance's explicit stable vertex identities.");
                        }
                        original = appearanceColor(vertex.AppearanceVertexIndex, original);
                    }
                    Vector4 color = vertex.Normal is Vector3 normal ? lighting.Shade(normal, original, frame.FieldColorArgb) : Tint(Color(original), frame.FieldColorArgb);
                    if (actor.NativeTransparencyMode != 0)
                    {
                        color.W = 192f / 255;
                    }
                    return new(FieldPoint(new Vector2(point.X, point.Y), width, height), projection.Project(point.Z), 1 / point.Z, vertex.Uv, color);
                }
                TriangleVertex a = Vertex(triangle.A);
                TriangleVertex b = Vertex(triangle.B);
                TriangleVertex c = Vertex(triangle.C);
                if (a.ReciprocalW > 0 && b.ReciprocalW > 0 && c.ReciprocalW > 0)
                {
                    TriangleMaterial submittedMaterial = TriangleSamplingProfile.Model(triangle.Material.Sampler).Apply(triangle.Material with { ClipRectangle = FieldClip(width, height), Blend = actor.NativeTransparencyMode != 0 && triangle.Material.Blend != TriangleBlend.Replace ? TriangleBlend.Alpha : triangle.Material.Blend });
                    renderer.Draw(a, b, c, submittedMaterial);
                    lastActorMaterial = submittedMaterial;
                }
            }
            return;
        }
        PcFieldAnimation animation = resources.Animations[animationName];
        IReadOnlyList<Matrix4x4> bones = Ff7FieldPoseSampler.Sample(resources, animation, actor.PosePhase, ModelBasis(descriptor).M11).Bones;
        Matrix4x4 actorTransform = ModelBasis(descriptor) * Matrix4x4.CreateRotationZ(actor.Direction * MathF.Tau / 256) * Matrix4x4.CreateTranslation(actor.X4096 / 4096f, actor.Y4096 / 4096f, actor.Z4096 / 4096f);
        Matrix4x4 actorNormalTransform = Matrix4x4.CreateRotationX(-MathF.PI / 2) * Matrix4x4.CreateRotationZ(actor.Direction * MathF.Tau / 256);
        PcFieldCamera camera = frame.CameraOverride ?? field.Cameras[frame.CameraIndex];
        for (int partIndex = 0; partIndex < resources.Geometry.Count; partIndex++)
        {
            if (actor.ModelEffects?.PartVisible(partIndex) == false)
            {
                continue;
            }
            Ff7BoneGeometry part = resources.Geometry[partIndex];
            PcPolygonModel model = part.Polygon;
            Matrix4x4 transform = bones[part.BoneIndex] * actorTransform;
            Matrix4x4 normalTransform = bones[part.BoneIndex] * actorNormalTransform;
            for (int groupIndex = 0; groupIndex < model.Groups.Count; groupIndex++)
            {
                PcModelGroup group = model.Groups[groupIndex];
                if (model.RenderStates.Count != model.Groups.Count)
                {
                    throw new NotSupportedException("FF7 P groups require their corresponding original render-state records.");
                }
                PcTexture? alternate = resources.Eyes?.Alternate(partIndex, groupIndex, actor.NativeEyeLeft, actor.NativeEyeRight);
                if ((actor.NativeEyeLeft == 2 || actor.NativeEyeRight == 2) && resources.Eyes is null)
                {
                    throw new NotSupportedException("Native closed eyes require the original model eye-resource binding.");
                }
                PcRenderState state = alternate is null ? PcRenderState.Read(model.RenderStates[groupIndex].Span) : Ff7FieldEyeResources.AlternateMaterial;
                PixelImage? texture = null;
                if (group.TexturesUsed != 0)
                {
                    if ((uint)group.TextureNumber >= part.Textures.Count)
                    {
                        throw new InvalidDataException("FF7 model group references an absent texture.");
                    }
                    PcTexture tex = alternate ?? part.Textures[group.TextureNumber];
                    if (!modelImages.TryGetValue(tex, out texture))
                    {
                        texture = DecodeTexture(tex);
                        modelImages.Add(tex, texture);
                    }
                }
                currentState.Apply(state, actor.NativeTransparencyMode != 0);
                if (!modelNormalBindings.TryGetValue((model, groupIndex), out int[]? normalBindings))
                {
                    normalBindings = Ff7FieldLighting.BindNormals(model, group);
                    modelNormalBindings.Add((model, groupIndex), normalBindings);
                }
                TriangleMaterial material = TriangleSamplingProfile.Model(currentState.Sampler).Apply(new(texture, currentState.Blend, currentState.DepthTest ? TriangleDepthComparison.LessEqual : TriangleDepthComparison.Always, currentState.DepthWrite, currentState.AlphaReference) { PreserveTargetAlpha = false, ClipRectangle = FieldClip(width, height) });
                for (int index = group.PolygonStart; index < group.PolygonStart + group.PolygonCount; index++)
                {
                    PcModelPolygon polygon = model.Polygons[index];
                    TriangleVertex Vertex(int local)
                    {
                        int vertex = group.VertexStart + local;
                        Vector3 position = Project(Vector3.Transform(model.Vertices[vertex], transform), camera, frame.CameraOffsetPixels);
                        Vector2 uv = texture is null ? Vector2.Zero : model.TextureCoordinates[group.TextureCoordinateStart + local];
                        uint originalColor = vertex < model.VertexColors.Count ? model.VertexColors[vertex] : uint.MaxValue;
                        int normalIndex = normalBindings[local];
                        if ((normalIndex >= 0 || actor.ModelEffects?.RequiresOriginalTopology == false) && actor.ModelEffects?.VertexColor is { } effectColor)
                        {
                            originalColor = effectColor(model.Vertices[vertex], normalIndex >= 0 ? model.Normals[normalIndex] : Vector3.Zero, originalColor);
                        }
                        if (actor.ModelEffects?.SourceVertexColor is { } sourceColor)
                        {
                            originalColor = sourceColor(partIndex, vertex, originalColor);
                        }
                        Vector4 color = normalIndex >= 0 && actor.ModelEffects?.SuppressNativeLighting != true ? lighting.Shade(Vector3.TransformNormal(model.Normals[normalIndex], normalTransform), originalColor, frame.FieldColorArgb) : Tint(Color(originalColor), frame.FieldColorArgb);
                        if (actor.NativeTransparencyMode != 0)
                        {
                            color.W = 192f / 255;
                        }
                        else
                        {
                            color.W *= state.VertexAlpha / 255f;
                        }
                        return new(FieldPoint(new Vector2(position.X, position.Y), width, height), projection.Project(position.Z), 1 / position.Z, uv, color);
                    }
                    TriangleVertex a = Vertex(polygon.A);
                    TriangleVertex b = Vertex(polygon.B);
                    TriangleVertex c = Vertex(polygon.C);
                    if (!currentState.Gouraud)
                    {
                        b = b with { Color = a.Color };
                        c = c with { Color = a.Color };
                    }
                    if (currentState.Cull && Cross(b.Position - a.Position, c.Position - a.Position) >= 0)
                    {
                        continue;
                    }
                    if (a.ReciprocalW > 0 && b.ReciprocalW > 0 && c.ReciprocalW > 0)
                    {
                        renderer.Draw(a, b, c, material);
                        lastActorMaterial = material;
                    }
                }
            }
        }
    }

    public static PixelImage DecodeTile(PcFieldBackground background, PcFieldPalettes palettes, PcFieldTile tile, bool paletteRowsUseCachedIndices = false)
    {
        PcFieldTexturePage page = background.Textures.Single(texture => texture.Id == tile.EffectiveTextureId);
        int size = tile.PixelSize;
        if (tile.EffectiveSourceX < 0 || tile.EffectiveSourceY < 0 || tile.EffectiveSourceX > 256 - size || tile.EffectiveSourceY > 256 - size)
        {
            throw new NotSupportedException($"FF7 tile {tile.Layer}/{tile.Index}: shape {tile.Width}x{tile.Height}, source {tile.EffectiveSourceX},{tile.EffectiveSourceY}, texture {tile.EffectiveTextureId} has an unsupported extent.");
        }
        PixelImage image = new(size, size);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int index = page.GetIndex(tile.EffectiveSourceX + x, tile.EffectiveSourceY + y);
                if (page.Depth == 2)
                {
                    // Cached direct-color pages use native RGB565, independently of PS1 RGB555 palette rows.
                    int green = index >> 5 & 63;
                    image[x, y] = new Rgba32(Expand(index >> 11 & 31), (byte)((green << 2) | (green >> 4)), Expand(index & 31), index == 0 ? (byte)0 : (byte)255);
                    continue;
                }
                if (tile.PaletteId == palettes.Palettes.Count || tile.PaletteId == palettes.Palettes.Count + 1)
                {
                    // The native background texture constructor appends opaque black and half-alpha white RGBA palettes.
                    // These rows include index zero and do not use the resource RGB555 transparency exchange.
                    image[x, y] = tile.PaletteId == palettes.Palettes.Count ? new Rgba32(0, 0, 0, 255) : new Rgba32(255, 255, 255, 128);
                    continue;
                }
                if (tile.PaletteId < 0 || tile.PaletteId >= palettes.Palettes.Count)
                {
                    throw new InvalidDataException("FFVII background tile selects a palette outside the native texture palette bank.");
                }
                ushort color = palettes.Palettes[tile.PaletteId][index];
                if (!paletteRowsUseCachedIndices && page.Depth != 2 && background.PaletteHasTransparentColor[tile.PaletteId] != 0)
                {
                    // Cached PC texture indices already reserve zero; its original color occupies every formerly zero palette entry.
                    color = index == 0 ? (ushort)0 : color == 0 ? palettes.Palettes[tile.PaletteId][0] : color;
                }
                bool transparent = color == 0;
                image[x, y] = new Rgba32(Expand(color & 31), Expand((color >> 5) & 31), Expand((color >> 10) & 31), transparent ? (byte)0 : (byte)255);
            }
        }
        return image;
    }

    public static PixelImage DecodeTexture(PcTexture texture, bool useColorKey = false, int? paletteOverride = null)
    {
        if (texture.RenderInfo.UsePaletteColorKey != 0)
        {
            throw new NotSupportedException("FF7 TEX per-palette color keys require their original selected-palette contract.");
        }
        if (texture.BytesPerPixel is not (1 or 4) || texture.BytesPerPixel == 1 && texture.Palettes.Count == 0)
        {
            throw new NotSupportedException("FF7 model texture requires the admitted indexed8 or BGRA32 profile.");
        }
        int palette = paletteOverride ?? checked((int)texture.RenderInfo.PaletteIndex);
        if (texture.BytesPerPixel == 1 && (texture.RenderInfo.UsePalette == 0 || texture.RenderInfo.BitsPerIndex != 8 || (uint)palette >= texture.Palettes.Count))
        {
            throw new NotSupportedException("FF7 indexed texture has an unsupported selected-palette or index-width contract.");
        }
        PixelImage image = new(texture.Width, texture.Height);
        bool keyed = useColorKey && texture.BytesPerPixel == 1 && texture.RenderInfo.ColorKey != 0;
        ReadOnlySpan<byte> bytes = texture.Pixels.Span;
        for (int i = 0; i < image.Pixels.Length; i++)
        {
            uint color = texture.BytesPerPixel == 1 ? texture.Palettes[palette][bytes[i]] : System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(bytes[(i * 4)..]);
            if (keyed && bytes[i] == 0)
            {
                color = 0;
            }
            image.Pixels[i] = new Rgba32((byte)(color >> 16), (byte)(color >> 8), (byte)color, (byte)(color >> 24));
        }
        return image;
    }

    internal static Matrix4x4 Rotate(Vector3 degrees)
    {
        return Ff7NativeFieldPoseMath.Rotate(degrees);
    }

    private static Vector4 Color(uint argb) => new((argb >> 16 & 255) / 255f, (argb >> 8 & 255) / 255f, (argb & 255) / 255f, (argb >> 24) / 255f);
    private static uint Argb(Vector4 color) => (uint)Math.Clamp((int)MathF.Round(color.W * 255), 0, 255) << 24 | (uint)Math.Clamp((int)MathF.Round(color.X * 255), 0, 255) << 16 | (uint)Math.Clamp((int)MathF.Round(color.Y * 255), 0, 255) << 8 | (uint)Math.Clamp((int)MathF.Round(color.Z * 255), 0, 255);
    internal static Vector2 FieldPoint(Vector2 point, int width, int height)
    {
        float scale = height / 240f;
        return new Vector2((width - 320 * scale) / 2 + point.X * scale, point.Y * scale);
    }

    private Vector4 FieldClip(int width, int height) => new(viewportLeft, height * 8f / 240, viewportRight, height * 232f / 240);
    private static float Cross(Vector2 first, Vector2 second) => first.X * second.Y - first.Y * second.X;
    private static byte Expand(int channel) => (byte)((channel << 3) | (channel >> 2));

    private static Vector4 FieldColor(Ff7FieldRenderFrame frame) => frame.FieldColorArgb is uint argb ? Tint(Vector4.One, argb) : Vector4.One;

    private static Vector4 Tint(Vector4 color, uint? argb)
    {
        if (argb is not uint tint)
        {
            return color;
        }
        return new Vector4(color.X * ((tint >> 16) & 255) / 255f, color.Y * ((tint >> 8) & 255) / 255f, color.Z * (tint & 255) / 255f, color.W);
    }

}
