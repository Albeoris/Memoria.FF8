namespace Memoria.FF8.Runtime;

public sealed partial class GameSession
{
    public bool NativeFieldDebugRequested => State.NativeFieldDebugRequested;
    public bool CanStoreNativeFieldDebugRequested => true;

    public void SetNativeFieldDebugRequested(bool requested)
    {
        State = State with { NativeFieldDebugRequested = requested };
    }
}
