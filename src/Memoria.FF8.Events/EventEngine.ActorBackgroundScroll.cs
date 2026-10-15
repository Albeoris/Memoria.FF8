using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

/// <summary>Layer0 continuous/linear/cosine actor camera; unknown old center words remain unknown until native callbacks establish them.</summary>
public sealed record NativeActorBackgroundScroll(int Version, int NativeObjectId, int ObjectId, byte NativeActorId, byte Status, short? X, short? Y, short? PreviousX, short? PreviousY, [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] int? PartySlot = null, [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)] byte Mode = 0, [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)] short Duration = 0, [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)] short Counter = 0, [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] short? ProjectedTargetX = null, [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] short? ProjectedTargetY = null);

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
            Scene.ActorBackgroundScroll = null;
            // 475DF0 overwrites the previous words/counter before the new mode consumes them.
            return new BackgroundScrollPosition(actor.X, actor.Y, 0, 0, actor.PreviousX, actor.PreviousY, actor.Counter, 0, actor.Status, actor.Duration, actor.NativeActorId);
        }
        if (layer == 0 && !Scene.BackgroundScrollPositions.ContainsKey(0) && Scene.NormalBackgroundScrollCenter is NativeNormalBackgroundScrollCenter normal && normal.Version == 1 && normal.FieldId == FieldId && normal.CameraIndex == Scene.CameraIndex && normal.Frame <= Frame)
        {
            return new BackgroundScrollPosition(normal.X, normal.Y, normal.X, normal.Y, normal.X, normal.Y, 0, 0, 1);
        }
        if (Scene.BackgroundScrollPositions.TryGetValue(layer, out BackgroundScrollPosition? current))
        {
            return current;
        }
        if (KnownNativeLinearScrollStart(layer) is { } known)
        {
            // 475E60 has actually established this selected/nonselected mode0 center.
            return new BackgroundScrollPosition(known.X, known.Y, known.X, known.Y, known.X, known.Y, 0, 0, 1);
        }
        return new BackgroundScrollPosition(null, null, 0, 0, null, null, null, 0, 1);
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
        BeginNativeActorBackgroundScroll(args[0], objectId, nativeActor);
        // 520D50 writes mode0/status0 and selector+598, then returns3.
        WaitFrames(fiber, 1);
        return args;
    }

    private void BeginNativeActorBackgroundScroll(int rawObjectId, int objectId, byte nativeActor, int? partySlot = null, byte mode = 0, short duration = 0)
    {
        short? x = Scene.ActorBackgroundScroll?.X;
        short? y = Scene.ActorBackgroundScroll?.Y;
        short counter = Scene.ActorBackgroundScroll?.Counter ?? 0;
        if (Scene.BackgroundScrollPositions.TryGetValue(0, out BackgroundScrollPosition? prior))
        {
            x = prior.X;
            y = prior.Y;
            if (prior.Counter is short previousCounter)
            {
                counter = previousCounter;
            }
            Scene.BackgroundScrollPositions.Remove(0);
        }
        else if (Scene.NormalBackgroundScrollCenter is { Version: 1 } normal && normal.FieldId == FieldId && normal.CameraIndex == Scene.CameraIndex)
        {
            x = normal.X;
            y = normal.Y;
        }
        else if (Scene.NativeLayerZeroScrollCurrent is { Version: 1 } process)
        {
            x = process.X;
            y = process.Y;
        }
        Scene.ActorBackgroundScroll = new NativeActorBackgroundScroll(1, rawObjectId, objectId, nativeActor, 0, x, y, Scene.ActorBackgroundScroll?.PreviousX, Scene.ActorBackgroundScroll?.PreviousY, partySlot, mode, duration, mode is 1 or 2 ? counter : (short)0);
        Scene.NormalBackgroundScrollCenter = null;
    }

    private void StepNativeActorBackgroundScroll()
    {
        if (Scene.ActorBackgroundScroll is not NativeActorBackgroundScroll state)
        {
            return;
        }
        if (state.Mode is 1 or 2)
        {
            StepNativeActorInterpolatedBackgroundScroll(state);
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
        if (state.Version != 1 || state.PartySlot is < 0 or > 2 || (uint)state.NativeObjectId >= program!.NativeObjects.Count || program.NativeObjects[state.NativeObjectId] != state.ObjectId || state.NativeActorId >= saved.Scene.NativeActorObjectIds.Count || saved.Scene.NativeActorObjectIds[state.NativeActorId] != state.ObjectId || !NativeActorScrollPhase.IsValid(state) || saved.Scene.BackgroundScrollPositions.ContainsKey(0) || saved.Scene.NativeActorLayoutVersion != 1 || saved.Scene.NativeFieldInitialization is not { Stage: NativeFieldInitializationStage.Completed } proof || !proof.IsFrom(NativeRuntimeProfile, saved.FieldId, saved.ProgramHash) || ResolveNativeActorScrollCenter is null || CanRestoreNativeActorScroll?.Invoke(saved.Scene, saved.Actors) != true)
        {
            throw new InvalidDataException("Saved actor camera lost its raw/retained actor binding, phase or projection authority.");
        }
    }
}
