using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private string? UnsupportedNativePartyLinearBackgroundScroll(Fiber fiber)
    {
        int[] top = fiber.Stack.ToArray();
        if (top.Length < 2 || (short)top[0] < 0 || (uint)top[1] >= 3 || program is null || NativeRuntimeProfile is not { IsVerified: true } || Scene.NativeActorLayoutVersion != 1 || Scene.NativeFieldInitialization is not { Stage: NativeFieldInitializationStage.Completed } || Scene.PartyBindingMemory is not { Version: 1 })
        {
            return "LSCROLLP requires its original saved party slot, signed-WORD duration and completed native allocation";
        }
        int slot = top[1];
        int nativeActor = Scene.PartyBindingMemory.At(slot);
        if ((uint)nativeActor >= Scene.NativeActorObjectIds.Count || variables.TryGetValue(194 + slot, out int alias) && alias != nativeActor || !CanReplaceActorBackgroundScroll(0) || CanSetNativeActorLinearBackgroundScroll?.Invoke((byte)nativeActor, (short)top[0]) != true)
        {
            return "LSCROLLP requires the actual retained saved-party body, authentic current center and certified callback projection";
        }
        return null;
    }

    private int[] ExecuteNativePartyLinearBackgroundScroll(Fiber fiber)
    {
        int[] args = Pop(fiber, 2);
        byte nativeActor = Scene.PartyBindingMemory!.At(args[0]);
        int objectId = Scene.NativeActorObjectIds[nativeActor];
        int rawObjectId = Enumerable.Range(0, program!.NativeObjects.Count).Single(index => program.NativeObjects[index] == objectId);
        // 520E90 samples saved B194+slot once, writes mode1/status0 and
        // lowWORD duration, preserves current/previous/counter and returns2.
        BeginNativeActorBackgroundScroll(rawObjectId, objectId, nativeActor, args[0], mode: 1, duration: (short)args[1]);
        return args;
    }
}
