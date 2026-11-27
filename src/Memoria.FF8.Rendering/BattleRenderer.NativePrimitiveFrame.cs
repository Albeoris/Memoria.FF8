using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Rendering;

[Flags]
public enum NativeBattleTexturePrefixBlockers
{
    None = 0,
    Window = 1,
    Affine = 2,
    ModelReplacement = 4,
    EffectReplacement = 8,
    Format = 16,
    NoReduction = 32,
    RetainedPixels = 64
}

/// <summary>Last-render counters; previous hits include older resident assets. Promotions do not require a new final material.</summary>
public readonly record struct NativeBattleTextureDecodingObservation(long RenderOrdinal, long FullFinalCurrentHits, long FullFinalPreviousHits, long RawCurrentHits, long RawPreviousHits, long RawPromotions, long DecodeCount, long DecodedPixels, long RawNoCandidate, long RawWordMismatches, long RawPaletteMismatches, long RawBudgetSkips, int CurrentCandidates, int CurrentBytes)
{
    public long RawEvictions { get; init; }
    public long PrefixEligibleCommands { get; init; }
    public long PrefixDecodedImages { get; init; }
    public long PrefixFullPixels { get; init; }
    public long PrefixCroppedPixels { get; init; }
    public long PrefixBlockedCommands { get; init; }
    public NativeBattleTexturePrefixBlockers PrefixBlockers { get; init; }
}

public readonly record struct NativeBattleRawTextureContentSummary(long RenderOrdinal, int RawEntries, int ReferenceUniqueImages, int ContentUniqueImages, long ReferenceUniqueImageBytes, long ContentUniqueImageBytes, long DuplicateImageBytes, int EntriesUsedInLastRender)
{
    public string Scope => "RetainedRawCacheAfterFinalPresent";
    public string Coverage => "Retained raw decoded images only. Evicted and oversized candidates, final replacements, captures and foreign outputs are excluded. Last-render usage refers to the last renderer call, which can precede the final Present.";
}

public sealed partial class BattleRenderer
{
    private readonly record struct PrimitiveVertex(float X, float Y, float Z, float ReciprocalW, Vector2 Uv, Vector3 Color, float Alpha);
    private readonly record struct NativeTextureKey(ushort Page, ushort Clut, byte? AlternateShift, int X, int Y, int Width, int Height, NativeBattlePcTextureWindowPixels? Pixels, string? TextureSourceOwner, int SampleKey, int PrefixWidth = 0, int PrefixHeight = 0, string? ModelTextureActorId = null);
    private Dictionary<NativeTextureKey, PixelImage> nativeTexturePages = [];
    private Dictionary<NativeTextureKey, PixelImage> previousNativeTexturePages = [];
    private readonly Dictionary<string, (NativeBattlePresentationTextureBank Current, NativeBattleEffectTextureState? Previous, bool SameOrigins)> nativeActorTextureViews = new(StringComparer.Ordinal);
    private const int MaximumRawTextureCandidates = 256;
    private const int MaximumRawTextureBytes = 64 * 1024 * 1024;
    // Provenance affects replacements, but the bounded native image cache retains only exact sampling inputs.
    private readonly record struct NativeRawTextureKey(ushort Page, ushort Clut, byte? AlternateShift, int X, int Y, int Width, int Height, NativeBattlePcDirectTextureFormat? DirectFormat);
    private sealed class NativeRawTextureCandidate
    {
        internal readonly NativeRawTextureKey Key;
        internal readonly PixelImage Image;
        internal readonly int X;
        internal readonly int Y;
        internal readonly int Width;
        internal readonly int Height;
        internal readonly ImmutableArray<byte> Data;
        internal readonly NativeBattlePcPaletteReceipt? Palette;
        internal readonly int Bytes;
        internal long LastRender;

        internal NativeRawTextureCandidate(NativeRawTextureKey key, PixelImage image, NativeBattlePcTextureWindowPixels pixels, NativeBattlePcPaletteReceipt? palette, int bytes, long render)
        {
            Key = key;
            Image = image;
            X = pixels.X;
            Y = pixels.Y;
            Width = pixels.Width;
            Height = pixels.Height;
            Data = pixels.Data;
            Palette = palette;
            Bytes = bytes;
            LastRender = render;
        }
    }
    private readonly Dictionary<NativeRawTextureKey, LinkedListNode<NativeRawTextureCandidate>> nativeRawTexturePages = [];
    private readonly LinkedList<NativeRawTextureCandidate> nativeRawTextureRecent = [];
    private int nativeRawTextureBytes;
    private long textureDiagnosticRender;
    private long textureDiagnosticFinalCurrent;
    private long textureDiagnosticFinalPrevious;
    private long textureDiagnosticRawCurrent;
    private long textureDiagnosticRawPrevious;
    private long textureDiagnosticPromotions;
    private long textureDiagnosticDecodes;
    private long textureDiagnosticPixels;
    private long textureDiagnosticNoCandidate;
    private long textureDiagnosticWordMismatch;
    private long textureDiagnosticPaletteMismatch;
    private long textureDiagnosticBudgetSkip;
    private long textureDiagnosticEvictions;
    private long textureDiagnosticPrefixEligible;
    private long textureDiagnosticPrefixImages;
    private long textureDiagnosticPrefixFullPixels;
    private long textureDiagnosticPrefixCroppedPixels;
    private long textureDiagnosticPrefixBlocked;
    private NativeBattleTexturePrefixBlockers textureDiagnosticPrefixBlockers;
    public NativeBattleTextureDecodingObservation? TextureDecoding => PilotDiagnosticsEnabled ? new NativeBattleTextureDecodingObservation(textureDiagnosticRender, textureDiagnosticFinalCurrent, textureDiagnosticFinalPrevious, textureDiagnosticRawCurrent, textureDiagnosticRawPrevious, textureDiagnosticPromotions, textureDiagnosticDecodes, textureDiagnosticPixels, textureDiagnosticNoCandidate, textureDiagnosticWordMismatch, textureDiagnosticPaletteMismatch, textureDiagnosticBudgetSkip, nativeRawTexturePages.Count, nativeRawTextureBytes) { RawEvictions = textureDiagnosticEvictions, PrefixEligibleCommands = textureDiagnosticPrefixEligible, PrefixDecodedImages = textureDiagnosticPrefixImages, PrefixFullPixels = textureDiagnosticPrefixFullPixels, PrefixCroppedPixels = textureDiagnosticPrefixCroppedPixels, PrefixBlockedCommands = textureDiagnosticPrefixBlocked, PrefixBlockers = textureDiagnosticPrefixBlockers } : null;
    private NativeBattleEffectTextureState? nativeTextureOwner;
    private NativeBattlePcDirectTextureFormat? nativeDirectTextureFormat;
    private IReadOnlyList<NativeBattleTextureReplacement>? nativeModelReplacements;
    private INativeBattleEffectTextureSource? nativeEffectTextureSource;
    private long effectTextureGeneration;
    private bool sameNativeTextureOrigins;
    public SceneColorGrade ColorGrade { get; set; }
    public SceneDirectionalLighting SceneSun { get; set; }
    public float FighterSunShare { get; set; }

    private readonly List<NativeRasterTriangle> nativeTriangles = [];
    private readonly record struct NativeRasterTriangle(PrimitiveVertex A, PrimitiveVertex B, PrimitiveVertex C, PixelImage? Texture, int Blend, byte Pass, float Area, int Left, int Right, int Top, int Bottom, bool Line = false, float ActorOpacity = 1, SceneColorGrade Grade = default);

    /// <summary>Render the executed original battle OT into a viewport representing its320x216 draw environment.</summary>
    public byte[] RenderNativePrimitiveFrame(NativeBattlePrimitiveFrame frame, NativeBattleEffectTextureState? textures, BattleCamera? overlayCamera = null, NativeBattlePrimitiveCapabilities? capabilities = null, NativeBattlePcTextureWindowCache? textureWindowCache = null, IReadOnlyList<NativeBattleTextureReplacement>? replacements = null, NativeBattlePcUiFrame? uiFrame = null, Func<int, int, bool, PixelImage>? uiTexture = null, INativeBattleEffectTextureSource? effectTextureSource = null)
        => RenderNativePrimitiveFrame(NativeBattlePreparedPrimitiveFrame.From(frame), textures, overlayCamera, capabilities, textureWindowCache, replacements, uiFrame, uiTexture, effectTextureSource);

    public byte[] RenderNativePrimitiveFrame(NativeBattlePreparedPrimitiveFrame frame, NativeBattleEffectTextureState? textures, BattleCamera? overlayCamera = null, NativeBattlePrimitiveCapabilities? capabilities = null, NativeBattlePcTextureWindowCache? textureWindowCache = null, IReadOnlyList<NativeBattleTextureReplacement>? replacements = null, NativeBattlePcUiFrame? uiFrame = null, Func<int, int, bool, PixelImage>? uiTexture = null, INativeBattleEffectTextureSource? effectTextureSource = null, bool overlay = false)
    {
        nativeActorTextureViews.Clear();
        //The executed OT already owns projected XY/depth; a native WAIT can legitimately publish coincident eye and target.
        if (!overlay)
        {
            sceneProjectionReady = false;
            actorBounds.Clear();
        }
        textureDiagnosticRender++;
        if (PilotDiagnosticsEnabled)
        {
            textureDiagnosticFinalCurrent = 0;
            textureDiagnosticFinalPrevious = 0;
            textureDiagnosticRawCurrent = 0;
            textureDiagnosticRawPrevious = 0;
            textureDiagnosticPromotions = 0;
            textureDiagnosticDecodes = 0;
            textureDiagnosticPixels = 0;
            textureDiagnosticNoCandidate = 0;
            textureDiagnosticWordMismatch = 0;
            textureDiagnosticPaletteMismatch = 0;
            textureDiagnosticBudgetSkip = 0;
            textureDiagnosticEvictions = 0;
            textureDiagnosticPrefixEligible = 0;
            textureDiagnosticPrefixImages = 0;
            textureDiagnosticPrefixFullPixels = 0;
            textureDiagnosticPrefixCroppedPixels = 0;
            textureDiagnosticPrefixBlocked = 0;
            textureDiagnosticPrefixBlockers = NativeBattleTexturePrefixBlockers.None;
        }
        BeginPilotSubmission();
        TriangleRenderer?.Begin(Width, Height, new Vector4(0, 0, 0, 1));
        if (TriangleRenderer is null)
        {
            if (!overlay)
            {
                Array.Clear(pixels);
            }
            Array.Fill(depth, float.PositiveInfinity);
        }
        DrawnTriangles = 0;
        nativeTriangles.Clear();
        sceneCopyImages.Clear();
        sceneCopyImageBytes = 0;
        sceneCopyUiTexture = uiTexture;
        if (!isSceneReplay)
        {
            BeginSceneReplay(frame, capabilities, replacements, effectTextureSource);
        }
        for (int index = 3; TriangleRenderer is null && index < pixels.Length; index += 4)
        {
            pixels[index] = 255;
        }
        (nativeTexturePages, previousNativeTexturePages) = (previousNativeTexturePages, nativeTexturePages);
        nativeTexturePages.Clear();
        NativeBattleEffectTextureState? previousTextureOwner = nativeTextureOwner;
        bool sameReplacements = ReferenceEquals(nativeModelReplacements, replacements) || nativeModelReplacements is not null && replacements is not null && nativeModelReplacements.SequenceEqual(replacements);
        sameReplacements &= ReferenceEquals(nativeEffectTextureSource, effectTextureSource) && effectTextureGeneration == (effectTextureSource?.Generation ?? 0);
        sameNativeTextureOrigins = SameTextureOrigins(previousTextureOwner, textures);
        nativeEffectTextureSource = effectTextureSource;
        nativeModelReplacements = replacements?.ToArray();
        if (nativeDirectTextureFormat != capabilities?.DirectTextureFormat)
        {
            previousNativeTexturePages.Clear();
            ClearNativeRawTextureCache();
        }
        nativeTextureOwner = textures;
        nativeDirectTextureFormat = capabilities?.DirectTextureFormat;
        DrawNativePcUi(uiFrame, uiTexture, false);
        // Offscreen source packets have already populated VRAM and do not cover the visible framebuffer.
        Span<NativeBattlePrimitiveDepthVertex> projected = stackalloc NativeBattlePrimitiveDepthVertex[4];
        foreach (ref readonly NativeBattleLogicalResolvedCommand resolved in frame.LastCommands)
        {
            if (resolved.Pass == 0)
            {
                continue;
            }
            NativeBattleLogicalCommandView command = resolved.Command.Resolve();
            if (command.Opcode is >= 0xE1 and <= 0xE6 || command.SceneCapture is { Kind: NativeBattleSceneCaptureKind.Source } capture && (capture.X >= 320 || capture.Y >= 216))
            {
                continue;
            }
            NativeBattlePrimitiveDrawMode drawMode = resolved.DrawMode;
            if (command.ModelTextureOwner is not null && (command.SceneCapture is { Kind: NativeBattleSceneCaptureKind.ScreenSample } || drawMode.SceneCaptureSample is not null))
            {
                throw new NotSupportedException("A model texture command cannot also sample the global screen capture.");
            }
            float actorOpacity = frame.ActorOpacity(command.PresentationActorId);
            if (actorOpacity == 0)
            {
                continue;
            }
            ReadOnlySpan<uint> words = command.WordSpan;
            if (command.Opcode == 0x80)
            {
                if (resolved.Pass != 1 || words.Length != 4)
                {
                    throw new InvalidDataException("Native VRAM transfer presentation requires its executed first-pass command and four payload DWORDs.");
                }
                //The shared software handler has already copied the absolute VRAM words and invalidated their texture cache.
                continue;
            }
            if (command.Opcode == 0x02)
            {
                if (resolved.Pass != 1 || words.Length != 3)
                {
                    throw new InvalidDataException("Native VRAM fill presentation requires its executed first-pass command and three payload DWORDs.");
                }
                //The absolute VRAM fill has already executed through NativeBattleSoftwarePrimitiveExecution and emits no viewport primitive.
                continue;
            }
            if (command.Opcode is 0x40 or 0x42 or 0x50 or 0x52)
            {
                DrawNativePrimitiveLine(frame, command, drawMode, resolved.Pass);
                continue;
            }
            if ((command.Opcode & 0xFC) is 0x60 or 0x68)
            {
                DrawNativePrimitiveTile(frame, command, drawMode, resolved.Pass);
                continue;
            }
            if (command.Opcode is 0x64 or 0x66)
            {
                DrawNativePrimitiveSprite(frame, command, drawMode, resolved.Pass, textures, capabilities, textureWindowCache, replacements, previousTextureOwner, sameReplacements);
                continue;
            }
            if ((command.Opcode & 0xE0) != 0x20)
            {
                throw new NotSupportedException($"Native battle opcode{command.Opcode:X2} requires its original CPU rasterizer.");
            }
            bool textured = (command.Opcode & 4) != 0;
            bool gouraud = (command.Opcode & 16) != 0;
            bool quad = (command.Opcode & 8) != 0;
            bool semi = (command.Opcode & 2) != 0;
            NativeBattleForeignSurface? foreignSurface = command.Pilot.ForeignSurface;
            NativeBattleForeignSurface? capturedSurface = foreignSurface?.Material is NativeBattleArenaCaptureMaterial ? foreignSurface : null;
            if ((foreignSurface is null || capturedSurface is not null) && (resolved.Pass == 2 && !semi || resolved.Pass == 1 && semi))
            {
                continue;
            }
            int corners = quad ? 4 : 3;
            if (command.SceneCapture?.SampleCoordinates is { } sampleCoordinates && (sampleCoordinates.IsDefault || sampleCoordinates.Length != corners))
            {
                throw new InvalidDataException("Scene sampling requires one retained coordinate per original corner.");
            }
            ReadOnlySpan<BattleSceneScreenPoint> projectedPoints = command.ProjectedCoordinateSpan;
            if (command.HasProjectedCoordinates && projectedPoints.Length != corners)
            {
                throw new InvalidDataException("Expanded polygon coordinates require one point per original corner.");
            }
            int stride = (gouraud ? 1 : 0) + 1 + (textured ? 1 : 0);
            int expectedWords = corners * stride + (gouraud ? 0 : 1);
            if (words.Length != expectedWords)
            {
                throw new InvalidDataException("Native polygon command does not contain its original packed corner fields.");
            }
            ReadOnlySpan<int> depths = command.VertexPayloadSpan;
            bool perspective = !depths.IsEmpty && depths[0] != 0 && (frame.LastDescriptorPerspective ?? throw new NotSupportedException("Native polygon rendering requires the executed OT descriptor DWORD8; migrate the named snapshot explicitly."));
            NativeBattleScreenCoverage? coverage = command.ScreenCoverage;
            if (coverage is not null && (!quad || textured))
            {
                throw new InvalidDataException("Screen coverage requires an untextured overlay quad.");
            }
            NativeBattlePrimitiveDepthState state = frame.LastDepthState ?? throw new NotSupportedException("Native polygon rendering requires its retained460B60 depth owner.");
            NativeBattlePrimitiveColorState colorState = frame.LastColorState ?? throw new NotSupportedException("Native polygon rendering requires its retained PC diffuse-color dispatch owner.");
            state.Project461220(command.OrderingBucket ?? throw new InvalidDataException("Native polygon lacks its original OT bucket."), perspective, command.Flags20, depths, projected);
            PixelImage? texture = null;
            Vector2 prefixUvScale = Vector2.One;
            if (capturedSurface is not null)
            {
                if (capturedSurface.Uv.Length != corners || capturedSurface.Colors.Length != corners)
                {
                    throw new InvalidDataException("Arena image sampling requires complete native primitive corner attributes.");
                }
                texture = ForeignMaterialResolver?.Invoke(capturedSurface.Material).Material.Texture ?? throw new NotSupportedException("Arena image sampling requires its resolved captured framebuffer.");
            }
            if (textured && foreignSurface is null)
            {
                bool affine = activePilotInterval is null || !perspective;
                for (int corner = 0; corner < (quad ? 4 : 3); corner++)
                {
                    affine &= projected[corner].ReciprocalW == 1;
                }
                texture = drawMode.SceneCaptureSample is null
                    ? ResolveNativePrimitiveTexture(frame, command, drawMode, textures, capabilities, textureWindowCache, replacements, colorState, previousTextureOwner, sameReplacements, quad ? 4 : 3, affine, out prefixUvScale)
                    : ResolveSceneCaptureTexture(frame, drawMode, capabilities, colorState);
                if (drawMode.SceneCaptureSample is null && command.PresentationActorId is { } actorId && frame.PetrifiedEnemyActors?.Contains(actorId) == true)
                {
                    texture = PetrifiedActorTextures.GetValue(texture, CreatePetrifiedActorTexture);
                }
            }
            NativeBattlePilotPrimitiveSample pilot = command.SceneCapture?.Kind == NativeBattleSceneCaptureKind.Image ? default : PreparePilotPrimitiveSample(command, state, perspective);
            NativeBattlePrimitiveViewportProjector viewport = default;
            PrimitiveVertex At(in NativeBattleLogicalCommandView command, in NativeBattlePilotPrimitiveSample pilot, int corner, NativeBattlePrimitiveDepthVertex selectedDepth, ReadOnlySpan<uint> words, ReadOnlySpan<BattleSceneScreenPoint> points)
            {
                int coordinateWord = 1 + corner * stride;
                uint coordinate = words[coordinateWord];
                uint color = words[gouraud ? corner * stride : 0];
                bool foreignMaterial = foreignSurface is not null && capturedSurface is null;
                int mode = foreignMaterial && !semi ? 0 : drawMode.BlendMode ?? throw new NotSupportedException("Native polygon diffuse color requires its executed ABR.");
                NativeBattlePrimitiveColor diffuse = foreignMaterial ? colorState.Textured(color, semi, mode, false) : textured ? colorState.Textured(color, semi, mode, (command.Opcode & 1) != 0) : colorState.Untextured(color, semi, mode);
                uint uv = textured ? words[coordinateWord + 1] : 0;
                Vector2 coordinates = new((byte)uv, (byte)(uv >> 8));
                if (capturedSurface is not null)
                {
                    coordinates = capturedSurface.Uv[corner];
                }
                else if (textured && drawMode.SceneCaptureSample is { } sceneSample)
                {
                    coordinates = SceneCaptureUv(sceneSample, drawMode.TexturePage!.Value, uv, command.SceneCapture?.SampleCoordinates is { } samples ? samples[corner] : null);
                }
                else if (textured && foreignSurface is null)
                {
                    NativeBattlePcTextureWindowSelection window = drawMode.TextureSelection!;
                    coordinates *= new Vector2(window.ScaleU, window.ScaleV);
                    coordinates *= prefixUvScale;
                }
                BattleSceneScreenPoint point = !points.IsEmpty ? points[corner] : new(unchecked((short)coordinate), unchecked((short)(coordinate >> 16)));
                if (!viewport.IsPrepared)
                {
                    viewport = new(ProjectionBounds, command.Flags20, drawMode);
                }
                if (TrySamplePilotCorner(pilot, viewport, corner, out Vector2 position, out NativeBattlePrimitiveDepthVertex sampledDepth))
                {
                    selectedDepth = sampledDepth;
                }
                else
                {
                    position = viewport.Project(point.X, point.Y);
                }
                if (command.SceneCapture?.Kind == NativeBattleSceneCaptureKind.Image)
                {
                    position = NativeSceneCaptureImagePosition(point.X, point.Y, command.Flags20);
                }
                if (coverage is not null)
                {
                    position = NativeScreenCoveragePosition(coverage, corner);
                }
                return new(position.X, position.Y, selectedDepth.Depth, selectedDepth.ReciprocalW, coordinates, diffuse.Rgb, diffuse.Alpha);
            }
            int blend = (foreignSurface is null || capturedSurface is not null) && semi && resolved.Pass == 2 ? drawMode.BlendMode ?? throw new NotSupportedException("Native semi-transparent polygon requires its inherited ABR.") : -1;
            PrimitiveVertex a = At(command, pilot, 0, projected[0], words, projectedPoints);
            PrimitiveVertex b = At(command, pilot, 1, projected[1], words, projectedPoints);
            PrimitiveVertex c = At(command, pilot, 2, projected[2], words, projectedPoints);
            if (capturedSurface is null && foreignSurface is { } foreign)
            {
                NativeBattleForeignRenderMaterial? hostMaterial = foreign.Material as NativeBattleForeignRenderMaterial ?? ForeignMaterialResolver?.Invoke(foreign.Material);
                if (hostMaterial is null || foreign.Uv.Length != corners || foreign.Colors.Length != corners)
                {
                    throw new NotSupportedException("A foreign primitive requires its original host material or captured surface and complete corner attributes.");
                }
                ITriangleRenderer target = TriangleRenderer ?? throw new NotSupportedException("Foreign actor materials require the host triangle renderer.");
                TriangleMaterial original = hostMaterial.Material with { ActorOpacity = actorOpacity };
                if (semi)
                {
                    int nativeBlend = drawMode.BlendMode ?? throw new NotSupportedException("Native host-material overlays require their original blend equation.");
                    original = WithNativeActorOpacity(NativePrimitiveMaterial(original.Texture, nativeBlend, resolved.Pass, original.CentroidTextureSampling) with { Sampler = original.Sampler }, nativeBlend, actorOpacity);
                }
                TriangleVertex Vertex(PrimitiveVertex vertex, int corner) => new(new(vertex.X, vertex.Y), vertex.Z, vertex.ReciprocalW, foreign.Uv[corner], new Vector4(vertex.Color / 255, vertex.Alpha / 255) * foreign.Colors[corner]);
                float area = (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
                if (!hostMaterial.Cull || area >= 0)
                {
                    target.Draw(Vertex(a, 0), Vertex(b, 1), Vertex(c, 2), original);
                    DrawnTriangles++;
                }
                if (quad)
                {
                    PrimitiveVertex d = At(command, pilot, 3, projected[3], words, projectedPoints);
                    float secondArea = (d.X - b.X) * (c.Y - b.Y) - (d.Y - b.Y) * (c.X - b.X);
                    if (!hostMaterial.Cull || secondArea >= 0)
                    {
                        target.Draw(Vertex(b, 1), Vertex(d, 3), Vertex(c, 2), original);
                        DrawnTriangles++;
                    }
                }
                continue;
            }
            SceneColorGrade grade = default;
            if (ColorGrade.Enabled && textured && blend is not (1 or 3) && command.SceneCapture is null
                && (command.TextureSourceOwner?.StartsWith("battle/a0stg", StringComparison.OrdinalIgnoreCase) == true
                    || command.PresentationActorId is not null && !command.Pilot.IsEmpty && command.Pilot.Identity.Bank is 0x502D40 or 0x507BF0))
            {
                bool fighter = command.PresentationActorId is not null;
                grade = fighter ? ColorGrade with { LinearTint = null } : ColorGrade;
                if (!command.Pilot.IsEmpty && !command.Pilot.ScreenSpace && overlayCamera is { } camera)
                {
                    Vector3 first = command.Pilot.Corner(0);
                    Vector3 second = command.Pilot.Corner(1);
                    Vector3 third = command.Pilot.Corner(2);
                    Vector3 normal = Vector3.Cross(second - first, third - first);
                    Vector3 nativeEye = camera.Eye * new Vector3(100, -100, -100);
                    if (Vector3.Dot(normal, nativeEye - first) < 0)
                    {
                        normal = -normal;
                    }
                    SceneDirectionalLighting sun = fighter ? SceneSun with { Strength = SceneSun.Strength * FighterSunShare } : SceneSun;
                    float multiplier = sun.Multiplier(normal);
                    if (multiplier != 1)
                    {
                        grade = grade with { LinearTint = (grade.LinearTint ?? Vector3.One) * multiplier };
                    }
                }
            }
            int submitted = DrawnTriangles;
            QueueNativePrimitive(a, b, c, texture, blend, resolved.Pass, centroidTextureSampling: textured, actorOpacity: actorOpacity, grade: grade);
            if (DrawnTriangles > submitted)
            {
                ObserveActorTriangle(command.PresentationActorId, a, b, c);
            }
            if (PilotDiagnosticsEnabled && DrawnTriangles > submitted)
            {
                ObservePilotSubmittedTriangle(command, drawMode, resolved.Pass, a, b, c, 0, 1, 2);
            }
            if (quad)
            {
                PrimitiveVertex d = At(command, pilot, 3, projected[3], words, projectedPoints);
                submitted = DrawnTriangles;
                QueueNativePrimitive(b, d, c, texture, blend, resolved.Pass, centroidTextureSampling: textured, actorOpacity: actorOpacity, grade: grade);
                if (DrawnTriangles > submitted)
                {
                    ObserveActorTriangle(command.PresentationActorId, b, d, c);
                }
                if (PilotDiagnosticsEnabled && DrawnTriangles > submitted)
                {
                    ObservePilotSubmittedTriangle(command, drawMode, resolved.Pass, b, d, c, 1, 3, 2);
                }
            }
        }
        nativeActorTextureViews.Clear();
        if (TriangleRenderer is not null)
        {
            DrawNativePcRenderTail(frame.LastPcRenderTail, capabilities);
            DrawNativePcUi(uiFrame, uiTexture, true, true);
            TriangleRenderer.End();
            return pixels;
        }
        int stripes = Math.Min(8, Math.Min(Environment.ProcessorCount, Math.Max(1, Height / 90)));
        if (stripes == 1)
        {
            RasterStripe(0);
        }
        else
        {
            Parallel.For(0, stripes, new ParallelOptions { MaxDegreeOfParallelism = stripes }, RasterStripe);
        }
        DrawNativePcRenderTail(frame.LastPcRenderTail, capabilities);
        DrawNativePcUi(uiFrame, uiTexture, true, true);
        return pixels;

        void RasterStripe(int stripe)
        {
            int top = Height * stripe / stripes;
            int bottom = Height * (stripe + 1) / stripes - 1;
            // Every stripe executes the original submission order, with exclusive color/depth rows.
            foreach (NativeRasterTriangle triangle in nativeTriangles)
            {
                if (triangle.Line)
                {
                    RasterNativeLine(triangle, top, bottom);
                }
                else
                {
                    RasterNativePrimitive(triangle, top, bottom);
                }
            }
        }
    }

    private PixelImage ResolveNativePrimitiveTexture(NativeBattlePreparedPrimitiveFrame frame, in NativeBattleLogicalCommandView command, NativeBattlePrimitiveDrawMode drawMode, NativeBattleEffectTextureState? textures, NativeBattlePrimitiveCapabilities? capabilities, NativeBattlePcTextureWindowCache? textureWindowCache, IReadOnlyList<NativeBattleTextureReplacement>? replacements, NativeBattlePrimitiveColorState colorState, NativeBattleEffectTextureState? previousTextureOwner, bool sameReplacements, TexturePrimitiveSample? firstSample = null)
        => ResolveNativePrimitiveTexture(frame, command, drawMode, textures, capabilities, textureWindowCache, replacements, colorState, previousTextureOwner, sameReplacements, 0, false, out _, firstSample);

    private PixelImage ResolveNativePrimitiveTexture(NativeBattlePreparedPrimitiveFrame frame, in NativeBattleLogicalCommandView command, NativeBattlePrimitiveDrawMode drawMode, NativeBattleEffectTextureState? textures, NativeBattlePrimitiveCapabilities? capabilities, NativeBattlePcTextureWindowCache? textureWindowCache, IReadOnlyList<NativeBattleTextureReplacement>? replacements, NativeBattlePrimitiveColorState colorState, NativeBattleEffectTextureState? previousTextureOwner, bool sameReplacements, int prefixCorners, bool affine, out Vector2 prefixUvScale, TexturePrimitiveSample? firstSample = null)
    {
        string? modelTextureActorId = null;
        bool sameTextureOrigins = sameNativeTextureOrigins;
        if (command.ModelTextureOwner is { } owner)
        {
            modelTextureActorId = owner.RequireActorId();
            if (!nativeActorTextureViews.TryGetValue(modelTextureActorId, out var view))
            {
                NativeBattlePresentationTextureBank bank = (textures ?? throw new InvalidDataException("The actor has no bound presentation texture bank.")).RequirePresentationTexture(modelTextureActorId);
                NativeBattleEffectTextureState? previous = previousTextureOwner is not null && previousTextureOwner.PresentationTextures?.ContainsKey(modelTextureActorId) == true
                    ? previousTextureOwner.RequirePresentationTexture(modelTextureActorId).Textures
                    : null;
                view = (bank, previous, previous is not null && SameTextureOrigins(previous, bank.Textures));
                nativeActorTextureViews.Add(modelTextureActorId, view);
            }
            textures = view.Current.Textures;
            textureWindowCache = view.Current.Cache;
            previousTextureOwner = view.Previous;
            sameTextureOrigins = view.SameOrigins;
        }
        PixelImage image = ResolveNativePrimitiveTextureCore(frame, command, drawMode, textures, capabilities, textureWindowCache, replacements, colorState, previousTextureOwner, sameReplacements, sameTextureOrigins, modelTextureActorId, prefixCorners, affine, out prefixUvScale, firstSample);
        return ApplySceneCopyTexture(frame, drawMode, textures, capabilities, colorState, image);
    }

    private PixelImage ResolveNativePrimitiveTextureCore(NativeBattlePreparedPrimitiveFrame frame, in NativeBattleLogicalCommandView command, NativeBattlePrimitiveDrawMode drawMode, NativeBattleEffectTextureState? textures, NativeBattlePrimitiveCapabilities? capabilities, NativeBattlePcTextureWindowCache? textureWindowCache, IReadOnlyList<NativeBattleTextureReplacement>? replacements, NativeBattlePrimitiveColorState colorState, NativeBattleEffectTextureState? previousTextureOwner, bool sameReplacements, bool sameTextureOrigins, string? modelTextureActorId, int prefixCorners, bool affine, out Vector2 prefixUvScale, TexturePrimitiveSample? firstSample = null)
    {
        prefixUvScale = Vector2.One;
        PixelImage? texture;
        ushort page = drawMode.TexturePage ?? throw new NotSupportedException("Native textured polygon requires its executed texture page.");
        _ = drawMode.TextureWindowBits ?? throw new NotSupportedException("Native465CE0 requires its retained45BC60 texture-window owner; explicitly migrate the named snapshot.");
        NativeBattlePcTextureWindowSelection window = drawMode.TextureSelection ?? throw new NotSupportedException("Native465CE0 requires its executed PC window-cache selection; explicitly migrate the named snapshot.");
        window.Validate();
        NativeBattlePcTextureWindowPixels? retainedPixels;
        if (drawMode.TexturePixelSnapshot is int snapshot)
        {
            ImmutableArray<NativeBattlePcTextureWindowPixels> snapshots = frame.LastTextureSnapshots ?? throw new NotSupportedException("Native texture draw requires its executed resource pixel snapshots.");
            if ((uint)snapshot >= snapshots.Length)
            {
                throw new InvalidDataException("Native texture draw refers outside its retained resource pixel snapshots.");
            }
            retainedPixels = snapshots[snapshot];
            retainedPixels.ValidateMapping(window.Row, new(window.X, window.Y, window.Width, window.Height));
        }
        else
        {
            retainedPixels = textureWindowCache is not null ? textureWindowCache.PixelWords(window) : window.UsesRetainedPixelWords == false ? null : throw new NotSupportedException("Native texture selection requires its retained cache pixel owner.");
        }
        ushort clut = (ushort)(command.WordSpan[2] >> 16);
        byte? alternateShift = null;
        if (drawMode.BlendMode == 2)
        {
            if (colorState.SubtractUsesNormalizedColor)
            {
                throw new NotSupportedException("Native PC ABR2 requires the selected alpha-palette device profile.");
            }
            alternateShift = colorState.AlternatePaletteShift ?? throw new NotSupportedException("Native PC ABR2 requires its retained45B540 alternate-palette shift.");
        }
        TexturePrimitiveSample primitiveSample = firstSample ?? new((byte)command.WordSpan[2] * window.ScaleU, (byte)(command.WordSpan[2] >> 8) * window.ScaleV);
        int sampleKey = nativeEffectTextureSource?.GetPrimitiveSampleKey(primitiveSample) ?? 0;
        NativeTextureKey fullKey = new(page, clut, alternateShift, window.X, window.Y, window.Width, window.Height, retainedPixels, command.TextureSourceOwner, sampleKey, ModelTextureActorId: modelTextureActorId);
        if (nativeTexturePages.TryGetValue(fullKey, out texture))
        {
            if (PilotDiagnosticsEnabled)
            {
                textureDiagnosticFinalCurrent++;
            }
            return texture;
        }
        int decodeWidth = window.Width;
        int decodeHeight = window.Height;
        long prefixSourceGeneration = nativeEffectTextureSource?.Generation ?? 0;
        bool prefix = prefixCorners > 0 && TryPlanNativePrefix(command, prefixCorners, affine, page, clut, alternateShift, window, retainedPixels, textures, capabilities, replacements, out decodeWidth, out decodeHeight);
        if (prefix)
        {
            NativeTextureKey candidate = new(page, clut, alternateShift, window.X, window.Y, window.Width, window.Height, retainedPixels, command.TextureSourceOwner, sampleKey, decodeWidth, decodeHeight, modelTextureActorId);
            if (nativeTexturePages.TryGetValue(candidate, out texture))
            {
                prefixUvScale = new(256f / decodeWidth, 256f / decodeHeight);
                if (PilotDiagnosticsEnabled)
                {
                    textureDiagnosticPrefixEligible++;
                    textureDiagnosticFinalCurrent++;
                }
                return texture;
            }
            if (nativeEffectTextureSource is not null && (nativeEffectTextureSource is not NativeBattleEffectTextureSource source || !source.TryProveNoReplacement(textures!, page, clut, window.X, window.Y, window.Width, window.Height, retainedPixels, alternateShift, capabilities?.DirectTextureFormat, primitiveSample)))
            {
                BlockNativePrefix(NativeBattleTexturePrefixBlockers.EffectReplacement);
                prefix = false;
                decodeWidth = window.Width;
                decodeHeight = window.Height;
            }
        }
        if (prefix)
        {
            prefixUvScale = new(256f / decodeWidth, 256f / decodeHeight);
            effectTextureGeneration = prefixSourceGeneration;
            if (PilotDiagnosticsEnabled)
            {
                textureDiagnosticPrefixEligible++;
            }
        }
        NativeTextureKey key = new(page, clut, alternateShift, window.X, window.Y, window.Width, window.Height, retainedPixels, command.TextureSourceOwner, sampleKey, prefix ? decodeWidth : 0, prefix ? decodeHeight : 0, modelTextureActorId);
        if (!nativeTexturePages.TryGetValue(key, out texture))
        {
            int depth = (page >> 7) & 3;
            // Raw sampling excludes blend bits; replacement selection still uses the complete key above.
            NativeRawTextureKey rawKey = new((ushort)(page & 0x19F), depth == 2 ? (ushort)0 : (ushort)(clut & 0x7FFF), depth == 2 ? null : alternateShift, window.X, window.Y, decodeWidth, decodeHeight, depth == 2 ? capabilities?.DirectTextureFormat : null);
            bool reusableOrigins = sameTextureOrigins || modelTextureActorId is null && retainedPixels is not null;
            bool reusable = sameReplacements && reusableOrigins && previousNativeTexturePages.TryGetValue(key, out texture) && (ReferenceEquals(previousTextureOwner, textures) || previousTextureOwner is not null && textures is not null && NativeBattleEffectTextureReader.HasSamePcRegionSource(previousTextureOwner, textures, page, clut, window.X, window.Y, window.Width, window.Height, retainedPixels));
            if (!reusable)
            {
                NativeBattleEffectTextureState textureState = textures ?? throw new NotSupportedException("Native polygon texture sampling requires its retained VRAM owner.");
                if (!TryReuseNativeRawTexture(rawKey, retainedPixels, textureState, out texture))
                {
                    NativeBattlePcPaletteReceipt? palette = null;
                    texture = retainedPixels is null
                        ? NativeBattleEffectTextureReader.DecodePcRegion(textureState, page, clut, window.X, window.Y, decodeWidth, decodeHeight, alternateShift, capabilities?.DirectTextureFormat)
                        : NativeBattleEffectTextureReader.DecodePcRegion(textureState, page, clut, window.X, window.Y, decodeWidth, decodeHeight, out palette, alternateShift, capabilities?.DirectTextureFormat, retainedPixels);
                    if (PilotDiagnosticsEnabled)
                    {
                        textureDiagnosticDecodes++;
                        textureDiagnosticPixels += texture.Pixels.Length;
                        if (prefix)
                        {
                            textureDiagnosticPrefixImages++;
                            textureDiagnosticPrefixFullPixels += window.Width * window.Height;
                            textureDiagnosticPrefixCroppedPixels += decodeWidth * decodeHeight;
                        }
                    }
                    RetainNativeRawTexture(rawKey, texture, retainedPixels, palette);
                }
                PixelImage? decodedNative = alternateShift is null ? texture : null;
                if (replacements is not null && alternateShift is null)
                {
                    texture = NativeBattleEffectTextureReader.ApplyModelReplacements(texture, textures!, page, clut, window.X, window.Y, retainedPixels, replacements, command.TextureSourceOwner);
                }
                if (nativeEffectTextureSource is not null && !prefix)
                {
                    texture = nativeEffectTextureSource.ApplyDecoded(texture, decodedNative, textures!, page, clut, window.X, window.Y, window.Width, window.Height, retainedPixels, alternateShift, capabilities?.DirectTextureFormat, primitiveSample);
                    effectTextureGeneration = nativeEffectTextureSource.Generation;
                }
                nativeEffectTextureSource?.ObservePresented(texture, textureState, page, clut, window.X, window.Y, decodeWidth, decodeHeight, retainedPixels);
            }
            else
            {
                if (PilotDiagnosticsEnabled)
                {
                    textureDiagnosticFinalPrevious++;
                }
                if (textures is not null)
                {
                    TryReuseNativeRawTexture(rawKey, retainedPixels, textures, out _, promotion: true);
                }
            }
            nativeTexturePages.Add(key, texture!);
        }
        else if (PilotDiagnosticsEnabled)
        {
            textureDiagnosticFinalCurrent++;
        }
        return texture!;
    }

    private bool TryPlanNativePrefix(in NativeBattleLogicalCommandView command, int corners, bool affine, ushort page, ushort clut, byte? alternateShift, NativeBattlePcTextureWindowSelection window, NativeBattlePcTextureWindowPixels? retainedPixels, NativeBattleEffectTextureState? textures, NativeBattlePrimitiveCapabilities? capabilities, IReadOnlyList<NativeBattleTextureReplacement>? replacements, out int width, out int height)
    {
        width = window.Width;
        height = window.Height;
        if (!affine)
        {
            return BlockNativePrefix(NativeBattleTexturePrefixBlockers.Affine);
        }
        if (window.X != 0 || window.Y != 0 || window.Width != 256 || window.Height != 256 || window.ScaleU != 1f / 256 || window.ScaleV != 1f / 256)
        {
            return BlockNativePrefix(NativeBattleTexturePrefixBlockers.Window);
        }
        if (retainedPixels is null || textures is null)
        {
            return BlockNativePrefix(NativeBattleTexturePrefixBlockers.RetainedPixels);
        }
        int depth = (page >> 7) & 3;
        if (depth == 3 || alternateShift > 1 || depth == 1 && (clut & 63) * 16 + 256 > 1024 || depth == 2 && capabilities?.DirectTextureFormat is not (NativeBattlePcDirectTextureFormat.Rgb555 or NativeBattlePcDirectTextureFormat.Rgb565))
        {
            return BlockNativePrefix(NativeBattleTexturePrefixBlockers.Format);
        }
        retainedPixels.ValidateMapping(window.Row, new(0, 0, 256, 256));
        int pixelsPerWord = 4 >> depth;
        if (retainedPixels.X != (page & 15) * 64 || retainedPixels.Y != (page & 16) * 16 || retainedPixels.Width != 256 / pixelsPerWord || retainedPixels.Height != 256)
        {
            return BlockNativePrefix(NativeBattleTexturePrefixBlockers.RetainedPixels);
        }
        int maximumU = 0;
        int maximumV = 0;
        int stride = (command.Opcode & 16) != 0 ? 3 : 2;
        for (int corner = 0; corner < corners; corner++)
        {
            uint uv = command.WordSpan[2 + corner * stride];
            maximumU = Math.Max(maximumU, (byte)uv);
            maximumV = Math.Max(maximumV, (byte)(uv >> 8));
        }
        int prefixWidth = 1;
        int prefixHeight = 1;
        while (prefixWidth <= maximumU)
        {
            prefixWidth *= 2;
        }
        while (prefixHeight <= maximumV)
        {
            prefixHeight *= 2;
        }
        if (prefixWidth == 256 && prefixHeight == 256)
        {
            return BlockNativePrefix(NativeBattleTexturePrefixBlockers.NoReduction);
        }
        textures.Validate();
        if (alternateShift is null && replacements is not null && NativeBattleEffectTextureReader.HasModelReplacementCandidate(page, clut, window.X, window.Y, window.Width, window.Height, replacements, command.TextureSourceOwner))
        {
            return BlockNativePrefix(NativeBattleTexturePrefixBlockers.ModelReplacement);
        }
        width = prefixWidth;
        height = prefixHeight;
        return true;
    }

    private bool BlockNativePrefix(NativeBattleTexturePrefixBlockers reason)
    {
        if (PilotDiagnosticsEnabled)
        {
            textureDiagnosticPrefixBlocked++;
            textureDiagnosticPrefixBlockers |= reason;
        }
        return false;
    }

    private bool TryReuseNativeRawTexture(NativeRawTextureKey key, NativeBattlePcTextureWindowPixels? pixels, NativeBattleEffectTextureState textures, [NotNullWhen(true)] out PixelImage? image, bool promotion = false)
    {
        image = null;
        if (pixels is null || !nativeRawTexturePages.TryGetValue(key, out LinkedListNode<NativeRawTextureCandidate>? node))
        {
            if (PilotDiagnosticsEnabled && !promotion)
            {
                textureDiagnosticNoCandidate++;
            }
            return false;
        }
        pixels.Validate();
        NativeRawTextureCandidate candidate = node.Value;
        if (candidate.X != pixels.X || candidate.Y != pixels.Y || candidate.Width != pixels.Width || candidate.Height != pixels.Height || !candidate.Data.AsSpan().SequenceEqual(pixels.Data.AsSpan()))
        {
            if (PilotDiagnosticsEnabled && !promotion)
            {
                textureDiagnosticWordMismatch++;
            }
            return false;
        }
        if (candidate.Palette is { } palette)
        {
            if (!palette.Matches(textures))
            {
                if (PilotDiagnosticsEnabled && !promotion)
                {
                    textureDiagnosticPaletteMismatch++;
                }
                return false;
            }
        }
        else
        {
            textures.Validate();
        }
        if (PilotDiagnosticsEnabled)
        {
            if (promotion)
            {
                textureDiagnosticPromotions++;
            }
            else if (candidate.LastRender == textureDiagnosticRender)
            {
                textureDiagnosticRawCurrent++;
            }
            else
            {
                textureDiagnosticRawPrevious++;
            }
        }
        candidate.LastRender = textureDiagnosticRender;
        nativeRawTextureRecent.Remove(node);
        nativeRawTextureRecent.AddLast(node);
        image = candidate.Image;
        return true;
    }

    private void RetainNativeRawTexture(NativeRawTextureKey key, PixelImage image, NativeBattlePcTextureWindowPixels? pixels, NativeBattlePcPaletteReceipt? palette)
    {
        if (pixels is null)
        {
            return;
        }
        if (((key.Page >> 7) & 3) < 2 && (palette is null || (palette.Clut & 0x7FFF) != key.Clut))
        {
            throw new InvalidDataException("An indexed native image cache entry requires its decoder's exact CLUT receipt.");
        }
        int bytes = checked(image.Pixels.Length * 4 + pixels.Data.Length + (palette?.RetainedBytes ?? 0));
        if (bytes > MaximumRawTextureBytes)
        {
            if (PilotDiagnosticsEnabled)
            {
                textureDiagnosticBudgetSkip++;
            }
            return;
        }
        if (nativeRawTexturePages.Remove(key, out LinkedListNode<NativeRawTextureCandidate>? previous))
        {
            nativeRawTextureRecent.Remove(previous);
            nativeRawTextureBytes -= previous.Value.Bytes;
        }
        while (nativeRawTexturePages.Count >= MaximumRawTextureCandidates || nativeRawTextureBytes > MaximumRawTextureBytes - bytes)
        {
            LinkedListNode<NativeRawTextureCandidate> oldest = nativeRawTextureRecent.First!;
            nativeRawTextureRecent.RemoveFirst();
            nativeRawTexturePages.Remove(oldest.Value.Key);
            nativeRawTextureBytes -= oldest.Value.Bytes;
            if (PilotDiagnosticsEnabled)
            {
                textureDiagnosticEvictions++;
            }
        }
        NativeRawTextureCandidate candidate = new(key, image, pixels, palette, bytes, textureDiagnosticRender);
        nativeRawTexturePages.Add(key, nativeRawTextureRecent.AddLast(candidate));
        nativeRawTextureBytes += bytes;
    }

    public void ClearNativeRawTextureCache()
    {
        nativeRawTexturePages.Clear();
        nativeRawTextureRecent.Clear();
        nativeRawTextureBytes = 0;
    }

    public NativeBattleRawTextureContentSummary InspectNativeRawTextureContent()
    {
        HashSet<PixelImage> references = new(ReferenceEqualityComparer.Instance);
        Dictionary<int, List<PixelImage>> contents = [];
        int uniqueContents = 0;
        int lastRenderEntries = 0;
        long referenceBytes = 0;
        long contentBytes = 0;
        foreach (NativeRawTextureCandidate candidate in nativeRawTextureRecent)
        {
            if (candidate.LastRender == textureDiagnosticRender)
            {
                lastRenderEntries++;
            }
            PixelImage image = candidate.Image;
            if (!references.Add(image))
            {
                continue;
            }
            long bytes = checked((long)image.Pixels.Length * 4);
            referenceBytes += bytes;
            HashCode hash = new();
            hash.Add(image.Width);
            hash.Add(image.Height);
            foreach (Rgba32 pixel in image.Pixels)
            {
                hash.Add(pixel);
            }
            int key = hash.ToHashCode();
            if (!contents.TryGetValue(key, out List<PixelImage>? matches))
            {
                matches = [];
                contents.Add(key, matches);
            }
            bool duplicate = false;
            foreach (PixelImage prior in matches)
            {
                if (prior.Width == image.Width && prior.Height == image.Height && prior.Pixels.AsSpan().SequenceEqual(image.Pixels))
                {
                    duplicate = true;
                    break;
                }
            }
            if (!duplicate)
            {
                matches.Add(image);
                uniqueContents++;
                contentBytes += bytes;
            }
        }
        return new(textureDiagnosticRender, nativeRawTextureRecent.Count, references.Count, uniqueContents, referenceBytes, contentBytes, referenceBytes - contentBytes, lastRenderEntries);
    }

    private static bool SameTextureOrigins(NativeBattleEffectTextureState? previous, NativeBattleEffectTextureState? current)
    {
        if (ReferenceEquals(previous, current))
        {
            return true;
        }
        bool samePixels = previous?.LiveTextureSources is { } before && current?.LiveTextureSources is { } after ? before.SequenceEqual(after) : previous?.LiveTextureSources is null && current?.LiveTextureSources is null;
        bool sameRegistrations = previous?.TextureRegistrations is { } previousRegistrations && current?.TextureRegistrations is { } currentRegistrations ? previousRegistrations.SequenceEqual(currentRegistrations) : previous?.TextureRegistrations is null && current?.TextureRegistrations is null;
        return samePixels && sameRegistrations;
    }

    private Vector2 NativeSceneCaptureImagePosition(int x, int y, uint flags)
    {
        float unit = (flags & 1) != 0 ? 1 : 0.125f;
        return new(x * unit * Width / 320, y * unit * Height / 216);
    }

    private Vector2 NativeScreenCoveragePosition(NativeBattleScreenCoverage coverage, int corner)
    {
        return new(((corner & 1) == 0 ? coverage.Left : coverage.Right) * Width, (corner < 2 ? coverage.Top : coverage.Bottom) * Height);
    }

    private void DrawNativePrimitiveTile(NativeBattlePreparedPrimitiveFrame frame, in NativeBattleLogicalCommandView command, NativeBattlePrimitiveDrawMode drawMode, byte pass)
    {
        bool semi = (command.Opcode & 2) != 0;
        if (pass == 1 && semi || pass == 2 && !semi)
        {
            return;
        }
        bool point = (command.Opcode & 0xFC) == 0x68;
        if (command.WordSpan.Length != (point ? 2 : 3))
        {
            throw new InvalidDataException("Native TILE requires its original RGB, XY and selected size fields.");
        }
        int blend = drawMode.BlendMode ?? throw new NotSupportedException("Native TILE requires its executed ABR.");
        NativeBattlePrimitiveColor diffuse = (frame.LastColorState ?? throw new NotSupportedException("Native TILE requires its retained PC color owner.")).Untextured(command.WordSpan[0], semi, blend);
        ReadOnlySpan<int> sourceDepths = command.VertexPayloadSpan;
        bool perspective = !sourceDepths.IsEmpty && sourceDepths[0] != 0 && (frame.LastDescriptorPerspective ?? throw new NotSupportedException("Native TILE requires its executed descriptor depth mode."));
        ImmutableArray<int> depths = sourceDepths.IsEmpty ? [] : Enumerable.Repeat(sourceDepths[0], 4).ToImmutableArray();
        ImmutableArray<NativeBattlePrimitiveDepthVertex> vertices = (frame.LastDepthState ?? throw new NotSupportedException("Native TILE requires its retained depth dispatch owner.")).Project461220(command.OrderingBucket ?? throw new InvalidDataException("Native TILE lacks its original bucket."), perspective, command.Flags20, depths);
        int x = unchecked((short)command.WordSpan[1]);
        int y = unchecked((short)(command.WordSpan[1] >> 16));
        int width = point ? (command.Flags20 & 1) != 0 ? 1 : 8 : (ushort)command.WordSpan[2];
        int height = point ? width : (ushort)(command.WordSpan[2] >> 16);
        NativeBattlePilotPrimitiveSample pilot = command.SceneCapture?.Kind == NativeBattleSceneCaptureKind.Image ? default : PreparePilotPrimitiveSample(command, frame.LastDepthState!, perspective);
        NativeBattlePrimitiveViewportProjector viewport = default;
        PrimitiveVertex At(in NativeBattleLogicalCommandView command, in NativeBattlePilotPrimitiveSample pilot, int px, int py, int corner)
        {
            if (!viewport.IsPrepared)
            {
                viewport = new(ProjectionBounds, command.Flags20, drawMode);
            }
            NativeBattlePrimitiveDepthVertex depth = vertices[0];
            if (TrySamplePilotCorner(pilot, viewport, corner, out Vector2 position, out NativeBattlePrimitiveDepthVertex sampledDepth))
            {
                depth = sampledDepth;
            }
            else
            {
                position = viewport.Project(px, py);
            }
            if (command.SceneCapture?.Kind == NativeBattleSceneCaptureKind.Image)
            {
                position = NativeSceneCaptureImagePosition(px, py, command.Flags20);
            }
            if (command.ScreenCoverage is { } coverage)
            {
                position = NativeScreenCoveragePosition(coverage, corner);
            }
            return new(position.X, position.Y, depth.Depth, depth.ReciprocalW, Vector2.Zero, diffuse.Rgb, diffuse.Alpha);
        }
        PrimitiveVertex a = At(command, pilot, x, y, 0);
        PrimitiveVertex b = At(command, pilot, x + width, y, 1);
        PrimitiveVertex c = At(command, pilot, x, y + height, 2);
        PrimitiveVertex d = At(command, pilot, x + width, y + height, 3);
        int submitted = DrawnTriangles;
        QueueNativePrimitive(a, b, c, null, semi ? blend : -1, pass, actorOpacity: frame.ActorOpacity(command.PresentationActorId));
        if (PilotDiagnosticsEnabled && DrawnTriangles > submitted)
        {
            ObservePilotSubmittedTriangle(command, drawMode, pass, a, b, c, 0, 1, 2);
        }
        submitted = DrawnTriangles;
        QueueNativePrimitive(b, d, c, null, semi ? blend : -1, pass, actorOpacity: frame.ActorOpacity(command.PresentationActorId));
        if (PilotDiagnosticsEnabled && DrawnTriangles > submitted)
        {
            ObservePilotSubmittedTriangle(command, drawMode, pass, b, d, c, 1, 3, 2);
        }
    }

    private void QueueNativePrimitive(PrimitiveVertex a, PrimitiveVertex b, PrimitiveVertex c, PixelImage? texture, int blend, byte pass, bool centroidTextureSampling = false, float actorOpacity = 1, SceneColorGrade grade = default)
    {
        static float Edge(in PrimitiveVertex first, in PrimitiveVertex second, float x, float y) => (second.X - first.X) * (y - first.Y) - (second.Y - first.Y) * (x - first.X);
        float area = Edge(a, b, c.X, c.Y);
        if (!float.IsFinite(area) || area == 0)
        {
            return;
        }
        if (area < 0)
        {
            (b, c) = (c, b);
            area = -area;
        }
        DrawnTriangles++;
        if (TriangleRenderer is { } renderer)
        {
            static TriangleVertex Convert(in PrimitiveVertex vertex) => new(new Vector2(vertex.X, vertex.Y), vertex.Z, vertex.ReciprocalW, vertex.Uv, new Vector4(vertex.Color / 255f, vertex.Alpha / 255f));
            TriangleMaterial material = NativePrimitiveMaterial(texture, blend, pass, centroidTextureSampling);
            material = WithNativeActorOpacity(material, blend, actorOpacity) with { ColorGrade = grade };
            renderer.Draw(Convert(a), Convert(b), Convert(c), material);
            return;
        }
        int left = Math.Clamp((int)MathF.Floor(Math.Min(a.X, Math.Min(b.X, c.X))), 0, Width - 1);
        int right = Math.Clamp((int)MathF.Ceiling(Math.Max(a.X, Math.Max(b.X, c.X))), 0, Width - 1);
        int top = Math.Clamp((int)MathF.Floor(Math.Min(a.Y, Math.Min(b.Y, c.Y))), 0, Height - 1);
        int bottom = Math.Clamp((int)MathF.Ceiling(Math.Max(a.Y, Math.Max(b.Y, c.Y))), 0, Height - 1);
        nativeTriangles.Add(new(a, b, c, texture, blend, pass, area, left, right, top, bottom, ActorOpacity: actorOpacity, Grade: grade));
    }

    private static TriangleMaterial NativePrimitiveMaterial(PixelImage? texture, int blend, byte pass, bool centroidTextureSampling)
    {
        TriangleBlend equation = blend switch
        {
            -1 => TriangleBlend.Replace,
            0 or 2 => TriangleBlend.Alpha,
            1 => TriangleBlend.Add,
            3 => TriangleBlend.AlphaAdd,
            _ => throw new NotSupportedException("Unsupported native blend equation.")
        };
        float minimumAlpha = texture is null ? 0 : pass == 1 ? 112f / 255 : pass == 2 && blend == 0 ? 122f / 255 : 1f / 255;
        return new(texture, equation, TriangleDepthComparison.LessEqual, pass == 1, minimumAlpha, TriangleSampler.PointWrap)
        {
            CentroidTextureSampling = centroidTextureSampling && texture is not null
        };
    }

    private static TriangleMaterial WithNativeActorOpacity(TriangleMaterial material, int nativeBlend, float opacity)
    {
        if (opacity == 1)
        {
            return material;
        }
        return material with
        {
            Blend = nativeBlend == -1 ? TriangleBlend.Alpha : nativeBlend == 1 ? TriangleBlend.AlphaAdd : material.Blend,
            ActorOpacity = opacity,
            ActorOpacityReplacesAlpha = nativeBlend is -1 or 1,
            DepthWrite = false
        };
    }

    private void RasterNativePrimitive(NativeRasterTriangle triangle, int stripeTop, int stripeBottom)
    {
        static float Edge(PrimitiveVertex first, PrimitiveVertex second, float x, float y) => (second.X - first.X) * (y - first.Y) - (second.Y - first.Y) * (x - first.X);
        static bool Inside(float value, PrimitiveVertex first, PrimitiveVertex second) => value > 0 || value == 0 && (second.Y < first.Y || second.Y == first.Y && second.X > first.X);
        PrimitiveVertex a = triangle.A;
        PrimitiveVertex b = triangle.B;
        PrimitiveVertex c = triangle.C;
        PixelImage? texture = triangle.Texture;
        int blend = triangle.Blend;
        byte pass = triangle.Pass;
        float area = triangle.Area;
        int left = triangle.Left;
        int right = triangle.Right;
        int top = Math.Max(triangle.Top, stripeTop);
        int bottom = Math.Min(triangle.Bottom, stripeBottom);
        for (int y = top; y <= bottom; y++)
        {
            if (sceneReplaySampleRows is { } sampleRows && !sampleRows[y])
            {
                continue;
            }
            for (int x = left; x <= right; x++)
            {
                if (sceneReplaySampleColumns is { } sampleColumns && !sampleColumns[x])
                {
                    continue;
                }
                //The PC's -.5 transformed-vertex bias and integer pixel centers are equivalent to these half-pixel samples.
                float ea = Edge(b, c, x + 0.5f, y + 0.5f);
                float eb = Edge(c, a, x + 0.5f, y + 0.5f);
                float ec = Edge(a, b, x + 0.5f, y + 0.5f);
                if (!Inside(ea, b, c) || !Inside(eb, c, a) || !Inside(ec, a, b))
                {
                    continue;
                }
                float wa = ea / area;
                float wb = eb / area;
                float wc = ec / area;
                float z = a.Z + (b.Z - a.Z) * wb + (c.Z - a.Z) * wc;
                int index = y * Width + x;
                if (!float.IsFinite(z) || z < 0 || z > 1 || z > depth[index])
                {
                    continue;
                }
                Vector3 color = a.Color + (b.Color - a.Color) * wb + (c.Color - a.Color) * wc;
                float alpha = (a.Alpha + (b.Alpha - a.Alpha) * wb + (c.Alpha - a.Alpha) * wc) / 255;
                if (texture is not null)
                {
                    float rhw = a.ReciprocalW * wa + b.ReciprocalW * wb + c.ReciprocalW * wc;
                    if (!float.IsFinite(rhw) || rhw <= 0)
                    {
                        continue;
                    }
                    Vector2 uv = (a.Uv * (a.ReciprocalW * wa) + b.Uv * (b.ReciprocalW * wb) + c.Uv * (c.ReciprocalW * wc)) / rhw;
                    int u = (int)MathF.Floor(uv.X * texture.Width) % texture.Width;
                    int v = (int)MathF.Floor(uv.Y * texture.Height) % texture.Height;
                    Rgba32 texel = texture[u < 0 ? u + texture.Width : u, v < 0 ? v + texture.Height : v];
                    if (texel.A == 0 || pass == 1 && texel.A < 112 || pass == 2 && blend == 0 && texel.A < 122)
                    {
                        continue;
                    }
                    color *= new Vector3(texel.R, texel.G, texel.B) / 255f;
                    //D3DTBLEND_MODULATE selects texture alpha when the texture has an alpha channel.
                    alpha = texel.A / 255f;
                }
                if (triangle.Grade.Enabled)
                {
                    color = triangle.Grade.Apply(color / 255) * 255;
                }
                BlendNativePcPrimitive(index * 4, color, alpha, blend, triangle.ActorOpacity);
                if (pass == 1 && triangle.ActorOpacity == 1)
                {
                    depth[index] = z;
                }
            }
        }
    }

    private void BlendNativePcPrimitive(int pixel, Vector3 color, float alpha, int blend, float actorOpacity = 1)
    {
        for (int component = 0; component < 3; component++)
        {
            float source = Math.Clamp(color[component], 0, 255);
            float destination = pixels[pixel + component];
            float result = blend switch
            {
                -1 => source,
                0 or 2 => source * alpha + destination * (1 - alpha),
                1 => source + destination,
                3 => source * alpha + destination,
                _ => throw new NotSupportedException("Native PC primitive has no supported device blend equation.")
            };
            result = destination + (result - destination) * actorOpacity;
            pixels[pixel + component] = (byte)Math.Clamp((int)result, 0, 255);
        }
        pixels[pixel + 3] = 255;
    }
}
