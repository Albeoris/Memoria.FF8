using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class EncounterCameraPresentationTests
{
    [Fact]
    public void ElevationRaisesTheEyeWhilePreservingTargetDistanceAndNativePose()
    {
        NativeBattleCameraPose source = new(new(3000, -1600, 6000), new(0, -500, -2000), 512, 0);
        NativeBattleCameraPose raised = EncounterCameraPresentation.Elevate(source, 12);
        Assert.True(raised.Eye.Y < source.Eye.Y);
        Assert.Equal(source.Target, raised.Target);
        Assert.Equal(source.FocalLength, raised.FocalLength);
        Assert.InRange(Math.Abs(Distance(raised) - Distance(source)), 0, 1);
        Assert.Equal(new NativeBattleCameraPoint(3000, -1600, 6000), source.Eye);
        Assert.Same(source, EncounterCameraPresentation.Elevate(source, 0));
    }

    [Fact]
    public void RepeatedPublicationAndJsonRestoreNeverAccumulateTheAuthoredPitch()
    {
        NativeBattleCameraPose source = new(new(3000, -1600, 6000), new(0, -500, -2000), 512, 0);
        NativeBattleCameraState state = NativeBattleCameraState.FromColdProcess() with { CurrentPose = source, FocalLength = 512 };
        NativeBattleCameraState first = state.PublishProjection(4, 12);
        NativeBattleCameraState repeated = first.PublishProjection(4, 12);
        NativeBattleCameraState restored = JsonSerializer.Deserialize<NativeBattleCameraState>(JsonSerializer.Serialize(first))!.PublishProjection(4, 12);
        Assert.Equal(source, repeated.CurrentPose);
        Assert.Equal(JsonSerializer.Serialize(first.Projection), JsonSerializer.Serialize(repeated.Projection));
        Assert.Equal(JsonSerializer.Serialize(first.Projection), JsonSerializer.Serialize(restored.Projection));
        Assert.Equal(JsonSerializer.Serialize(state.PublishProjection(4).Projection), JsonSerializer.Serialize(first.PublishProjection(4, 0).Projection));
        Assert.Equal(NativeBattlePoseMatrices.CameraView(EncounterCameraPresentation.Elevate(source, 12)).Rotation, first.Projection!.View!.Rotation);
    }

    [Theory]
    [InlineData(-46)]
    [InlineData(46)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void UnsupportedElevationsFailBeforePublication(double degrees)
    {
        NativeBattleCameraPose pose = new(new(0, -1000, 6000), new(0, 0, 0), 512, 0);
        Assert.Throws<InvalidDataException>(() => EncounterCameraPresentation.Elevate(pose, degrees));
    }

    private static double Distance(NativeBattleCameraPose pose)
    {
        double x = pose.Eye.X - pose.Target.X;
        double y = pose.Eye.Y - pose.Target.Y;
        double z = pose.Eye.Z - pose.Target.Z;
        return Math.Sqrt(x * x + y * y + z * z);
    }
}
