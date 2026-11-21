using System.Numerics;

namespace Memoria.FF7.Runtime;

internal sealed record Ff7SnowboardRiderMotionState
{
    internal uint Flags { get; set; }
    internal short AirFrames { get; set; }
    internal short Speed { get; set; }
    internal short Action { get; set; }
    internal int ResistanceShift { get; set; }
    internal Vector3 Position { get; set; }
    internal Vector3 Velocity { get; set; }
    internal Vector3 Direction { get; set; }
    internal Vector3 Force { get; set; }
    internal float Gravity { get; set; }
    internal float FloorHeight { get; set; }
}

internal static class Ff7SnowboardRiderMotion
{
    internal static void AdvanceHorizontal(Ff7SnowboardRiderMotionState rider, byte surfaceFlags)
    {
        if ((rider.Flags & 1) == 0)
        {
            Vector3 velocity = rider.Velocity;
            if (rider.AirFrames < 1)
            {
                float scale = 1f / (1 << rider.ResistanceShift);
                float resistance = 1f - rider.Speed / 2048f;
                float forceX = rider.Force.X * scale;
                float forceZ = rider.Force.Z * scale;
                float changeX = (forceX >= 0) == (velocity.X >= 0) ? resistance * forceX : forceX;
                float changeZ = (forceZ >= 0) == (velocity.Z >= 0) ? resistance * forceZ : forceZ;
                velocity.X += changeX;
                velocity.Z += changeZ;
                if ((rider.Action & 255) is < 3 or > 6 && (surfaceFlags & 8) != 0)
                {
                    velocity.Y = rider.Speed;
                    rider.Gravity = 0;
                }
            }
            else
            {
                int shift = Math.Max(6, 16 - (rider.AirFrames >> 2));
                float resistance = 1f / (1 << shift);
                velocity.X -= velocity.X * resistance;
                velocity.Z -= velocity.Z * resistance;
            }
            double length = Math.Sqrt((double)velocity.X * velocity.X + (double)velocity.Z * velocity.Z);
            float inverse = (float)(1d / length);
            rider.Direction = new(velocity.X * inverse, 0, velocity.Z * inverse);
            rider.Velocity = velocity;
            rider.Position = new(rider.Position.X + velocity.X, rider.Position.Y, rider.Position.Z + velocity.Z);
        }
        float x = rider.Velocity.X * (1f / 256);
        float z = rider.Velocity.Z * (1f / 256);
        rider.Speed = unchecked((short)(int)Math.Sqrt((double)x * x + (double)z * z));
    }

    internal static bool AdvanceVertical(Ff7SnowboardRiderMotionState rider)
    {
        Vector3 velocity = rider.Velocity;
        velocity.Y += rider.Gravity;
        Vector3 position = rider.Position;
        position.Y += velocity.Y;
        bool landed = position.Y > rider.FloorHeight;
        if (landed)
        {
            position.Y = rider.FloorHeight;
            rider.Gravity = velocity.Y;
            velocity.Y = 0;
            rider.AirFrames = 0;
            rider.Gravity = rider.Gravity > 4096 ? rider.Gravity - 4096 : 4096;
        }
        else
        {
            rider.AirFrames = unchecked((short)(rider.AirFrames + 1));
            rider.Gravity = 4096;
        }
        rider.Position = position;
        rider.Velocity = velocity;
        return landed;
    }
}
