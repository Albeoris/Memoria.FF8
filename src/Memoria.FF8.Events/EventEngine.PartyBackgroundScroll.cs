using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private string? UnsupportedNativePartyBackgroundScroll(Fiber fiber)
    {
        if (fiber.Stack.Count == 0 || (uint)fiber.Stack.Peek() >= 3 || program is null || NativeRuntimeProfile is not { IsVerified: true } || Scene.NativeActorLayoutVersion != 1 || Scene.NativeFieldInitialization is not { Stage: NativeFieldInitializationStage.Completed } || Scene.PartyBindingMemory is not { Version: 1 })
        {
            return "DSCROLLP requires its original saved party slot and completed native allocation";
        }
        int slot = fiber.Stack.Peek();
        int nativeActor = Scene.PartyBindingMemory.At(slot);
        if (Scene.ActorBackgroundScroll is { Layer: not 0 } || (uint)nativeActor >= Scene.NativeActorObjectIds.Count || variables.TryGetValue(194 + slot, out int alias) && alias != nativeActor || ResolveNativeActorScrollCenter?.Invoke((byte)nativeActor) is null)
        {
            return "DSCROLLP requires the actual retained saved-party body and its certified projection endpoint";
        }
        return null;
    }

    private int[] ExecuteNativePartyBackgroundScroll(Fiber fiber)
    {
        int[] args = Pop(fiber, 1);
        byte nativeActor = Scene.PartyBindingMemory!.At(args[0]);
        int objectId = Scene.NativeActorObjectIds[nativeActor];
        int rawObjectId = Enumerable.Range(0, program!.NativeObjects.Count).Single(index => program.NativeObjects[index] == objectId);
        BeginNativeActorBackgroundScroll(rawObjectId, objectId, nativeActor, args[0]);
        // Native520E50 samples saved B194+slot once, writes mode0/status0,
        // and returns3. Continuous475E60 follows that retained body thereafter.
        WaitFrames(fiber, 1);
        return args;
    }
}
