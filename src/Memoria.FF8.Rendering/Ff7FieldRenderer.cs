using System.Globalization;
using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;

namespace Memoria.FF8.Rendering;

public sealed record Ff7ActorVisual(int EntityId, int ModelIndex, int X4096, int Y4096, int Z4096, byte Direction, int AnimationIndex, int AnimationFrame16, bool Visible = true)
{
    public int PartyCharacter { get; init; } = -1;
    public bool IsWorldModel { get; init; }
    public bool AutomaticBlinkDisabled { get; init; }
    public byte NativeEyeLeft { get; init; }
    public byte NativeEyeRight { get; init; }
    public Ff7ModelRenderOverrides? ModelEffects { get; init; }
}
public sealed record Ff7ModelRenderOverrides(Func<int, bool> PartVisible, Func<Vector3, Vector3, uint, uint>? VertexColor, bool SuppressNativeLighting);
public sealed record Ff7FieldPointerVisual(PixelImage Image, Vector2 Position, bool FlipHorizontal, bool FlipVertical);
public sealed record Ff7FieldRenderFrame(int CameraIndex, Vector2 CameraOffsetPixels, IReadOnlyList<Ff7ActorVisual> Actors, IReadOnlyList<ushort> BackgroundParameters, bool MovieActive = false, PcFieldCamera? CameraOverride = null, bool DrawBackgroundDuringMovie = false, IReadOnlyList<ushort[]>? PaletteRows = null, long PaletteVersion = 0)
{
    public uint? FieldColorArgb { get; init; }
    public uint? QuadColorArgb { get; init; }
    public TriangleBlend QuadBlend { get; init; } = TriangleBlend.Alpha;
    public Ff7FieldPointerVisual? PlayerPointer { get; init; }
}

public readonly record struct Ff7WorldVertex(Vector3 Position, Vector2 Uv, Vector4 Color)
{
    public Vector3? Normal { get; init; }
}
public sealed record Ff7AppearanceTriangle(Ff7WorldVertex A, Ff7WorldVertex B, Ff7WorldVertex C, TriangleMaterial Material);
public interface IFf7ActorAppearance
{
    bool SupportsPartVisibility => false;
    bool TryCreate(Ff7ActorVisual actor, PcFieldModelDescriptor descriptor, Ff7FieldModelResources original, out IReadOnlyList<Ff7AppearanceTriangle> triangles);
}

/// <summary>Explicit selected PC projection contract. These values must come from the admitted resource/execution profile.</summary>
public readonly record struct Ff7ProjectionDepth(float NearPlane, float FarPlane)
{
    public float Project(float cameraZ)
    {
        if (!float.IsFinite(NearPlane) || !float.IsFinite(FarPlane) || NearPlane <= 0 || FarPlane <= NearPlane)
        {
            throw new InvalidDataException("FF7 rendering requires a finite admitted near/far projection contract.");
        }
        return FarPlane / (FarPlane - NearPlane) - FarPlane * NearPlane / ((FarPlane - NearPlane) * cameraZ);
    }
}

/// <summary>Native FF7 field resources submitted to the shared triangle backend. It never advances simulation or animation waits.</summary>
public sealed class Ff7FieldRenderer
{
    private readonly Ff7FieldResources field;
    private readonly IReadOnlyDictionary<int, Ff7FieldModelResources> models;
    private readonly Ff7ProjectionDepth projection;
    private readonly IFf7ActorAppearance? appearance;
    private readonly Dictionary<PcFieldTile, PixelImage> tileImages = [];
    private long paletteVersion = -1;
    private IReadOnlyList<ushort[]>? paletteRows;
    private PcFieldPalettes? renderedPalettes;
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
        if (frame.PaletteRows is { Count: > 0 } && (!ReferenceEquals(paletteRows, frame.PaletteRows) || paletteVersion != frame.PaletteVersion))
        {
            if (frame.PaletteRows.Count != field.Palettes.Palettes.Count || frame.PaletteRows.Any(row => row.Length != field.Palettes.ColorsPerPalette))
            {
                throw new InvalidDataException("FFVII presented palette dimensions differ from the selected field.");
            }
            paletteRows = frame.PaletteRows;
            paletteVersion = frame.PaletteVersion;
            renderedPalettes = field.Palettes with { Palettes = frame.PaletteRows.Select(row => (IReadOnlyList<ushort>)row).ToArray() };
            tileImages.Clear();
        }
        renderer.Begin(width, height, new Vector4(0, 0, 0, frame.MovieActive ? 0 : 1));
        try
        {
            if (!frame.MovieActive || frame.DrawBackgroundDuringMovie)
            {
                DrawBackground(renderer, width, height, frame, false);
            }
            Ff7ModelMaterialState modelState = new();
            foreach (Ff7ActorVisual actor in frame.Actors.Where(actor => actor.Visible))
            {
                DrawActor(renderer, width, height, frame, actor, modelState);
            }
            if (!frame.MovieActive || frame.DrawBackgroundDuringMovie)
            {
                DrawBackground(renderer, width, height, frame, true);
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
        float scale = float.Parse(descriptor.Scale, CultureInfo.InvariantCulture) / 4096 * 30.43090057373047f;
        if (!float.IsFinite(scale) || scale <= 0)
        {
            throw new InvalidDataException("FFVII model scale must be finite and positive.");
        }
        return Matrix4x4.CreateRotationX(-MathF.PI / 2) * Matrix4x4.CreateScale(scale);
    }

    public static IReadOnlyList<Matrix4x4> Pose(Ff7FieldModelResources resources, PcFieldAnimation animation, int frameIndex, float nativeTranslationScale = float.NaN)
    {
        if (animation.RotationOrder != 0x020001 || (uint)frameIndex >= animation.Frames.Count)
        {
            throw new NotSupportedException("FF7 pose requires the admitted Z-X-Y rotation order, bounded native HRC channels and an original frame within the clip.");
        }
        if (resources.KnownPoseBinding is { } binding && ReferenceEquals(animation, binding.Motion))
        {
            return Ff7KnownFieldPoseRetargeter.Pose(resources, binding, frameIndex);
        }
        PcFieldAnimationFrame frame = animation.Frames[frameIndex];
        Matrix4x4 root = Rotate(frame.RootRotation) * Matrix4x4.CreateTranslation(frame.RootTranslation * new Vector3(1, -1, 1));
        Matrix4x4[] transforms = new Matrix4x4[resources.Hierarchy.Bones.Count];
        Dictionary<string, int> indices = resources.Hierarchy.Bones.ToDictionary(bone => bone.Name, bone => bone.Index, StringComparer.OrdinalIgnoreCase);
        foreach (HrcBone bone in resources.Hierarchy.Bones)
        {
            Matrix4x4 parent = root;
            if (!bone.ParentName.Equals("root", StringComparison.OrdinalIgnoreCase))
            {
                HrcBone parentBone = resources.Hierarchy.Bones[indices[bone.ParentName]];
                parent = Matrix4x4.CreateTranslation(0, 0, -parentBone.Length) * transforms[parentBone.Index];
            }
            Vector3 rotation = resources.Hierarchy.IsRootOnly ? Vector3.Zero : animation.ReadNativeBoneRotation(frameIndex, bone.Index, nativeTranslationScale);
            transforms[bone.Index] = Rotate(rotation) * parent;
        }
        return Array.AsReadOnly(transforms);
    }

    private void DrawBackground(ITriangleRenderer renderer, int width, int height, Ff7FieldRenderFrame frame, bool blended)
    {
        foreach (PcFieldTile tile in field.Background.Layers.SelectMany(layer => layer))
        {
            if ((tile.Blending != 0) != blended || tile.Layer > 0 && tile.State != 0 && (frame.BackgroundParameters[tile.Parameter] & tile.State) == 0)
            {
                continue;
            }
            if (!tileImages.TryGetValue(tile, out PixelImage? image))
            {
                image = DecodeTile(field.Background, renderedPalettes ?? field.Palettes, tile, frame.PaletteRows is { Count: > 0 });
                tileImages.Add(tile, image);
            }
            // Native background records persist normalized device depth scaled by ten million.
            float z = tile.Layer == 0 ? 0.9997f : tile.Z / 10000000f;
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
            TriangleMaterial material = new(image, blend, TriangleDepthComparison.LessEqual, !blended, Sampler: TriangleSampler.PointClamp) { PreserveTargetAlpha = false, ClipRectangle = FieldClip(width, height) };
            Vector2 origin = new(160 + tile.DestinationX + frame.CameraOffsetPixels.X, 120 + tile.DestinationY + frame.CameraOffsetPixels.Y);
            TrianglePrimitives.Quad(renderer, FieldPoint(origin, width, height), new Vector2(image.Width, image.Height) * (height / 240f), z, FieldColor(frame), material, Vector2.One);
        }
    }

    private void DrawActor(ITriangleRenderer renderer, int width, int height, Ff7FieldRenderFrame frame, Ff7ActorVisual actor, Ff7ModelMaterialState currentState)
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
            if (actor.ModelEffects is not null && (!appearance.SupportsPartVisibility || actor.ModelEffects.VertexColor is not null || actor.ModelEffects.SuppressNativeLighting))
            {
                throw new NotSupportedException("FFVII topology-owned model effects require an explicit replacement-appearance binding.");
            }
            foreach (Ff7AppearanceTriangle triangle in replacement)
            {
                TriangleVertex Vertex(Ff7WorldVertex vertex)
                {
                    Vector3 point = Project(vertex.Position, frame.CameraOverride ?? field.Cameras[frame.CameraIndex], frame.CameraOffsetPixels);
                    Vector4 color = vertex.Normal is Vector3 normal ? lighting.Shade(normal, Argb(vertex.Color), frame.FieldColorArgb) : Tint(vertex.Color, frame.FieldColorArgb);
                    return new(FieldPoint(new Vector2(point.X, point.Y), width, height), projection.Project(point.Z), 1 / point.Z, vertex.Uv, color);
                }
                TriangleVertex a = Vertex(triangle.A);
                TriangleVertex b = Vertex(triangle.B);
                TriangleVertex c = Vertex(triangle.C);
                if (a.ReciprocalW > 0 && b.ReciprocalW > 0 && c.ReciprocalW > 0)
                {
                    renderer.Draw(a, b, c, triangle.Material with { ClipRectangle = FieldClip(width, height) });
                }
            }
            return;
        }
        PcFieldAnimation animation = resources.Animations[animationName];
        int animationFrame = actor.AnimationFrame16 >> 4;
        IReadOnlyList<Matrix4x4> bones = Pose(resources, animation, animationFrame, ModelBasis(descriptor).M11);
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
                currentState.Apply(state);
                if (!modelNormalBindings.TryGetValue((model, groupIndex), out int[]? normalBindings))
                {
                    normalBindings = Ff7FieldLighting.BindNormals(model, group);
                    modelNormalBindings.Add((model, groupIndex), normalBindings);
                }
                TriangleMaterial material = new(texture, currentState.Blend, currentState.DepthTest ? TriangleDepthComparison.LessEqual : TriangleDepthComparison.Always, currentState.DepthWrite, currentState.AlphaReference, currentState.Sampler) { PreserveTargetAlpha = false, ClipRectangle = FieldClip(width, height) };
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
                        if (normalIndex >= 0 && actor.ModelEffects?.VertexColor is { } effectColor)
                        {
                            originalColor = effectColor(model.Vertices[vertex], model.Normals[normalIndex], originalColor);
                        }
                        Vector4 color = normalIndex >= 0 && actor.ModelEffects?.SuppressNativeLighting != true ? lighting.Shade(Vector3.TransformNormal(model.Normals[normalIndex], normalTransform), originalColor, frame.FieldColorArgb) : Tint(Color(originalColor), frame.FieldColorArgb);
                        color.W *= state.VertexAlpha / 255f;
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
                ushort color = page.Depth == 2 ? checked((ushort)index) : palettes.Palettes[tile.PaletteId][index];
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

    public static PixelImage DecodeTexture(PcTexture texture)
    {
        if (texture.RenderInfo.UsePaletteColorKey != 0)
        {
            throw new NotSupportedException("FF7 TEX per-palette color keys require their original selected-palette contract.");
        }
        if (texture.BytesPerPixel is not (1 or 4) || texture.BytesPerPixel == 1 && texture.Palettes.Count == 0)
        {
            throw new NotSupportedException("FF7 model texture requires the admitted indexed8 or BGRA32 profile.");
        }
        int palette = checked((int)texture.RenderInfo.PaletteIndex);
        if (texture.BytesPerPixel == 1 && (texture.RenderInfo.UsePalette == 0 || texture.RenderInfo.BitsPerIndex != 8 || (uint)palette >= texture.Palettes.Count))
        {
            throw new NotSupportedException("FF7 indexed texture has an unsupported selected-palette or index-width contract.");
        }
        PixelImage image = new(texture.Width, texture.Height);
        ReadOnlySpan<byte> bytes = texture.Pixels.Span;
        for (int i = 0; i < image.Pixels.Length; i++)
        {
            uint color = texture.BytesPerPixel == 1 ? texture.Palettes[palette][bytes[i]] : System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(bytes[(i * 4)..]);
            image.Pixels[i] = new Rgba32((byte)(color >> 16), (byte)(color >> 8), (byte)color, (byte)(color >> 24));
        }
        return image;
    }

    internal static Matrix4x4 Rotate(Vector3 degrees)
    {
        Vector3 radians = degrees * (MathF.PI / 180);
        return Matrix4x4.CreateRotationZ(radians.Z) * Matrix4x4.CreateRotationX(radians.X) * Matrix4x4.CreateRotationY(radians.Y);
    }

    private static Vector4 Color(uint argb) => new((argb >> 16 & 255) / 255f, (argb >> 8 & 255) / 255f, (argb & 255) / 255f, (argb >> 24) / 255f);
    private static uint Argb(Vector4 color) => (uint)Math.Clamp((int)MathF.Round(color.W * 255), 0, 255) << 24 | (uint)Math.Clamp((int)MathF.Round(color.X * 255), 0, 255) << 16 | (uint)Math.Clamp((int)MathF.Round(color.Y * 255), 0, 255) << 8 | (uint)Math.Clamp((int)MathF.Round(color.Z * 255), 0, 255);
    internal static Vector2 FieldPoint(Vector2 point, int width, int height)
    {
        float scale = height / 240f;
        return new Vector2((width - 320 * scale) / 2 + point.X * scale, point.Y * scale);
    }

    private static Vector4 FieldClip(int width, int height) => new(0, height * 8f / 240, width, height * 232f / 240);
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
