using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect66CameraCaptureTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData(-123, 456)]
    public void Capture_copies_only_current_vectors_padding_and_focal_preserving_both_rolls_and_count(short? eyePadding, short? targetPadding)
    {
        NativeBattleCameraState camera = NativeBattleCameraState.FromColdProcess() with { CapturedPose = new(new(1, 2, 3), new(4, 5, 6), 128, -321), CurrentPose = new(new(9, 10, 11), new(12, 13, 14), 512, 789), FocalLength = 216, Roll = 567, PoseCaptureCount = 7, EyePaddingF6 = eyePadding, TargetPaddingFE = targetPadding, CapturedEyePadding806 = 1234, CapturedTargetPadding80E = -1234 };
        NativeBattleCameraState imported = JsonSerializer.Deserialize<NativeBattleCameraState>(JsonSerializer.Serialize(camera))!;
        NativeBattleCameraState result = NativeBattleEffect66CameraCapture.Capture(imported);
        Assert.Equal(imported.CurrentPose!.Eye, result.CapturedPose!.Eye);
        Assert.Equal(imported.CurrentPose.Target, result.CapturedPose.Target);
        Assert.Equal((short)216, result.CapturedPose.FocalLength);
        Assert.Equal((short)-321, result.CapturedPose.Roll);
        Assert.Equal(eyePadding, result.CapturedEyePadding806);
        Assert.Equal(targetPadding, result.CapturedTargetPadding80E);
        Assert.Equal(imported, result with { CapturedPose = imported.CapturedPose, CapturedEyePadding806 = imported.CapturedEyePadding806, CapturedTargetPadding80E = imported.CapturedTargetPadding80E });
    }

    [Fact]
    public void Capture_requires_current_vectors_focal_and_retained_captured_roll()
    {
        NativeBattleCameraState camera = NativeBattleCameraState.FromColdProcess() with { CapturedPose = new(new(1, 2, 3), new(4, 5, 6), 128, -321), CurrentPose = new(new(9, 10, 11), new(12, 13, 14), 512, 789), FocalLength = 216 };
        Assert.Throws<NotSupportedException>(() => NativeBattleEffect66CameraCapture.Capture(camera with { CapturedPose = null }));
        Assert.Throws<NotSupportedException>(() => NativeBattleEffect66CameraCapture.Capture(camera with { CurrentPose = null }));
        Assert.Throws<NotSupportedException>(() => NativeBattleEffect66CameraCapture.Capture(camera with { FocalLength = null }));
    }
}
