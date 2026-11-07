using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattlePilotCameraCompatibilityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Coincident_or_vertical_native_poses_do_not_supply_an_interpolatable_basis(bool vertical)
    {
        NativeBattleCameraPose valid = new(new(0, 0, -1000), new(0, 0, 0), 200, 0);
        NativeBattleCameraPose singular = new(new(50, 20, 30), new(50, checked((short)(vertical ? 120 : 20)), 30), 200, 0);
        Assert.False(NativeBattlePilotVisualGeometry.CanSampleProjection(valid, singular));
        Assert.False(NativeBattlePilotVisualGeometry.CanSampleProjection(singular, valid));
        NativeBattleCameraPose restored = JsonSerializer.Deserialize<NativeBattleCameraPose>(JsonSerializer.Serialize(singular))!;
        Assert.False(NativeBattlePilotVisualGeometry.CanSampleProjection(valid, restored));
        NativeBattleCameraProjection earlier = new(NativeBattlePoseMatrices.CameraView(valid), 200, 160, 108);
        NativeBattleCameraProjection later = new(NativeBattlePoseMatrices.CameraView(singular), 200, 160, 108);
        Assert.Same(earlier, NativeBattlePilotVisualGeometry.SampleProjection(earlier, later, valid, singular, 0));
        Assert.Same(later, NativeBattlePilotVisualGeometry.SampleProjection(earlier, later, valid, singular, 1));
    }

    [Fact]
    public void Valid_camera_motion_keeps_the_existing_fractional_projection()
    {
        NativeBattleCameraPose a = new(new(0, 0, -1000), new(0, 0, 0), 200, 0);
        NativeBattleCameraPose b = a with { Eye = new(200, 0, -1000), Roll = 64 };
        Assert.True(NativeBattlePilotVisualGeometry.CanSampleProjection(a, b));
        NativeBattleCameraProjection earlier = new(NativeBattlePoseMatrices.CameraView(a), 200, 160, 108);
        NativeBattleCameraProjection later = new(NativeBattlePoseMatrices.CameraView(b), 200, 160, 108);
        foreach (float alpha in new[] { 0.25f, 0.5f, 0.75f })
        {
            NativeBattleCameraProjection sample = NativeBattlePilotVisualGeometry.SampleProjection(earlier, later, a, b, alpha);
            Assert.NotEqual(earlier, sample);
            Assert.NotEqual(later, sample);
        }
    }
}
