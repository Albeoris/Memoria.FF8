using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    public Func<byte, short, bool>? CanSetNativeActorLinearBackgroundScroll { get; set; }

    private string? UnsupportedNativeActorLinearBackgroundScroll(Fiber fiber)
    {
        int[] top = fiber.Stack.ToArray();
        if (top.Length < 2 || (short)top[0] < 0 || program is null || (uint)top[1] >= program.NativeObjects.Count || NativeRuntimeProfile is not { IsVerified: true } || Scene.NativeActorLayoutVersion != 1 || Scene.NativeFieldInitialization is not { Stage: NativeFieldInitializationStage.Completed })
        {
            return "LSCROLLA requires its original raw actor target, signed-WORD duration and completed native allocation";
        }
        int objectId = program.NativeObjects[top[1]];
        int nativeActor = Scene.NativeActorObjectIds.IndexOf(objectId);
        if (nativeActor is < 0 or >= 255 || !CanReplaceActorBackgroundScroll(0) || CanSetNativeActorLinearBackgroundScroll?.Invoke((byte)nativeActor, (short)top[0]) != true)
        {
            return "LSCROLLA requires a retained target, authentic previous center and certified per-callback projection";
        }
        return null;
    }

    private int[] ExecuteNativeActorLinearBackgroundScroll(Fiber fiber)
    {
        int[] args = Pop(fiber, 2);
        int objectId = program!.NativeObjects[args[0]];
        byte nativeActor = checked((byte)Scene.NativeActorObjectIds.IndexOf(objectId));
        // 520D90 writes mode1/status0, lowWORD duration and selected actor
        // BYTE+598, preserves current/previous/counter and returns2.
        BeginNativeActorBackgroundScroll(args[0], objectId, nativeActor, mode: 1, duration: (short)args[1]);
        return args;
    }
}
