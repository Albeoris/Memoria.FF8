using System.Numerics;

namespace Memoria.FF7.Formats;

public sealed record Ff7BattleBoltStrike(short X, short Y, short Z, int CollisionRadius);
public sealed record Ff7BattleBoltSpark(short X, short Y, short Z, int CollisionRadius, int Flip, int Frame = 0);
public sealed record Ff7BattleBoltState(ushort TargetMask, int Frame = -1, IReadOnlyList<Ff7BattleBoltStrike>? Strikes = null, IReadOnlyList<Ff7BattleBoltSpark>? Sparks = null)
{
    public bool Completed => Frame >= 16 && Sparks is not { Count: > 0 };
}

public sealed record Ff7BattleBoltUpdate(Ff7BattleBoltState State, ushort ImpactTargets);

public static class Ff7BattleBoltEffect
{
    public static Ff7BattleBoltState Start(ushort targetMask)
    {
        if (targetMask == 0 || (targetMask & ~0x03F7) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(targetMask));
        }
        return new(targetMask);
    }

    public static Ff7BattleBoltUpdate Advance(Ff7BattleBoltState state, Func<int, Vector3> targetAnchor, Func<int, int> collisionRadius, Func<int> nextVisualRandom)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(targetAnchor);
        ArgumentNullException.ThrowIfNull(collisionRadius);
        ArgumentNullException.ThrowIfNull(nextVisualRandom);
        if (state.Completed)
        {
            return new(state, 0);
        }
        int frame = state.Frame + 1;
        List<Ff7BattleBoltSpark> sparks = [];
        foreach (Ff7BattleBoltSpark spark in state.Sparks ?? [])
        {
            if (spark.Frame < 7)
            {
                sparks.Add(spark with { Frame = spark.Frame + 1 });
            }
        }

        if (frame == 0)
        {
            List<Ff7BattleBoltStrike> strikes = [];
            for (int slot = 0; slot < 10; slot++)
            {
                if ((state.TargetMask & (1 << slot)) != 0)
                {
                    Vector3 point = targetAnchor(slot);
                    strikes.Add(new((short)point.X, (short)point.Y, (short)point.Z, collisionRadius(slot)));
                }
            }
            return new(state with { Frame = frame, Strikes = strikes.AsReadOnly(), Sparks = sparks.AsReadOnly() }, state.TargetMask);
        }
        if (frame is >= 2 and <= 15)
        {
            foreach (Ff7BattleBoltStrike strike in state.Strikes ?? [])
            {
                short x = (short)(strike.X + nextVisualRandom() % 1000 - 500);
                short y = (short)(strike.Y + nextVisualRandom() % 1000 - 500);
                short z = (short)(strike.Z + nextVisualRandom() % 1000 - 500);
                sparks.Add(new(x, y, z, strike.CollisionRadius, nextVisualRandom() & 3));
            }
        }
        return new(state with { Frame = frame, Sparks = sparks.AsReadOnly() }, 0);
    }
}
