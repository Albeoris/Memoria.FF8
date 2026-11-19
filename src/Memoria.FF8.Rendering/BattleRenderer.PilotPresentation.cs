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

    private void ObservePilotSubmittedTriangle(in NativeBattleLogicalCommandView command, NativeBattlePrimitiveDrawMode drawMode, byte pass, PrimitiveVertex a, PrimitiveVertex b, PrimitiveVertex c, int cornerA, int cornerB, int cornerC)
    {
        if (command.Pilot is not { IsEmpty: false } geometry || !NativeBattleSmoothPresentation.Supports(geometry.Identity.Effect))
        {
            return;
        }
        ulong material = PilotHash(pilotSubmission.MaterialSignature, command.Opcode);
        material = PilotHash(material, pass);
        material = PilotHash(material, drawMode.TexturePage ?? 0);
        material = PilotHash(material, drawMode.TextureWindowBits ?? 0);
        if ((command.Opcode & 4) != 0)
        {
            material = PilotHash(material, command.WordSpan[2] >> 16);
        }
        pilotSubmission = pilotSubmission with { MaterialSignature = material };
        ObservePilotSubmittedCorner(command, cornerA, new(a.X, a.Y), new(a.Z, a.ReciprocalW), a.Uv, a.Color, a.Alpha);
        ObservePilotSubmittedCorner(command, cornerB, new(b.X, b.Y), new(b.Z, b.ReciprocalW), b.Uv, b.Color, b.Alpha);
        ObservePilotSubmittedCorner(command, cornerC, new(c.X, c.Y), new(c.Z, c.ReciprocalW), c.Uv, c.Color, c.Alpha);
    }

    private void ObservePilotSubmittedCorner(in NativeBattleLogicalCommandView command, int corner, Vector2 position, NativeBattlePrimitiveDepthVertex depth, Vector2 uv, Vector3 color, float alpha)
    {
        if (!PilotDiagnosticsEnabled || command.Pilot is not { IsEmpty: false } geometry || !NativeBattleSmoothPresentation.Supports(geometry.Identity.Effect))
        {
            return;
        }
        Vector3 world = geometry.Corner(corner);
        bool covered = false;
        if (activePilotInterval is { } interval && geometry.Continuous && interval.TryGetLaterGeometry(geometry.Identity, out NativeBattlePilotGeometryReference next) && next.Continuous && geometry.CornerCount == next.CornerCount)
        {
            world = NativeBattlePilotVisualGeometry.SampleCorner(geometry, next, corner, pilotAlpha);
            covered = true;
        }
        ulong worldHash = PilotHash(pilotSubmission.WorldSignature, geometry.Identity.Bank);
        if (geometry.Identity.ModelOwner is { } modelOwner)
        {
            (uint domain, string identity) = modelOwner switch
            {
                BattlePartyActorModel owner => (1u, owner.Actor.ActorId),
                BattleEnemyActorModel owner => (2u, owner.Actor.ActorId),
                BattleForeignPartyModel owner => (3u, owner.Presentation.Key),
                BattleForeignEnemyModel owner => (4u, owner.Presentation.Key),
                _ => throw new InvalidDataException("The visual identity has an unsupported model owner.")
            };
            worldHash = PilotHash(worldHash, (ulong)domain);
            foreach (char value in identity)
            {
                worldHash = PilotHash(worldHash, (ulong)value);
            }
        }
        else
        {
            worldHash = PilotHash(worldHash, unchecked((uint)geometry.Identity.Slot));
        }
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

    private sealed class NativeBattlePilotInterval(NativeBattlePreparedPresentation earlier, NativeBattlePreparedPresentation later, string? fallbackReason, int fallbackCount)
    {
        private Dictionary<NativeBattlePilotVisualIdentity, NativeBattlePilotGeometryReference>? geometry;
        private bool geometryPrepared;
        internal NativeBattlePreparedPresentation Earlier { get; } = earlier;
        internal NativeBattlePreparedPresentation Later { get; } = later;
        internal string? FallbackReason { get; } = fallbackReason;
        internal int FallbackCount { get; } = fallbackCount;

        internal void PrepareLaterGeometry()
        {
            if (geometryPrepared)
            {
                return;
            }
            Dictionary<NativeBattlePilotVisualIdentity, NativeBattlePilotGeometryReference> indexed = geometry ??= [];
            try
            {
                HashSet<NativeBattlePilotModelPose> poses = [];
                foreach (ref readonly NativeBattleLogicalResolvedCommand resolved in Later.Primitives.LastCommands)
                {
                    if (resolved.Command.Pilot is { IsEmpty: false } pilot)
                    {
                        if (pilot.ModelPose is { } pose && poses.Add(pose))
                        {
                            foreach (NativeBattlePilotGeometryReference surface in pose.Geometry())
                            {
                                indexed[surface.Identity] = surface;
                            }
                        }
                        indexed[pilot.Identity] = pilot;
                    }
                }
                geometryPrepared = true;
            }
            catch
            {
                indexed.Clear();
                throw;
            }
        }

        internal void ReuseLaterGeometryCapacity(NativeBattlePilotInterval retired)
        {
            geometry = retired.ReleaseLaterGeometry();
        }

        internal Dictionary<NativeBattlePilotVisualIdentity, NativeBattlePilotGeometryReference>? ReleaseLaterGeometry()
        {
            Dictionary<NativeBattlePilotVisualIdentity, NativeBattlePilotGeometryReference>? retired = geometry;
            geometry = null;
            geometryPrepared = false;
            retired?.Clear();
            return retired;
        }

        internal bool TryGetLaterGeometry(NativeBattlePilotVisualIdentity identity, out NativeBattlePilotGeometryReference pilot) => geometry!.TryGetValue(identity, out pilot);
    }

    private NativeBattlePilotInterval? pilotInterval;
    private NativeBattlePilotInterval? activePilotInterval;
    private NativeBattlePilotProjector pilotProjector;
    private float pilotAlpha;
    public string? LastPilotFallbackReason { get; private set; }
    public int LastPilotFallbackCount { get; private set; }

    public void ClearNativePilotInterval()
    {
        if (pilotInterval is { } retired && !ReferenceEquals(retired, activePilotInterval))
        {
            retired.ReleaseLaterGeometry();
        }
        pilotInterval = null;
    }

    public bool CanSampleNativePilotInterval(NativeBattlePreparedPresentation earlier, NativeBattlePreparedPresentation later)
    {
        NativeBattlePilotInterval? interval = pilotInterval;
        if (interval is null || !ReferenceEquals(interval.Earlier, earlier) || !ReferenceEquals(interval.Later, later))
        {
            NativeBattlePilotInterval replacement = PrepareNativePilotInterval(earlier, later);
            if (interval is not null && !ReferenceEquals(interval, activePilotInterval))
            {
                replacement.ReuseLaterGeometryCapacity(interval);
            }
            pilotInterval = interval = replacement;
        }
        LastPilotFallbackCount = interval.FallbackCount;
        LastPilotFallbackReason = interval.FallbackReason;
        return interval.FallbackCount == 0;
    }

    private static NativeBattlePilotInterval PrepareNativePilotInterval(NativeBattlePreparedPresentation earlier, NativeBattlePreparedPresentation later)
    {
        string? fallbackReason = null;
        int fallbackCount = 0;
        Check(earlier.Primitives);
        Check(later.Primitives);
        if (fallbackCount != 0)
        {
            fallbackReason = "The completed interval contains geometry without a world-space presentation contract.";
        }
        if (earlier.PilotCamera is { } earlierCamera && later.PilotCamera is { } laterCamera && !NativeBattlePilotVisualGeometry.CanSampleProjection(earlierCamera, laterCamera))
        {
            fallbackCount++;
            fallbackReason = "The completed interval contains a noninvertible native camera pose basis.";
        }
        return new(earlier, later, fallbackReason, fallbackCount);

        void Check(NativeBattlePreparedPrimitiveFrame frame)
        {
            foreach (ref readonly NativeBattleLogicalResolvedCommand resolved in frame.LastCommands)
            {
                if (resolved.Pass == 0)
                {
                    continue;
                }
                NativeBattleLogicalCommandReference command = resolved.Command;
                byte opcode = command.Opcode;
                if (opcode is >= 0xE1 and <= 0xE6 or 0x02 or 0x80)
                {
                    continue;
                }
                bool polygon = (opcode & 0xE0) == 0x20;
                bool tile = (opcode & 0xFC) is 0x60 or 0x68;
                bool line = (opcode & 0xF8) is 0x40 or 0x50;
                int corners = line ? 2 : tile || (opcode & 8) != 0 ? 4 : 3;
                if ((polygon || tile || line) && command.Pilot is { IsEmpty: false } geometry && geometry.CornerCount == corners)
                {
                    continue;
                }
                bool screenFlash = command.ScreenCoverage is not null;
                if (!screenFlash)
                {
                    fallbackCount++;
                }
            }
        }
    }

    public byte[] RenderNativePilotPrimitiveFrame(NativeBattlePreparedPresentation earlier, NativeBattlePreparedPresentation later, float alpha, BattleCamera? overlayCamera = null, NativeBattlePrimitiveCapabilities? capabilities = null, IReadOnlyList<NativeBattleTextureReplacement>? replacements = null, Func<int, int, bool, PixelImage>? uiTexture = null, INativeBattleEffectTextureSource? effectTextureSource = null)
    {
        if (!earlier.CanInterpolateMotion || !later.CanInterpolateMotion || earlier.SceneMotion != later.SceneMotion || earlier.PilotEffect != later.PilotEffect || earlier.PilotGeneration != later.PilotGeneration || later.Callback != earlier.Callback + 1 || !float.IsFinite(alpha))
        {
            throw new ArgumentException("Compatible battle motion requires consecutive endpoints from the same supported presentation.");
        }
        pilotAlpha = Math.Clamp(alpha, 0, 1);
        if (!CanSampleNativePilotInterval(earlier, later))
        {
            NativeBattlePreparedPresentation endpoint = pilotAlpha == 1 ? later : earlier;
            return RenderNativePrimitiveFrame(endpoint.Primitives, endpoint.Textures, overlayCamera, capabilities, endpoint.TextureWindows, replacements, endpoint.UiFrame, uiTexture, effectTextureSource);
        }
        if (pilotAlpha is 0 or 1)
        {
            NativeBattlePreparedPresentation endpoint = pilotAlpha == 0 ? earlier : later;
            return RenderNativePrimitiveFrame(endpoint.Primitives, endpoint.Textures, overlayCamera, capabilities, endpoint.TextureWindows, replacements, endpoint.UiFrame, uiTexture, effectTextureSource);
        }
        try
        {
            NativeBattleCameraProjection projection = NativeBattlePilotVisualGeometry.SampleProjection(earlier.PilotProjection!, later.PilotProjection!, earlier.PilotCamera!, later.PilotCamera!, pilotAlpha);
            pilotProjector = NativeBattlePilotVisualGeometry.PrepareProjector(projection);
            NativeBattlePilotInterval interval = pilotInterval!;
            interval.PrepareLaterGeometry();
            activePilotInterval = interval;
            return RenderNativePrimitiveFrame(earlier.Primitives, earlier.Textures, overlayCamera, capabilities, earlier.TextureWindows, replacements, earlier.UiFrame, uiTexture, effectTextureSource);
        }
        finally
        {
            pilotProjector = default;
            NativeBattlePilotInterval? retired = activePilotInterval;
            activePilotInterval = null;
            if (retired is not null && !ReferenceEquals(retired, pilotInterval))
            {
                retired.ReleaseLaterGeometry();
            }
        }
    }

    private readonly record struct NativeBattlePilotPrimitiveSample(bool IsActive, NativeBattlePilotGeometryReference Earlier, NativeBattlePilotCornerSampler Corners, ushort Flags, float? BucketDepth, bool Perspective, float VertexWeight, float BucketMix, float CoordinateScale, bool ScreenSpace, ushort? FocalOverride);

    private NativeBattlePilotPrimitiveSample PreparePilotPrimitiveSample(in NativeBattleLogicalCommandView command, NativeBattlePrimitiveDepthState depthState, bool perspective)
    {
        if (activePilotInterval is not { } interval || command.Pilot is not { IsEmpty: false } earlier)
        {
            return default;
        }
        NativeBattlePilotCornerSampler corners = earlier.Continuous && interval.TryGetLaterGeometry(earlier.Identity, out NativeBattlePilotGeometryReference later) && later.Continuous && earlier.CornerCount == later.CornerCount
            ? NativeBattlePilotVisualGeometry.PrepareCornerSampling(earlier, later, pilotAlpha)
            : NativeBattlePilotVisualGeometry.PrepareCornerSampling(earlier, earlier, 0);
        float vertexWeight = perspective ? (float)((depthState.RemainingWeight < 0.99999999 ? depthState.RemainingWeight : (double)depthState.RemainingWeight - NativeBattlePrimitiveDepthState.DepthBias) / 65536) : 0;
        ushort flags = command.Flags20;
        float coordinateScale = (flags & 1) != 0 ? 0.125f : 1;
        float step = Math.Max((float)(0.9985 / (2 * NativeBattlePrimitiveOrdering.BattleTableLength)), 1f / 65536);
        float? bucketDepth = command.OrderingBucket is int bucket ? bucket * step : null;
        return new(true, earlier, corners, flags, bucketDepth, perspective, vertexWeight, depthState.BucketMix, coordinateScale, earlier.ScreenSpace, earlier.FocalLengthOverride);
    }

    private bool TrySamplePilotCorner(in NativeBattlePilotPrimitiveSample sample, in NativeBattlePrimitiveViewportProjector viewport, int corner, out Vector2 position, out NativeBattlePrimitiveDepthVertex depthVertex)
    {
        position = default;
        depthVertex = default;
        if (!sample.IsActive)
        {
            return false;
        }
        NativeBattlePilotGeometryReference earlier = sample.Earlier;
        Vector3 world = sample.Corners.SampleCorner(corner);
        Vector3 projected = sample.ScreenSpace ? world : pilotProjector.Project(world, sample.FocalOverride);
        if (earlier.ProjectionScreenBounds is { } bounds)
        {
            projected.X = Math.Clamp(projected.X, bounds.X, bounds.Z);
            projected.Y = Math.Clamp(projected.Y, bounds.Y, bounds.W);
        }
        projected.X *= earlier.ScreenScale;
        projected.Y *= earlier.ScreenScale;
        if (!earlier.ScreenOffsets.IsDefaultOrEmpty)
        {
            if (earlier.ScreenOffsets.Length != earlier.CornerCount)
            {
                throw new InvalidDataException("Compatible outline motion requires one authored screen offset per corner.");
            }
            Vector2 offset = earlier.ScreenOffsets[corner];
            projected.X += offset.X;
            projected.Y += offset.Y;
        }
        position = viewport.Project(projected.X * sample.CoordinateScale, projected.Y * sample.CoordinateScale);
        float bucketDepth = sample.BucketDepth ?? throw new InvalidDataException("Compatible battle motion requires the authored ordering bucket.");
        float depth;
        float reciprocalW = 1;
        float vertexDepth = earlier.UseSourceDepths ? (earlier.SourceDepths ?? throw new InvalidDataException("Compatible outline motion requires its retained native depth FIFO."))[corner] : earlier.SampleDepth(projected.Z);
        if (sample.Perspective)
        {
            depth = (sample.Flags & 4) != 0 ? vertexDepth * NativeBattlePrimitiveDepthState.VertexDepthScale + NativeBattlePrimitiveDepthState.DepthBias : sample.VertexWeight * vertexDepth + sample.BucketMix * bucketDepth + NativeBattlePrimitiveDepthState.DepthBias;
            reciprocalW = 1 / vertexDepth;
        }
        else
        {
            depth = bucketDepth + NativeBattlePrimitiveDepthState.DepthBias;
        }
        depthVertex = new(depth, reciprocalW);
        return true;
    }
}
