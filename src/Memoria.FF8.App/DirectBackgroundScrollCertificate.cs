using System.Numerics;
using System.Security.Cryptography;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

/// <summary>Shared native layer0 direct/cosine camera center for ordinary nonwrapping field resources.</summary>
internal static class DirectBackgroundScrollCertificate
{
    public static bool CanSet(FieldAssets field, SceneEventState scene, int layer, short x, short y)
    {
        return layer == 0 && x is >= -4095 and <= 4096 && y is >= -4095 and <= 4096 && HasBaseline(field, scene) && ValidPositions(scene);
    }

    public static bool CanSetCosine(FieldAssets field, SceneEventState scene, int layer, short x, short y, short duration)
    {
        if (layer != 0 || duration < 0 || !InRange(x) || !InRange(y) || !HasBaseline(field, scene) || !ValidPositions(scene))
        {
            return false;
        }
        return scene.BackgroundScrollPositions.ContainsKey(0) || scene.ActorBackgroundScroll is { X: short actorX, Y: short actorY } && InRange(actorX) && InRange(actorY) || scene.NormalBackgroundScrollCenter is { Version: 1 } normal && normal.FieldId == field.Id && normal.CameraIndex == scene.CameraIndex && normal.Frame > 0 && scene.FieldCallbackClock is { Version: 1 } clock && normal.Frame <= clock.OrdinaryCallbacks + clock.MovieCallbacks && InRange(normal.X) && InRange(normal.Y);
    }

    internal static (short X, short Y)? ResolveNormalCenter(FieldAssets field, SceneEventState scene, IReadOnlyList<ActorState> actors)
    {
        if (!HasBaseline(field, scene) || scene.BackgroundScrollPositions.ContainsKey(0) || scene.ActorBackgroundScroll is not null)
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
        if (scene.BackgroundScrollPositions.Count == 0 || !HasBaseline(field, scene) || !ValidPositions(scene))
        {
            return null;
        }
        BackgroundScrollPosition position = scene.BackgroundScrollPositions[0];
        return PrepareCenter(field, scene, position.X, position.Y);
    }

    internal static BackgroundScrollFrame PrepareCenter(FieldAssets field, SceneEventState scene, short x, short y)
    {
        Vector2 center = new(x, y);
        Dictionary<int, Vector2> layers = [];
        foreach (int layer in field.Background.Select(tile => tile.Layer).Distinct())
        {
            BackgroundScrollSettings settings = scene.BackgroundScrollLayers.GetValueOrDefault(layer) ?? new BackgroundScrollSettings(0, 0, 256, 256);
            Vector2 offset = layer == 0 ? -center : new Vector2(-x * settings.ScaleX / 256, -y * settings.ScaleY / 256);
            if (offset != Vector2.Zero)
            {
                layers[layer] = offset;
            }
        }
        return new BackgroundScrollFrame(layers, -center);
    }

    private static bool HasBaseline(FieldAssets field, SceneEventState scene)
    {
        return scene.NativeFieldInitialization is { Stage: NativeFieldInitializationStage.Completed } proof && proof.IsFrom(NativeFieldRuntimeProfile.FF8En, field.Id, Convert.ToHexString(SHA256.HashData(field.Jsm))) && scene.NativeFieldLoadVersion == 1 && NormalCameraCertificate.HasBaseline(field, scene.CameraIndex) && field.Information!.BackgroundWrapMask == 0 && scene.BackgroundScrollLayers.All(pair => BackgroundScrollCertificate.CanSetNormal(field, pair.Key, pair.Value, scene.CameraIndex));
    }

    private static bool ValidPositions(SceneEventState scene)
    {
        return scene.BackgroundScrollPositions.Count <= 1 && scene.BackgroundScrollPositions.All(pair => pair.Key == 0 && ValidPosition(pair.Value));
    }

    private static bool ValidPosition(BackgroundScrollPosition state)
    {
        if (state.NativeLinearVersion != 0 || !InRange(state.X) || !InRange(state.Y) || !InRange(state.TargetX) || !InRange(state.TargetY))
        {
            return false;
        }
        if (state.Mode == 3)
        {
            return state.Status is 0 or 2 && (state.Status == 0 || state.Counter == 0 && state.X == state.TargetX && state.Y == state.TargetY);
        }
        if (state.Mode != 5 || state.Duration < 0 || state.Status is not (0 or 1 or 2))
        {
            return false;
        }
        if (state.Status == 0)
        {
            return true;
        }
        if (!InRange(state.PreviousX) || !InRange(state.PreviousY) || state.Counter < 0 || state.Counter > state.Duration || state.Status == 1 && state.Counter == 0 || state.Status == 2 && state.Counter != state.Duration)
        {
            return false;
        }
        int elapsed = state.Status == 2 ? state.Counter : state.Counter - 1;
        return state.X == NativeCosineInterpolation.Interpolate(state.PreviousX, state.TargetX, state.Duration, elapsed) && state.Y == NativeCosineInterpolation.Interpolate(state.PreviousY, state.TargetY, state.Duration, elapsed);
    }

    internal static bool IsCompleted(BackgroundScrollPosition state)
    {
        return state.Status == 2 && ValidPosition(state);
    }

    private static bool InRange(short value)
    {
        return value is >= -4095 and <= 4096;
    }
}
