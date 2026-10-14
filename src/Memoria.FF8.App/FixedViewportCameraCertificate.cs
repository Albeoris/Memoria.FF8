using System.Numerics;
using System.Security.Cryptography;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal static class FixedViewportCameraCertificate
{
    public static BackgroundScrollFrame? Prepare(FieldAssets field, SceneEventState scene)
    {
        if (field.NativeExecutableHash != NativeFieldRuntimeProfile.FF8EnExecutableHash || scene.NativeFieldInitialization is not { Stage: NativeFieldInitializationStage.Completed } proof || !proof.IsFrom(NativeFieldRuntimeProfile.FF8En, field.Id, Convert.ToHexString(SHA256.HashData(field.Jsm))) || scene.NativeFieldLoadVersion != 1 || scene.CameraIndex is < 0 or > 1 || scene.CameraIndex >= field.Cameras.Count || field.GetCamera(scene.CameraIndex).Pan != Vector2.Zero || field.Information is not FieldInformation information || information.BackgroundWrapMask != 0 || information.ScreenBounds.Count == 0 || information.ScreenBounds[0] != new FieldBounds(0, 224, 320, 0) || information.CameraBounds.Count <= scene.CameraIndex || information.CameraBounds[scene.CameraIndex] != new FieldBounds(-112, 112, 160, -160) || field.Background.Count == 0 || field.Background.Any(tile => tile.Layer != 0) || scene.ActorBackgroundScroll is not null || scene.BackgroundScrollPositions.Count != 0 || !BackgroundScrollCertificate.HasDefaultSettings(scene))
        {
            return null;
        }
        // Native475D00 uses bounds0 for camera0, bounds1 otherwise and screen0.
        // These cap-then-floor bounds force both coordinates to zero for every
        // signed-WORD projection. Native475480/476450 cancel the layer0 PAN.
        return new BackgroundScrollFrame(new Dictionary<int, Vector2>(), Vector2.Zero);
    }
}
