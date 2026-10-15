using System.Numerics;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;

namespace Memoria.FF8;

public static class NormalCameraCertificate
{
    internal static bool HasBaseline(FieldAssets field, int cameraIndex)
    {
        // Native4767B0 selects CA base for slot0 and base+40 for slot1.
        // Each selection consumes its own matrix/PAN and INF clamp bounds.
        return field.Id >= 72 && cameraIndex is >= 0 and <= 1 && cameraIndex < field.Cameras.Count && BackgroundParallaxPlacement.SupportsPan(field, cameraIndex) && field.Information is FieldInformation information && information.CameraBounds.Count == 8 && information.ScreenBounds.Count > 0 && information.ScreenBounds[0] == new FieldBounds(0, 224, 320, 0) && field.Background.All(tile => tile.Layer is >= 0 and <= 7) && (field.Background.All(tile => tile.Layer == 0) || information.BackgroundWrapMask == 0 || FieldNativeNormalBackgroundWrap.SupportsMetadata(field, cameraIndex));
    }

    public static BackgroundScrollFrame? Prepare(FieldAssets field, SceneEventState scene, IReadOnlyList<ActorState> actors)
    {
        if (field.Id < 72)
        {
            return null;
        }
        if (FixedViewportCameraCertificate.Prepare(field, scene) is BackgroundScrollFrame fixedViewport)
        {
            return fixedViewport;
        }
        FieldInformation? information = field.Information;
        FieldCamera camera = field.GetCamera(scene.CameraIndex);
        bool genericLayerScroll = NativeWrappedBackgroundScrollCertificate.SupportsPhase(field, scene);
        bool genericWrapping = information is { BackgroundWrapMask: > 0 };
        bool baseline = scene.NativeFieldLoadVersion == 1 && BackgroundScrollCertificate.HasCurrentSettings(scene) && HasBaseline(field, scene.CameraIndex) && (!genericWrapping || genericLayerScroll || NormalBackgroundWrapCertificate.Supports(field, scene)) && (scene.BackgroundScrollPositions.Count == 0 || genericLayerScroll || NativeNonselectedBackgroundScrollCertificate.Supports(field, scene)) && BackgroundScrollCertificate.HasSupportedSettings(field, scene);
        if (!baseline)
        {
            throw new NotSupportedException("Normal camera tracking requires the current native loader, supported camera metadata and supported scroll state.");
        }
        ActorState? leader = NativeFieldPartyBinding.ResolveActor(scene, actors, 0);
        if (leader is null || (uint)leader.RenderModelId >= field.Models.Count)
        {
            throw new NotSupportedException("Normal camera tracking cannot substitute an unproved actor for the compact party leader.");
        }
        FieldNativeCameraResult result = ProjectActorCenter(field, scene, leader);
        if (!result.Supported || result.Center is not FieldNativeCameraCenter center)
        {
            throw new NotSupportedException(result.UnsupportedReason ?? "Normal camera projection has no certified center.");
        }
        if (!BackgroundParallaxPlacement.SupportsDrawingOrigin(center.X, center.Y, camera.Pan))
        {
            throw new NotSupportedException("Normal camera drawing origin exceeds the proved native signed-WORD/E5 domain.");
        }
        Vector2 offset = new(-center.X, -center.Y);
        Dictionary<int, Vector2> layers = [];
        foreach (int layer in field.Background.Select(tile => tile.Layer).Distinct())
        {
            Vector2 layerOffset = offset;
            if (layer > scene.CameraIndex)
            {
                if (center.X is < -4096 or > 4095 || center.Y is < -4096 or > 4095)
                {
                    throw new NotSupportedException("Normal parallax translation would exceed the certified native signed-word eighth-pixel range.");
                }
                BackgroundScrollSettings settings = BackgroundScrollCertificate.ReadSettings(scene, layer);
                if (genericLayerScroll && genericWrapping)
                {
                    // The native wrap-frame layer translation owns offset/ratio/current once, before this common -C.
                    layerOffset = offset;
                }
                else if (!BackgroundParallaxPlacement.TryOffset(center.X, center.Y, settings, scene.BackgroundScrollPositions.GetValueOrDefault(layer), out layerOffset))
                {
                    throw new NotSupportedException("Normal parallax offsets exceed the proved native signed-WORD/eighth-pixel placement domain.");
                }
                // Native layer0 cancels PAN in its raw pair. Nonselected
                // normal layers retain the common DRAWENV PAN contribution.
                layerOffset += camera.Pan;
            }
            if (layerOffset != Vector2.Zero)
            {
                layers[layer] = layerOffset;
            }
        }
        FieldNativeBackgroundWrapFrame? wrap = genericLayerScroll && genericWrapping ? NativeWrappedBackgroundScrollCertificate.PrepareWrap(field, scene, center) : genericWrapping ? NormalBackgroundWrapCertificate.Prepare(field, scene, center) : null;
        return new BackgroundScrollFrame(layers, offset) { NativeWrap = wrap };
    }

    private static int Anchor(float coordinate)
    {
        if (!float.IsFinite(coordinate) || MathF.Floor(coordinate) is < short.MinValue or > short.MaxValue)
        {
            throw new NotSupportedException("Normal camera cannot recover a supported integer anchor from the displayed actor coordinate.");
        }
        return (int)MathF.Floor(coordinate) << 12;
    }

    internal static FieldNativeCameraResult ProjectActorCenter(FieldAssets field, SceneEventState scene, ActorState actor)
    {
        FieldFixedPosition position;
        if (actor.NativeScriptMovement is NativeScriptMovementState motion && actor.X == motion.FixedX / 4096f && actor.Y == motion.FixedY / 4096f && actor.Z == motion.FixedZ / 4096f)
        {
            position = new FieldFixedPosition(motion.FixedX, motion.FixedY, motion.FixedZ);
        }
        else if (scene.NativePlayerPosition is FieldFixedPosition raw && actor.X == raw.X / 4096f && actor.Y == raw.Y / 4096f && actor.Z == raw.Z / 4096f)
        {
            position = raw;
        }
        else
        {
            // The projection consumes SAR12 integer anchors, not an inferred motion trajectory.
            position = new FieldFixedPosition(Anchor(actor.X), Anchor(actor.Y), Anchor(actor.Z));
        }
        return FieldNativeCameraProbe.Resolve(field.GetCamera(scene.CameraIndex), field.Information!, position.X, position.Y, position.Z, scene.CameraIndex);
    }
}
