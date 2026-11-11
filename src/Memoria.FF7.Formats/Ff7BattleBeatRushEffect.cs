using System.Numerics;

namespace Memoria.FF7.Formats;

public sealed record Ff7BattleBeatRushSample(Ff7BattleBraverPoint Center, Ff7BattleBraverPoint Left, Ff7BattleBraverPoint Right);
public sealed record Ff7BattleBeatRushTrail(int StartedAt, int Duration, IReadOnlyList<Ff7BattleBeatRushSample> Samples);
public sealed record Ff7BattleBeatRushSprite(Ff7BattleBraverPoint Anchor, int StartedAt, int Scale);
public sealed record Ff7BattleBeatRushState(int Target, int Frame = -1, IReadOnlyList<Ff7BattleBeatRushTrail>? Trails = null, IReadOnlyList<Ff7BattleBeatRushSprite>? Sprites = null)
{
    public Ff7BattlePrimaryState? Primary { get; init; }
    public bool Completed => Frame >= 20;
}

public sealed record Ff7BattleBeatRushUpdate(Ff7BattleBeatRushState State, ushort ImpactTargets);

public static class Ff7BattleBeatRushEffect
{
    public static Ff7BattleBeatRushState Start(ushort targetMask)
    {
        Ff7BattleBeatRushState state = CreateState(targetMask);
        return state with { Primary = Ff7BattlePrimaryEffects.ScheduleBeatRush(new(), 0, targetMask) };
    }

    internal static Ff7BattleBeatRushState CreateState(ushort targetMask)
    {
        if (targetMask == 0 || (targetMask & ~0x03F7) != 0 || (targetMask & (targetMask - 1)) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(targetMask), targetMask, "Beat Rush requires one physical battle target.");
        }
        return new(BitOperations.TrailingZeroCount(targetMask));
    }

    public static Ff7BattleBeatRushUpdate Advance(Ff7BattleBeatRushState state, Func<Vector3, Vector3> jointPoint, Func<int> nextVisualRandom)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(jointPoint);
        ArgumentNullException.ThrowIfNull(nextVisualRandom);
        if (state.Completed)
        {
            return new(state, 0);
        }

        Ff7BattlePrimaryContext context = new((_, _, offset) => jointPoint(offset), _ => default, _ => throw new NotSupportedException(), nextVisualRandom);
        Ff7BattlePrimaryState primary = state.Primary ?? Ff7BattlePrimaryEffects.ImportBeatRush(new(), state, 0);
        primary = Ff7BattlePrimaryEffects.Advance(primary, context);
        return new(Ff7BattlePrimaryEffects.ProjectBeatRush(primary, state, retainPrimary: true), primary.ImpactTargets);
    }

    internal static Ff7BattleBeatRushSample Sample(Func<Vector3, Vector3> jointPoint)
    {
        return new(Ff7BattleBraverPoint.At(jointPoint(new(-170, 0, -80))), Ff7BattleBraverPoint.At(jointPoint(new(-110, -120, -80))), Ff7BattleBraverPoint.At(jointPoint(new(-110, 120, -80))));
    }
}
