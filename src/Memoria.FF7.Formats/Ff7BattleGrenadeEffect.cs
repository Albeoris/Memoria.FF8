using System.Numerics;

namespace Memoria.FF7.Formats;

public sealed record Ff7BattleGrenadeState(int Frame, ushort Targets, short X = 0, short Y = 0, short Z = 0, int CollisionRadius = 0)
{
    public bool Completed => Frame >= Ff7BattleGrenadeEffect.SpriteFrameCount && Targets == 0;
}

public sealed record Ff7BattleGrenadeUpdate(Ff7BattleGrenadeState State, ushort ImpactTargets);

public static class Ff7BattleGrenadeEffect
{
    public const int SpriteFrameCount = 8;

    public static Ff7BattleGrenadeState Start(ushort targetMask)
    {
        if (targetMask == 0 || (targetMask & ~0x03F7) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(targetMask));
        }
        return new(-1, targetMask);
    }

    public static Ff7BattleGrenadeUpdate Advance(Ff7BattleGrenadeState state, Func<int, Vector3> targetAnchor, Func<int, int> collisionRadius)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(targetAnchor);
        ArgumentNullException.ThrowIfNull(collisionRadius);
        if (state.Completed)
        {
            return new(state, 0);
        }

        int frame = state.Frame + 1;
        if (frame == 0)
        {
            int slot = BitOperations.TrailingZeroCount(state.Targets);
            Vector3 anchor = targetAnchor(slot);
            return new(state with { Frame = frame, X = (short)anchor.X, Y = (short)anchor.Y, Z = (short)anchor.Z, CollisionRadius = collisionRadius(slot) }, 0);
        }

        ushort impact = (ushort)(state.Targets & -state.Targets);
        return new(state with { Frame = frame, Targets = (ushort)(state.Targets & ~impact) }, impact);
    }
}
