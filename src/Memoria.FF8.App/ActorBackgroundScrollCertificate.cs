using Memoria.FF8.Assets;
using Memoria.FF8.Events;

namespace Memoria.FF8;

internal static class ActorBackgroundScrollCertificate
{
    public static bool CanRestore(FieldAssets field, SceneEventState scene, IReadOnlyList<ActorState> actors)
    {
        if (scene.ActorBackgroundScroll is not NativeActorBackgroundScroll state || !NativeActorScrollPhase.IsValid(state) || !SupportsOptionalCenter(field, scene, state.X, state.Y) || state.Mode is 1 or 2 && (!SupportsOptionalCenter(field, scene, state.PreviousX, state.PreviousY) || !SupportsOptionalCenter(field, scene, state.ProjectedTargetX, state.ProjectedTargetY)))
        {
            return false;
        }
        if (state.Mode is 1 or 2 && state.Status == 2)
        {
            return HasBaseline(field, scene);
        }
        (short X, short Y)? endpoint = Resolve(field, scene, actors, state.NativeActorId);
        return endpoint is { } target && (state.Mode == 0 || state.Status == 0 || state.ProjectedTargetX == target.X && state.ProjectedTargetY == target.Y);
    }

    public static bool CanSetInterpolated(FieldAssets field, SceneEventState scene, IReadOnlyList<ActorState> actors, byte nativeActor, short duration)
    {
        if (duration < 0 || Resolve(field, scene, actors, nativeActor) is null)
        {
            return false;
        }
        if (scene.ActorBackgroundScroll is NativeActorBackgroundScroll actor)
        {
            return NativeActorScrollPhase.IsValid(actor) && actor.X is short x && actor.Y is short y && DirectBackgroundScrollCertificate.SupportsCenter(field, scene, x, y);
        }
        if (scene.BackgroundScrollPositions.TryGetValue(0, out BackgroundScrollPosition? position))
        {
            return DirectBackgroundScrollCertificate.IsCompleted(position) && position.X is short x && position.Y is short y && DirectBackgroundScrollCertificate.SupportsCenter(field, scene, x, y);
        }
        return scene.NormalBackgroundScrollCenter is { Version: 1 } normal && normal.FieldId == field.Id && normal.CameraIndex == scene.CameraIndex && normal.Frame > 0 && scene.FieldCallbackClock is { Version: 1 } clock && normal.Frame <= clock.OrdinaryCallbacks + clock.MovieCallbacks && DirectBackgroundScrollCertificate.SupportsCenter(field, scene, normal.X, normal.Y);
    }

    public static (short X, short Y)? Resolve(FieldAssets field, SceneEventState scene, IReadOnlyList<ActorState> actors, byte nativeActor)
    {
        if (!HasBaseline(field, scene) || nativeActor >= scene.NativeActorObjectIds.Count)
        {
            return null;
        }
        ActorState? actor = actors.FirstOrDefault(item => item.ObjectId == scene.NativeActorObjectIds[nativeActor]);
        if (actor is null || !actor.Active || (uint)actor.ModelId >= field.Models.Count)
        {
            return null;
        }
        FieldNativeCameraResult projected = NormalCameraCertificate.ProjectActorCenter(field, scene, actor);
        if (!projected.Supported || projected.Center is not FieldNativeCameraCenter center || center.X is < -4095 or > 4096 || center.Y is < -4095 or > 4096 || !DirectBackgroundScrollCertificate.SupportsCenter(field, scene, (short)center.X, (short)center.Y))
        {
            return null;
        }
        return ((short)center.X, (short)center.Y);
    }

    public static BackgroundScrollFrame? Prepare(FieldAssets field, SceneEventState scene)
    {
        if (scene.ActorBackgroundScroll is not NativeActorBackgroundScroll state)
        {
            return null;
        }
        if (!HasBaseline(field, scene) || !NativeActorScrollPhase.IsValid(state) || state.X is not short x || state.Y is not short y)
        {
            throw new NotSupportedException("Native actor-follow camera has no certified current endpoint; no leader or zero center is substituted.");
        }
        return DirectBackgroundScrollCertificate.PrepareCenter(field, scene, x, y);
    }

    private static bool SupportsOptionalCenter(FieldAssets field, SceneEventState scene, short? x, short? y)
    {
        return x is null && y is null || x is short currentX && y is short currentY && DirectBackgroundScrollCertificate.SupportsCenter(field, scene, currentX, currentY);
    }

    private static bool HasBaseline(FieldAssets field, SceneEventState scene)
    {
        return DirectBackgroundScrollCertificate.HasBaseline(field, scene) && scene.NativeActorLayoutVersion == 1 && scene.BackgroundScrollPositions.All(pair => pair.Key == 0 && DirectBackgroundScrollCertificate.IsCompleted(pair.Value));
    }
}
