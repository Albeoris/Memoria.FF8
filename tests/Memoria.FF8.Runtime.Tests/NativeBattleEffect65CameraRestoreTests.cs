using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect65CameraRestoreTests
{
    [Fact]
    public void Restore_copies_captured_vectors_padding_and_roll_while_retaining_current_focal_and_projection()
    {
        NativeBattleCameraState camera = NativeBattleCameraState.FromColdProcess() with { CapturedPose = new(new(1, 2, 3), new(4, 5, 6), 128, -321), CurrentPose = new(new(9, 10, 11), new(12, 13, 14), 216, 789), FocalLength = 216, Roll = 789, EyePaddingF6 = 100, TargetPaddingFE = 200, CapturedEyePadding806 = -123, CapturedTargetPadding80E = 456 };
        NativeBattleCameraState restored = NativeBattleEffect65CameraRestore.Restore(camera);
        Assert.Equal(camera.CapturedPose!.Eye, restored.CurrentPose!.Eye);
        Assert.Equal(camera.CapturedPose!.Target, restored.CurrentPose.Target);
        Assert.Equal((short)-321, restored.Roll);
        Assert.Equal((short)-321, restored.CurrentPose.Roll);
        Assert.Equal((short)216, restored.FocalLength);
        Assert.Equal((short)216, restored.CurrentPose.FocalLength);
        Assert.Equal((short)-123, restored.EyePaddingF6);
        Assert.Equal((short)456, restored.TargetPaddingFE);
        Assert.Same(camera.Projection, restored.Projection);
        Assert.Equal(camera.CapturedPose, restored.CapturedPose);
    }

    [Fact]
    public void Restore_does_not_replace_unknown_padding_or_read_the_captured_focal()
    {
        NativeBattleCameraState camera = NativeBattleCameraState.FromColdProcess() with { CapturedPose = new(new(1, 2, 3), new(4, 5, 6), null, 17), CurrentPose = new(new(9, 10, 11), new(12, 13, 14), 320, 0), FocalLength = 320, EyePaddingF6 = 100, TargetPaddingFE = 200, CapturedEyePadding806 = null, CapturedTargetPadding80E = null };
        NativeBattleCameraState imported = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleCameraState>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(camera)))!);
        NativeBattleCameraState restored = NativeBattleEffect65CameraRestore.Restore(imported);
        Assert.Equal((short)320, restored.FocalLength);
        Assert.Equal((short)320, restored.CurrentPose!.FocalLength);
        Assert.Null(restored.EyePaddingF6);
        Assert.Null(restored.TargetPaddingFE);
        Assert.Equal((short)17, restored.Roll);
    }
}
