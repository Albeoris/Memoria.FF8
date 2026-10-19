using Memoria.FF8.Assets;
using Memoria.FF8.Events;

namespace Memoria.FF8;

internal static class ExplicitCosineScrollCertificate
{
    public static bool CanSet(FieldAssets field, NativeLinearScrollQuery query, SceneEventState scene)
    {
        if ((uint)query.Layer >= 8 || query.Duration < 0 || scene.ActorBackgroundScroll is { } actor && actor.Layer == query.Layer || !scene.BackgroundScrollLayers.TryGetValue(query.Layer, out BackgroundScrollSettings? settings))
        {
            return false;
        }
        // Admission uses detached scroll dictionaries. A completed direct row
        // certifies the explicit current words without publishing them to the VM.
        SceneEventState projection = scene.CreateBackgroundScrollProjection(query.Layer, settings);
        projection.BackgroundScrollPositions[query.Layer] = new BackgroundScrollPosition(query.StartX, query.StartY, query.StartX, query.StartY, null, null, 0, 3, 2);
        return BackgroundScrollCertificate.CanSetCosinePosition(field, projection, query.Layer, query.TargetX, query.TargetY, query.Duration);
    }
}
