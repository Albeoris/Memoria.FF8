using System.Numerics;

namespace Memoria.FF7.Formats;

public sealed record Ff7BattleIceCrystal(short X, short Y, short Z, int CollisionRadius);
public sealed record Ff7BattleIceState(ushort TargetMask, int Frame = -1, IReadOnlyList<Ff7BattleIceCrystal>? Crystals = null)
{
    public bool Completed => Frame >= Ff7BattleIceEffect.FrameCount;
}

public sealed record Ff7BattleIceUpdate(Ff7BattleIceState State, ushort ImpactTargets);

public static class Ff7BattleIceEffect
{
    public const int FrameCount = 15;

    public static Ff7BattleIceState Start(ushort targetMask)
    {
        if (targetMask == 0 || (targetMask & ~0x03F7) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(targetMask));
        }
        return new(targetMask);
    }

    public static Ff7BattleIceUpdate Advance(Ff7BattleIceState state, Func<int, Vector3> targetAnchor, Func<int, int> collisionRadius)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(targetAnchor);
        ArgumentNullException.ThrowIfNull(collisionRadius);
        if (state.Completed)
        {
            return new(state, 0);
        }
        if (state.Frame >= 0)
        {
            return new(state with { Frame = state.Frame + 1 }, 0);
        }

        List<Ff7BattleIceCrystal> crystals = [];
        for (int slot = 0; slot < 10; slot++)
        {
            if ((state.TargetMask & (1 << slot)) != 0)
            {
                Vector3 point = targetAnchor(slot);
                crystals.Add(new((short)point.X, (short)point.Y, (short)point.Z, collisionRadius(slot)));
            }
        }
        return new(state with { Frame = 0, Crystals = crystals.AsReadOnly() }, state.TargetMask);
    }

    public static float Scale(int frame, int collisionRadius)
    {
        return Math.Min(frame * 0x36D * (collisionRadius << 4) >> 12, short.MaxValue) / 4096f;
    }

    public static float Opacity(int frame)
    {
        return Math.Max(0, 255 - (frame * 0x124 >> 4)) / 255f;
    }
}
