namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect66CameraCapture
{
    public static NativeBattleCameraState Capture(NativeBattleCameraState camera)
    {
        ArgumentNullException.ThrowIfNull(camera);
        NativeBattleCameraPose current = camera.CurrentPose ?? throw new NotSupportedException("Effect66 requires its current camera vectors.");
        NativeBattleCameraPose captured = camera.CapturedPose ?? throw new NotSupportedException("Effect66 requires its retained captured camera roll.");
        short focal = camera.FocalLength ?? throw new NotSupportedException("Effect66 requires its current camera focal length.");
        return camera with { CapturedPose = captured with { Eye = current.Eye, Target = current.Target, FocalLength = focal }, CapturedEyePadding806 = camera.EyePaddingF6, CapturedTargetPadding80E = camera.TargetPaddingFE };
    }
}
