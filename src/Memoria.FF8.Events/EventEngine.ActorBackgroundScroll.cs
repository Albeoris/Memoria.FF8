using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

/// <summary>Layer0 continuous actor follow; unknown old center words remain unknown until native post-VM projection.</summary>
public sealed record NativeActorBackgroundScroll(int Version, int NativeObjectId, int ObjectId, byte NativeActorId, byte Status, short? X, short? Y, short? PreviousX, short? PreviousY);

public sealed partial class EventEngine
{
    public Func<byte, (short X, short Y)?>? ResolveNativeActorScrollCenter { get; set; }
    public Func<SceneEventState, IReadOnlyList<ActorState>, bool>? CanRestoreNativeActorScroll { get; set; }

    private bool CanReplaceActorBackgroundScroll(int layer)
    {
        return layer != 0 || Scene.ActorBackgroundScroll is null || Scene.ActorBackgroundScroll is { X: not null, Y: not null };
    }

    private BackgroundScrollPosition TakeBackgroundScrollPosition(int layer)
    {
        if (layer == 0 && Scene.ActorBackgroundScroll is NativeActorBackgroundScroll actor)
        {
            if (actor.X is not short x || actor.Y is not short y)
            {
                throw new NotSupportedException("A replacement cannot infer unknown native actor-scroll center words.");
            }
            Scene.ActorBackgroundScroll = null;
            // 475DF0 overwrites the previous words/counter before the new mode consumes them.
            return new BackgroundScrollPosition(x, y, x, y, x, y, 0, 0, actor.Status);
        }
        return Scene.BackgroundScrollPositions.GetValueOrDefault(layer) ?? new BackgroundScrollPosition(0, 0, 0, 0, 0, 0, 0, 0, 1);
    }

    private byte? BackgroundScrollStatus(int layer)
    {
        return layer == 0 && Scene.ActorBackgroundScroll is NativeActorBackgroundScroll actor ? actor.Status : Scene.BackgroundScrollPositions.GetValueOrDefault(layer)?.Status;
    }

    private string? UnsupportedActorBackgroundScroll(Fiber fiber)
    {
        if (fiber.Stack.Count == 0 || program is null || (uint)fiber.Stack.Peek() >= program.NativeObjects.Count || NativeRuntimeProfile is not { IsVerified: true } || Scene.NativeActorLayoutVersion != 1 || Scene.NativeFieldInitialization is not { Stage: NativeFieldInitializationStage.Completed })
        {
            return "DSCROLLA requires its original raw object-table operand and completed native actor allocation";
        }
        int objectId = program.NativeObjects[fiber.Stack.Peek()];
        int nativeActor = Scene.NativeActorObjectIds.IndexOf(objectId);
        if (nativeActor is < 0 or >= 255 || ResolveNativeActorScrollCenter?.Invoke((byte)nativeActor) is null)
        {
            return "DSCROLLA requires a retained source actor and its certified projection/clamp endpoint";
        }
        return null;
    }

    private int[] ExecuteActorBackgroundScroll(Fiber fiber)
    {
        int[] args = Pop(fiber, 1);
        int objectId = program!.NativeObjects[args[0]];
        byte nativeActor = checked((byte)Scene.NativeActorObjectIds.IndexOf(objectId));
        short? x = Scene.ActorBackgroundScroll?.X;
        short? y = Scene.ActorBackgroundScroll?.Y;
        if (Scene.BackgroundScrollPositions.TryGetValue(0, out BackgroundScrollPosition? prior))
        {
            x = prior.X;
            y = prior.Y;
            Scene.BackgroundScrollPositions.Remove(0);
        }
        Scene.ActorBackgroundScroll = new NativeActorBackgroundScroll(1, args[0], objectId, nativeActor, 0, x, y, Scene.ActorBackgroundScroll?.PreviousX, Scene.ActorBackgroundScroll?.PreviousY);
        // 520D50 writes mode0/status0 and selector+598, then returns3.
        WaitFrames(fiber, 1);
        return args;
    }

    private void StepNativeActorBackgroundScroll()
    {
        if (Scene.ActorBackgroundScroll is not NativeActorBackgroundScroll state)
        {
            return;
        }
        (short X, short Y) center = ResolveNativeActorScrollCenter?.Invoke(state.NativeActorId) ?? throw new NotSupportedException("Continuous native actor camera projection lost its certified endpoint; no guessed center is committed.");
        // 475DF0 captures the prior center once;475E60 mode0 remains status1 and follows every callback.
        Scene.ActorBackgroundScroll = state with { Status = 1, X = center.X, Y = center.Y, PreviousX = state.Status == 0 ? state.X : state.PreviousX, PreviousY = state.Status == 0 ? state.Y : state.PreviousY };
    }

    private void ValidateActorBackgroundScrollCheckpoint(Checkpoint saved)
    {
        if (saved.Scene.ActorBackgroundScroll is not NativeActorBackgroundScroll state)
        {
            return;
        }
        if (state.Version != 1 || (uint)state.NativeObjectId >= program!.NativeObjects.Count || program.NativeObjects[state.NativeObjectId] != state.ObjectId || state.NativeActorId >= saved.Scene.NativeActorObjectIds.Count || saved.Scene.NativeActorObjectIds[state.NativeActorId] != state.ObjectId || state.Status is not (0 or 1) || state.X.HasValue != state.Y.HasValue || state.PreviousX.HasValue != state.PreviousY.HasValue || state.Status == 1 && !state.X.HasValue || saved.Scene.BackgroundScrollPositions.ContainsKey(0) || saved.Scene.NativeActorLayoutVersion != 1 || saved.Scene.NativeFieldInitialization is not { Stage: NativeFieldInitializationStage.Completed } proof || !proof.IsFrom(NativeRuntimeProfile, saved.FieldId, saved.ProgramHash) || ResolveNativeActorScrollCenter is null || CanRestoreNativeActorScroll?.Invoke(saved.Scene, saved.Actors) != true)
        {
            throw new InvalidDataException("Saved continuous actor scroll lost its raw/retained actor binding, phase or projection authority.");
        }
    }
}
