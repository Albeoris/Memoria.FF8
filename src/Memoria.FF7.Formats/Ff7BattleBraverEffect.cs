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
    public Ff7BattlePrimaryState? Primary { get; init; }
    public bool Completed => Frame >= 37;
}

public sealed record Ff7BattleBraverUpdate(Ff7BattleBraverState State, ushort ImpactTargets);

public static class Ff7BattleBraverEffect
{
    public static Ff7BattleBraverState Start(ushort targetMask)
    {
        Ff7BattleBraverState state = CreateState(targetMask);
        return state with { Primary = Ff7BattlePrimaryEffects.ScheduleBraver(new(), 0, targetMask) };
    }

    internal static Ff7BattleBraverState CreateState(ushort targetMask)
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
        Ff7BattlePrimaryContext context = new((_, _, _) => throw new NotSupportedException(), _ => default, collisionRadius, () => throw new NotSupportedException(), JointPoint: (target, _) => targetAnchor(target), WeaponPoint: (_, offset) => weaponPoint(offset));
        Ff7BattlePrimaryState primary = state.Primary ?? Ff7BattlePrimaryEffects.ImportBraver(new(), state, 0);
        primary = Ff7BattlePrimaryEffects.Advance(primary, context);
        return new(Ff7BattlePrimaryEffects.ProjectBraver(primary, state, retainPrimary: true), primary.ImpactTargets);
    }
}
