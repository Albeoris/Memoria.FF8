using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleStageCameraCompletionTests
{
    [Theory]
    [InlineData(0u, false)]
    [InlineData(0u, true)]
    [InlineData(0x101u, false)]
    [InlineData(0x101u, true)]
    public void CompletedStageCameraBlendRestoresCapturedRegistersWithoutSelectedTrackAndConsumesOnce(uint flags, bool forced)
    {
        NativeBattleCameraPose captured = new(new(10, 20, 30), new(40, 50, 60), 512, 17);
        NativeBattleCameraState camera = NativeBattleCameraState.Initialize() with
        {
            CapturedPose = captured,
            CurrentPose = new(new(-1, -2, -3), new(-4, -5, -6), 216, -7),
            FocalLength = 216,
            Roll = -7,
            CapturedEyePadding806 = 91,
            CapturedTargetPadding80E = 92,
            ForceCamera = forced
        };
        camera = JsonSerializer.Deserialize<NativeBattleCameraState>(JsonSerializer.Serialize(camera))!;
        Assert.Null(camera.SelectedSlot);
        camera = camera.Tick(flags);
        Assert.Equal((ushort)0, camera.BlendProgress);
        Assert.Equal(captured, camera.CurrentPose);
        Assert.Equal((short)512, camera.FocalLength);
        Assert.Equal((short)17, camera.Roll);
        Assert.Equal((short)91, camera.EyePaddingF6);
        Assert.Equal((short)92, camera.TargetPaddingFE);
        Assert.Equal(forced, camera.ForceCamera);
        Assert.Equal((ulong)0, camera.PoseCaptureCount);
        NativeBattleCameraPose wobble = captured with { Eye = captured.Eye with { X = 99, Y = 88 } };
        camera = camera with { CurrentPose = wobble };
        camera = JsonSerializer.Deserialize<NativeBattleCameraState>(JsonSerializer.Serialize(camera))!.Tick(flags);
        Assert.Equal(wobble, camera.CurrentPose);
        Assert.Equal(captured, camera.CapturedPose);
    }

    [Fact]
    public void CompletedBlendDoesNotRequireTrackButFractionalBlendUsesOriginalTrackAndCapturedLens()
    {
        NativeBattleCameraPose captured = new(new(1000, 2000, 3000), new(4000, 5000, 6000), 800, 0);
        NativeBattleCameraTrackPose track = new(0, [], [], 200, 200, 0, 0, new(0, 0, 0), new(2000, 3000, 4000));
        NativeBattleCameraState fractional = NativeBattleCameraState.Initialize() with { BlendProgress = 2048, CapturedPose = captured, SelectedSlot = 0, RetainedPoses = [track, null] };
        fractional = fractional.Tick(0);
        Assert.Equal(new NativeBattleCameraPoint(500, 1000, 1500), fractional.CurrentPose!.Eye);
        Assert.Equal(new NativeBattleCameraPoint(3000, 4000, 5000), fractional.CurrentPose.Target);
        Assert.Equal((short)500, fractional.FocalLength);
        Assert.Equal((ushort)2048, fractional.BlendProgress);
        NativeBattleCameraState completed = (fractional with { BlendProgress = 4096 }).Tick(0);
        Assert.Equal(captured, completed.CurrentPose);
        Assert.Equal((ushort)0, completed.BlendProgress);
        NativeBattleCameraPose wobble = captured with { Eye = captured.Eye with { X = 1020 } };
        completed = (completed with { CurrentPose = wobble }).Tick(0);
        Assert.Equal(wobble, completed.CurrentPose);
    }

    [Fact]
    public void MissingCapturedOrFractionalTrackOwnerRemainsUnsupported()
    {
        Assert.Throws<NotSupportedException>(() => NativeBattleCameraState.Initialize().Tick(0));
        NativeBattleCameraState fractional = NativeBattleCameraState.Initialize() with { BlendProgress = 2048, CapturedPose = new(new(0, 0, 0), new(0, 0, 0), 0, 0) };
        Assert.Throws<NotSupportedException>(() => fractional.Tick(0));
        Assert.Null(NativeBattleCameraState.Initialize5041E0(null).CapturedPose);
    }

    [Fact]
    public void BattleResetRetainsCapturedPoseAndPaddingUntilCompletedBlendConsumesThem()
    {
        NativeBattleCameraPose captured = new(new(1, 2, 3), new(4, 5, 6), 777, 19);
        NativeBattleCameraState retained = NativeBattleCameraState.FromColdProcess() with { CapturedPose = captured, CapturedEyePadding806 = 51, CapturedTargetPadding80E = 52 };
        NativeBattleCameraState reset = NativeBattleCameraState.Initialize5041E0(retained);
        Assert.Equal(captured, reset.CapturedPose);
        Assert.Equal((short)0, reset.Roll);
        reset = JsonSerializer.Deserialize<NativeBattleCameraState>(JsonSerializer.Serialize(reset))!.Tick(0);
        Assert.Equal(captured, reset.CurrentPose);
        Assert.Equal((short)51, reset.EyePaddingF6);
        Assert.Equal((short)52, reset.TargetPaddingFE);
        Assert.Equal((ushort)0, reset.BlendProgress);
    }

    [Fact]
    public void ColdCapturedStaticVectorsReachFirstCameraPublicationWithoutInventingATrack()
    {
        NativeBattleCameraState cold = NativeBattleCameraState.FromColdProcess();
        Assert.NotNull(cold.CapturedPose);
        Assert.Equal(cold.CurrentPose, cold.CapturedPose);
        NativeBattleCameraState camera = NativeBattleCameraState.Initialize5041E0(cold).Tick(0);
        Assert.Equal(cold.CapturedPose, camera.CurrentPose);
        Assert.Null(camera.SelectedSlot);
        Assert.Empty(camera.Order);
        Assert.Equal((ushort)0, camera.BlendProgress);
    }
}
