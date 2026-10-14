using System.Numerics;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;

namespace Memoria.FF8;

public static class NormalCameraCertificate
{
    internal static bool HasBaseline(FieldAssets field, int cameraIndex)
    {
        return field.Id >= 72 && cameraIndex == 0 && field.Cameras.Count == 1 && BackgroundParallaxPlacement.SupportsPan(field) && field.Information is FieldInformation information && information.CameraBounds.Count == 8 && information.ScreenBounds.Count > 0 && information.ScreenBounds[0] == new FieldBounds(0, 224, 320, 0) && field.Background.All(tile => tile.Layer is >= 0 and <= 7) && (field.Background.All(tile => tile.Layer == 0) || information.BackgroundWrapMask == 0 || FieldNativeNormalBackgroundWrap.SupportsMetadata(field, cameraIndex));
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
        FieldCamera camera = field.Camera;
        bool genericLinear = NativeLinearBackgroundScrollCertificate.SupportsPhase(field, scene);
        bool genericWrapping = information is { BackgroundWrapMask: > 0 };
        bool baseline = scene.NativeFieldLoadVersion == 1 && BackgroundScrollCertificate.HasCurrentSettings(scene) && HasBaseline(field, scene.CameraIndex) && (!genericWrapping || genericLinear || NormalBackgroundWrapCertificate.Supports(field, scene)) && (scene.BackgroundScrollPositions.Count == 0 || genericLinear || NativeNonselectedBackgroundScrollCertificate.Supports(field, scene)) && BackgroundScrollCertificate.HasSupportedSettings(field, scene);
        if (!baseline)
        {
            throw new NotSupportedException("Normal camera tracking requires the current native loader, supported camera metadata and supported scroll state.");
        }
        int[] partyActors = scene.FieldPartyNativeActorIds.Where(id => id != 255).ToArray();
        if (scene.NativeActorLayoutVersion != 1 || scene.FieldPartyNativeActorIds.Count != 3 || scene.FieldPartyNativeActorIds[0] == 255 || partyActors.Distinct().Count() != partyActors.Length || partyActors.Any(id => (uint)id >= scene.NativeActorObjectIds.Count) || scene.NativeActorObjectIds.Count is < 1 or > 255 || scene.NativeActorObjectIds.Any(id => id < 0) || scene.NativeActorObjectIds.Distinct().Count() != scene.NativeActorObjectIds.Count)
        {
            throw new NotSupportedException("Normal camera tracking requires the original compact leader binding.");
        }
        int leaderObjectId = scene.NativeActorObjectIds[scene.FieldPartyNativeActorIds[0]];
        ActorState[] matchingActors = actors.Where(actor => actor.ObjectId == leaderObjectId).ToArray();
        if (matchingActors.Length != 1 || matchingActors[0].CharacterId != 0 || (uint)matchingActors[0].ModelId >= field.Models.Count)
        {
            throw new NotSupportedException("Normal camera tracking cannot substitute an unproved actor for the compact party leader.");
        }
        ActorState leader = matchingActors[0];
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
            if (layer != 0)
            {
                if (center.X is < -4096 or > 4095 || center.Y is < -4096 or > 4095)
                {
                    throw new NotSupportedException("Normal parallax translation would exceed the certified native signed-word eighth-pixel range.");
                }
                BackgroundScrollSettings settings = BackgroundScrollCertificate.ReadSettings(scene, layer);
                if (!(genericLinear && genericWrapping ? BackgroundParallaxPlacement.TryOffset(center.X, center.Y, settings, out layerOffset) : BackgroundParallaxPlacement.TryOffset(center.X, center.Y, settings, scene.BackgroundScrollPositions.GetValueOrDefault(layer), out layerOffset)))
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
        FieldNativeBackgroundWrapFrame? wrap = genericLinear && genericWrapping ? NativeLinearBackgroundScrollCertificate.PrepareWrap(field, scene, center) : genericWrapping ? NormalBackgroundWrapCertificate.Prepare(field, scene, center) : null;
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
