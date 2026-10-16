using System.Text.Json.Serialization;
using System.Text.Json;

namespace Memoria.FF8.Runtime;

/// <summary>Actual54D1A0 completion and54B460 case5 placement; type128 is the camera selector, while the controlled foot is shape0/entity0.</summary>
public sealed record NativeWorldRagnarokDismountReceipt([property: JsonRequired] NativeRagnarokTransferState Landing, [property: JsonRequired] NativeWorldPathPosition Placement, [property: JsonRequired] NativeRagnarokTransferCamera Camera, [property: JsonRequired] NativeWorldFootCameraState ProjectionCamera, [property: JsonRequired] long SourceProcessCallbacks)
{
    public const string DismountRule = "ff8-en-ragnarok-54d1a0-54b460-foot0-retained-process-v1";

    public void Validate(NativeRagnarokWorldRuntimeState? runtime = null, NativeWorldProcess? process = null)
    {
        Landing.Validate();
        Camera.Validate();
        ProjectionCamera.Validate();
        if (Landing.Kind != NativeRagnarokTransferKind.Landing || Landing.Counter != 61 || SourceProcessCallbacks < 0)
        {
            throw new InvalidDataException("Ragnarok foot receipt requires the actual completed sixty-one-callback landing.");
        }
        NativeRagnarokTransferStep completed = (Landing with { Counter = 60 }).Advance();
        if (Camera.ProjectionDistance != completed.Camera.ProjectionDistance || Camera.CurveThreshold != completed.Camera.CurveThreshold || Camera.Pitch != completed.Camera.Pitch || Camera.Yaw != completed.Camera.Yaw || Camera.OffsetZ != completed.Camera.OffsetZ || !Camera.ProjectionWords.AsSpan().SequenceEqual(completed.Camera.ProjectionWords.AsSpan()) || !Camera.PerspectiveWords.AsSpan().SequenceEqual(completed.Camera.PerspectiveWords.AsSpan()))
        {
            throw new InvalidDataException("Ragnarok foot receipt changed the actual unsnapped landing camera globals.");
        }
        if (runtime is not null)
        {
            runtime.RequireSceneState();
            NativeRagnarokWorldObject parked = runtime.Objects[runtime.Allocation.RagnarokIndex];
            bool retainedTransfer = runtime.Transfer is not null || runtime.LandingPlacement is not null;
            if (retainedTransfer && (!JsonElement.DeepEquals(JsonSerializer.SerializeToElement(runtime.Transfer), JsonSerializer.SerializeToElement(Landing)) || runtime.LandingPlacement != Placement) || parked.Position != completed.Position || parked.Yaw != Landing.BodyYaw)
            {
                throw new InvalidDataException("Ragnarok foot receipt differs from its retained original parked ship and placement owners.");
            }
        }
        if (process is not null && process.MainLoopCallbacks < SourceProcessCallbacks)
        {
            throw new InvalidDataException("Ragnarok foot receipt precedes its retained process callback.");
        }
    }
}

public sealed partial record NativeFootWorldRuntimeState
{
    public static NativeFootWorldRuntimeState CreateRagnarokDismount(NativeRagnarokWorldRuntimeState runtime, NativeRagnarokTransferState completed, NativeRagnarokTransferCamera camera, NativeWorldProcess process)
    {
        runtime.RequireSceneState();
        process.Validate();
        NativeWorldForestState forest = runtime.Forest ?? throw new NotSupportedException("Ragnarok landing requires the actual retained550070/555B80 particle and random owner.");
        NativeWorldGroundCacheState cache = runtime.GroundCache! with { Alternatives = runtime.GroundCache!.Alternatives.Select(alternative => alternative with { Entries = [] }).ToArray() };
        NativeWorldRagnarokDismountReceipt receipt = new(completed, runtime.LandingPlacement ?? throw new InvalidDataException("Ragnarok landing has no actual53E7A0 foot placement."), camera, runtime.ProjectionCamera!, process.MainLoopCallbacks);
        receipt.Validate(runtime, process);
        NativeFootWorldRuntimeState result = new()
        {
            ProofVersion = CurrentProofVersion,
            Rule = RagnarokDismountRule,
            EntityType = 0,
            AnimationVariant = 0,
            MapDisplayMode = runtime.Allocation.MapDisplayMode,
            CameraOffsetY = runtime.ProjectionCamera!.OffsetY,
            CameraOffsetZ = camera.OffsetZ,
            ReloadSourceWorldFrame = null,
            GroundCache = cache,
            GroundYaw = runtime.GroundYaw ?? throw new NotSupportedException("Ragnarok landing requires its retained selected ground yaw."),
            PreviousHorizontalInput = null,
            Forest = forest with { CameraPitch = camera.Pitch },
            RagnarokDismount = receipt
        };
        result.RequireNativeGroundState();
        result.RequireSupportedNormalFoot();
        return result;
    }
}
