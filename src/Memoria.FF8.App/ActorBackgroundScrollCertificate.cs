using System.Security.Cryptography;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal static class ActorBackgroundScrollCertificate
{
    public static bool CanRestore(FieldAssets field, SceneEventState scene, IReadOnlyList<ActorState> actors)
    {
        if (scene.ActorBackgroundScroll is not NativeActorBackgroundScroll state || !NativeActorScrollPhase.IsValid(state) || state.Mode is 1 or 2 && (!InRange(state.X) || !InRange(state.Y) || !InRange(state.PreviousX) || !InRange(state.PreviousY) || !InRange(state.ProjectedTargetX) || !InRange(state.ProjectedTargetY)))
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
            return NativeActorScrollPhase.IsValid(actor) && actor.X.HasValue && actor.Y.HasValue && InRange(actor.X) && InRange(actor.Y);
        }
        if (scene.BackgroundScrollPositions.TryGetValue(0, out BackgroundScrollPosition? position))
        {
            return DirectBackgroundScrollCertificate.IsCompleted(position);
        }
        return scene.NormalBackgroundScrollCenter is { Version: 1 } normal && normal.FieldId == field.Id && normal.CameraIndex == scene.CameraIndex && normal.Frame > 0 && scene.FieldCallbackClock is { Version: 1 } clock && normal.Frame <= clock.OrdinaryCallbacks + clock.MovieCallbacks && InRange(normal.X) && InRange(normal.Y);
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
        if (!projected.Supported || projected.Center is not FieldNativeCameraCenter center || center.X is < -4095 or > 4096 || center.Y is < -4095 or > 4096)
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

    private static bool InRange(short? value)
    {
        return value is null or >= -4095 and <= 4096;
    }

    private static bool HasBaseline(FieldAssets field, SceneEventState scene)
    {
        return scene.NativeFieldInitialization is { Stage: NativeFieldInitializationStage.Completed } proof && proof.IsFrom(NativeFieldRuntimeProfile.FF8En, field.Id, Convert.ToHexString(SHA256.HashData(field.Jsm))) && scene.NativeFieldLoadVersion == 1 && scene.NativeActorLayoutVersion == 1 && NormalCameraCertificate.HasBaseline(field, scene.CameraIndex) && field.Information!.BackgroundWrapMask == 0 && scene.BackgroundScrollPositions.All(pair => pair.Key == 0 && DirectBackgroundScrollCertificate.IsCompleted(pair.Value)) && scene.BackgroundScrollLayers.All(pair => BackgroundScrollCertificate.CanSetNormal(field, pair.Key, pair.Value, scene.CameraIndex));
    }
}
