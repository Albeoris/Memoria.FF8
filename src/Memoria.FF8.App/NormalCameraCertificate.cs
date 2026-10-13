using System.Numerics;
using System.Security.Cryptography;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;

namespace Memoria.FF8;

public static class NormalCameraCertificate
{
    internal static bool HasBaseline(FieldAssets field, int cameraIndex)
    {
        return field.Id >= 72 && cameraIndex == 0 && field.Cameras.Count == 1 && field.Camera.Pan == Vector2.Zero && field.Information is FieldInformation information && information.CameraBounds.Count == 8 && information.ScreenBounds.Count > 0 && information.ScreenBounds[0] == new FieldBounds(0, 224, 320, 0) && field.Background.All(tile => tile.Layer is >= 0 and <= 7) && (field.Background.All(tile => tile.Layer == 0) || information.BackgroundWrapMask == 0 || NativeField135ZeroCenterCertificate.Supports(field, cameraIndex) || FieldNativeNormalBackgroundWrap.SupportsMetadata(field, cameraIndex));
    }

    public static BackgroundScrollFrame? Prepare(FieldAssets field, SceneEventState scene, IReadOnlyList<ActorState> actors)
    {
        // TODO(TD-003): replace retained165/172 legacy gates with native state contracts
        if (field.Id < 72 || field.Id == 172 || scene.NativeFieldLoadVersion == 0)
        {
            return null;
        }
        FieldInformation? information = field.Information;
        FieldCamera camera = field.Camera;
        bool genericLinear = NativeLinearBackgroundScrollCertificate.SupportsPhase(field, scene);
        bool genericWrapping = information is { BackgroundWrapMask: > 0 } && !NativeField135ZeroCenterCertificate.Supports(field, scene.CameraIndex);
        bool baseline = scene.NativeFieldLoadVersion == 1 && HasBaseline(field, scene.CameraIndex) && (!genericWrapping || genericLinear || NormalBackgroundWrapCertificate.Supports(field, scene)) && (field.Id != 165 || field.Background.All(tile => tile.Layer == 0)) && (scene.BackgroundScrollPositions.Count == 0 || genericLinear || NativeField135LinearScrollCertificate.SupportsPhase(field, scene)) && scene.BackgroundScrollLayers.All(pair => BackgroundScrollCertificate.CanSetNormal(field, pair.Key, pair.Value, scene.CameraIndex));
        if (!baseline && field.Id != 165)
        {
            if (field.Id != 172 && field.Background.Any(tile => tile.Layer != 0))
            {
                throw new NotSupportedException("Normal multilayer tracking requires its original nonwrapping baseline without unsupported scroll state.");
            }
            return null;
        }
        if (!baseline || field.Id == 165 && (camera.FocalLength != 686 || information!.CameraHeight != 200 || information.CameraBounds.Any(bounds => bounds != new FieldBounds(-184, 184, 184, -184)) || Convert.ToHexString(SHA256.HashData(field.Jsm)) != "CF9C03471CD1935945606D9B12895F5789BB09755D6A80153A14007F3A1A0106"))
        {
            throw new NotSupportedException("Normal camera tracking requires its original single-layer baseline without scripted camera state.");
        }
        if (field.Id == 165 && (camera.AxisX != new Vector3(4058 / 4096f, 551 / 4096f, -1 / 4096f) || camera.AxisY != new Vector3(182 / 4096f, -1351 / 4096f, -3863 / 4096f) || camera.AxisZ != new Vector3(-521 / 4096f, 3827 / 4096f, -1363 / 4096f) || camera.Translation != new Vector3(883, -1917, 14813)))
        {
            throw new NotSupportedException("Normal Garden camera tracking requires the original165 matrix and translation.");
        }
        int[] partyActors = scene.FieldPartyNativeActorIds.TakeWhile(id => id != 255).ToArray();
        if (scene.NativeActorLayoutVersion != 1 || scene.FieldPartyNativeActorIds.Count != 3 || partyActors.Length == 0 || partyActors.Distinct().Count() != partyActors.Length || scene.FieldPartyNativeActorIds.Skip(partyActors.Length).Any(id => id != 255) || partyActors.Any(id => (uint)id >= scene.NativeActorObjectIds.Count) || scene.NativeActorObjectIds.Count is < 1 or > 255 || scene.NativeActorObjectIds.Any(id => id < 0) || scene.NativeActorObjectIds.Distinct().Count() != scene.NativeActorObjectIds.Count)
        {
            throw new NotSupportedException("Normal camera tracking requires the original compact leader binding.");
        }
        int leaderObjectId = scene.NativeActorObjectIds[partyActors[0]];
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
                BackgroundScrollSettings settings = scene.BackgroundScrollLayers.GetValueOrDefault(layer) ?? new BackgroundScrollSettings(0, 0, 256, 256);
                if (!BackgroundParallaxPlacement.TryOffset(center.X, center.Y, settings, out layerOffset))
                {
                    throw new NotSupportedException("Normal parallax offsets exceed the proved native signed-WORD/eighth-pixel placement domain.");
                }
            }
            if (layerOffset != Vector2.Zero)
            {
                layers[layer] = layerOffset;
            }
        }
        FieldNativeBackgroundWrapFrame? wrap = genericLinear ? NativeLinearBackgroundScrollCertificate.PrepareWrap(field, scene, center) : genericWrapping ? NormalBackgroundWrapCertificate.Prepare(field, scene, center) : null;
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
