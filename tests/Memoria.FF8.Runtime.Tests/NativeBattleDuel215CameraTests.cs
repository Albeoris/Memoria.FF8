namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDuel215CameraTests
{
    [Fact]
    public void OpeningWritesNativeFocalRollAndIndependentSixWordCameraEndpoints()
    {
        NativeBattleCameraState camera = NativeBattleCameraState.FromColdProcess();
        NativeBattleCameraState first = NativeBattleDuel215Controller.AdvanceCamera69B070(camera, 0, 100, 200, 300, 400);
        Assert.True(first.ForceCamera);
        Assert.Equal(0, first.Mask);
        Assert.Equal(new NativeBattleCameraPose(new(-18, -591, -568), new(497, -572, -4944), 288, -292), first.CurrentPose);
        NativeBattleCameraState last = NativeBattleDuel215Controller.AdvanceCamera69B070(first, 23, 100, 200, 300, 400);
        Assert.Equal(new NativeBattleCameraPose(new(-37, -794, 64), new(-21, -767, -555), 288, 0), last.CurrentPose);
        Assert.Same(camera.Projection, last.Projection);
    }

    [Fact]
    public void LateCameraUsesTheActualPriorRollAcrossFourNativeInterpolationTicks()
    {
        NativeBattleCameraState camera = NativeBattleDuel215Controller.AdvanceCamera69B070(NativeBattleCameraState.FromColdProcess(), 163, 100, 200, 300, 400);
        Assert.Equal(new NativeBattleCameraPose(new(39, -1707, 1836), new(330, -543, -4309), camera.FocalLength, 512), camera.CurrentPose);
        for (short tick = 221; tick <= 224; tick++)
        {
            camera = NativeBattleDuel215Controller.AdvanceCamera69B070(camera, tick, 100, 200, 300, 400);
            Assert.Equal((short)((224 - tick) * 128), camera.Roll);
        }
        Assert.Equal(new NativeBattleCameraPose(new(506, -1002, -2745), new(1159, 60, -5189), camera.FocalLength, 0), camera.CurrentPose);
    }
}
