namespace Memoria.FF8.Runtime;

public sealed partial class GameSession
{
    public NativeWorldProcess? WorldProcess => State.WorldProcess;
    public bool CanStoreWorldProcess => true;

    public void SetWorldProcess(NativeWorldProcess process)
    {
        ArgumentNullException.ThrowIfNull(process);
        process.Validate();
        State = State with { WorldProcess = process };
    }

    public static NativeWorldProcess PrepareWorldProcess(GameState departure, WorldState finalWorld)
    {
        ArgumentNullException.ThrowIfNull(departure);
        ArgumentNullException.ThrowIfNull(finalWorld);
        WorldState before = departure.World ?? throw new NotSupportedException("Native world callbacks require their current world departure.");
        NativeWorldProcess process = departure.WorldProcess ?? throw new NotSupportedException("Native world callbacks require executed process counter and camera cooldown history; migrate this save explicitly.");
        long callbacks = checked(finalWorld.Frame - before.Frame);
        NativeWorldGroundSelectionState? groundSelection = process.GroundSelection;
        if (callbacks > 0)
        {
            NativeWorldGroundCacheState? groundCache = finalWorld.NativeFootRuntime?.GroundCache ?? finalWorld.NativeCarRuntime?.GroundCache ?? finalWorld.NativeTrainRuntime?.GroundCache ?? finalWorld.NativeScriptedRuntime?.GroundCache ?? finalWorld.NativeGardenRuntime?.GroundCache ?? finalWorld.NativeRagnarokRuntime?.GroundCache;
            groundSelection = groundCache is null ? null : NativeWorldGroundSelectionState.FromCache(groundCache);
        }
        int? forcedAnimation = process.ForcedAnimationC76628;
        if (callbacks > 0)
        {
            //54A7F0 clears this global after draw once the normal entry hold ends.
            //Other transfer paths may overwrite it late; an unrepresented writer
            //invalidates this owner rather than retaining a stale known value.
            forcedAnimation = finalWorld.NativeFootRuntime?.EntityType == 2 ? finalWorld.NativeFootRuntime.NormalObjects?.ForcedAnimationC76628
                : before.NativeFootRuntime is not null && finalWorld.NativeFootRuntime is not null && finalWorld.Frame >= 24 && finalWorld.NativeRagnarokRuntime?.Transfer is null && (finalWorld.NativeGardenRuntime is null || finalWorld.NativeGardenRuntime.TransferControls.TransitionPhase == 0) ? -1 : null;
        }
        NativeWorldVerticalBiasState? bias = process.VerticalBias204DADC;
        if (finalWorld.NativeRagnarokRuntime is { } ragnarok && (finalWorld.NativeFootRuntime is null || before.NativeFootRuntime is null))
        {
            bias = ragnarok.Motor.VerticalBiasAuthority ?? NativeWorldVerticalBiasState.Exact(ragnarok.Motor.VerticalBias204DADC!.Value);
        }
        else if (before.NativeGardenRuntime is not null || before.NativeFootRuntime is not null || before.NativeCarRuntime is not null)
        {
            // These executed ordinary callbacks all consume557A90. Foot rate2
            //and ground-vehicle rate4 conservatively cover any real mode switch
            //inside the batch; an older null owner starts with the full WORD domain.
            bias = (bias ?? NativeWorldVerticalBiasState.UnknownWord).Damp(2, 4, callbacks);
        }
        return process.AdvanceMainLoopCallbacks(callbacks) with { CameraCooldown = finalWorld.NativeFootRuntime?.Forest?.CameraCooldown ?? finalWorld.NativeGardenRuntime?.Forest.CameraCooldown ?? finalWorld.NativeRagnarokRuntime?.Forest?.CameraCooldown ?? process.CameraCooldown, VehicleManualCameraVelocity = finalWorld.NativeCarRuntime?.ManualCameraVelocity ?? finalWorld.NativeGardenRuntime?.ManualCameraVelocity ?? finalWorld.NativeRagnarokRuntime?.ManualCameraVelocity ?? process.VehicleManualCameraVelocity, CarCameraFollowSpeed = finalWorld.NativeCarRuntime?.CameraFollowSpeed ?? finalWorld.NativeGardenRuntime?.CameraFollowSpeed ?? (finalWorld.NativeFootRuntime is null ? finalWorld.NativeRagnarokRuntime?.Motor.CameraFollowSpeed : null) ?? process.CarCameraFollowSpeed, VerticalBias204DADC = bias, ForcedAnimationC76628 = forcedAnimation, GroundSelection = groundSelection };
    }
}
