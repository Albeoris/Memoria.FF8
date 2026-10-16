using Memoria.FF8.Assets;
using Memoria.FF8.Events;

namespace Memoria.FF8;

/// <summary>Native475480 nonselected layer current8/10 subtraction with retained offset20/22 and ratio24/26.</summary>
internal static class NativeNonselectedBackgroundScrollCertificate
{
    public static bool CanSetDirect(FieldAssets field, SceneEventState scene, int layer, short x, short y)
    {
        return layer is >= 0 and <= 7 && layer != scene.CameraIndex && (layer < scene.CameraIndex || field.Background.Any(tile => tile.Layer == layer)) && HasBaseline(field, scene) && ValidPositions(field, scene) && SupportsEndpoint(field, scene, layer, x, y);
    }

    public static bool CanSetLinear(FieldAssets field, NativeLinearScrollQuery query)
    {
        return field.Information is { BackgroundWrapMask: 0 } && NormalCameraCertificate.HasBaseline(field, 0) && query.Layer is >= 1 and <= 7 && field.Background.Any(tile => tile.Layer == query.Layer) && query.Duration >= 0 && InRange(query.StartX) && InRange(query.StartY) && InRange(query.TargetX) && InRange(query.TargetY);
    }

    public static bool CanBeginLinear(FieldAssets field, NativeLinearScrollQuery query, SceneEventState scene)
    {
        return CanSetLinear(field, query) && HasBaseline(field, scene) && ValidPositions(field, scene) && SupportsEndpoint(field, scene, query.Layer, query.StartX, query.StartY) && SupportsEndpoint(field, scene, query.Layer, query.TargetX, query.TargetY);
    }

    public static bool CanSetCosine(FieldAssets field, SceneEventState scene, int layer, short x, short y, short duration)
    {
        if (duration < 0 || !CanSetDirect(field, scene, layer, x, y))
        {
            return false;
        }
        return scene.BackgroundScrollPositions.TryGetValue(layer, out BackgroundScrollPosition? current) ? current.X is short startX && current.Y is short startY && SupportsEndpoint(field, scene, layer, startX, startY) : SupportsEndpoint(field, scene, layer, 0, 0);
    }

    public static bool Supports(FieldAssets field, SceneEventState scene)
    {
        return scene.BackgroundScrollPositions.Count > 0 && !scene.BackgroundScrollPositions.ContainsKey(scene.CameraIndex) && HasBaseline(field, scene) && ValidPositions(field, scene);
    }

    public static bool SupportsLinearPhase(FieldAssets field, SceneEventState scene)
    {
        return scene.BackgroundScrollPositions.Values.Any(position => position.Mode == 4) && HasBaseline(field, scene) && ValidPositions(field, scene);
    }

    internal static bool ValidPosition(BackgroundScrollPosition position)
    {
        //475DF0 uses the same direct, linear and cosine phases for every row,
        //including a pending direct issue whose old current words are unknown.
        return DirectBackgroundScrollCertificate.ValidPosition(position);
    }

    internal static bool SupportsRetainedPosition(FieldAssets field, SceneEventState scene, int layer, BackgroundScrollPosition position)
    {
        if (layer is < 0 or > 7 || field.Information is not { BackgroundWrapMask: 0 } || !ValidPosition(position))
        {
            return false;
        }
        if (layer < scene.CameraIndex)
        {
            //475480 consumes the selected row for both raw pairs0/1 when
            //camera1 is selected. Lower physical rows still execute475DF0
            //and retain completion/current words, but do not affect drawing.
            return true;
        }
        if (!field.Background.Any(tile => tile.Layer == layer))
        {
            //475E60 retains every physical row even when no background tile
            //consumes it after a different camera becomes selected.
            return true;
        }
        if (position.Mode == 3 && position.Status == 0)
        {
            return SupportsEndpoint(field, scene, layer, position.TargetX, position.TargetY) && (position.X is null || InRange(position.X) && InRange(position.Y) && SupportsEndpoint(field, scene, layer, position.X.Value, position.Y!.Value));
        }
        return position.X is short currentX && position.Y is short currentY && SupportsEndpoint(field, scene, layer, currentX, currentY) && (position.Mode is not (4 or 5) || SupportsEndpoint(field, scene, layer, position.TargetX, position.TargetY) && (position.Status == 0 || SupportsEndpoint(field, scene, layer, position.PreviousX!.Value, position.PreviousY!.Value)));
    }

    private static bool HasBaseline(FieldAssets field, SceneEventState scene)
    {
        return field.Information is { BackgroundWrapMask: 0 } && DirectBackgroundScrollCertificate.HasSelectedBaseline(field, scene);
    }

    private static bool ValidPositions(FieldAssets field, SceneEventState scene)
    {
        return scene.BackgroundScrollPositions.All(pair => pair.Key == 0 ? DirectBackgroundScrollCertificate.ValidPosition(pair.Value) : SupportsRetainedPosition(field, scene, pair.Key, pair.Value));
    }

    private static bool SupportsEndpoint(FieldAssets field, SceneEventState scene, int layer, short x, short y)
    {
        if (layer < scene.CameraIndex)
        {
            return InRange(x) && InRange(y);
        }
        BackgroundScrollSettings settings = BackgroundScrollCertificate.ReadSettings(scene, layer);
        return InRange(x) && InRange(y) && BackgroundParallaxPlacement.SupportsCameraRange(field, settings, x, y, scene.CameraIndex);
    }

    private static bool InRange(short? value)
    {
        return value is >= -4095 and <= 4096;
    }
}
