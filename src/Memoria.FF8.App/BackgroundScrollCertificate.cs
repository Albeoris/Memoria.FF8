using System.Numerics;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;

namespace Memoria.FF8;

public sealed record BackgroundScrollFrame(IReadOnlyDictionary<int, Vector2> LayerOffsets, Vector2 ActorOffset, FieldNativeBackgroundWrapFrame? NativeWrap = null)
{
    public IReadOnlyDictionary<int, short>? LayerScales { get; init; }
}

/// <summary>Certifies background parallax and delegates scripted camera placement to shared native contracts.</summary>
public static class BackgroundScrollCertificate
{
    public static bool CanSet(FieldAssets field, int layer, BackgroundScrollSettings settings, int cameraIndex)
    {
        return layer is >= 1 and <= 7 && field.Background.Any(tile => tile.Layer == layer) && CanSetNormal(field, layer, settings, cameraIndex);
    }

    internal static bool HasCurrentSettings(SceneEventState scene)
    {
        return scene.BackgroundScrollLayers.Count == 8 && scene.BackgroundScrollLayers.Keys.All(layer => (uint)layer < 8) && scene.BackgroundScrollLayers.Values.All(settings => settings is not null);
    }

    internal static bool HasDefaultSettings(SceneEventState scene)
    {
        return HasCurrentSettings(scene) && scene.BackgroundScrollLayers.Values.All(settings => settings == new BackgroundScrollSettings(0, 0, 256, 256));
    }

    internal static bool HasSupportedSettings(FieldAssets field, SceneEventState scene)
    {
        return HasCurrentSettings(scene) && scene.BackgroundScrollLayers.All(pair => CanSetNormal(field, pair.Key, pair.Value, scene.CameraIndex));
    }

    internal static BackgroundScrollSettings ReadSettings(SceneEventState scene, int layer)
    {
        return scene.BackgroundScrollLayers.TryGetValue(layer, out BackgroundScrollSettings? settings) ? settings : throw new NotSupportedException("Native layer placement needs its executed retained offset/ratio record; absent records are unknown.");
    }

    internal static bool CanSetNormal(FieldAssets field, int layer, BackgroundScrollSettings settings, int cameraIndex)
    {
        return NormalCameraCertificate.HasBaseline(field, cameraIndex) && layer is >= 0 and <= 7 && (layer == 0 || !field.Background.Any(tile => tile.Layer == layer) || BackgroundParallaxPlacement.SupportsCameraRange(field, settings, cameraIndex: cameraIndex));
    }

    public static bool CanSetForState(FieldAssets field, SceneEventState scene, IReadOnlyList<ActorState> actors, int layer, BackgroundScrollSettings settings)
    {
        //5213D0 and521460 own the same offset/ratio words and leave the camera
        //mode/current/previous phase untouched. Both consume the renderer's actual
        //selected normal, direct or actor camera, including a same-callback DSCROLL.
        return CanSetRatioForState(field, scene, actors, layer, settings);
    }

    public static bool CanSetRatioForState(FieldAssets field, SceneEventState scene, IReadOnlyList<ActorState> actors, int layer, BackgroundScrollSettings settings)
    {
        if (field.Information is { BackgroundWrapMask: > 0 })
        {
            return NativeWrappedBackgroundScrollCertificate.CanSetSettings(field, scene, layer, settings);
        }
        if (!CanSetNormal(field, layer, settings, scene.CameraIndex))
        {
            return false;
        }
        try
        {
            //All actor VMs precede475DF0. Project every pending direct row into
            //detached scroll dictionaries for the next draw; never publish those
            //future current/previous/status words during opcode admission.
            SceneEventState projection = scene.CreateBackgroundScrollProjection(layer, settings);
            BackgroundScrollFrame? current = ActorBackgroundScrollCertificate.Prepare(field, projection) ?? DirectBackgroundScrollCertificate.Prepare(field, projection) ?? NormalCameraCertificate.Prepare(field, projection, actors);
            return current is not null;
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
        return DirectBackgroundScrollCertificate.CanSetCosine(field, scene, layer, x, y, duration) || NativeNonselectedBackgroundScrollCertificate.CanSetCosine(field, scene, layer, x, y, duration) || NativeWrappedBackgroundScrollCertificate.CanSetCosine(field, scene, layer, x, y, duration);
    }

    public static BackgroundScrollFrame Prepare(FieldAssets field, SceneEventState scene)
    {
        return ActorBackgroundScrollCertificate.Prepare(field, scene) ?? DirectBackgroundScrollCertificate.Prepare(field, scene) ?? throw new NotSupportedException("Scripted background placement requires a supported current native camera contract.");
    }
}
