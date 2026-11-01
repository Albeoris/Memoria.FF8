using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class WorldMapScene
{
    private static NativeWorldBackgroundRailState? GetFootBackgroundRails(WorldState state)
    {
        return state.NativeGardenRuntime?.BackgroundRails ?? state.NativeRagnarokRuntime?.BackgroundRails;
    }

    private WorldState StepFootBackgroundRails(WorldState cameraState)
    {
        if (GetFootBackgroundRails(cameraState) is not { } rails)
        {
            return cameraState;
        }
        NativeFootWorldRuntimeState foot = cameraState.NativeFootRuntime!;
        NativeWorldFootCameraState camera = NativeWorldFootCameraState.Create(cameraState);
        int curve = cameraState.NativeRagnarokRuntime?.TransferCamera?.CurveThreshold ?? cameraState.NativeRagnarokRuntime?.ProjectionCurveThreshold ?? 5216;
        rails = StepBackgroundRails(rails, camera, foot.EntityType, cameraState.EventUi?.ScriptMode ?? 0, curve);
        return PublishFootBackgroundRails(cameraState, rails);
    }

    private WorldState CompleteFootBackgroundRails(WorldState accepted)
    {
        if (GetFootBackgroundRails(accepted) is not { } rails)
        {
            return accepted;
        }
        return PublishFootBackgroundRails(accepted, CompleteBackgroundRails(rails, accepted.X, accepted.Z, accepted.NativeHeight));
    }

    private WorldState PublishFootBackgroundRails(WorldState state, NativeWorldBackgroundRailState rails)
    {
        if (state.NativeGardenRuntime is { } garden)
        {
            state = state with { NativeGardenRuntime = garden with { BackgroundRails = rails } };
            if (gardenFootCallback?.CameraState is { } cameraState)
            {
                gardenFootCallback.CameraState = cameraState with { NativeGardenRuntime = cameraState.NativeGardenRuntime! with { BackgroundRails = rails } };
            }
        }
        else if (state.NativeRagnarokRuntime is { } ragnarok)
        {
            state = state with { NativeRagnarokRuntime = ragnarok with { BackgroundRails = rails } };
            if (ragnarokFootCallback?.CameraState is { } cameraState)
            {
                ragnarokFootCallback.CameraState = cameraState with { NativeRagnarokRuntime = cameraState.NativeRagnarokRuntime! with { BackgroundRails = rails } };
            }
        }
        return state;
    }
}
