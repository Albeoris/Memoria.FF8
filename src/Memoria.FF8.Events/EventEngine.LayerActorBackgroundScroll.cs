using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    public Func<int, byte, short, bool>? CanSetNativeLayerActorCosineBackgroundScroll { get; set; }

    private string? UnsupportedLayerActorBackgroundScroll(Fiber fiber, bool cosine)
    {
        int[] top = fiber.Stack.ToArray();
        int actorIndex = cosine ? 1 : 0;
        int layerIndex = actorIndex + 1;
        if (top.Length <= layerIndex || (uint)top[layerIndex] >= 8 || cosine && (short)top[0] < 0 || program is null || (uint)top[actorIndex] >= program.NativeObjects.Count || NativeRuntimeProfile is not { IsVerified: true } || Scene.NativeActorLayoutVersion != 1 || Scene.NativeFieldInitialization is not { Stage: NativeFieldInitializationStage.Completed })
        {
            return "Layer actor scroll requires full DWORD layer/raw actor operands, supported signed-WORD duration and completed native allocation";
        }
        int layer = top[layerIndex];
        int objectId = program.NativeObjects[top[actorIndex]];
        int nativeActor = Scene.NativeActorObjectIds.IndexOf(objectId);
        if (Scene.ActorBackgroundScroll is { } independent && independent.Layer != layer)
        {
            return $"Layer actor scroll row{layer} cannot replace independent explicit actor row{independent.Layer}";
        }
        if (nativeActor is < 0 or >= 255 || ResolveActorScrollCenter(layer, (byte)nativeActor) is null || cosine && (!CanReplaceActorBackgroundScroll(layer) || CanSetNativeLayerActorCosineBackgroundScroll?.Invoke(layer, (byte)nativeActor, (short)top[0]) != true))
        {
            return $"Layer actor scroll requires retained actor{top[actorIndex]}, certified projection and authentic row{layer} current (selected camera{Scene.CameraIndex})";
        }
        return null;
    }

    private int[] ExecuteLayerActorBackgroundScroll(Fiber fiber, bool cosine)
    {
        int[] args = Pop(fiber, cosine ? 3 : 2);
        int objectId = program!.NativeObjects[args[1]];
        byte nativeActor = checked((byte)Scene.NativeActorObjectIds.IndexOf(objectId));
        //521110/5211F0 write only selector/mode/status and, for mode2, duration.
        //475DF0 establishes the previous current and counter after the VM budget.
        BeginNativeActorBackgroundScroll(args[1], objectId, nativeActor, mode: cosine ? (byte)2 : (byte)0, duration: cosine ? (short)args[2] : (short)0, layer: args[0]);
        if (!cosine)
        {
            WaitFrames(fiber, 1);
        }
        return args;
    }
}
