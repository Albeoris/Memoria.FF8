using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public static class NativeActorCosineScrollPhase
{
    public static bool IsValid(NativeActorBackgroundScroll state)
    {
        if (state.X.HasValue != state.Y.HasValue || state.PreviousX.HasValue != state.PreviousY.HasValue || state.ProjectedTargetX.HasValue != state.ProjectedTargetY.HasValue)
        {
            return false;
        }
        if (state.Mode == 0)
        {
            return state.Status is 0 or 1 && (state.Status == 0 || state.X.HasValue) && state.Duration == 0 && state.Counter == 0 && state.ProjectedTargetX is null;
        }
        if (state.Mode != 2 || state.PartySlot is not null || state.Duration < 0 || state.Status is not (0 or 1 or 2) || state.X is not short x || state.Y is not short y)
        {
            return false;
        }
        if (state.Status == 0)
        {
            return state.ProjectedTargetX is null;
        }
        if (state.PreviousX is not short previousX || state.PreviousY is not short previousY || state.ProjectedTargetX is not short targetX || state.ProjectedTargetY is not short targetY || state.Counter < 0 || state.Counter > state.Duration || state.Status == 1 && state.Counter == 0 || state.Status == 2 && state.Counter != state.Duration)
        {
            return false;
        }
        int elapsed = state.Status == 2 ? state.Counter : state.Counter - 1;
        return x == NativeCosineInterpolation.Interpolate(previousX, targetX, state.Duration, elapsed) && y == NativeCosineInterpolation.Interpolate(previousY, targetY, state.Duration, elapsed);
    }
}

public sealed partial class EventEngine
{
    public Func<byte, short, bool>? CanSetNativeActorCosineBackgroundScroll { get; set; }

    private string? UnsupportedNativeActorCosineBackgroundScroll(Fiber fiber)
    {
        int[] top = fiber.Stack.ToArray();
        if (top.Length < 2 || (short)top[0] < 0 || program is null || (uint)top[1] >= program.NativeObjects.Count || NativeRuntimeProfile is not { IsVerified: true } || Scene.NativeActorLayoutVersion != 1 || Scene.NativeFieldInitialization is not { Stage: NativeFieldInitializationStage.Completed })
        {
            return "CSCROLLA requires its original raw actor target, signed-WORD duration and completed native allocation";
        }
        int objectId = program.NativeObjects[top[1]];
        int nativeActor = Scene.NativeActorObjectIds.IndexOf(objectId);
        if (nativeActor is < 0 or >= 255 || !CanReplaceActorBackgroundScroll(0) || CanSetNativeActorCosineBackgroundScroll?.Invoke((byte)nativeActor, (short)top[0]) != true)
        {
            return "CSCROLLA requires a retained target, authentic previous center and certified per-callback projection";
        }
        return null;
    }

    private int[] ExecuteNativeActorCosineBackgroundScroll(Fiber fiber)
    {
        int[] args = Pop(fiber, 2);
        int objectId = program!.NativeObjects[args[0]];
        byte nativeActor = checked((byte)Scene.NativeActorObjectIds.IndexOf(objectId));
        // 520DF0 preserves current/previous/counter;475DF0 snapshots current
        // and resets counter after all scripts. Return2 keeps the VM budget.
        BeginNativeActorBackgroundScroll(args[0], objectId, nativeActor, mode: 2, duration: (short)args[1]);
        return args;
    }

    private void StepNativeActorCosineBackgroundScroll(NativeActorBackgroundScroll state)
    {
        if (!NativeActorCosineScrollPhase.IsValid(state))
        {
            throw new NotSupportedException("Native actor cosine camera has an unknown current center or interpolation phase.");
        }
        if (state.Status == 2)
        {
            return;
        }
        if (state.Status == 0)
        {
            state = state with { PreviousX = state.X, PreviousY = state.Y, Counter = 0, Status = 1 };
        }
        (short X, short Y) target = ResolveNativeActorScrollCenter?.Invoke(state.NativeActorId) ?? throw new NotSupportedException("Native actor cosine camera lost its selected actor projection; no leader or static target is substituted.");
        short x = (short)NativeCosineInterpolation.Interpolate(state.PreviousX!.Value, target.X, state.Duration, state.Counter);
        short y = (short)NativeCosineInterpolation.Interpolate(state.PreviousY!.Value, target.Y, state.Duration, state.Counter);
        // 475E60 mode2 reprojects the selected body on every active callback.
        Scene.ActorBackgroundScroll = state with { X = x, Y = y, ProjectedTargetX = target.X, ProjectedTargetY = target.Y, Counter = state.Counter == state.Duration ? state.Counter : (short)(state.Counter + 1), Status = state.Counter == state.Duration ? (byte)2 : (byte)1 };
    }
}
