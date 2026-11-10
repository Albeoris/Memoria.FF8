using System.Numerics;

namespace Memoria.FF7.Runtime;

public readonly record struct Ff7BattleCameraPoint(short X, short Y, short Z)
{
    public Vector3 ToVector() => new(X, Y, Z);
}

public sealed record Ff7BattleCameraMotion(Ff7BattleCameraPoint Start, Ff7BattleCameraPoint Destination, short Remaining, bool Initialized = false, short Total = 0, short Elapsed = 0, Ff7BattleCameraPoint Speed = default);
public readonly record struct Ff7BattleCameraMotionUpdate(Ff7BattleCameraPoint Point, Ff7BattleCameraMotion? Motion);

public static class Ff7NativeBattleCameraMotion
{
    public static Ff7BattleCameraMotionUpdate Advance(Ff7BattleCameraMotion motion, Ff7BattleCameraPoint current, bool linear, Func<int, int> cosine)
    {
        ArgumentNullException.ThrowIfNull(motion);
        ArgumentNullException.ThrowIfNull(cosine);
        if (motion.Remaining <= 0)
        {
            throw new InvalidDataException("FFVII camera motion requires a positive native duration.");
        }

        if (!motion.Initialized)
        {
            motion = linear
                ? motion with { Initialized = true, Speed = new(Speed(motion.Start.X, motion.Destination.X, motion.Remaining), Speed(motion.Start.Y, motion.Destination.Y, motion.Remaining), Speed(motion.Start.Z, motion.Destination.Z, motion.Remaining)) }
                : motion with { Initialized = true, Total = motion.Remaining, Elapsed = 0 };
        }

        if (motion.Remaining == 1)
        {
            return new(motion.Destination, null);
        }

        Ff7BattleCameraPoint point;
        if (linear)
        {
            point = new(Step(current.X, motion.Speed.X), Step(current.Y, motion.Speed.Y), Step(current.Z, motion.Speed.Z));
        }
        else
        {
            if (motion.Total == 0)
            {
                throw new InvalidDataException("FFVII camera easing has no native initial duration.");
            }
            point = new(Ease(motion.Start.X, motion.Destination.X, motion.Total, motion.Elapsed, cosine), Ease(motion.Start.Y, motion.Destination.Y, motion.Total, motion.Elapsed, cosine), Ease(motion.Start.Z, motion.Destination.Z, motion.Total, motion.Elapsed, cosine));
            motion = motion with { Elapsed = unchecked((short)(motion.Elapsed + 1)) };
        }
        return new(point, motion with { Remaining = unchecked((short)(motion.Remaining - 1)) });
    }

    private static short Speed(short start, short destination, short duration)
    {
        int difference = destination - start;
        int square = unchecked(difference * difference);
        if (square < 0)
        {
            throw new NotSupportedException("FFVII camera motion with an overflowing native squared distance is unsupported.");
        }
        short magnitude = unchecked((short)(int)Math.Sqrt(square));
        int signed = difference < 0 ? -magnitude : magnitude;
        return unchecked((short)(((signed + 1) << 4) / duration));
    }

    private static short Step(short point, short speed) => unchecked((short)(((point << 4) + speed) >> 4));

    private static short Ease(short start, short destination, short duration, short elapsed, Func<int, int> cosine)
    {
        short angle = unchecked((short)(((elapsed << 11) / duration) + 0x800));
        int distance = destination - start;
        int product = unchecked((cosine(angle) + 0x1000) * distance);
        return unchecked((short)(start + product / 0x2000));
    }
}
