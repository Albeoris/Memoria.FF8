using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeVehicleCameraTests
{
    [Theory]
    [InlineData(0u, 0.75f, 0.75f)]
    [InlineData(4u, 0.75f, -1f)]
    [InlineData(8u, -0.75f, 1f)]
    [InlineData(12u, 0.75f, 0f)]
    public void NativeCameraButtonPairOverridesAnalogAndCancelsWhenBothHeld(uint buttons, float analog, float expected)
    {
        Assert.Equal(expected, NativeVehicleCamera.ReadManualCameraDirection(buttons, analog));
    }

    [Theory]
    [InlineData(0, 0, 1, 1, 8)]
    [InlineData(0, 0, -1, 4095, -8)]
    [InlineData(4090, 128, 1, 10, 128)]
    [InlineData(0, -128, -1, 4080, -128)]
    [InlineData(0, -9, 0, 4095, -7)]
    [InlineData(100, 7, 0, 100, 0)]
    public void ManualYawRetainsSignedAccelerationClampAndArithmeticShift(int yaw, int velocity, int direction, int expectedYaw, int expectedVelocity)
    {
        Assert.Equal((expectedYaw, expectedVelocity), NativeVehicleCamera.Step(yaw, velocity, direction));
    }

    [Fact]
    public void ReboardCamera183UsesGroundVehicleTargetsAndDoesNotRearmFootCollisionPitchBit()
    {
        NativeFootCameraTransition transition = new(183, 640);
        var first = transition.StepWords(false, -512, -341, -7168, true, false);
        Assert.Equal(648, first.Transition.ProjectionDistance);
        Assert.Equal((short)-496, first.Pitch);
        Assert.Equal(17, first.Transition.Flags);
        var vehicleClamp = new NativeFootCameraTransition(16, 1024).StepWords(false, -120, -341, -7168, true, false);
        var footClamp = new NativeFootCameraTransition(16, 1024).StepWords(false, -120, -341, -7168, true, true);
        Assert.Equal(0, vehicleClamp.Transition.Flags);
        Assert.Equal(16, footClamp.Transition.Flags);
    }

    [Fact]
    public void AlternateGardenUsesQuarterRateSteeringWithoutChangingAutoFollowOwner()
    {
        NativeGardenMotorResult ordinary = NativeGardenMotor.Step(0, 1024, 0, 0, 24, 127, 127);
        NativeGardenMotorResult alternate = NativeGardenMotor.Step(0, 1024, 0, 0, 24, 127, 127, 1);
        Assert.Equal(4088, ordinary.BodyYaw);
        Assert.Equal(4080, alternate.BodyYaw);
        Assert.Equal(1024, alternate.CameraYaw);
        Assert.Equal(24, alternate.CameraFollowSpeed);
        Assert.Equal(ordinary.Speed, alternate.Speed);
    }

    [Fact]
    public void AlternateRagnarokRemovesYawSpeedCapWhileRetainingHeightPitchTrackingAndRestoresExactly()
    {
        NativeRagnarokMotorState before = new(195, 0, 0, 0, -120, 1024, -200, 24, 0);
        NativeRagnarokMotorResult ordinary = NativeRagnarokMotor.Step(before, 127, 127, 127);
        NativeRagnarokMotorResult alternate = NativeRagnarokMotor.Step(before, 127, 127, 127, 1);
        Assert.Equal(4080, ordinary.State.BodyYaw);
        Assert.Equal(4064, alternate.State.BodyYaw);
        Assert.True(ordinary.State.Speed < alternate.State.Speed);
        Assert.Equal(200, alternate.State.Speed);
        Assert.Equal(before.CameraYaw, alternate.State.CameraYaw);
        Assert.Equal(before.CameraFollowSpeed, alternate.State.CameraFollowSpeed);
        Assert.Equal((short)-232, alternate.State.CameraPitch);
        NativeRagnarokMotorState restored = JsonSerializer.Deserialize<NativeRagnarokMotorState>(JsonSerializer.Serialize(alternate.State))!;
        Assert.Equal(NativeRagnarokMotor.Step(alternate.State, -127, 0, 0, 1), NativeRagnarokMotor.Step(restored, -127, 0, 0, 1));
    }
}
