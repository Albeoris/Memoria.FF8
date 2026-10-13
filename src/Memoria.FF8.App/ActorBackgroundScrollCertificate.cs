using System.Security.Cryptography;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal static class ActorBackgroundScrollCertificate
{
    public static bool CanRestore(FieldAssets field, SceneEventState scene, IReadOnlyList<ActorState> actors)
    {
        return scene.ActorBackgroundScroll is NativeActorBackgroundScroll state && Resolve(field, scene, actors, state.NativeActorId) is not null;
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
        if (!HasBaseline(field, scene) || state.X is not short x || state.Y is not short y)
        {
            throw new NotSupportedException("Native actor-follow camera has no certified current endpoint; no leader or zero center is substituted.");
        }
        return DirectBackgroundScrollCertificate.PrepareCenter(field, scene, x, y);
    }

    private static bool HasBaseline(FieldAssets field, SceneEventState scene)
    {
        return scene.NativeFieldInitialization is { Stage: NativeFieldInitializationStage.Completed } proof && proof.IsFrom(NativeFieldRuntimeProfile.FF8En, field.Id, Convert.ToHexString(SHA256.HashData(field.Jsm))) && scene.NativeFieldLoadVersion == 1 && scene.NativeActorLayoutVersion == 1 && NormalCameraCertificate.HasBaseline(field, scene.CameraIndex) && field.Information!.BackgroundWrapMask == 0 && scene.BackgroundScrollPositions.All(pair => pair.Key == 0 && DirectBackgroundScrollCertificate.IsCompleted(pair.Value)) && scene.BackgroundScrollLayers.All(pair => BackgroundScrollCertificate.CanSetNormal(field, pair.Key, pair.Value, scene.CameraIndex));
    }
}
