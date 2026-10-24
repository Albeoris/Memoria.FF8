namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect65CameraRestore
{
    public static NativeBattleCameraState Restore(NativeBattleCameraState camera)
    {
        NativeBattleCameraPose captured = camera.CapturedPose ?? throw new NotSupportedException("Effect65 requires its captured camera pose.");
        return camera.WriteEffectGlobals(captured with { FocalLength = camera.FocalLength }) with { EyePaddingF6 = camera.CapturedEyePadding806, TargetPaddingFE = camera.CapturedTargetPadding80E };
    }
}
