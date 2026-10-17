namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleQuezacotlPathCameraTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(19)]
    public void NativeOrbitUsesFortyFrameSineDivisorAndWordRollWithoutPublishingAView(int localFrame)
    {
        NativeBattleCameraState camera = NativeBattleCameraState.FromColdProcess() with { CurrentPose = new(new(-4000, -4000, 7690), new(0, 0, 3690), 288, 32764), FocalLength = 288, Roll = 32764, EyePaddingF6 = 0x1234, TargetPaddingFE = 0x5678 };
        NativeBattleCameraState changed = NativeBattleQuezacotlLaterCamera.Orbit6C3AEC(camera, localFrame, 3690);
        short angle = (short)(NativeBattleCameraMath.Sine(localFrame * 4096 / 40) >> 7);
        (int x, int y, int z) = NativeBattleGfMatrices.Rotate56C3D0(NativeBattleGfMatrices.Euler701310(new(0, angle, 0)), new(-4000, -4000, 4000));
        Assert.Equal(new NativeBattleCameraPoint(unchecked((short)x), unchecked((short)y), unchecked((short)(z + 3690))), changed.CurrentPose!.Eye);
        Assert.Equal((short)-32764, changed.Roll);
        Assert.Equal(camera.CurrentPose.Target, changed.CurrentPose.Target);
        Assert.Equal(camera.EyePaddingF6, changed.EyePaddingF6);
        Assert.Equal(camera.TargetPaddingFE, changed.TargetPaddingFE);
        Assert.Equal(camera.Projection, changed.Projection);
    }

    [Fact]
    public void Phase276ReadsActualCapturedRollAndMissingOwnerStaysStrict()
    {
        NativeBattleCameraState camera = NativeBattleCameraState.FromColdProcess() with { FocalLength = 288, Roll = 123, CapturedPose = new(new(1, 2, 3), new(4, 5, 6), 216, -9), EyePaddingF6 = 7, TargetPaddingFE = 8 };
        NativeBattleCameraState changed = NativeBattleQuezacotlLaterCamera.Apply6C4F80(camera, 276, 3690);
        Assert.Equal((short)-9, changed.Roll);
        Assert.Equal(new NativeBattleCameraPoint(-430, -4188, 4866), changed.CurrentPose!.Eye);
        Assert.Equal(new NativeBattleCameraPoint(-17, -5387, 3994), changed.CurrentPose.Target);
        Assert.Equal(camera.EyePaddingF6, changed.EyePaddingF6);
        Assert.Equal(camera.TargetPaddingFE, changed.TargetPaddingFE);
        Assert.Throws<NotSupportedException>(() => NativeBattleQuezacotlLaterCamera.Apply6C4F80(camera with { CapturedPose = null }, 276, 3690));
    }
}
