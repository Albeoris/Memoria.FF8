using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Rendering;

public sealed partial class BattleRenderer
{
    private readonly Dictionary<NativeBattlePilotVisualIdentity, NativeBattlePilotVisualGeometry> pilotNextGeometry = [];
    private NativeBattleCameraProjection? pilotProjection;
    private float pilotAlpha;

    public byte[] RenderNativePilotPrimitiveFrame(NativeBattlePreparedPresentation earlier, NativeBattlePreparedPresentation later, float alpha, BattleCamera? overlayCamera = null, NativeBattlePrimitiveCapabilities? capabilities = null, IReadOnlyList<NativeBattleTextureReplacement>? replacements = null, Func<int, int, bool, PixelImage>? uiTexture = null, INativeBattleEffectTextureSource? effectTextureSource = null)
    {
        if (earlier.PilotEffect is not (2 or 3 or 144) || earlier.PilotEffect != later.PilotEffect || earlier.PilotGeneration != later.PilotGeneration || later.Callback != earlier.Callback + 1 || !float.IsFinite(alpha))
        {
            throw new ArgumentException("Compatible battle motion requires consecutive endpoints from the same supported spell.");
        }
        pilotAlpha = Math.Clamp(alpha, 0, 1);
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
            world = Vector3.Lerp(world, later.Corner(corner), pilotAlpha);
        }
        Vector3 projected = NativeBattlePilotVisualGeometry.Project(world, pilotProjection);
        float coordinateScale = (command.Flags20 & 1) != 0 ? 0.125f : 1;
        position = NativePrimitivePosition(projected.X * coordinateScale, projected.Y * coordinateScale, command.Flags20, mode);
        int bucket = command.OrderingBucket ?? throw new InvalidDataException("Compatible battle motion requires the authored ordering bucket.");
        float step = Math.Max((float)(0.9985 / (2 * NativeBattlePrimitiveOrdering.BattleTableLength)), 1f / 65536);
        float bucketDepth = bucket * step;
        float depth;
        float reciprocalW = 1;
        if (perspective)
        {
            float vertexWeight = (float)((depthState.RemainingWeight < 0.99999999 ? depthState.RemainingWeight : (double)depthState.RemainingWeight - NativeBattlePrimitiveDepthState.DepthBias) / 65536);
            depth = (command.Flags20 & 4) != 0 ? projected.Z * NativeBattlePrimitiveDepthState.VertexDepthScale + NativeBattlePrimitiveDepthState.DepthBias : vertexWeight * projected.Z + depthState.BucketMix * bucketDepth + NativeBattlePrimitiveDepthState.DepthBias;
            reciprocalW = 1 / Math.Max(projected.Z, 1);
        }
        else
        {
            depth = bucketDepth + NativeBattlePrimitiveDepthState.DepthBias;
        }
        depthVertex = new(depth, reciprocalW);
        return true;
    }
}
