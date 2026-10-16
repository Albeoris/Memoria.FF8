using System.Numerics;
using System.Runtime.InteropServices;
using Albeoris.Games.FF8.Imaging;
using Albeoris.Games.FF8.Imaging.Map;
using Memoria.FF8.Assets;

namespace Memoria.FF8.Rendering;

/// <param name="Direction">FF8 direction units, one turn is 256.</param>
/// <param name="RootTranslationZ">Separate native SETROOTTRANS elevation word; never the SETGETA byte.</param>
public readonly record struct ActorVisual(int ObjectId, int ModelId, float X, float Y, float Z, float Direction, int AnimationId, float AnimationFrame, bool Visible, Vector3? FirstControlRotation = null, float RootTranslationZ = 0, bool AdditionalControlDisabled = false, Vector3? PolygonColor = null, byte? GroundShadowRadius601To608 = null, byte? GroundShadowLevel609 = null, Vector3? GroundShadowCentre = null, IReadOnlyList<byte>? GroundShadowRadii601To608 = null, IReadOnlyDictionary<int, Vector3>? SkinTranslations = null, bool PrimitiveTransparencyEnabled = false);

public enum ModelTextureSampling
{
    Nearest,
    Bilinear
}

/// <summary>Rasterizes original tiles and animated model meshes into an RGBA field framebuffer.</summary>
public sealed partial class FieldRenderer
{
    private static readonly IReadOnlyDictionary<int, Vector2> EmptyScrollOffsets = new Dictionary<int, Vector2>();
    public const int Width = 320;
    public const int Height = 240;
    private const int NativeDrawingHeight = 224;
    private readonly FieldAssets field;
    private readonly byte[] pixels;
    private readonly float[] depth;
    private readonly float scale;
    private readonly float verticalScale;
    private readonly float offsetX;
    private readonly float offsetY;
    private readonly Vector2 drawingOrigin;
    private readonly int clipLeft;
    private readonly int clipTop;
    private readonly int clipRight;
    private readonly int clipBottom;
    private Vector3 backgroundColor = new(128);
    private IReadOnlyDictionary<int, Vector3>? moduleColors;
    private FieldCamera camera;
    private readonly ModelTextureSampling modelTextureSampling;
    private readonly TileRaster[] background;
    private readonly Memoria.FF8.Resources.AssetHandle<Memoria.FF8.Resources.TextureValue>? backgroundPublication;
    private long backgroundTextureGeneration;
    private TileRaster[] renderBackground;
    private Dictionary<int, Vector2> backgroundScrollOffsets = [];
    private FieldNativeBackgroundWrapFrame? backgroundWrapFrame;
    private int backgroundCameraIndex;
    private Vector2 backgroundJitter;
    private Vector2 actorScrollOffset;
    private ProjectedPoint[] projected = [];

    public FieldRenderer(FieldAssets field, int outputWidth = Width, int outputHeight = Height, ModelTextureSampling modelTextureSampling = ModelTextureSampling.Bilinear, bool nativeMovieViewport = false)
    {
        if (outputWidth < 1 || outputHeight < 1 || outputWidth > 8192 || outputHeight > 8192)
        {
            throw new ArgumentOutOfRangeException(nameof(outputWidth), "Render dimensions must be within 1..8192.");
        }
        this.field = field;
        if (!Enum.IsDefined(modelTextureSampling))
        {
            throw new ArgumentOutOfRangeException(nameof(modelTextureSampling));
        }
        this.modelTextureSampling = modelTextureSampling;
        camera = field.Camera;
        OutputWidth = outputWidth;
        OutputHeight = outputHeight;
        pixels = new byte[checked(outputWidth * outputHeight * 4)];
        depth = new float[outputWidth * outputHeight];
        int presentationHeight = nativeMovieViewport ? NativeDrawingHeight : Height;
        scale = Math.Min(outputWidth / (float)Width, outputHeight / (float)presentationHeight);
        // Native 471F70 installs a 320x224 DRAWENV. 45B6F0 scales that
        // rectangle independently into the PC driver's 4:3 viewport.
        verticalScale = presentationHeight * scale / NativeDrawingHeight;
        offsetX = (outputWidth - Width * scale) / 2;
        offsetY = (outputHeight - presentationHeight * scale) / 2;
        FieldBounds screen = field.Information is { ScreenBounds.Count: > 0 } information ? information.ScreenBounds[0] : new FieldBounds(0, NativeDrawingHeight, Width, 0);
        drawingOrigin = new Vector2(screen.Left + (screen.Right - screen.Left) / 2, screen.Top + (screen.Bottom - screen.Top) / 2);
        clipLeft = Math.Clamp((int)MathF.Ceiling(offsetX - 0.5f), 0, outputWidth);
        clipTop = Math.Clamp((int)MathF.Ceiling(offsetY - 0.5f), 0, outputHeight);
        clipRight = Math.Clamp((int)MathF.Ceiling(offsetX + Width * scale - 0.5f), 0, outputWidth);
        clipBottom = Math.Clamp((int)MathF.Ceiling(offsetY + presentationHeight * scale - 0.5f), 0, outputHeight);
        background = field.Background.Select(tile => CreateTileRaster(tile, Vector2.Zero)).ToArray();
        backgroundPublication = field.Background.Select(tile => tile.Texture).FirstOrDefault(texture => texture is not null);
        backgroundTextureGeneration = backgroundPublication?.Generation ?? 0;
        renderBackground = PrepareBackgroundRasters(EmptyScrollOffsets, null, Vector2.Zero);
    }

    public int OutputWidth { get; }
    public int OutputHeight { get; }

    /// <summary>The returned reusable framebuffer is valid until the next Render call.</summary>
    public byte[] Render(IReadOnlyDictionary<int, int> backgroundStates, IEnumerable<ActorVisual> actors, TimeSpan elapsed, Vector3? backgroundShade = null, int cameraIndex = 0, IReadOnlyDictionary<int, Vector3>? backgroundModuleShades = null, bool backgroundVisible = true, IReadOnlySet<int>? hiddenBackgroundModules = null, IReadOnlyDictionary<int, Vector2>? backgroundScrollOffsets = null, Vector2 actorScrollOffset = default, FieldNativeBackgroundWrapFrame? nativeWrapFrame = null, sbyte drawingJitterX = 0, sbyte drawingJitterY = 0, IReadOnlyList<CardMarkerVisual>? cardMarkers = null)
    {
        ActorVisual[] preparedActors = actors.ToArray();
        foreach (ActorVisual actor in preparedActors.Where(actor => actor.Visible && (uint)actor.ModelId < field.Models.Count && field.Models[actor.ModelId].Bones.Count > 0 && field.Models[actor.ModelId].Faces.Count > 0))
        {
            if (actor.PolygonColor is not Vector3 color || new[] { color.X, color.Y, color.Z }.Any(value => !float.IsFinite(value) || value < 0 || value > 255 || value != MathF.Truncate(value)))
            {
                throw new NotSupportedException("Model drawing requires explicit primitive RGB bytes; shadow level or neutral defaults cannot replace them.");
            }
        }
        IReadOnlyDictionary<int, Vector2> nextOffsets = backgroundScrollOffsets ?? EmptyScrollOffsets;
        ValidateNativeWrapFrame(nativeWrapFrame, cameraIndex, actorScrollOffset, nextOffsets);
        Vector2 jitter = new(drawingJitterX, drawingJitterY);
        if (jitter != Vector2.Zero)
        {
            Vector2 nativeOrigin = drawingOrigin + field.GetCamera(cameraIndex).Pan + actorScrollOffset + jitter;
            if (!float.IsFinite(nativeOrigin.X) || !float.IsFinite(nativeOrigin.Y) || nativeOrigin.X != MathF.Truncate(nativeOrigin.X) || nativeOrigin.Y != MathF.Truncate(nativeOrigin.Y) || nativeOrigin.X is < -1024 or > 1023 || nativeOrigin.Y is < -1024 or > 1023)
            {
                throw new NotSupportedException("Native shaken drawing origin exceeds the proved signed11-bit E5 domain.");
            }
            // 476450 adds shake only to DRAWENV.475480 relocation/culling retains the unshaken camera window.
            nextOffsets = field.Background.Select(tile => tile.Layer).Distinct().ToDictionary(layer => layer, layer => nextOffsets.GetValueOrDefault(layer) + jitter);
        }
        camera = field.GetCamera(cameraIndex);
        backgroundColor = backgroundShade ?? new Vector3(128);
        moduleColors = backgroundModuleShades;
        this.actorScrollOffset = actorScrollOffset + jitter;
        bool layoutChanged = false;
        if (backgroundPublication is not null && backgroundPublication.Generation != backgroundTextureGeneration)
        {
            for (int index = 0; index < background.Length; index++)
            {
                TileRaster raster = background[index];
                PixelImage image = raster.Tile.ResolvedImage;
                if (image.Width != raster.SourceWidth || image.Height != raster.SourceHeight)
                {
                    background[index] = CreateTileRaster(raster.Tile, Vector2.Zero);
                    layoutChanged = true;
                }
            }
            backgroundTextureGeneration = backgroundPublication.Generation;
        }
        if (layoutChanged || this.backgroundScrollOffsets.Count != nextOffsets.Count || nextOffsets.Any(pair => !this.backgroundScrollOffsets.TryGetValue(pair.Key, out Vector2 previous) || previous != pair.Value) || !SameWrapFrame(backgroundWrapFrame, nativeWrapFrame) || backgroundCameraIndex != cameraIndex || backgroundJitter != jitter)
        {
            TileRaster[] nextBackground = PrepareBackgroundRasters(nextOffsets, nativeWrapFrame, jitter);
            this.backgroundScrollOffsets = nextOffsets.ToDictionary(pair => pair.Key, pair => pair.Value);
            backgroundWrapFrame = nativeWrapFrame;
            backgroundCameraIndex = cameraIndex;
            backgroundJitter = jitter;
            renderBackground = nextBackground;
        }
        MemoryMarshal.Cast<byte, uint>(pixels.AsSpan()).Fill(BitConverter.IsLittleEndian ? 0xFF000000u : 0x000000FFu);
        Array.Fill(depth, float.PositiveInfinity);
        List<(float Depth, Action Draw)> translucent = [];
        foreach (TileRaster raster in renderBackground)
        {
            BackgroundTile tile = raster.Tile;
            int state = backgroundStates.GetValueOrDefault(tile.AnimationId, -1);
            if (!backgroundVisible || hiddenBackgroundModules?.Contains(tile.AnimationId) == true || !tile.Draw || (tile.AnimationId != 255 && tile.AnimationState != state))
            {
                continue;
            }
            DrawTile(raster);
            if (tile.Blend && tile.BlendMode != MapBlendMode.None)
            {
                translucent.Add((raster.Tile.Z * 4f, () => DrawTile(raster, translucentPass: true)));
            }
        }
        foreach (ActorVisual actor in preparedActors)
        {
            if (actor.Visible && (uint)actor.ModelId < field.Models.Count)
            {
                DrawActorShadow(actor);
                DrawActor(actor, translucent);
            }
        }
        if (cardMarkers is { Count: > 0 })
        {
            DrawCardMarkers(preparedActors, cardMarkers, elapsed);
        }
        // Native45D080 draws opaque primitives before the translucent pass.
        // STP texels test the completed opaque/model depth without writing it.
        foreach (var primitive in translucent.OrderByDescending(primitive => primitive.Depth))
        {
            primitive.Draw();
        }
        return pixels;
    }

    private void ValidateNativeWrapFrame(FieldNativeBackgroundWrapFrame? frame, int cameraIndex, Vector2 actorOffset, IReadOnlyDictionary<int, Vector2> offsets)
    {
        if (frame is null)
        {
            return;
        }
        if (frame.NativeNormalVersion is not (1 or 2) || !FieldNativeNormalBackgroundWrap.Validate(field, cameraIndex, actorOffset, offsets, frame))
        {
            throw new NotSupportedException("Native tile wrapping requires the current normal or linear contract, original periods, camera center and shared drawing offset.");
        }
    }

    private static bool SameWrapFrame(FieldNativeBackgroundWrapFrame? first, FieldNativeBackgroundWrapFrame? second)
    {
        if (first is null || second is null)
        {
            return first is null && second is null;
        }
        return first.NativeNormalVersion == second.NativeNormalVersion && first.FieldId == second.FieldId && first.ProgramHash == second.ProgramHash && first.Left == second.Left && first.Right == second.Right && first.Top == second.Top && first.Bottom == second.Bottom && first.Layers.SequenceEqual(second.Layers) && (first.LayerCurrents.IsDefault == second.LayerCurrents.IsDefault && (first.LayerCurrents.IsDefault || first.LayerCurrents.SequenceEqual(second.LayerCurrents)));
    }

    private TileRaster[] PrepareBackgroundRasters(IReadOnlyDictionary<int, Vector2> offsets, FieldNativeBackgroundWrapFrame? frame, Vector2 jitter)
    {
        List<TileRaster> rasters = [];
        FieldBounds screen = field.Information is { ScreenBounds.Count: > 0 } information ? information.ScreenBounds[0] : new FieldBounds(0, NativeDrawingHeight, Width, 0);
        int halfWidth = (screen.Right - screen.Left) / 2;
        int halfHeight = (screen.Bottom - screen.Top) / 2;
        Dictionary<ushort, int> paletteOrders = [];
        int previousDepth = background.Length == 0 ? 0 : background[0].Tile.Z;
        if (background.Length != 0)
        {
            // 475480 seeds the first MAP depth/CLUT before scanning, even if
            // that first tile subsequently fails origin culling.
            paletteOrders.Add(background[0].Tile.PackedPalette, 0);
        }
        foreach (TileRaster raster in background)
        {
            BackgroundTile tile = raster.Tile;
            Vector2 scrollOffset = offsets.GetValueOrDefault(tile.Layer);
            FieldNativeBackgroundWrapLayer? wrapping = frame?.Layers.Where(layer => layer.Layer == tile.Layer).Select(layer => (FieldNativeBackgroundWrapLayer?)layer).FirstOrDefault();
            if (frame is not null && wrapping is FieldNativeBackgroundWrapLayer layer)
            {
                if (tile.X is < short.MinValue or > short.MaxValue || tile.Y is < short.MinValue or > short.MaxValue)
                {
                    throw new NotSupportedException("Native wrapped tile origin exceeds its original signed-word coordinates.");
                }
                FieldNativeBackgroundTileOrigin? origin = FieldNativeBackgroundTileProbe.Relocate((short)tile.X, (short)tile.Y, layer, frame);
                if (origin is not FieldNativeBackgroundTileOrigin placed)
                {
                    continue;
                }
                scrollOffset += new Vector2(placed.X - tile.X, placed.Y - tile.Y);
            }
            // 475480 culls raw origins against C-PAN+[-192,176] / [-144,128]
            // before assigning CLUT depths. C and PAN cancel against the
            // common DRAWENV displacement; shake never enters this window.
            Vector2 originInWindow = new Vector2(tile.X, tile.Y) + scrollOffset - jitter;
            if (originInWindow.X <= -halfWidth - 32 || originInWindow.X >= halfWidth + 16 || originInWindow.Y <= -halfHeight - 32 || originInWindow.Y >= halfHeight + 16)
            {
                continue;
            }
            if (tile.Z != previousDepth)
            {
                paletteOrders.Clear();
                previousDepth = tile.Z;
            }
            if (!paletteOrders.TryGetValue(tile.PackedPalette, out int paletteOrder))
            {
                paletteOrder = paletteOrders.Count;
                if (paletteOrder >= 16)
                {
                    throw new NotSupportedException("Native MAP depth group exceeds its sixteen CLUT slots.");
                }
                paletteOrders.Add(tile.PackedPalette, paletteOrder);
            }
            // Original PC quad depth is proportional to 16*MAPZ+CLUT ordinal.
            // Model camera Z uses four units per MAP ordering-table slot.
            TileRaster placedRaster = scrollOffset == Vector2.Zero ? raster : CreateTileRaster(tile, scrollOffset);
            rasters.Add(placedRaster with { Depth = tile.Z * 4f + paletteOrder / 4f });
        }
        // 45C870 prepends to the OT slot;45D310 traverses that chain. Preserve
        // reverse file order within equal Z, independently of layer/CLUT.
        return rasters.AsEnumerable().Reverse().OrderByDescending(raster => raster.Tile.Z).ToArray();
    }

    private TileRaster CreateTileRaster(BackgroundTile tile, Vector2 scrollOffset)
    {
        PixelImage image = tile.ResolvedImage;
        // Cover sample centers in [origin, origin + 16). Adjacent MAP tiles
        // share an edge, so fractional scaling must not blend that row twice.
        int startX = Math.Clamp((int)MathF.Ceiling(offsetX + (tile.X + drawingOrigin.X + scrollOffset.X) * scale - 0.5f), clipLeft, clipRight);
        int startY = Math.Clamp((int)MathF.Ceiling(offsetY + (tile.Y + drawingOrigin.Y + scrollOffset.Y) * verticalScale - 0.5f), clipTop, clipBottom);
        int endX = Math.Clamp((int)MathF.Ceiling(offsetX + (tile.X + drawingOrigin.X + 16 + scrollOffset.X) * scale - 0.5f), clipLeft, clipRight);
        int endY = Math.Clamp((int)MathF.Ceiling(offsetY + (tile.Y + drawingOrigin.Y + 16 + scrollOffset.Y) * verticalScale - 0.5f), clipTop, clipBottom);
        RasterRun[] columns = CreateRuns(startX, endX, destinationX =>
        {
            float logicalX = (destinationX + 0.5f - offsetX) / scale - tile.X - drawingOrigin.X - scrollOffset.X;
            return Math.Clamp((int)(logicalX * image.Width / 16), 0, image.Width - 1);
        });
        RasterRun[] rows = CreateRuns(startY, endY, destinationY =>
        {
            float logicalY = (destinationY + 0.5f - offsetY) / verticalScale - tile.Y - drawingOrigin.Y - scrollOffset.Y;
            return Math.Clamp((int)(logicalY * image.Height / 16), 0, image.Height - 1);
        });
        return new TileRaster(tile, columns, rows);
    }

    private static RasterRun[] CreateRuns(int start, int end, Func<int, int> sourceIndex)
    {
        List<RasterRun> runs = [];
        while (start < end)
        {
            int source = sourceIndex(start);
            int next = start + 1;
            while (next < end && sourceIndex(next) == source)
            {
                next++;
            }
            runs.Add(new RasterRun(start, next, source));
            start = next;
        }
        return runs.ToArray();
    }

    private void DrawTile(TileRaster raster, bool translucentPass = false)
    {
        BackgroundTile tile = raster.Tile;
        PixelImage image = tile.ResolvedImage;
        // Native MAP byte 14 indexes the module RGB/frame array directly; 255 has no module.
        Vector3 shade = moduleColors is not null && tile.AnimationId != 255 && moduleColors.TryGetValue(tile.AnimationId, out Vector3 moduleShade) ? moduleShade : backgroundColor;
        float tileDepth = raster.Depth;
        Span<uint> packedPixels = MemoryMarshal.Cast<byte, uint>(pixels.AsSpan());
        foreach (RasterRun row in raster.Rows)
        {
            int sourceRow = row.Source * image.Width;
            foreach (RasterRun column in raster.Columns)
            {
                Rgba32 color = Shade(image.Pixels[sourceRow + column.Source], shade);
                if (color.A == 0)
                {
                    continue;
                }
                MapBlendMode blend = tile.Blend && color.A == 128 ? tile.BlendMode : MapBlendMode.None;
                if (translucentPass != (blend != MapBlendMode.None))
                {
                    continue;
                }
                uint packed = BitConverter.IsLittleEndian ? (uint)(color.R | color.G << 8 | color.B << 16 | 255 << 24) : (uint)(color.R << 24 | color.G << 16 | color.B << 8 | 255);
                for (int y = row.Start; y < row.End; y++)
                {
                    int index = y * OutputWidth + column.Start;
                    int length = column.End - column.Start;
                    if (!translucentPass)
                    {
                        if (tileDepth == tile.Z * 4f)
                        {
                            // The nearest CLUT ordinal cannot fail against
                            // any earlier far-to-near background primitive.
                            packedPixels.Slice(index, length).Fill(packed);
                            depth.AsSpan(index, length).Fill(tileDepth);
                            continue;
                        }
                        for (int x = 0; x < length; x++)
                        {
                            // Native PC ZFUNC is LESSEQUAL. A later palette
                            // group must not overwrite an earlier group's Z.
                            if (tileDepth <= depth[index + x])
                            {
                                packedPixels[index + x] = packed;
                                depth[index + x] = tileDepth;
                            }
                        }
                    }
                    else
                    {
                        for (int x = 0; x < length; x++)
                        {
                            if (tileDepth <= depth[index + x])
                            {
                                WritePixel(index + x, color, blend);
                            }
                        }
                    }
                }
            }
        }
    }

    private readonly record struct RasterRun(int Start, int End, int Source);
    private sealed record TileRaster(BackgroundTile Tile, RasterRun[] Columns, RasterRun[] Rows)
    {
        public int SourceWidth { get; } = Tile.ResolvedImage.Width;
        public int SourceHeight { get; } = Tile.ResolvedImage.Height;
        public float Depth { get; init; }
    }

    private void DrawActor(ActorVisual actor, List<(float Depth, Action Draw)> translucent)
    {
        FieldModel model = field.Models[actor.ModelId];
        if (model.Bones.Count == 0)
        {
            return;
        }
        Vector3[] pose = model.Pose(actor.AnimationId, actor.AnimationFrame, actor.FirstControlRotation, actor.RootTranslationZ, actor.SkinTranslations);
        // Native field-to-model bridge adds a quarter turn before converting DIR to the 4096-angle scale.
        float direction = (actor.Direction + 64) * MathF.Tau / 256;
        Matrix4x4 rotation = Matrix4x4.CreateRotationZ(direction);
        if (projected.Length < pose.Length)
        {
            projected = new ProjectedPoint[pose.Length];
        }
        for (int i = 0; i < pose.Length; i++)
        {
            if (actor.AdditionalControlDisabled && model.Vertices[i].Bone == model.ControlBones.Additional)
            {
                // Native model flag0x20 clears the declared additional group's projection and depth.
                projected[i] = default;
                continue;
            }
            Vector3 native = pose[i];
            Vector3 world = Vector3.Transform(native, rotation) + new Vector3(actor.X, actor.Y, actor.Z);
            ProjectedPoint point = camera.Project(world);
            projected[i] = ToViewport(point);
        }
        foreach (ModelFace face in model.Faces)
        {
            // Native 533920/533A90 reject the back face using the first three projected vertices, before splitting a quad.
            if (Edge(projected[face.Vertices[0]], projected[face.Vertices[1]], projected[face.Vertices[2]].X, projected[face.Vertices[2]].Y) > 0)
            {
                continue;
            }
            PixelImage texture = model.Textures[face.Texture];
            Vector2 textureScale = ResourceTextureList.PhysicalScale(model.Textures, face.Texture);
            bool transparent = face.SemiTransparent || actor.PrimitiveTransparencyEnabled;
            DrawModelTriangle(projected[face.Vertices[0]], projected[face.Vertices[1]], projected[face.Vertices[2]], face.TextureCoordinates[0] * textureScale, face.TextureCoordinates[1] * textureScale, face.TextureCoordinates[2] * textureScale, texture, actor.PolygonColor!.Value, transparent, translucent, projected[face.Vertices[0]].Depth);
            if (face.Quad)
            {
                DrawModelTriangle(projected[face.Vertices[1]], projected[face.Vertices[3]], projected[face.Vertices[2]], face.TextureCoordinates[1] * textureScale, face.TextureCoordinates[3] * textureScale, face.TextureCoordinates[2] * textureScale, texture, actor.PolygonColor!.Value, transparent, translucent, projected[face.Vertices[0]].Depth);
            }
        }
    }

    private void DrawModelTriangle(ProjectedPoint a, ProjectedPoint b, ProjectedPoint c, Vector2 uvA, Vector2 uvB, Vector2 uvC, PixelImage texture, Vector3 polygonColor, bool transparent, List<(float Depth, Action Draw)> translucent, float orderingDepth)
    {
        if (transparent)
        {
            // Native 532A40 selects ABR1 and sets STP on every nonzero model CLUT entry.
            // 45D080 traverses the shared ordering table again after all opaque drawing.
            translucent.Add((MathF.Floor(orderingDepth / 4) * 4, () => Triangle(a, b, c, uvA, uvB, uvC, texture, polygonColor, true)));
        }
        else
        {
            Triangle(a, b, c, uvA, uvB, uvC, texture, polygonColor, false);
        }
    }

    private ProjectedPoint ToViewport(ProjectedPoint point)
    {
        // FieldCamera.Project retains its 320x240 diagnostic canvas origin.
        // Remove that origin before applying the native shared DRAWENV origin.
        return new ProjectedPoint(offsetX + (point.X - 160 + drawingOrigin.X + actorScrollOffset.X) * scale, offsetY + (point.Y - 120 + drawingOrigin.Y + actorScrollOffset.Y) * verticalScale, point.Depth);
    }

    private void Triangle(ProjectedPoint a, ProjectedPoint b, ProjectedPoint c, Vector2 uvA, Vector2 uvB, Vector2 uvC, PixelImage texture, Vector3 polygonColor, bool primitiveTransparencyEnabled)
    {
        if (a.Depth <= 1 || b.Depth <= 1 || c.Depth <= 1)
        {
            return;
        }
        float area = Edge(a, b, c.X, c.Y);
        if (Math.Abs(area) < 0.001f)
        {
            return;
        }
        int minX = Math.Max((int)MathF.Floor(Math.Min(a.X, Math.Min(b.X, c.X))), clipLeft);
        int maxX = Math.Min((int)MathF.Ceiling(Math.Max(a.X, Math.Max(b.X, c.X))), clipRight - 1);
        int minY = Math.Max((int)MathF.Floor(Math.Min(a.Y, Math.Min(b.Y, c.Y))), clipTop);
        int maxY = Math.Min((int)MathF.Ceiling(Math.Max(a.Y, Math.Max(b.Y, c.Y))), clipBottom - 1);
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                float wA = Edge(b, c, x + 0.5f, y + 0.5f) / area;
                float wB = Edge(c, a, x + 0.5f, y + 0.5f) / area;
                float wC = primitiveTransparencyEnabled ? Edge(a, b, x + 0.5f, y + 0.5f) / area : 1 - wA - wB;
                if (wA < 0 || wB < 0 || wC < 0)
                {
                    continue;
                }
                if (primitiveTransparencyEnabled && ((wA == 0 && !OwnsRasterEdge(b, c, area)) || (wB == 0 && !OwnsRasterEdge(c, a, area)) || (wC == 0 && !OwnsRasterEdge(a, b, area))))
                {
                    continue;
                }
                float depthA = wA / a.Depth;
                float depthB = wB / b.Depth;
                float depthC = wC / c.Depth;
                float inverseDepth = depthA + depthB + depthC;
                float z = 1 / inverseDepth;
                int index = y * OutputWidth + x;
                if (primitiveTransparencyEnabled ? z > depth[index] : z >= depth[index])
                {
                    continue;
                }
                Vector2 uv = (uvA * depthA + uvB * depthB + uvC * depthC) / inverseDepth;
                Rgba32 color = Shade(SampleTexture(texture, uv), polygonColor);
                if (color.A == 0)
                {
                    continue;
                }
                WritePixel(index, color, primitiveTransparencyEnabled ? MapBlendMode.Add : MapBlendMode.None);
                if (!primitiveTransparencyEnabled)
                {
                    depth[index] = z;
                }
            }
        }
    }

    private static bool OwnsRasterEdge(ProjectedPoint start, ProjectedPoint end, float area)
    {
        if (area < 0)
        {
            (start, end) = (end, start);
        }
        return end.Y > start.Y || (end.Y == start.Y && end.X < start.X);
    }

    private Rgba32 SampleTexture(PixelImage texture, Vector2 uv)
    {
        int u = Math.Clamp((int)MathF.Floor(uv.X), 0, texture.Width - 1);
        int v = Math.Clamp((int)MathF.Floor(uv.Y), 0, texture.Height - 1);
        Rgba32[] texels = texture.Pixels;
        Rgba32 coverage = texels[v * texture.Width + u];
        if (modelTextureSampling == ModelTextureSampling.Nearest || coverage.A == 0)
        {
            return coverage;
        }
        float x = Math.Clamp(uv.X - 0.5f, 0, texture.Width - 1);
        float y = Math.Clamp(uv.Y - 0.5f, 0, texture.Height - 1);
        int left = (int)x;
        int top = (int)y;
        int right = Math.Min(left + 1, texture.Width - 1);
        int bottom = Math.Min(top + 1, texture.Height - 1);
        float horizontal = x - left;
        float vertical = y - top;
        int topRow = top * texture.Width;
        int bottomRow = bottom * texture.Width;
        Vector4 first = Premultiplied(texels[topRow + left]);
        Vector4 second = Premultiplied(texels[topRow + right]);
        Vector4 third = Premultiplied(texels[bottomRow + left]);
        Vector4 fourth = Premultiplied(texels[bottomRow + right]);
        Vector4 color = Vector4.Lerp(Vector4.Lerp(first, second, horizontal), Vector4.Lerp(third, fourth, horizontal), vertical);
        // Keep the original cutout coverage and exclude transparent texel RGB from interpolation.
        return new Rgba32((byte)Math.Clamp(MathF.Round(color.X / color.W), 0, 255), (byte)Math.Clamp(MathF.Round(color.Y / color.W), 0, 255), (byte)Math.Clamp(MathF.Round(color.Z / color.W), 0, 255), coverage.A);
    }

    private static Vector4 Premultiplied(Rgba32 color)
    {
        float alpha = color.A / 255f;
        return new Vector4(color.R * alpha, color.G * alpha, color.B * alpha, alpha);
    }

    private static float Edge(ProjectedPoint a, ProjectedPoint b, float x, float y) => (x - a.X) * (b.Y - a.Y) - (y - a.Y) * (b.X - a.X);

    private static Rgba32 Shade(Rgba32 color, Vector3 level)
    {
        if (level == new Vector3(128))
        {
            return color;
        }
        return new Rgba32((byte)Math.Clamp(color.R * level.X / 128, 0, 255), (byte)Math.Clamp(color.G * level.Y / 128, 0, 255), (byte)Math.Clamp(color.B * level.Z / 128, 0, 255), color.A);
    }

    private void WritePixel(int index, Rgba32 color, MapBlendMode blend)
    {
        int destination = index * 4;
        if (blend == MapBlendMode.None)
        {
            pixels[destination] = color.R;
            pixels[destination + 1] = color.G;
            pixels[destination + 2] = color.B;
            pixels[destination + 3] = 255;
            return;
        }
        pixels[destination] = Blend(pixels[destination], color.R, blend);
        pixels[destination + 1] = Blend(pixels[destination + 1], color.G, blend);
        pixels[destination + 2] = Blend(pixels[destination + 2], color.B, blend);
        pixels[destination + 3] = 255;
    }

    private static byte Blend(byte destination, byte source, MapBlendMode mode)
    {
        int value = mode switch
        {
            MapBlendMode.Average => (destination + source) / 2,
            MapBlendMode.Add => destination + source,
            MapBlendMode.Subtract => destination - source,
            MapBlendMode.QuarterAdd => destination + source / 4,
            _ => source
        };
        return (byte)Math.Clamp(value, 0, 255);
    }
}
