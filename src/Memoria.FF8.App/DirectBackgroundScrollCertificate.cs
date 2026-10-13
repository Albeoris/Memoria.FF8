using System.Numerics;
using System.Security.Cryptography;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

/// <summary>Shared native layer0 direct camera center for ordinary nonwrapping field resources.</summary>
internal static class DirectBackgroundScrollCertificate
{
    public static bool CanSet(FieldAssets field, SceneEventState scene, int layer, short x, short y)
    {
        return layer == 0 && x is >= -4095 and <= 4096 && y is >= -4095 and <= 4096 && HasBaseline(field, scene) && ValidPositions(scene);
    }

    public static BackgroundScrollFrame? Prepare(FieldAssets field, SceneEventState scene)
    {
        if (scene.BackgroundScrollPositions.Count == 0 || !HasBaseline(field, scene) || !ValidPositions(scene))
        {
            return null;
        }
        BackgroundScrollPosition position = scene.BackgroundScrollPositions[0];
        Vector2 center = new(position.X, position.Y);
        Dictionary<int, Vector2> layers = [];
        foreach (int layer in field.Background.Select(tile => tile.Layer).Distinct())
        {
            BackgroundScrollSettings settings = scene.BackgroundScrollLayers.GetValueOrDefault(layer) ?? new BackgroundScrollSettings(0, 0, 256, 256);
            Vector2 offset = layer == 0 ? -center : new Vector2(-position.X * settings.ScaleX / 256, -position.Y * settings.ScaleY / 256);
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
        return scene.BackgroundScrollPositions.Count <= 1 && scene.BackgroundScrollPositions.All(pair => pair.Key == 0 && pair.Value is { Mode: 3, NativeLinearVersion: 0 } state && state.Status is 0 or 2 && state.X is >= -4095 and <= 4096 && state.Y is >= -4095 and <= 4096 && state.TargetX is >= -4095 and <= 4096 && state.TargetY is >= -4095 and <= 4096 && (state.Status == 0 || state.Counter == 0 && state.X == state.TargetX && state.Y == state.TargetY));
    }
}
