using System.Numerics;
using System.Security.Cryptography;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal static class NormalBackgroundWrapCertificate
{
    public static bool Supports(FieldAssets field, SceneEventState scene)
    {
        return scene.NativeFieldInitialization is { Stage: NativeFieldInitializationStage.Completed } proof && proof.IsFrom(NativeFieldRuntimeProfile.FF8En, field.Id, Convert.ToHexString(SHA256.HashData(field.Jsm))) && scene.NativeFieldLoadVersion == 1 && scene.NativeActorLayoutVersion == 1 && scene.BackgroundScrollLayers.Count == 0 && scene.BackgroundScrollPositions.Count == 0 && FieldNativeNormalBackgroundWrap.SupportsMetadata(field, scene.CameraIndex);
    }

    public static FieldNativeBackgroundWrapFrame Prepare(FieldAssets field, SceneEventState scene, FieldNativeCameraCenter center)
    {
        if (!Supports(field, scene))
        {
            throw new NotSupportedException("Normal native wrapping requires its completed verified loader and unchanged normal layer records.");
        }
        return FieldNativeNormalBackgroundWrap.Prepare(field, scene.CameraIndex, center) ?? throw new NotSupportedException("Normal native wrapping has unsupported periods, camera bounds or signed-word/eighth tile placement.");
    }
}
