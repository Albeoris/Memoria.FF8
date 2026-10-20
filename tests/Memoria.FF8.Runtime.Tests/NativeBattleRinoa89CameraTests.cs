namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRinoa89CameraTests
{
    [Fact]
    public void Camera_interpolation_uses_original_GTE_rounding_and_separate_roll_division()
    {
        NativeBattleCameraPose initial = new(new(0, 0, 0), new(0, 0, 0), 288, 0);
        NativeBattleCameraPose pose = NativeBattleRinoa89Camera.Interpolate6FC250(initial, new(-5, 7, 20), new(100, -100, 0), new(6, -8, 22), new(200, -200, 10), -5, 6, 1, 3);
        Assert.Equal(new NativeBattleCameraPoint(1, 0, 21), pose.Target);
        Assert.Equal(new NativeBattleCameraPoint(150, -150, 5), pose.Eye);
        Assert.Equal((short)0, pose.Roll);
        Assert.Equal((short)288, pose.FocalLength);
    }

    [Fact]
    public void Orbit_scales_vertical_difference_twice_in_native_store_order()
    {
        NativeBattleCameraPose pose = new(new(100, 100, -200), new(0, 20, 0), 288, 0);
        pose = NativeBattleRinoa89Camera.Orbit6FC300(pose, 0, 0, 2048);
        Assert.Equal(new NativeBattleCameraPoint(50, 40, -100), pose.Eye);
    }

    [Theory]
    [InlineData(176, 182)]
    [InlineData(181, 182)]
    public void Camera_late_unsigned_age_keeps_pre182_frames_at_the_start(int frame, int expectedFrame)
    {
        NativeBattleCameraPose pose = new(new(0, 0, 0), new(0, 0, 0), 288, 0);
        Assert.Equal(NativeBattleRinoa89Camera.Advance(pose, (short)expectedFrame, new(100, -200, 300), default), NativeBattleRinoa89Camera.Advance(pose, (short)frame, new(100, -200, 300), default));
    }
}
