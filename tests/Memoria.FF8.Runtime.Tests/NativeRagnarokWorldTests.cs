using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeRagnarokWorldTests
{
    private static NativeRagnarokMotorState Fresh => new(0, 0, 0, 0, -120, 0, -251, 0, 0);

    [Fact]
    public void DigitalInputUsesNativeDistinctThrottleSteeringVerticalAndOpposingPriorities()
    {
        Assert.Equal(new NativeRagnarokInputPacket(-127, 127, 127), NativeRagnarokWorldInput.ReadDigitalButtons(0x80 | 0x2000 | 0x1000));
        Assert.Equal(new NativeRagnarokInputPacket(127, -127, -127), NativeRagnarokWorldInput.ReadDigitalButtons(0x10 | 0x8000 | 0x4000));
        Assert.Equal(new NativeRagnarokInputPacket(127, -127, 127), NativeRagnarokWorldInput.ReadDigitalButtons(0xF090));
        Assert.Equal(default, NativeRagnarokWorldInput.ReadDigitalButtons(0x42));
        Assert.Throws<NotSupportedException>(() => NativeRagnarokWorldInput.ReadDigitalButtons(0, scriptMode: 13));
        Assert.Throws<NotSupportedException>(() => NativeRagnarokWorldInput.ReadDigitalButtons(0, variant: 1));
    }

    [Fact]
    public void OrdinaryMotorPreservesRawSeamSteeringFullTurnAndRagnarokRoll()
    {
        NativeRagnarokMotorResult first = NativeRagnarokMotor.Step(Fresh, 127, 127, 0);
        Assert.Equal((ushort)4064, first.State.BodyYaw);
        Assert.Equal((short)63, first.State.BodyRoll);
        Assert.Equal(15, first.State.Speed);
        Assert.Equal((ushort)0, first.State.CameraYaw);
        NativeRagnarokMotorResult second = NativeRagnarokMotor.Step(first.State, 127, 127, 0);
        Assert.Equal((ushort)4048, second.State.BodyYaw);
        Assert.Equal((short)126, second.State.BodyRoll);
        Assert.Equal(30, second.State.Speed);
        Assert.Equal((short)256, NativeRagnarokMotor.Step(Fresh with { BodyRoll = 250 }, 127, 0, 0).State.BodyRoll);
        Assert.Equal((short)-256, NativeRagnarokMotor.Step(Fresh with { BodyRoll = -250 }, -127, 0, 0).State.BodyRoll);
    }

    [Fact]
    public void ThrottleCapUsesSignedShiftBeforeAbsoluteAndReleaseHasNoTurnCap()
    {
        Assert.Equal(200, NativeRagnarokMotor.Step(Fresh with { Speed = 200 }, 0, 127, 0).State.Speed);
        Assert.Equal(199, NativeRagnarokMotor.Step(Fresh with { Speed = 200, CameraYaw = 1 }, 0, 127, 0).State.Speed);
        Assert.Equal(200, NativeRagnarokMotor.Step(Fresh with { Speed = 200, BodyYaw = 1 }, 0, 127, 0).State.Speed);
        Assert.Equal(184, NativeRagnarokMotor.Step(Fresh with { Speed = 200, CameraYaw = 1024 }, 0, 0, 0).State.Speed);
        Assert.Equal(-15, NativeRagnarokMotor.Step(Fresh, 0, -127, 0).State.Speed);
        Assert.Equal(0, NativeRagnarokMotor.Step(Fresh with { Speed = -16 }, 0, 0, 0).State.Speed);
    }

    [Fact]
    public void VerticalControlUsesLoader120ScaleSignedShiftsAndIndependentPitch()
    {
        NativeRagnarokMotorState up = NativeRagnarokMotor.Step(Fresh with { BodyPitch = 61 }, 0, 0, 127).State;
        Assert.Equal((short)-61, up.FlightHeight);
        Assert.Equal((short)-29, up.VerticalBias204DADC);
        Assert.Equal((short)61, up.BodyPitch);
        Assert.Equal((short)-254, up.CameraPitch);
        NativeRagnarokMotorState down = NativeRagnarokMotor.Step(Fresh, 0, 0, -127).State;
        Assert.Equal((short)-180, down.FlightHeight);
        Assert.Equal((short)30, down.VerticalBias204DADC);
        Assert.Equal((short)-249, down.CameraPitch);
        Assert.Equal((short)-4096, NativeRagnarokMotor.Step(Fresh with { VerticalBias204DADC = -4090 }, 0, 0, 127).State.VerticalBias204DADC);
        Assert.Equal((short)4096, NativeRagnarokMotor.Step(Fresh with { VerticalBias204DADC = 4090 }, 0, 0, -127).State.VerticalBias204DADC);
        Assert.Equal(unchecked((short)(short.MaxValue + 59)), NativeRagnarokMotor.Step(Fresh with { FlightHeight = short.MaxValue }, 0, 0, 127).State.FlightHeight);
    }

    [Fact]
    public void ReleaseDampsThreeOwnersAtTheirDistinctNativeRates()
    {
        NativeRagnarokMotorState after = NativeRagnarokMotor.Step(Fresh with { BodyRoll = -33, BodyPitch = 19, VerticalBias204DADC = -16 }, 0, 0, 0).State;
        Assert.Equal((short)-1, after.BodyRoll);
        Assert.Equal((short)3, after.BodyPitch);
        Assert.Equal((short)-1, after.VerticalBias204DADC);
        Assert.Equal((short)-219, NativeRagnarokMotor.Step(Fresh with { FlightHeight = -3584 }, 0, 0, 0).State.CameraPitch);
    }

    [Fact]
    public void NativeYawVectorIsPublishedBeforeCameraFollowAndSnapshotPreservesNextStep()
    {
        NativeRagnarokMotorState before = Fresh with { Speed = 64, BodyYaw = 1024, BodyRoll = 112, FlightHeight = -800, CameraFollowSpeed = 64, VerticalBias204DADC = 128 };
        NativeRagnarokMotorResult first = NativeRagnarokMotor.Step(before, 0, 127, 0);
        // The1024 camera lag caps the otherwise79 throttle result to72.
        Assert.Equal(72, first.MotionX);
        Assert.Equal(0, first.MotionGraphicsZ);
        Assert.Equal((ushort)9, first.State.CameraYaw);
        string json = JsonSerializer.Serialize(first.State);
        NativeRagnarokMotorState restored = JsonSerializer.Deserialize<NativeRagnarokMotorState>(json)!;
        restored.Validate();
        Assert.Equal(json, JsonSerializer.Serialize(restored));
        Assert.Equal(NativeRagnarokMotor.Step(first.State, -127, -127, -127), NativeRagnarokMotor.Step(restored, -127, -127, -127));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeRagnarokMotorState>("{}"));
        Assert.Throws<NotSupportedException>(() => NativeRagnarokMotor.Step(before, 0, 0, 0, scriptMode: 6));
        Assert.Throws<NotSupportedException>(() => NativeRagnarokMotor.Step(before, 0, 0, 0, variant: 1));
    }
}
