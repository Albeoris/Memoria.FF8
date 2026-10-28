using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Rendering;

public readonly record struct NativeBattlePilotSubmission(long SourceCallback, long NextSourceCallback, long Generation, ushort? Effect, float Alpha, ulong WorldSignature, ulong ProjectedSignature, ulong MaterialSignature, int Corners, int ContinuousCorners, int CoveredContinuousCorners, int DiscreteCorners, string? FallbackReason, int FallbackCount);

public sealed partial class BattleRenderer
{
    private NativeBattlePilotSubmission pilotSubmission;
    public bool PilotDiagnosticsEnabled { get; set; }
    public NativeBattlePilotSubmission PilotSubmission => pilotSubmission;

    public void ObservePilotSource(NativeBattlePreparedPresentation frame, NativeBattlePreparedPresentation? next, float alpha, bool compatibleRequested = false)
    {
        if (PilotDiagnosticsEnabled)
        {
            pilotSubmission = pilotSubmission with { SourceCallback = frame.Callback, NextSourceCallback = next?.Callback ?? frame.Callback, Generation = frame.PilotGeneration, Effect = frame.PilotEffect, Alpha = next is null ? 0 : alpha, FallbackReason = compatibleRequested ? LastPilotFallbackReason : null, FallbackCount = compatibleRequested ? LastPilotFallbackCount : 0 };
        }
    }

    private void BeginPilotSubmission()
    {
        if (PilotDiagnosticsEnabled)
        {
            pilotSubmission = pilotSubmission with { WorldSignature = 14695981039346656037UL, ProjectedSignature = 14695981039346656037UL, MaterialSignature = 14695981039346656037UL, Corners = 0, ContinuousCorners = 0, CoveredContinuousCorners = 0, DiscreteCorners = 0 };
        }
    }

    private void ObservePilotSubmittedTriangle(NativeBattleLogicalResolvedCommand resolved, PrimitiveVertex a, PrimitiveVertex b, PrimitiveVertex c, int cornerA, int cornerB, int cornerC)
    {
        NativeBattleLogicalCommand command = resolved.Command;
        if (command.PilotGeometry is not { Identity.Effect: 2 or 3 or 144 })
        {
            return;
        }
        ulong material = PilotHash(pilotSubmission.MaterialSignature, command.Opcode);
        material = PilotHash(material, resolved.Pass);
        material = PilotHash(material, resolved.DrawMode.TexturePage ?? 0);
        material = PilotHash(material, resolved.DrawMode.TextureWindowBits ?? 0);
        if ((command.Opcode & 4) != 0)
        {
            material = PilotHash(material, command.Words[2] >> 16);
        }
        pilotSubmission = pilotSubmission with { MaterialSignature = material };
        ObservePilotSubmittedCorner(command, cornerA, new(a.X, a.Y), new(a.Z, a.ReciprocalW), a.Uv, a.Color, a.Alpha);
        ObservePilotSubmittedCorner(command, cornerB, new(b.X, b.Y), new(b.Z, b.ReciprocalW), b.Uv, b.Color, b.Alpha);
        ObservePilotSubmittedCorner(command, cornerC, new(c.X, c.Y), new(c.Z, c.ReciprocalW), c.Uv, c.Color, c.Alpha);
    }

    private void ObservePilotSubmittedCorner(NativeBattleLogicalCommand command, int corner, Vector2 position, NativeBattlePrimitiveDepthVertex depth, Vector2 uv, Vector3 color, float alpha)
    {
        if (!PilotDiagnosticsEnabled || command.PilotGeometry is not { Identity.Effect: 2 or 3 or 144 } geometry)
        {
            return;
        }
        Vector3 world = geometry.Corner(corner);
        bool covered = false;
        if (pilotProjection is not null && geometry.Continuous && pilotNextGeometry.TryGetValue(geometry.Identity, out NativeBattlePilotVisualGeometry? next) && next.Continuous && geometry.CornerCount == next.CornerCount)
        {
            world = NativeBattlePilotVisualGeometry.SampleCorner(geometry, next, corner, pilotAlpha);
            covered = true;
        }
        ulong worldHash = PilotHash(pilotSubmission.WorldSignature, geometry.Identity.Bank);
        worldHash = PilotHash(worldHash, unchecked((uint)geometry.Identity.Slot));
        worldHash = PilotHash(worldHash, unchecked((ulong)geometry.Identity.Generation));
        worldHash = PilotHash(worldHash, unchecked((uint)geometry.Identity.Primitive));
        worldHash = PilotHash(worldHash, (uint)corner);
        worldHash = PilotHash(PilotHash(PilotHash(worldHash, world.X), world.Y), world.Z);
        ulong projected = PilotHash(PilotHash(PilotHash(PilotHash(pilotSubmission.ProjectedSignature, position.X), position.Y), depth.Depth), depth.ReciprocalW);
        ulong material = PilotHash(PilotHash(PilotHash(PilotHash(PilotHash(PilotHash(pilotSubmission.MaterialSignature, uv.X), uv.Y), color.X), color.Y), color.Z), alpha);
        pilotSubmission = pilotSubmission with { WorldSignature = worldHash, ProjectedSignature = projected, MaterialSignature = material, Corners = pilotSubmission.Corners + 1, ContinuousCorners = pilotSubmission.ContinuousCorners + (geometry.Continuous ? 1 : 0), CoveredContinuousCorners = pilotSubmission.CoveredContinuousCorners + (covered ? 1 : 0), DiscreteCorners = pilotSubmission.DiscreteCorners + (geometry.Continuous ? 0 : 1) };
    }

    private static ulong PilotHash(ulong hash, float value) => PilotHash(hash, BitConverter.SingleToUInt32Bits(value));
    private static ulong PilotHash(ulong hash, ulong value) => unchecked((hash ^ value) * 1099511628211UL);

    private readonly Dictionary<NativeBattlePilotVisualIdentity, NativeBattlePilotVisualGeometry> pilotNextGeometry = [];
    private NativeBattleCameraProjection? pilotProjection;
    private float pilotAlpha;
    public string? LastPilotFallbackReason { get; private set; }
    public int LastPilotFallbackCount { get; private set; }

    public bool CanSampleNativePilotInterval(NativeBattlePreparedPresentation earlier, NativeBattlePreparedPresentation later)
    {
        LastPilotFallbackReason = null;
        LastPilotFallbackCount = 0;
        Check(earlier.Primitives);
        Check(later.Primitives);
        if (LastPilotFallbackCount != 0)
        {
            LastPilotFallbackReason = "The completed interval contains geometry without a world-space presentation contract.";
        }
        return LastPilotFallbackCount == 0;

        void Check(NativeBattlePreparedPrimitiveFrame frame)
        {
            foreach (NativeBattleLogicalResolvedCommand resolved in frame.LastCommands)
            {
                NativeBattleLogicalCommand command = resolved.Command;
                if (resolved.Pass == 0 || command.Opcode is >= 0xE1 and <= 0xE6 or 0x02 or 0x80)
                {
                    continue;
                }
                bool polygon = (command.Opcode & 0xE0) == 0x20;
                int corners = (command.Opcode & 8) != 0 ? 4 : 3;
                if (polygon && command.PilotGeometry is { } geometry && geometry.CornerCount == corners)
                {
                    continue;
                }
                bool screenFlash = (command.Opcode & 0xFC) == 0x28 && command.Words.Length == 5 && (command.Flags20 & 1) != 0 && command.VertexPayload4.All(value => value == 0) && NativeScreenQuadCoversProjection(command, resolved.DrawMode);
                if (!screenFlash)
                {
                    LastPilotFallbackCount++;
                }
            }
        }
    }

    public byte[] RenderNativePilotPrimitiveFrame(NativeBattlePreparedPresentation earlier, NativeBattlePreparedPresentation later, float alpha, BattleCamera? overlayCamera = null, NativeBattlePrimitiveCapabilities? capabilities = null, IReadOnlyList<NativeBattleTextureReplacement>? replacements = null, Func<int, int, bool, PixelImage>? uiTexture = null, INativeBattleEffectTextureSource? effectTextureSource = null)
    {
        if (earlier.PilotEffect is not (2 or 3 or 144) || earlier.PilotEffect != later.PilotEffect || earlier.PilotGeneration != later.PilotGeneration || later.Callback != earlier.Callback + 1 || !float.IsFinite(alpha))
        {
            throw new ArgumentException("Compatible battle motion requires consecutive endpoints from the same supported spell.");
        }
        pilotAlpha = Math.Clamp(alpha, 0, 1);
        if (!CanSampleNativePilotInterval(earlier, later))
        {
            return RenderNativePrimitiveFrame(earlier.Primitives, earlier.Textures, overlayCamera, capabilities, earlier.TextureWindows, replacements, earlier.UiFrame, uiTexture, effectTextureSource);
        }
        if (pilotAlpha is 0 or 1)
        {
            NativeBattlePreparedPresentation endpoint = pilotAlpha == 0 ? earlier : later;
            return RenderNativePrimitiveFrame(endpoint.Primitives, endpoint.Textures, overlayCamera, capabilities, endpoint.TextureWindows, replacements, endpoint.UiFrame, uiTexture, effectTextureSource);
        }
        pilotProjection = NativeBattlePilotVisualGeometry.SampleProjection(earlier.PilotProjection!, later.PilotProjection!, earlier.PilotCamera!, later.PilotCamera!, pilotAlpha);
        pilotNextGeometry.Clear();
        foreach (NativeBattleLogicalResolvedCommand resolved in later.Primitives.LastCommands)
        {
            if (resolved.Command.PilotGeometry is { } geometry)
            {
                pilotNextGeometry[geometry.Identity] = geometry;
            }
        }
        try
        {
            return RenderNativePrimitiveFrame(earlier.Primitives, earlier.Textures, overlayCamera, capabilities, earlier.TextureWindows, replacements, earlier.UiFrame, uiTexture, effectTextureSource);
        }
        finally
        {
            pilotProjection = null;
            pilotNextGeometry.Clear();
        }
    }

    private bool TrySamplePilotVertex(NativeBattleLogicalCommand command, int corner, NativeBattlePrimitiveDrawMode mode, NativeBattlePrimitiveDepthState depthState, bool perspective, out Vector2 position, out NativeBattlePrimitiveDepthVertex depthVertex)
    {
        position = default;
        depthVertex = default;
        if (pilotProjection is null || command.PilotGeometry is not { } earlier)
        {
            return false;
        }
        Vector3 world = earlier.Corner(corner);
        if (earlier.Continuous && pilotNextGeometry.TryGetValue(earlier.Identity, out NativeBattlePilotVisualGeometry? later) && later.Continuous && earlier.CornerCount == later.CornerCount)
        {
            world = NativeBattlePilotVisualGeometry.SampleCorner(earlier, later, corner, pilotAlpha);
        }
        Vector3 projected = NativeBattlePilotVisualGeometry.Project(world, pilotProjection);
        float coordinateScale = (command.Flags20 & 1) != 0 ? 0.125f : 1;
        position = SampledNativePrimitivePosition(projected.X * coordinateScale, projected.Y * coordinateScale, command.Flags20, mode);
        int bucket = command.OrderingBucket ?? throw new InvalidDataException("Compatible battle motion requires the authored ordering bucket.");
        float step = Math.Max((float)(0.9985 / (2 * NativeBattlePrimitiveOrdering.BattleTableLength)), 1f / 65536);
        float bucketDepth = bucket * step;
        float depth;
        float reciprocalW = 1;
        float vertexDepth = earlier.SampleDepth(projected.Z);
        if (perspective)
        {
            float vertexWeight = (float)((depthState.RemainingWeight < 0.99999999 ? depthState.RemainingWeight : (double)depthState.RemainingWeight - NativeBattlePrimitiveDepthState.DepthBias) / 65536);
            depth = (command.Flags20 & 4) != 0 ? vertexDepth * NativeBattlePrimitiveDepthState.VertexDepthScale + NativeBattlePrimitiveDepthState.DepthBias : vertexWeight * vertexDepth + depthState.BucketMix * bucketDepth + NativeBattlePrimitiveDepthState.DepthBias;
            reciprocalW = 1 / vertexDepth;
        }
        else
        {
            depth = bucketDepth + NativeBattlePrimitiveDepthState.DepthBias;
        }
        depthVertex = new(depth, reciprocalW);
        return true;
    }

    private Vector2 SampledNativePrimitivePosition(float x, float y, uint flags, NativeBattlePrimitiveDrawMode mode)
    {
        if (mode.NormalViewport is not null || mode.ViewportInputs is not null)
        {
            NativeSoftwareNormalViewportDerived viewport = mode.NormalViewport ?? throw new NotSupportedException("Compatible primitive viewport requires its retained derived outputs.");
            NativeSoftwareViewportInputs inputs = mode.ViewportInputs ?? throw new NotSupportedException("Compatible primitive viewport requires its actual input dimensions.");
            if (inputs.Width <= 0 || inputs.Height <= 0)
            {
                throw new InvalidDataException("Compatible primitive viewport requires positive dimensions.");
            }
            bool full = (flags & 1) != 0;
            float nativeX = (float)((double)x * (full ? viewport.ScaleX : viewport.LowCoordinateScaleX) + viewport.NormalOffsetX);
            float nativeY = (float)((double)y * (full ? viewport.ScaleY : viewport.LowCoordinateScaleY) + viewport.NormalOffsetY);
            return new(ProjectionBounds.X + (float)(((double)nativeX + 0.5) * ProjectionBounds.Width / inputs.Width), ProjectionBounds.Y + (float)(((double)nativeY + 0.5) * ProjectionBounds.Height / inputs.Height));
        }
        float unit = (flags & 1) != 0 ? 1 : 0.125f;
        return new(ProjectionBounds.X + x * unit * ProjectionBounds.Width / 320, ProjectionBounds.Y + y * unit * ProjectionBounds.Height / 216);
    }
}
