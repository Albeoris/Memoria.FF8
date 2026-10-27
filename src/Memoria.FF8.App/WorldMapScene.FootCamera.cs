using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class WorldMapScene
{
    private bool ReadFootTerrainOcclusion(WorldState before, WorldState after)
    {
        NativeFootWorldRuntimeState foot = after.NativeFootRuntime!;
        NativeWorldFootCameraState camera;
        if (before.NativeTrainRuntime is { } train)
        {
            camera = new NativeWorldFootCameraState(before.X, before.Z, before.NativeHeight, before.CameraTargetX, before.CameraTargetZ, before.CameraTargetNativeHeight, train.CameraGeometry.Yaw & 4095, train.CameraGeometry.Pitch, 0, train.CameraGeometry.OffsetZ);
        }
        else if (before.NativeCarRuntime is { } car)
        {
            camera = car.ProjectionCamera ?? new NativeWorldFootCameraState(before.X, before.Z, before.NativeHeight, before.CameraTargetX, before.CameraTargetZ, before.CameraTargetNativeHeight, before.CameraYaw, -112, -341, -7168);
        }
        else
        {
            camera = foot.StationTrainProcess?.ProjectionCamera ?? foot.NormalObjects?.Camera ?? NativeWorldFootCameraState.Create(before);
        }
        short groundHeight = unchecked((short)-(before.NativeHeight - (before.GroundType is <= 5 ? 320 : 0)));
        var result = assets.CertifyNativeTerrainCameraOcclusion(camera, groundHeight, replacements, foot.EntityType);
        return result.Occluded ?? throw new NotSupportedException(result.Reason);
    }

    private static WorldState PrepareFootCameraToggle(WorldState state, NativeWorldInputHistory? input)
    {
        if (input is null || (input.CurrentButtons & 2) == 0)
        {
            return state;
        }
        ushort previous = input.PreviousButtons ?? throw new NotSupportedException("Camera mode requires the preceding native digital input sample.");
        if ((previous & 2) != 0 || NativeWorldEntryControl.DelayAfterCallback(state.Frame, 0) > 0 || state.NativeTrainRuntime is not null || state.NativeScriptedRuntime is not null || state.EventUi?.ScriptMode is > 0)
        {
            return state;
        }
        if (state.NativeFootRuntime is null)
        {
            if (state.NativeCarRuntime is { } car)
            {
                return state with { NativeCarRuntime = car with { AnimationVariant = car.AnimationVariant == 0 ? (byte)1 : (byte)0 } };
            }
            if (state.NativeGardenRuntime is { } garden)
            {
                return state with { NativeGardenRuntime = garden with { AnimationVariant = garden.AnimationVariant == 0 ? (byte)1 : (byte)0 } };
            }
            if (state.NativeRagnarokRuntime is { } ragnarok)
            {
                return state with { NativeRagnarokRuntime = ragnarok with { AnimationVariant = ragnarok.AnimationVariant == 0 ? (byte)1 : (byte)0 } };
            }
            throw new NotSupportedException("Camera mode requires a supported native entity owner.");
        }
        NativeFootWorldRuntimeState foot = state.NativeFootRuntime;
        return state with { NativeFootRuntime = foot with { AnimationVariant = foot.AnimationVariant == 0 ? (byte)1 : (byte)0, CameraTransition = new NativeFootCameraTransition(17, foot.CameraTransition?.ProjectionDistance ?? 1024) } };
    }
}
