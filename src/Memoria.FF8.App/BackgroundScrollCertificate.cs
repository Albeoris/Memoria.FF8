using System.Numerics;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;

namespace Memoria.FF8;

public sealed record BackgroundScrollFrame(IReadOnlyDictionary<int, Vector2> LayerOffsets, Vector2 ActorOffset, FieldNativeBackgroundWrapFrame? NativeWrap = null);

/// <summary>Certifies background parallax and delegates scripted camera placement to shared native contracts.</summary>
public static class BackgroundScrollCertificate
{
    public static bool CanSet(FieldAssets field, int layer, BackgroundScrollSettings settings, int cameraIndex)
    {
        return CanSetNormal(field, layer, settings, cameraIndex);
    }

    internal static bool CanSetNormal(FieldAssets field, int layer, BackgroundScrollSettings settings, int cameraIndex)
    {
        return NormalCameraCertificate.HasBaseline(field, cameraIndex) && field.Information!.BackgroundWrapMask == 0 && layer is >= 1 and <= 7 && field.Background.Any(tile => tile.Layer == layer) && BackgroundParallaxPlacement.SupportsCameraRange(field, settings);
    }

    public static bool CanSetForState(FieldAssets field, SceneEventState scene, IReadOnlyList<ActorState> actors, int layer, BackgroundScrollSettings settings)
    {
        if (!CanSet(field, layer, settings, scene.CameraIndex))
        {
            return false;
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
        try
        {
            return NormalCameraCertificate.Prepare(field, scene, actors) is not null;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }

    public static bool CanSetPartyPosition(FieldAssets field, SceneEventState scene, IReadOnlyList<ActorState> actors, int partySlot, byte nativeActorId, short duration)
    {
        return partySlot is >= 0 and <= 2 && scene.PartyBindingMemory is { Version: 1 } binding && binding.At(partySlot) == nativeActorId && ActorBackgroundScrollCertificate.CanSetInterpolated(field, scene, actors, nativeActorId, duration);
    }

    public static bool CanSetCosinePosition(FieldAssets field, SceneEventState scene, int layer, short x, short y, short duration)
    {
        return DirectBackgroundScrollCertificate.CanSetCosine(field, scene, layer, x, y, duration);
    }

    public static BackgroundScrollFrame Prepare(FieldAssets field, SceneEventState scene)
    {
        return ActorBackgroundScrollCertificate.Prepare(field, scene) ?? DirectBackgroundScrollCertificate.Prepare(field, scene) ?? throw new NotSupportedException("Scripted background placement requires a supported current native camera contract.");
    }
}
