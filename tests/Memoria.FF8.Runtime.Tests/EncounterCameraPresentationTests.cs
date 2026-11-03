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

    [Fact]
    public void CastFramingIncludesSeparatedRecipientsBehindTheCasterCameraWithoutChangingNativeState()
    {
        NativeBattleCameraPose source = new(new(110, -1257, -2452), new(-1, -912, -2703), 512, 0);
        NativeBattleVector[] points = [new(-1900, -1500, -3200), new(1900, 0, -2200), new(-1700, -1200, 2600), new(1700, 0, 2600)];
        NativeBattleCameraPose framed = EncounterCameraPresentation.FrameTargets(source, points, 512);
        Assert.NotEqual(source.Eye, framed.Eye);
        Assert.Equal(source.Target, framed.Target);
        Assert.Equal(source.FocalLength, framed.FocalLength);
        NativeBattleCameraState state = NativeBattleCameraState.FromColdProcess() with { CurrentPose = source, FocalLength = 512, CapturedPose = source };
        NativeBattleCameraState published = state.PublishProjection(4, presentationPose: framed);
        Assert.Equal(framed, published.PublishedPresentationPose);
        Assert.Equal(source, published.CurrentPose);
        Assert.Equal(source, published.CapturedPose);
        NativeBattleCameraState restored = JsonSerializer.Deserialize<NativeBattleCameraState>(JsonSerializer.Serialize(published))!;
        Assert.Equal(JsonSerializer.Serialize(published), JsonSerializer.Serialize(restored.PublishProjection(4, presentationPose: EncounterCameraPresentation.FrameTargets(source, points, 512))));
        NativeBattleActionMatrix view = published.Projection!.View!;
        foreach (NativeBattleVector point in points)
        {
            double Row(int row) => (view.Rotation[row * 3] * (double)point.X + view.Rotation[row * 3 + 1] * (double)point.Y + view.Rotation[row * 3 + 2] * (double)point.Z) / 4096;
            double z = Row(2) + view.Z;
            Assert.True(z > 0);
            Assert.InRange(160 + 512 * (Row(0) + view.X) / z, 1, 319);
            Assert.InRange(108 + 512 * (Row(1) + view.Y) / z, 1, 215);
        }
    }

    [Fact]
    public void TargetsAlreadyInsideTheNativeFramePreserveTheExactOriginalShot()
    {
        NativeBattleCameraPose source = new(new(0, -500, 8000), new(0, -500, 0), 512, 0);
        Assert.Same(source, EncounterCameraPresentation.FrameTargets(source, [new(-500, -1000, 0), new(500, 0, 0)], 512));
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
