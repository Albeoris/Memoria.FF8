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
        return process.AdvanceMainLoopCallbacks(callbacks) with { CameraCooldown = finalWorld.NativeFootRuntime?.Forest?.CameraCooldown ?? process.CameraCooldown, CarCameraFollowSpeed = finalWorld.NativeCarRuntime?.CameraFollowSpeed ?? process.CarCameraFollowSpeed };
    }
}
