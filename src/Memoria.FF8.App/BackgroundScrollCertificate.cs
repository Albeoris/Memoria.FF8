using System.Numerics;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;

namespace Memoria.FF8;

public sealed record BackgroundScrollFrame(IReadOnlyDictionary<int, Vector2> LayerOffsets, Vector2 ActorOffset, FieldNativeBackgroundWrapFrame? NativeWrap = null);

/// <summary>Certifies the reached original first-floor layer records and shared draw offset.</summary>
public static class BackgroundScrollCertificate
{
    // TODO(TD-003): replace the field172 legacy camera/scroll admission gates with shared native state contracts
    public static bool CanSet(FieldAssets field, int layer, BackgroundScrollSettings settings, int cameraIndex)
    {
        return layer == 1 && settings == new BackgroundScrollSettings(0, 0, 0, 0) && HasKnownBaseline(field, cameraIndex) || CanSetNormal(field, layer, settings, cameraIndex);
    }

    internal static bool CanSetNormal(FieldAssets field, int layer, BackgroundScrollSettings settings, int cameraIndex)
    {
        return field.Id is not (165 or 172) && NormalCameraCertificate.HasBaseline(field, cameraIndex) && field.Information!.BackgroundWrapMask == 0 && layer is >= 1 and <= 7 && field.Background.Any(tile => tile.Layer == layer) && BackgroundParallaxPlacement.SupportsCameraRange(field, settings);
    }

    public static bool CanSetForState(FieldAssets field, SceneEventState scene, IReadOnlyList<ActorState> actors, int layer, BackgroundScrollSettings settings)
    {
        if (!CanSet(field, layer, settings, scene.CameraIndex))
        {
            return false;
        }
        if (field.Id == 172)
        {
            return Supports(field, scene);
        }
        try
        {
            BackgroundScrollFrame? current = NormalCameraCertificate.Prepare(field, scene, actors);
            return current is not null && BackgroundParallaxPlacement.TryOffset((int)-current.ActorOffset.X, (int)-current.ActorOffset.Y, settings, out _);
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }

    public static bool Supports(FieldAssets field, SceneEventState scene, IReadOnlyList<ActorState> actors)
    {
        if (field.Id == 172)
        {
            return Supports(field, scene);
        }
        try
        {
            return NormalCameraCertificate.Prepare(field, scene, actors) is not null;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }

    public static bool CanSetPosition(FieldAssets field, int layer, short x, short y, int cameraIndex)
    {
        return layer == 0 && x == 0 && y is 0 or 224 && HasKnownBaseline(field, cameraIndex);
    }

    public static bool CanSetPartyPosition(FieldAssets field, SceneEventState scene, int partySlot, byte nativeActorId, short duration)
    {
        return partySlot == 0 && nativeActorId == 0 && duration == 15 && HasKnownBaseline(field, scene.CameraIndex) && scene.NativeActorLayoutVersion == 1 && scene.FieldPartyNativeActorIds.Count == 3 && scene.FieldPartyNativeActorIds[0] == nativeActorId && scene.NativeActorObjectIds.Count > nativeActorId && scene.NativeActorObjectIds[nativeActorId] == 0 && Supports(field, scene);
    }

    public static bool CanSetCosinePosition(FieldAssets field, SceneEventState scene, int layer, short x, short y, short duration)
    {
        return DirectBackgroundScrollCertificate.CanSetCosine(field, scene, layer, x, y, duration) || layer == 0 && x == 0 && y is 0 or 224 && duration == 40 && HasKnownBaseline(field, scene.CameraIndex) && Supports(field, scene);
    }

    public static bool Supports(FieldAssets field, SceneEventState scene)
    {
        try
        {
            _ = Prepare(field, scene);
            return true;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }

    public static BackgroundScrollFrame Prepare(FieldAssets field, SceneEventState scene)
    {
        foreach ((int layer, BackgroundScrollSettings settings) in scene.BackgroundScrollLayers)
        {
            if (layer != 1 || settings != new BackgroundScrollSettings(0, 0, 0, 0) || !HasKnownBaseline(field, scene.CameraIndex))
            {
                throw new NotSupportedException("The saved background scroll requires unproved field camera translation.");
            }
        }
        Vector2 center = Vector2.Zero;
        foreach ((int layer, BackgroundScrollPosition position) in scene.BackgroundScrollPositions)
        {
            if (layer != 0 || !HasKnownBaseline(field, scene.CameraIndex) || !IsKnownPhase(scene, position))
            {
                throw new NotSupportedException("The saved direct background scroll has an unproved camera phase.");
            }
            center = new Vector2(position.X, position.Y);
        }
        Dictionary<int, Vector2> offsets = [];
        if (center != Vector2.Zero)
        {
            foreach (int layer in field.Background.Select(tile => tile.Layer).Distinct())
            {
                offsets[layer] = -center;
            }
            if (scene.BackgroundScrollLayers.ContainsKey(1))
            {
                // Layer1's raw MAP displacement +C cancels shared DRAWENV displacement -C.
                offsets[1] = Vector2.Zero;
            }
        }
        return new BackgroundScrollFrame(offsets, -center);
    }

    private static bool IsKnownPhase(SceneEventState scene, BackgroundScrollPosition position)
    {
        if (position.X != 0 || position.Y is < 0 or > 224 || position.TargetX != 0 || position.TargetY is not (0 or 224) || position.PreviousX != 0 || position.PreviousY is < 0 or > 224)
        {
            return false;
        }
        if (position.Mode == 3)
        {
            return position.Status == 0 && position.Counter is >= 0 and <= 15 || position.Status == 2 && position.Counter == 0 && position.X == position.TargetX && position.Y == position.TargetY;
        }
        if (position.Mode == 5)
        {
            if (position.Duration != 40 || position.Counter < 0 || position.Counter > position.Duration)
            {
                return false;
            }
            return position.Status switch
            {
                0 => true,
                1 => position.Counter > 0 && position.Y == NativeCosineInterpolation.Interpolate(position.PreviousY, position.TargetY, position.Duration, position.Counter - 1),
                2 => position.Counter == position.Duration && position.Y == position.TargetY,
                _ => false
            };
        }
        if (position.Mode != 2 || position.Duration != 15 || position.NativeActorId != 0 || scene.NativeActorLayoutVersion != 1 || scene.FieldPartyNativeActorIds.Count != 3 || scene.FieldPartyNativeActorIds[0] != 0 || scene.NativeActorObjectIds.Count == 0 || scene.NativeActorObjectIds[0] != 0 || position.Counter < 0 || position.Counter > position.Duration)
        {
            return false;
        }
        return position.Status switch
        {
            0 => true,
            1 => position.Counter > 0 && position.Y == NativeCosineInterpolation.Interpolate(position.PreviousY, 0, position.Duration, position.Counter - 1),
            2 => position.Counter == position.Duration && position.Y == 0,
            _ => false
        };
    }

    private static bool HasKnownBaseline(FieldAssets field, int cameraIndex)
    {
        if (field.Id != 172 || cameraIndex != 0 || field.Camera.Pan != Vector2.Zero || field.Information is not FieldInformation information || information.CameraBounds.Count != 8 || information.ScreenBounds.Count != 2)
        {
            return false;
        }
        FieldBounds camera = information.CameraBounds[0];
        FieldBounds screen = information.ScreenBounds[0];
        int halfWidth = (screen.Right - screen.Left) / 2;
        int halfHeight = (screen.Bottom - screen.Top) / 2;
        // Native 475D00 clamps the projected center to both of these bounds.
        return camera.Right - halfWidth == 0 && camera.Left + halfWidth == 0 && camera.Bottom - halfHeight == 0 && camera.Top + halfHeight == 0;
    }
}
