using System.Collections.Immutable;
using System.Security.Cryptography;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal static class NativeWrappedBackgroundScrollCertificate
{
    public static bool CanSetDirect(FieldAssets field, SceneEventState scene, int layer, short x, short y)
    {
        return HasNormalRuntime(field, scene) && layer > scene.CameraIndex && layer <= 7 && field.Background.Any(tile => tile.Layer == layer) && InRange(x) && InRange(y) && ValidPositions(field, scene) && BackgroundParallaxPlacement.SupportsCameraRange(field, BackgroundScrollCertificate.ReadSettings(scene, layer), x, y, scene.CameraIndex);
    }

    public static bool CanSetSettings(FieldAssets field, SceneEventState scene, int layer, BackgroundScrollSettings settings)
    {
        return HasNormalRuntime(field, scene) && BackgroundScrollCertificate.CanSetNormal(field, layer, settings, scene.CameraIndex) && ValidPositions(field, scene, layer, settings);
    }

    public static bool CanSet(FieldAssets field, NativeLinearScrollQuery query, int cameraIndex)
    {
        return FieldNativeNormalBackgroundWrap.SupportsLayerCurrentMetadata(field, cameraIndex) && query.Layer > cameraIndex && query.Layer <= 7 && field.Background.Any(tile => tile.Layer == query.Layer) && query.Duration >= 0 && InRange(query.StartX) && InRange(query.StartY) && InRange(query.TargetX) && InRange(query.TargetY);
    }

    public static bool CanBegin(FieldAssets field, NativeLinearScrollQuery query, SceneEventState scene)
    {
        return CanSet(field, query, scene.CameraIndex) && HasNormalRuntime(field, scene) && ValidPositions(field, scene) && FitsSettings(field, BackgroundScrollCertificate.ReadSettings(scene, query.Layer), query.StartX, query.StartY, scene.CameraIndex) && FitsSettings(field, BackgroundScrollCertificate.ReadSettings(scene, query.Layer), query.TargetX, query.TargetY, scene.CameraIndex);
    }

    public static bool CanSetCosine(FieldAssets field, SceneEventState scene, int layer, short x, short y, short duration)
    {
        if (duration < 0 || !CanSetDirect(field, scene, layer, x, y))
        {
            return false;
        }
        BackgroundScrollSettings settings = BackgroundScrollCertificate.ReadSettings(scene, layer);
        return scene.BackgroundScrollPositions.TryGetValue(layer, out BackgroundScrollPosition? current) ? current.X is short startX && current.Y is short startY && FitsSettings(field, settings, startX, startY, scene.CameraIndex) : FitsSettings(field, settings, 0, 0, scene.CameraIndex);
    }

    public static bool SupportsPhase(FieldAssets field, SceneEventState scene)
    {
        return !scene.BackgroundScrollPositions.ContainsKey(0) && (scene.BackgroundScrollPositions.Count > 0 || !BackgroundScrollCertificate.HasDefaultSettings(scene)) && HasNormalRuntime(field, scene) && ValidPositions(field, scene) && scene.BackgroundScrollPositions.Values.All(position => InRange(position.X) && InRange(position.Y));
    }

    public static bool SupportsLinearPhase(FieldAssets field, SceneEventState scene)
    {
        if (!scene.BackgroundScrollPositions.Values.Any(position => position.Mode == 4) || !HasNormalRuntime(field, scene) || !ValidPositions(field, scene))
        {
            return false;
        }
        if (!scene.BackgroundScrollPositions.TryGetValue(0, out BackgroundScrollPosition? selected))
        {
            return true;
        }
        if (!DirectBackgroundScrollCertificate.SupportsCenter(field, scene, selected.TargetX, selected.TargetY))
        {
            return false;
        }
        // Pending direct mode3 commits its target before drawing; retained
        // current/previous words are not inferred when that issue overwrites them.
        if (selected.Mode == 3 && selected.Status == 0)
        {
            return true;
        }
        return selected.X is short x && selected.Y is short y && DirectBackgroundScrollCertificate.SupportsCenter(field, scene, x, y) && (selected.Status == 0 || selected.Mode == 3 || selected.PreviousX is short previousX && selected.PreviousY is short previousY && DirectBackgroundScrollCertificate.SupportsCenter(field, scene, previousX, previousY));
    }

    public static BackgroundScrollFrame? Prepare(FieldAssets field, SceneEventState scene, IReadOnlyList<ActorState> actors)
    {
        return SupportsPhase(field, scene) ? NormalCameraCertificate.Prepare(field, scene, actors) : null;
    }

    public static FieldNativeBackgroundWrapFrame PrepareWrap(FieldAssets field, SceneEventState scene, FieldNativeCameraCenter center)
    {
        return TryPrepareWrap(field, scene, center) ?? throw new NotSupportedException("Native wrapped layer placement has unsupported signed-word offsets or camera bounds.");
    }

    public static FieldNativeBackgroundWrapFrame? TryPrepareWrap(FieldAssets field, SceneEventState scene, FieldNativeCameraCenter center)
    {
        ImmutableArray<FieldNativeBackgroundLayerCurrent>.Builder currents = ImmutableArray.CreateBuilder<FieldNativeBackgroundLayerCurrent>();
        foreach (int layer in field.Background.Select(tile => tile.Layer).Distinct().Where(layer => layer > scene.CameraIndex).Order())
        {
            BackgroundScrollSettings settings = BackgroundScrollCertificate.ReadSettings(scene, layer);
            if (scene.BackgroundScrollPositions.TryGetValue(layer, out BackgroundScrollPosition? position))
            {
                if (position.X is not short x || position.Y is not short y)
                {
                    return null;
                }
                currents.Add(new FieldNativeBackgroundLayerCurrent((byte)layer, x, y, settings.OffsetX, settings.OffsetY, settings.ScaleX, settings.ScaleY));
            }
            else if (settings != new BackgroundScrollSettings(0, 0, 256, 256))
            {
                // Existing normal475E60 mode0 producer writes zero current8/10, independently of retained ratio/offset words.
                currents.Add(new FieldNativeBackgroundLayerCurrent((byte)layer, 0, 0, settings.OffsetX, settings.OffsetY, settings.ScaleX, settings.ScaleY));
            }
        }
        return FieldNativeNormalBackgroundWrap.Prepare(field, scene.CameraIndex, center, currents.ToImmutable());
    }

    private static bool HasNormalRuntime(FieldAssets field, SceneEventState scene)
    {
        return scene.NativeFieldInitialization is { Stage: NativeFieldInitializationStage.Completed } proof && proof.IsFrom(NativeFieldRuntimeProfile.FF8En, field.Id, Convert.ToHexString(SHA256.HashData(field.Jsm))) && scene.NativeFieldLoadVersion == 1 && scene.NativeActorLayoutVersion == 1 && BackgroundScrollCertificate.HasSupportedSettings(field, scene) && (scene.CameraIndex == 0 || scene.ActorBackgroundScroll is null && !scene.BackgroundScrollPositions.ContainsKey(0) && !scene.BackgroundScrollPositions.ContainsKey(1)) && FieldNativeNormalBackgroundWrap.SupportsLayerCurrentMetadata(field, scene.CameraIndex);
    }

    private static bool ValidPositions(FieldAssets field, SceneEventState scene, int? changedLayer = null, BackgroundScrollSettings? changedSettings = null)
    {
        return scene.BackgroundScrollPositions.All(pair => pair.Key == 0 ? DirectBackgroundScrollCertificate.ValidPosition(pair.Value) : ValidPosition(field, pair.Key, pair.Value, changedLayer == pair.Key ? changedSettings! : BackgroundScrollCertificate.ReadSettings(scene, pair.Key), scene.CameraIndex));
    }

    public static bool SupportsRetainedPosition(FieldAssets field, SceneEventState scene, int layer, BackgroundScrollPosition position)
    {
        return ValidPosition(field, layer, position, BackgroundScrollCertificate.ReadSettings(scene, layer), scene.CameraIndex);
    }

    private static bool ValidPosition(FieldAssets field, int layer, BackgroundScrollPosition position, BackgroundScrollSettings settings, int cameraIndex)
    {
        if (layer <= cameraIndex || layer > 7 || !field.Background.Any(tile => tile.Layer == layer))
        {
            return false;
        }
        if (position.Mode == 3)
        {
            return DirectBackgroundScrollCertificate.ValidPosition(position) && FitsSettings(field, settings, position.TargetX, position.TargetY, cameraIndex) && (position.X is null || position.X is short x && position.Y is short y && FitsSettings(field, settings, x, y, cameraIndex));
        }
        return DirectBackgroundScrollCertificate.ValidPosition(position) && position.X is short currentX && position.Y is short currentY && FitsSettings(field, settings, currentX, currentY, cameraIndex) && (position.Status == 0 ? position.X : position.PreviousX) is short startX && (position.Status == 0 ? position.Y : position.PreviousY) is short startY && FitsSettings(field, settings, startX, startY, cameraIndex) && FitsSettings(field, settings, position.TargetX, position.TargetY, cameraIndex) && position.Duration is short duration && CanSet(field, new NativeLinearScrollQuery(layer, startX, startY, position.TargetX, position.TargetY, duration), cameraIndex);
    }

    private static bool FitsSettings(FieldAssets field, BackgroundScrollSettings settings, short x, short y, int cameraIndex)
    {
        return InRange(x) && InRange(y) && BackgroundParallaxPlacement.SupportsCameraRange(field, settings, x, y, cameraIndex);
    }

    private static bool InRange(short? value)
    {
        return value is >= -4095 and <= 4096;
    }
}
