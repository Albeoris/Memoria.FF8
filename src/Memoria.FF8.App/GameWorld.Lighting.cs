using System.Numerics;
using Memoria.FF8.Assets;
using Memoria.FF8.Mods;
using Memoria.FF8.Rendering;

namespace Memoria.FF8;

internal sealed partial class GameWorld
{
    private static readonly IReadOnlyDictionary<int, FieldActorShadowState> EmptyLightingShadowStates = new Dictionary<int, FieldActorShadowState>();
    private FieldPresentationFrame? lastLightingPresentationFrame;
    private FieldPresentationSample? lastLightingPresentationSample;
    private FieldAssets? lastLightingPresentationField;
    private FieldPresentationFrame? frozenLightingPresentationFrame;
    private FieldPresentationSample? frozenLightingPresentationSample;

    internal FieldLightingScene? ActiveLighting { get; set; }
    internal string? LightingUnavailableReason => renderer?.LightingUnavailableReason;
    internal string? LightingShadowDiagnostic => renderer?.LightingShadowDiagnostic;
    internal IReadOnlyDictionary<int, FieldActorShadowState> LightingShadowStates => renderer?.LightingShadowStates ?? EmptyLightingShadowStates;
    internal IReadOnlyList<WalkmeshTriangle> LightingNativeShadowReceivers => renderer?.LightingNativeShadowReceivers ?? [];
    internal string LightingStatus => LightingUnavailableReason ?? LightingShadowDiagnostic ?? (renderer?.UsesAuthoredLighting == true ? "Spotlight shading active" : "Native field lighting");
    internal Vector2? ProjectLightingPoint(Vector3 world) => renderer?.ProjectWorldPoint(world);
    internal bool TryPickLightingGround(Vector2 screen, out Vector3 point)
    {
        point = default;
        return renderer?.TryPickGround(screen, out point) == true;
    }
    internal bool TryLightingScreenRay(Vector2 screen, out Vector3 origin, out Vector3 direction)
    {
        origin = direction = default;
        return renderer?.TryScreenRay(screen, out origin, out direction) == true;
    }

    internal bool BeginLightingEdit()
    {
        if (lastLightingPresentationFrame is null || lastLightingPresentationSample is null || !ReferenceEquals(lastLightingPresentationField, Field) || !ReferenceEquals(lastLightingPresentationFrame.Owner, Events.Scene) || lastLightingPresentationFrame.CameraIndex != Events.Scene.CameraIndex)
        {
            return false;
        }
        frozenLightingPresentationFrame = lastLightingPresentationFrame;
        frozenLightingPresentationSample = lastLightingPresentationSample;
        return true;
    }

    internal void EndLightingEdit()
    {
        frozenLightingPresentationFrame = null;
        frozenLightingPresentationSample = null;
    }
}
