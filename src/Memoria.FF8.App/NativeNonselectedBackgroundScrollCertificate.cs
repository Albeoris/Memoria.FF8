using Memoria.FF8.Assets;
using Memoria.FF8.Events;

namespace Memoria.FF8;

/// <summary>Native475480 nonselected layer current8/10 subtraction with retained offset20/22 and ratio24/26.</summary>
internal static class NativeNonselectedBackgroundScrollCertificate
{
    public static bool CanSetDirect(FieldAssets field, SceneEventState scene, int layer, short x, short y)
    {
        return layer is >= 1 and <= 7 && field.Background.Any(tile => tile.Layer == layer) && HasBaseline(field, scene) && ValidPositions(field, scene) && SupportsEndpoint(field, scene, layer, x, y);
    }

    public static bool CanSetLinear(FieldAssets field, NativeLinearScrollQuery query)
    {
        return field.Information is { BackgroundWrapMask: 0 } && NormalCameraCertificate.HasBaseline(field, 0) && query.Layer is >= 1 and <= 7 && field.Background.Any(tile => tile.Layer == query.Layer) && query.Duration >= 0 && InRange(query.StartX) && InRange(query.StartY) && InRange(query.TargetX) && InRange(query.TargetY);
    }

    public static bool CanBeginLinear(FieldAssets field, NativeLinearScrollQuery query, SceneEventState scene)
    {
        return CanSetLinear(field, query) && HasBaseline(field, scene) && ValidPositions(field, scene) && SupportsEndpoint(field, scene, query.Layer, query.StartX, query.StartY) && SupportsEndpoint(field, scene, query.Layer, query.TargetX, query.TargetY);
    }

    public static bool Supports(FieldAssets field, SceneEventState scene)
    {
        return scene.BackgroundScrollPositions.Count > 0 && !scene.BackgroundScrollPositions.ContainsKey(0) && HasBaseline(field, scene) && ValidPositions(field, scene);
    }

    public static bool SupportsLinearPhase(FieldAssets field, SceneEventState scene)
    {
        return scene.BackgroundScrollPositions.Values.Any(position => position.Mode == 4) && HasBaseline(field, scene) && ValidPositions(field, scene);
    }

    internal static bool ValidPosition(BackgroundScrollPosition position)
    {
        return InRange(position.X) && InRange(position.Y) && (NativeLinearScrollPhase.IsValid(position) || position is { Mode: 3, Status: 2, NativeLinearVersion: 0, Counter: 0 } && position.X == position.TargetX && position.Y == position.TargetY);
    }

    internal static bool SupportsRetainedPosition(FieldAssets field, SceneEventState scene, int layer, BackgroundScrollPosition position)
    {
        return layer is >= 1 and <= 7 && field.Information is { BackgroundWrapMask: 0 } && field.Background.Any(tile => tile.Layer == layer) && ValidPosition(position) && SupportsEndpoint(field, scene, layer, position.X!.Value, position.Y!.Value) && (position.Mode != 4 || SupportsEndpoint(field, scene, layer, position.TargetX, position.TargetY) && (position.Status == 0 || SupportsEndpoint(field, scene, layer, position.PreviousX!.Value, position.PreviousY!.Value)));
    }

    private static bool HasBaseline(FieldAssets field, SceneEventState scene)
    {
        return field.Information is { BackgroundWrapMask: 0 } && DirectBackgroundScrollCertificate.HasBaseline(field, scene);
    }

    private static bool ValidPositions(FieldAssets field, SceneEventState scene)
    {
        return scene.BackgroundScrollPositions.All(pair => pair.Key == 0 ? DirectBackgroundScrollCertificate.ValidPosition(pair.Value) : SupportsRetainedPosition(field, scene, pair.Key, pair.Value));
    }

    private static bool SupportsEndpoint(FieldAssets field, SceneEventState scene, int layer, short x, short y)
    {
        BackgroundScrollSettings settings = BackgroundScrollCertificate.ReadSettings(scene, layer);
        return InRange(x) && InRange(y) && BackgroundParallaxPlacement.SupportsCameraRange(field, settings, x, y);
    }

    private static bool InRange(short? value)
    {
        return value is >= -4095 and <= 4096;
    }
}
