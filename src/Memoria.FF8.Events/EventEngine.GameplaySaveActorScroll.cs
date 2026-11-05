using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private GameplaySaveFieldActorScroll? CaptureGameplaySaveActorScroll()
    {
        if (Scene.ActorBackgroundScroll is not { } state)
        {
            return null;
        }
        if (state.Version != 1 || (uint)state.NativeObjectId >= program!.NativeObjects.Count || program.NativeObjects[state.NativeObjectId] != state.ObjectId || state.NativeActorId >= Scene.NativeActorObjectIds.Count || Scene.NativeActorObjectIds[state.NativeActorId] != state.ObjectId || !NativeActorScrollPhase.IsValid(state) || Scene.BackgroundScrollPositions.ContainsKey(state.Layer) || !extractingLegacyGameplay && CanRestoreNativeActorScroll?.Invoke(Scene, actors) != true)
        {
            throw new InvalidDataException("Gameplay actor-camera capture requires its current retained target and proved projection phase.");
        }
        GameplaySaveFieldActorScroll result = new(state.ObjectId, state.PartySlot, state.Layer, state.Mode, state.Status, state.Duration, state.Counter, state.X, state.Y, state.PreviousX, state.PreviousY, state.ProjectedTargetX, state.ProjectedTargetY);
        result.Validate();
        return result;
    }

    private void RestoreGameplaySaveActorScroll(GameplaySaveFieldActorScroll? saved)
    {
        if (saved is null)
        {
            Scene.ActorBackgroundScroll = null;
            return;
        }
        saved.Validate();
        int nativeObject = -1;
        for (int index = 0; index < program!.NativeObjects.Count; index++)
        {
            if (program.NativeObjects[index] == saved.ObjectId)
            {
                nativeObject = index;
                break;
            }
        }
        int actor = Scene.NativeActorObjectIds.IndexOf(saved.ObjectId);
        if (nativeObject < 0 || actor is < 0 or >= 255 || Scene.NativeActorLayoutVersion != 1 || Scene.BackgroundScrollPositions.ContainsKey(saved.Layer))
        {
            throw new InvalidDataException("Gameplay actor-camera target is absent from the current retained layout or conflicts with a layer owner.");
        }
        NativeActorBackgroundScroll state = new(1, nativeObject, saved.ObjectId, (byte)actor, saved.Status, saved.X, saved.Y, saved.PreviousX, saved.PreviousY, saved.PartySlot, saved.Mode, saved.Duration, saved.Counter, saved.ProjectedTargetX, saved.ProjectedTargetY, saved.Layer);
        if (!NativeActorScrollPhase.IsValid(state))
        {
            throw new InvalidDataException("Gameplay actor-camera has an invalid saved interpolation phase.");
        }
        if (state.Status != 0 && state.Status != 2)
        {
            (short X, short Y) target = ResolveActorScrollCenter(state.Layer, state.NativeActorId) ?? throw new NotSupportedException("Gameplay actor-camera requires the current installed target projection.");
            if (state.Mode == 0)
            {
                state = state with { X = target.X, Y = target.Y };
            }
            else
            {
                short elapsed = checked((short)(state.Counter - 1));
                state = state with { X = NativeActorScrollPhase.Interpolate(state.Mode, state.PreviousX!.Value, target.X, state.Duration, elapsed), Y = NativeActorScrollPhase.Interpolate(state.Mode, state.PreviousY!.Value, target.Y, state.Duration, elapsed), ProjectedTargetX = target.X, ProjectedTargetY = target.Y };
            }
        }
        // A completed row holds its endpoint even if its original target has moved.
        Scene.ActorBackgroundScroll = state;
        if (CanRestoreNativeActorScroll?.Invoke(Scene, actors) != true)
        {
            throw new InvalidDataException("Gameplay actor-camera phase is unsupported by the current installed projection contract.");
        }
        if (state.Layer == 0)
        {
            PublishNativeLayerZeroScrollCurrent();
        }
        Scene.NativeBackgroundScrollCurrents = CaptureNativeBackgroundScrollCurrents(Scene, false);
    }
}
