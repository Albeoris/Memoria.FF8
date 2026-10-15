using System.Numerics;
using System.Security.Cryptography;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

/// <summary>Shared native layer0 direct/interpolated camera center and supported native tile wrapping.</summary>
internal static class DirectBackgroundScrollCertificate
{
    public static bool CanSet(FieldAssets field, SceneEventState scene, int layer, short x, short y)
    {
        //520C40/520FA0 may issue a direct target during52C650 initialization.
        //No current center is consumed;475DF0 publishes the target before draw.
        return layer == scene.CameraIndex && InRange(x) && InRange(y) && HasSelectedDirectBaseline(field, scene, allowInitialization: true) && ValidPositions(field, scene) && SupportsCenter(field, scene, x, y);
    }

    public static bool CanSetCosine(FieldAssets field, SceneEventState scene, int layer, short x, short y, short duration)
    {
        if (layer != 0 || duration < 0 || !InRange(x) || !InRange(y) || !HasBaseline(field, scene) || !ValidPositions(field, scene) || !SupportsCenter(field, scene, x, y))
        {
            return false;
        }
        if (scene.BackgroundScrollPositions.TryGetValue(0, out BackgroundScrollPosition? current))
        {
            return current.X is short currentX && current.Y is short currentY && SupportsCenter(field, scene, currentX, currentY);
        }
        if (scene.ActorBackgroundScroll is { X: short actorX, Y: short actorY })
        {
            return InRange(actorX) && InRange(actorY) && SupportsCenter(field, scene, actorX, actorY);
        }
        if (scene.NormalBackgroundScrollCenter is { Version: 1 } normal && normal.FieldId == field.Id && normal.CameraIndex == scene.CameraIndex && normal.Frame > 0 && scene.FieldCallbackClock is { Version: 1 } clock && normal.Frame <= clock.OrdinaryCallbacks + clock.MovieCallbacks)
        {
            return InRange(normal.X) && InRange(normal.Y) && SupportsCenter(field, scene, normal.X, normal.Y);
        }
        return scene.NativeLayerZeroScrollCurrent is { Version: 1 } retained && InRange(retained.X) && InRange(retained.Y) && SupportsCenter(field, scene, retained.X, retained.Y);
    }

    public static bool CanSetLinear(FieldAssets field, NativeLinearScrollQuery query)
    {
        return query.Layer == 0 && query.Duration >= 0 && InRange(query.StartX) && InRange(query.StartY) && InRange(query.TargetX) && InRange(query.TargetY) && NormalCameraCertificate.HasBaseline(field, 0) && BackgroundParallaxPlacement.SupportsDrawingOrigin(query.StartX, query.StartY, field.Camera.Pan) && BackgroundParallaxPlacement.SupportsDrawingOrigin(query.TargetX, query.TargetY, field.Camera.Pan) && (field.Information!.BackgroundWrapMask == 0 || FieldNativeNormalBackgroundWrap.SupportsLayerCurrentMetadata(field, 0) && FieldNativeNormalBackgroundWrap.Prepare(field, 0, new FieldNativeCameraCenter(query.StartX, query.StartY)) is not null && FieldNativeNormalBackgroundWrap.Prepare(field, 0, new FieldNativeCameraCenter(query.TargetX, query.TargetY)) is not null);
    }

    public static bool CanBeginLinear(FieldAssets field, NativeLinearScrollQuery query, SceneEventState scene)
    {
        return CanSetLinear(field, query) && HasBaseline(field, scene) && ValidPositions(field, scene) && SupportsCenter(field, scene, query.StartX, query.StartY) && SupportsCenter(field, scene, query.TargetX, query.TargetY);
    }

    public static bool SupportsLinearPhase(FieldAssets field, SceneEventState scene)
    {
        return scene.BackgroundScrollPositions.TryGetValue(0, out BackgroundScrollPosition? position) && position.Mode == 4 && NativeLinearScrollPhase.IsValid(position) && (position.Status == 0 ? position.X : position.PreviousX) is short startX && (position.Status == 0 ? position.Y : position.PreviousY) is short startY && position.Duration is short duration && CanBeginLinear(field, new NativeLinearScrollQuery(0, startX, startY, position.TargetX, position.TargetY, duration), scene);
    }

    internal static (short X, short Y)? ResolveNormalCenter(FieldAssets field, SceneEventState scene, IReadOnlyList<ActorState> actors)
    {
        if (!(HasBaseline(field, scene) || scene.CameraIndex == 1 && NativeWrappedBackgroundScrollCertificate.SupportsPhase(field, scene)) || scene.BackgroundScrollPositions.ContainsKey(scene.CameraIndex) || scene.ActorBackgroundScroll is not null)
        {
            return null;
        }
        try
        {
            BackgroundScrollFrame? frame = NormalCameraCertificate.Prepare(field, scene, actors);
            if (frame is null || -frame.ActorOffset.X is < -4095 or > 4096 || -frame.ActorOffset.Y is < -4095 or > 4096)
            {
                return null;
            }
            return ((short)-frame.ActorOffset.X, (short)-frame.ActorOffset.Y);
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    public static BackgroundScrollFrame? Prepare(FieldAssets field, SceneEventState scene)
    {
        if (!scene.BackgroundScrollPositions.TryGetValue(scene.CameraIndex, out BackgroundScrollPosition? position) || !HasSelectedDirectBaseline(field, scene) || !ValidPositions(field, scene))
        {
            return null;
        }
        return position.X is short x && position.Y is short y ? PrepareCenter(field, scene, x, y) : null;
    }

    internal static BackgroundScrollFrame? PreparePendingDirectForNextDraw(FieldAssets field, SceneEventState scene)
    {
        if (!scene.BackgroundScrollPositions.TryGetValue(scene.CameraIndex, out BackgroundScrollPosition? position) || position is not { Mode: 3, Status: 0 } || !HasSelectedDirectBaseline(field, scene) || !ValidPositions(field, scene) || !SupportsCenter(field, scene, position.TargetX, position.TargetY))
        {
            return null;
        }
        //521460 only changes ratio words. Later475DF0 publishes the mode3 target
        //before475E60/475480 draw, so admission needs that next placement proof,
        //not retained current words that this issue will overwrite.
        return PrepareCenter(field, scene, position.TargetX, position.TargetY);
    }

    internal static BackgroundScrollFrame PrepareCenter(FieldAssets field, SceneEventState scene, short x, short y)
    {
        Vector2 pan = field.GetCamera(scene.CameraIndex).Pan;
        if (!BackgroundParallaxPlacement.SupportsDrawingOrigin(x, y, pan))
        {
            throw new NotSupportedException("Scripted camera drawing origin exceeds the proved native signed-WORD/E5 domain.");
        }
        Vector2 center = new(x, y);
        Dictionary<int, Vector2> layers = [];
        foreach (int layer in field.Background.Select(tile => tile.Layer).Distinct())
        {
            BackgroundScrollSettings settings = BackgroundScrollCertificate.ReadSettings(scene, layer);
            Vector2 offset = -center;
            if (layer > scene.CameraIndex && field.Information!.BackgroundWrapMask == 0 && !BackgroundParallaxPlacement.TryOffset(x, y, settings, scene.BackgroundScrollPositions.GetValueOrDefault(layer), out offset))
            {
                throw new NotSupportedException("Scripted camera parallax offsets exceed the proved native signed-WORD/eighth-pixel placement domain.");
            }
            if (layer > scene.CameraIndex)
            {
                offset += pan;
            }
            if (offset != Vector2.Zero)
            {
                layers[layer] = offset;
            }
        }
        FieldNativeBackgroundWrapFrame? wrap = field.Information!.BackgroundWrapMask == 0 ? null : NativeWrappedBackgroundScrollCertificate.PrepareWrap(field, scene, new FieldNativeCameraCenter(x, y));
        return new BackgroundScrollFrame(layers, -center) { NativeWrap = wrap };
    }

    internal static bool HasBaseline(FieldAssets field, SceneEventState scene)
    {
        return scene.CameraIndex == 0 && HasSelectedDirectBaseline(field, scene);
    }

    private static bool HasSelectedDirectBaseline(FieldAssets field, SceneEventState scene, bool allowInitialization = false)
    {
        //475480/476450 consume the selected record. Camera1 cancels PAN for both
        //raw pairs0/1; only subsequent layers use their independent parallax rows.
        return scene.CameraIndex is >= 0 and <= 1 && (scene.CameraIndex == 0 || scene.ActorBackgroundScroll is null && (!scene.BackgroundScrollPositions.TryGetValue(scene.CameraIndex, out BackgroundScrollPosition? selected) || selected.Mode == 3)) && scene.NativeFieldInitialization is { } proof && (proof.Stage == NativeFieldInitializationStage.Completed && scene.NativeFieldLoadVersion == 1 || allowInitialization && proof.Stage == NativeFieldInitializationStage.Initializing && scene.NativeFieldLoadVersion == 0 && scene.NativeActorLayoutVersion == 0) && proof.IsFrom(NativeFieldRuntimeProfile.FF8En, field.Id, Convert.ToHexString(SHA256.HashData(field.Jsm))) && BackgroundScrollCertificate.HasCurrentSettings(scene) && NormalCameraCertificate.HasBaseline(field, scene.CameraIndex) && (field.Information!.BackgroundWrapMask == 0 || FieldNativeNormalBackgroundWrap.SupportsMetadata(field, scene.CameraIndex)) && BackgroundScrollCertificate.HasSupportedSettings(field, scene);
    }

    internal static bool SupportsCenter(FieldAssets field, SceneEventState scene, short x, short y)
    {
        return BackgroundParallaxPlacement.SupportsDrawingOrigin(x, y, field.GetCamera(scene.CameraIndex).Pan) && (field.Information!.BackgroundWrapMask == 0 || NativeWrappedBackgroundScrollCertificate.TryPrepareWrap(field, scene, new FieldNativeCameraCenter(x, y)) is not null);
    }

    private static bool ValidPositions(FieldAssets field, SceneEventState scene)
    {
        return scene.BackgroundScrollPositions.All(pair => pair.Key == scene.CameraIndex ? ValidPosition(pair.Value) : field.Information!.BackgroundWrapMask == 0 ? NativeNonselectedBackgroundScrollCertificate.SupportsRetainedPosition(field, scene, pair.Key, pair.Value) : NativeWrappedBackgroundScrollCertificate.SupportsRetainedPosition(field, scene, pair.Key, pair.Value));
    }

    internal static bool ValidPosition(BackgroundScrollPosition state)
    {
        if (state.X.HasValue != state.Y.HasValue || state.PreviousX.HasValue != state.PreviousY.HasValue || !InRange(state.TargetX) || !InRange(state.TargetY))
        {
            return false;
        }
        if (state.Mode == 3)
        {
            return state.NativeLinearVersion == 0 && state.Status is 0 or 2 && (state.Status == 0 || state.Counter == 0 && state.X == state.TargetX && state.Y == state.TargetY);
        }
        if (!InRange(state.X) || !InRange(state.Y))
        {
            return false;
        }
        if (state.Mode == 4)
        {
            return NativeLinearScrollPhase.IsValid(state) && (state.Status == 0 || InRange(state.PreviousX) && InRange(state.PreviousY));
        }
        if (state.NativeLinearVersion != 0)
        {
            return false;
        }
        if (state.Mode != 5 || state.Duration is not short duration || duration < 0 || state.Status is not (0 or 1 or 2))
        {
            return false;
        }
        if (state.Status == 0)
        {
            return true;
        }
        if (state.PreviousX is not short previousX || state.PreviousY is not short previousY || state.Counter is not short counter || !InRange(previousX) || !InRange(previousY) || counter < 0 || counter > duration || state.Status == 1 && counter == 0 || state.Status == 2 && counter != duration)
        {
            return false;
        }
        int elapsed = state.Status == 2 ? counter : counter - 1;
        return state.X == NativeCosineInterpolation.Interpolate(previousX, state.TargetX, duration, elapsed) && state.Y == NativeCosineInterpolation.Interpolate(previousY, state.TargetY, duration, elapsed);
    }

    internal static bool IsCompleted(BackgroundScrollPosition state)
    {
        return state.Status == 2 && ValidPosition(state);
    }

    private static bool InRange(short? value)
    {
        return value is >= -4095 and <= 4096;
    }
}
