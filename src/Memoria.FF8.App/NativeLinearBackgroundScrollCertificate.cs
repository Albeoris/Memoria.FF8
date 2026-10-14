using System.Collections.Immutable;
using System.Security.Cryptography;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal static class NativeLinearBackgroundScrollCertificate
{
    public static bool CanSet(FieldAssets field, NativeLinearScrollQuery query)
    {
        return FieldNativeNormalBackgroundWrap.SupportsLinearMetadata(field, 0) && query.Layer is >= 1 and <= 7 && field.Background.Any(tile => tile.Layer == query.Layer) && query.Duration >= 0 && InRange(query.StartX) && InRange(query.StartY) && InRange(query.TargetX) && InRange(query.TargetY);
    }

    public static bool CanBegin(FieldAssets field, NativeLinearScrollQuery query, SceneEventState scene)
    {
        return CanSet(field, query) && HasNormalRuntime(field, scene) && ValidPositions(field, scene);
    }

    public static bool SupportsPhase(FieldAssets field, SceneEventState scene)
    {
        return scene.BackgroundScrollPositions.Count > 0 && HasNormalRuntime(field, scene) && ValidPositions(field, scene);
    }

    public static BackgroundScrollFrame? Prepare(FieldAssets field, SceneEventState scene, IReadOnlyList<ActorState> actors)
    {
        return SupportsPhase(field, scene) ? NormalCameraCertificate.Prepare(field, scene, actors) : null;
    }

    public static FieldNativeBackgroundWrapFrame PrepareWrap(FieldAssets field, SceneEventState scene, FieldNativeCameraCenter center)
    {
        ImmutableArray<FieldNativeBackgroundLayerCurrent> currents = scene.BackgroundScrollPositions.OrderBy(pair => pair.Key).Select(pair => new FieldNativeBackgroundLayerCurrent((byte)pair.Key, pair.Value.X!.Value, pair.Value.Y!.Value)).ToImmutableArray();
        return FieldNativeNormalBackgroundWrap.Prepare(field, scene.CameraIndex, center, currents) ?? throw new NotSupportedException("Native linear layer placement has unsupported signed-word offsets or camera bounds.");
    }

    private static bool HasNormalRuntime(FieldAssets field, SceneEventState scene)
    {
        return scene.NativeFieldInitialization is { Stage: NativeFieldInitializationStage.Completed } proof && proof.IsFrom(NativeFieldRuntimeProfile.FF8En, field.Id, Convert.ToHexString(SHA256.HashData(field.Jsm))) && scene.NativeFieldLoadVersion == 1 && scene.NativeActorLayoutVersion == 1 && scene.BackgroundScrollLayers.Count == 0 && FieldNativeNormalBackgroundWrap.SupportsLinearMetadata(field, scene.CameraIndex);
    }

    private static bool ValidPositions(FieldAssets field, SceneEventState scene)
    {
        return scene.BackgroundScrollPositions.All(pair => NativeLinearScrollPhase.IsValid(pair.Value) && InRange(pair.Value.X) && InRange(pair.Value.Y) && (pair.Value.Status == 0 ? pair.Value.X : pair.Value.PreviousX) is short startX && (pair.Value.Status == 0 ? pair.Value.Y : pair.Value.PreviousY) is short startY && pair.Value.Duration is short duration && CanSet(field, new NativeLinearScrollQuery(pair.Key, startX, startY, pair.Value.TargetX, pair.Value.TargetY, duration)));
    }

    private static bool InRange(short? value)
    {
        return value is >= -4095 and <= 4096;
    }
}
