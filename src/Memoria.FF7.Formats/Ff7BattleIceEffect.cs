using System.Numerics;

namespace Memoria.FF7.Formats;

public sealed record Ff7BattleIceCrystal(short X, short Y, short Z, int CollisionRadius, int StartedAt = 0);
public sealed record Ff7BattleIceState(ushort TargetMask, int Frame = -1, IReadOnlyList<Ff7BattleIceCrystal>? Crystals = null)
{
    public Ff7BattlePrimaryState? Primary { get; init; }
    public bool Completed => Frame >= Ff7BattleIceEffect.FrameCount + (BitOperations.PopCount(TargetMask) - 1) * 4;
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
        return new(targetMask) { Primary = Ff7BattlePrimaryEffects.ScheduleIce(new(), targetMask) };
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
        Ff7BattlePrimaryContext context = new((actor, _, point) => targetAnchor(actor) + point, _ => default, collisionRadius, () => throw new InvalidOperationException("FFVII Ice has no random callback."), JointPoint: (actor, _) => targetAnchor(actor));
        Ff7BattlePrimaryState previous = state.Primary ?? Ff7BattlePrimaryEffects.ImportIce(new() { Tick = state.Frame }, state, context);
        Ff7BattlePrimaryState primary = Ff7BattlePrimaryEffects.Advance(previous, context);
        return new(Ff7BattlePrimaryEffects.ProjectIce(primary, state, retainPrimary: true), primary.ImpactTargets);
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
