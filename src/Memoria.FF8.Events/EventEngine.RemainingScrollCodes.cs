using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    public Func<int, byte, short, bool>? CanSetNativeLayerActorLinearBackgroundScroll { get; set; }
    public Func<NativeLinearScrollQuery, SceneEventState, bool>? CanSetExplicitCosineBackgroundScroll { get; set; }

    private string? UnsupportedRemainingLayerActorScroll(Fiber fiber, byte mode, bool party)
    {
        int[] top = fiber.Stack.ToArray();
        int selectorIndex = mode == 0 ? 0 : 1;
        int layerIndex = selectorIndex + 1;
        if (top.Length <= layerIndex || fiber.Stack.Capture().Take(layerIndex + 1).Any(value => !value.IsExact) || (uint)top[layerIndex] >= 8 || mode != 0 && (short)top[0] < 0 || program is null || NativeRuntimeProfile is not { IsVerified: true } || Scene.NativeActorLayoutVersion != 1 || Scene.NativeFieldInitialization is not { Stage: NativeFieldInitializationStage.Completed })
        {
            return "Layer actor scroll requires exact original operands, an allocated row, supported signed-WORD duration and completed native actor allocation";
        }
        int layer = top[layerIndex];
        int selector = top[selectorIndex];
        int nativeActor;
        if (party)
        {
            if ((uint)selector >= 3 || Scene.PartyBindingMemory is not { Version: 1 } binding)
            {
                return "Layer party scroll requires its retained saved-party BYTE selector";
            }
            nativeActor = binding.At(selector);
            if ((uint)nativeActor >= Scene.NativeActorObjectIds.Count || variables.TryGetValue(194 + selector, out int alias) && alias != nativeActor)
            {
                return "Layer party scroll saved-party binding disagrees with its allocated body or save194 alias";
            }
        }
        else
        {
            if ((uint)selector >= program.NativeObjects.Count)
            {
                return "Layer actor scroll requires its full DWORD raw-object selector";
            }
            nativeActor = Scene.NativeActorObjectIds.IndexOf(program.NativeObjects[selector]);
        }
        if (nativeActor is < 0 or >= 255 || Scene.ActorBackgroundScroll is { } independent && independent.Layer != layer || ResolveActorScrollCenter(layer, (byte)nativeActor) is null)
        {
            return "Layer actor scroll requires its retained allocated body, independent row owner and certified projection";
        }
        if (mode == 0)
        {
            return null;
        }
        short duration = (short)top[0];
        bool certified = mode == 1 ? CanSetNativeLayerActorLinearBackgroundScroll?.Invoke(layer, (byte)nativeActor, duration) == true : CanSetNativeLayerActorCosineBackgroundScroll?.Invoke(layer, (byte)nativeActor, duration) == true;
        return CanReplaceActorBackgroundScroll(layer) && certified ? null : "Layer actor interpolation requires authentic current words and a certified per-callback endpoint";
    }

    private int[] ExecuteRemainingLayerActorScroll(Fiber fiber, byte mode, bool party)
    {
        int[] args = Pop(fiber, mode == 0 ? 2 : 3);
        int selector = args[1];
        byte nativeActor = party ? Scene.PartyBindingMemory!.At(selector) : checked((byte)Scene.NativeActorObjectIds.IndexOf(program!.NativeObjects[selector]));
        int objectId = Scene.NativeActorObjectIds[nativeActor];
        int rawObjectId = party ? Enumerable.Range(0, program!.NativeObjects.Count).Single(index => program.NativeObjects[index] == objectId) : selector;
        // 521170/521270/5212D0/521350 preserve current/previous/counter,
        // sample the body selector once and leave the post-VM driver ownership intact.
        BeginNativeActorBackgroundScroll(rawObjectId, objectId, nativeActor, party ? selector : null, mode, mode == 0 ? (short)0 : (short)args[2], args[0]);
        if (mode == 0)
        {
            WaitFrames(fiber, 1);
        }
        return args;
    }

    private string? UnsupportedExplicitCosineBackgroundScroll(Fiber fiber)
    {
        int[] top = fiber.Stack.ToArray();
        if (top.Length < 6 || fiber.Stack.Capture().Take(6).Any(value => !value.IsExact) || (uint)top[5] >= 8 || (short)top[0] < 0)
        {
            return "CSCROLL3 requires six original operands, an allocated row and a supported signed-WORD duration";
        }
        NativeLinearScrollQuery query = new(top[5], (short)top[4], (short)top[3], (short)top[2], (short)top[1], (short)top[0]);
        return CanReplaceActorBackgroundScroll(query.Layer) && CanSetExplicitCosineBackgroundScroll?.Invoke(query, Scene) == true ? null : "CSCROLL3 requires certified explicit start and target placement before mutation";
    }

    private int[] ExecuteExplicitCosineBackgroundScroll(Fiber fiber)
    {
        int[] args = Pop(fiber, 6);
        BackgroundScrollPosition previous = TakeBackgroundScrollPosition(args[0]);
        // 5215F0 writes explicit current8/10, target16/18, duration4 and mode5/status0.
        // Previous12/14 and counter6 remain until475DF0 snapshots/resets them.
        Scene.BackgroundScrollPositions[args[0]] = previous with { X = (short)args[1], Y = (short)args[2], TargetX = (short)args[3], TargetY = (short)args[4], Duration = (short)args[5], Mode = 5, Status = 0, NativeLinearVersion = 0 };
        if (args[0] > 0 && Scene.NativeBackgroundScrollCurrents is { } currents)
        {
            currents[args[0]] = new(1, (short)args[1], (short)args[2]);
        }
        return args;
    }

    private void ExecuteNativeAxisSync(Fiber fiber)
    {
        // 5261A0 compares the retained elapsed and duration WORDs without consuming operands.
        if (axisElapsed != axisDuration)
        {
            fiber.Position--;
            WaitFrames(fiber, 1);
        }
    }
}
