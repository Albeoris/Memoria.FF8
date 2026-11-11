using System.Numerics;

namespace Memoria.FF7.Formats;

public sealed record Ff7BattleBoltStrike(short X, short Y, short Z, int CollisionRadius, int StartedAt = 0);
public sealed record Ff7BattleBoltSpark(short X, short Y, short Z, int CollisionRadius, int Flip, int Frame = 0);
public sealed record Ff7BattleBoltState(ushort TargetMask, int Frame = -1, IReadOnlyList<Ff7BattleBoltStrike>? Strikes = null, IReadOnlyList<Ff7BattleBoltSpark>? Sparks = null)
{
    public Ff7BattlePrimaryState? Primary { get; init; }
    public bool Completed => Frame >= 16 + (BitOperations.PopCount(TargetMask) - 1) * 2 && Sparks is not { Count: > 0 };
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
        return new(targetMask) { Primary = Ff7BattlePrimaryEffects.ScheduleBolt(new(), targetMask) };
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
        Ff7BattlePrimaryContext context = new((actor, _, point) => targetAnchor(actor) + point, _ => default, collisionRadius, nextVisualRandom, JointPoint: (actor, _) => targetAnchor(actor));
        Ff7BattlePrimaryState previous = state.Primary ?? Ff7BattlePrimaryEffects.ImportBolt(new() { Tick = state.Frame }, state, context);
        Ff7BattlePrimaryState primary = Ff7BattlePrimaryEffects.Advance(previous, context);
        return new(Ff7BattlePrimaryEffects.ProjectBolt(primary, state, retainPrimary: true), primary.ImpactTargets);
    }
}
