using System.Numerics;

namespace Memoria.FF7.Runtime;

internal static class Ff7SnowboardSteering
{
    internal static void Turn(Ff7SnowboardRiderMotionState rider, int amount)
    {
        if (rider.AirFrames > 24)
        {
            return;
        }
        int speedSquared = unchecked(rider.Speed * rider.Speed) >> 15;
        amount = unchecked((384 - speedSquared) * amount) / 384;
        if (rider.AirFrames > 8)
        {
            amount = unchecked(amount * (24 - rider.AirFrames)) >> 4;
        }
        float angle = (amount >> 2) * 0.0015339808305725455f;
        float sine = (float)Math.Sin(angle);
        float cosine = (float)Math.Cos(angle);
        float x = (float)((double)cosine * rider.Velocity.X + (double)sine * rider.Velocity.Z);
        float z = (float)(-(double)sine * rider.Velocity.X + (double)cosine * rider.Velocity.Z);
        int attenuation = unchecked(16384 - (unchecked(speedSquared * Math.Abs(amount)) >> 4)) >> 2;
        rider.Velocity = new((float)((double)attenuation * x / 4096), rider.Velocity.Y, (float)((double)attenuation * z / 4096));
    }

    internal static float FollowVelocityHeading(float heading, Ff7SnowboardRiderMotionState rider)
    {
        if (rider.Speed < 128)
        {
            return heading;
        }
        float target = (float)((double)(float)Math.Atan2(rider.Velocity.X, rider.Velocity.Z) + (float)Math.PI);
        return ApproachAngle(target, heading, 0.02454369328916073f);
    }

    internal static float ApproachAngle(float target, float current, float maximum)
    {
        float difference = Wrap(target) - Wrap(current);
        if (difference < 0)
        {
            difference += 6.2831854820251465f;
        }
        if (difference < (float)Math.PI)
        {
            if (difference > maximum)
            {
                difference = maximum;
            }
        }
        else
        {
            difference = -(6.2831854820251465f - difference);
            if (difference < -maximum)
            {
                difference = -maximum;
            }
        }
        return Wrap(current + difference);
    }

    internal static float Wrap(float angle)
    {
        if (!float.IsFinite(angle))
        {
            throw new InvalidDataException("The native snowboard heading is not finite.");
        }
        while (angle >= 6.2831854820251465f)
        {
            angle -= 6.2831854820251465f;
        }
        while (angle < 0)
        {
            angle += 6.2831854820251465f;
        }
        return angle;
    }
}
