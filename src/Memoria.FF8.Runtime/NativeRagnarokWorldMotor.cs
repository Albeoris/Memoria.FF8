using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Retained557A90 entity50/variant0 scalar owners for ordinary and full-map callbacks. Ground/contact publication is separate.</summary>
public sealed record NativeRagnarokMotorState([property: JsonRequired] int Speed, [property: JsonRequired] ushort BodyYaw, [property: JsonRequired] short BodyPitch, [property: JsonRequired] short BodyRoll, [property: JsonRequired] short FlightHeight, [property: JsonRequired] ushort CameraYaw, [property: JsonRequired] short CameraPitch, [property: JsonRequired] short CameraFollowSpeed, [property: JsonRequired] short? VerticalBias204DADC)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeWorldVerticalBiasState? VerticalBiasAuthority { get; init; }

    public void Validate()
    {
        if (Speed is < -200 or > 200 || BodyYaw >= 4096 || CameraYaw >= 4096 || BodyRoll is < -256 or > 256 || CameraFollowSpeed is < 0 or > 128 || VerticalBias204DADC is null && VerticalBiasAuthority is null || VerticalBias204DADC is not null && VerticalBiasAuthority is not null)
        {
            throw new InvalidDataException("Ragnarok motor lost its native scalar widths, ordinary speed or retained camera/vertical owners.");
        }
        VerticalBiasAuthority?.Validate();
    }
}

public readonly record struct NativeRagnarokMotorResult(NativeRagnarokMotorState State, int MotionX, int MotionGraphicsZ);

public static class NativeRagnarokMotor
{
    public const int LoaderVerticalScale = 120;

    public static NativeRagnarokMotorResult StepScripted(NativeRagnarokMotorState before, byte scriptMode, int verticalScale = LoaderVerticalScale)
    {
        before.Validate();
        if (scriptMode is not (5 or 6 or 14))
        {
            throw new ArgumentOutOfRangeException(nameof(scriptMode));
        }
        if (verticalScale is not (16 or 120))
        {
            throw new ArgumentOutOfRangeException(nameof(verticalScale));
        }
        int speed = Math.Abs(before.Speed) <= 16 ? 0 : before.Speed + (before.Speed > 0 ? -16 : 16);
        NativeWorldVerticalBiasState bias = (before.VerticalBiasAuthority ?? NativeWorldVerticalBiasState.Exact(before.VerticalBias204DADC!.Value)).Damp(verticalScale >> 3, verticalScale >> 3, 1);
        NativeRagnarokMotorState after = before with { Speed = speed, BodyPitch = ApproachZero(before.BodyPitch, 16), BodyRoll = ApproachZero(before.BodyRoll, 32), VerticalBias204DADC = bias.IsExact ? bias.Minimum : null, VerticalBiasAuthority = bias.IsExact ? null : bias };
        (int x, int z) = NativeGardenTransform.Offset(unchecked((short)speed), unchecked((short)before.BodyYaw));
        return new(after, x, z);
    }

    public static NativeRagnarokMotorResult Step(NativeRagnarokMotorState before, sbyte steering, sbyte throttle, sbyte vertical, byte variant = 0, byte scriptMode = 0)
    {
        before.Validate();
        if (variant != 0 || scriptMode is not (0 or 12))
        {
            throw new NotSupportedException("Ragnarok motor requires the ordinary variant0 or full-map script12 branch; scripted transfer and alternate camera require their own owners.");
        }
        short yaw = unchecked((short)before.BodyYaw);
        short roll = before.BodyRoll;
        if (steering != 0)
        {
            int shift = Math.Abs((short)before.CameraYaw - yaw) > 511 ? 3 : 2;
            yaw = unchecked((short)(yaw + (-steering >> shift)));
            int addition = steering >> 1;
            if (Math.Abs(roll + addition) > 256)
            {
                addition = (roll < 0 ? -256 : 256) - roll;
            }
            roll = unchecked((short)(roll + addition));
        }
        else
        {
            roll = ApproachZero(roll, 32);
        }
        int speed;
        if (throttle == 0)
        {
            speed = Math.Abs(before.Speed) <= 16 ? 0 : before.Speed + (before.Speed > 0 ? -16 : 16);
        }
        else
        {
            speed = before.Speed + throttle / 8;
            // Native arithmetic-shifts the signed shortest yaw delta before abs;
            // a small negative difference therefore reduces the cap by one.
            int cap = scriptMode == 0 ? 200 - Math.Abs(NativeGardenMotor.Delta(Wrap(yaw), before.CameraYaw) >> 3) : 200;
            if (speed < -cap)
            {
                speed = -cap;
            }
            else if (speed > cap)
            {
                speed = cap;
            }
        }
        //557A90 constructs its yaw-only short vector before vertical/camera updates.
        (int motionX, int motionZ) = NativeGardenTransform.Offset(unchecked((short)speed), yaw);
        short height = before.FlightHeight;
        short pitch = before.BodyPitch;
        NativeWorldVerticalBiasState bias = (before.VerticalBiasAuthority ?? NativeWorldVerticalBiasState.Exact(before.VerticalBias204DADC!.Value)).StepRagnarok(vertical);
        if (vertical != 0)
        {
            int product = LoaderVerticalScale * vertical;
            height = unchecked((short)(height + (product >> 8)));
        }
        else
        {
            pitch = ApproachZero(pitch, 16);
        }
        ushort bodyYaw = (ushort)Wrap(yaw);
        int delta = NativeGardenMotor.Delta(bodyYaw, before.CameraYaw);
        short follow = unchecked((short)(before.CameraFollowSpeed + (Math.Abs(delta) >= 64 ? 8 : -2)));
        int maximum = Math.Min(128, Math.Abs(delta >> 1));
        follow = (short)Math.Clamp((int)follow, 0, maximum);
        ushort cameraYaw = (ushort)Wrap(before.CameraYaw + (delta > 0 ? follow >> 3 : -(follow >> 3)));
        //203FE48 high WORD is flight height, not the body pitch at203FE50 low WORD.
        short targetPitch = unchecked((short)(-256 - height / 24));
        short pitchDifference = unchecked((short)(targetPitch - before.CameraPitch));
        short cameraPitch = Math.Abs((int)pitchDifference) <= 32 ? targetPitch : unchecked((short)(before.CameraPitch + (pitchDifference > 0 ? 32 : -32)));
        if (scriptMode == 12)
        {
            follow = before.CameraFollowSpeed;
            cameraYaw = before.CameraYaw;
            cameraPitch = before.CameraPitch;
        }
        var after = new NativeRagnarokMotorState(speed, bodyYaw, pitch, roll, height, cameraYaw, cameraPitch, follow, bias.IsExact ? bias.Minimum : null) { VerticalBiasAuthority = bias.IsExact ? null : bias };
        after.Validate();
        return new NativeRagnarokMotorResult(after, motionX, motionZ);
    }

    private static short ApproachZero(short value, int step)
    {
        return (short)(Math.Abs((int)value) <= step ? 0 : value + (value > 0 ? -step : step));
    }

    private static int Wrap(int angle)
    {
        return (angle % 4096 + 4096) % 4096;
    }
}
