using System.Numerics;

namespace Memoria.FF7.Formats;

public sealed record Ff7BattleBraverPoint(short X, short Y, short Z)
{
    public Vector3 Position => new(X, Y, Z);

    internal static Ff7BattleBraverPoint At(Vector3 point)
    {
        return new((short)point.X, (short)point.Y, (short)point.Z);
    }
}

public sealed record Ff7BattleBraverTrail(Ff7BattleBraverPoint Base, Ff7BattleBraverPoint Tip);
public sealed record Ff7BattleBraverImpact(Ff7BattleBraverPoint Anchor, int CollisionRadius);
public sealed record Ff7BattleBraverState(int Target, int Frame = -1, Ff7BattleBraverPoint? Shine = null, Ff7BattleBraverTrail? Trail = null, Ff7BattleBraverTrail? PreviousTrail = null, Ff7BattleBraverImpact? Impact = null)
{
    public bool Completed => Frame >= 37;
}

public sealed record Ff7BattleBraverUpdate(Ff7BattleBraverState State, ushort ImpactTargets);

public static class Ff7BattleBraverEffect
{
    public static Ff7BattleBraverState Start(ushort targetMask)
    {
        if (targetMask == 0 || (targetMask & ~0x03F7) != 0 || (targetMask & (targetMask - 1)) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(targetMask));
        }
        return new(BitOperations.TrailingZeroCount(targetMask));
    }

    public static Ff7BattleBraverUpdate Advance(Ff7BattleBraverState state, Func<Vector3, Vector3> weaponPoint, Func<int, Vector3> targetAnchor, Func<int, int> collisionRadius)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(weaponPoint);
        ArgumentNullException.ThrowIfNull(targetAnchor);
        ArgumentNullException.ThrowIfNull(collisionRadius);
        if (state.Completed)
        {
            return new(state, 0);
        }
        int frame = state.Frame + 1;
        Ff7BattleBraverPoint? shine = frame is >= 10 and < 20 ? Ff7BattleBraverPoint.At(weaponPoint(new(0, 0, -400))) : null;
        Ff7BattleBraverTrail? trail = null;
        if (frame is >= 17 and < 37)
        {
            trail = new(Ff7BattleBraverPoint.At(weaponPoint(Vector3.Zero)), Ff7BattleBraverPoint.At(weaponPoint(new(0, 0, -800))));
        }
        Ff7BattleBraverImpact? impact = frame switch
        {
            26 => new(Ff7BattleBraverPoint.At(targetAnchor(state.Target)), collisionRadius(state.Target)),
            < 33 => state.Impact,
            _ => null
        };
        return new(new(state.Target, frame, shine, trail, state.Trail, impact), frame == 26 ? (ushort)(1 << state.Target) : (ushort)0);
    }
}
