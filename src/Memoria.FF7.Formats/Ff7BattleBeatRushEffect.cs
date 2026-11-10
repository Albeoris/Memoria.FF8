using System.Numerics;

namespace Memoria.FF7.Formats;

public sealed record Ff7BattleBeatRushSample(Ff7BattleBraverPoint Center, Ff7BattleBraverPoint Left, Ff7BattleBraverPoint Right);
public sealed record Ff7BattleBeatRushTrail(int StartedAt, int Duration, IReadOnlyList<Ff7BattleBeatRushSample> Samples);
public sealed record Ff7BattleBeatRushSprite(Ff7BattleBraverPoint Anchor, int StartedAt, int Scale);
public sealed record Ff7BattleBeatRushState(int Target, int Frame = -1, IReadOnlyList<Ff7BattleBeatRushTrail>? Trails = null, IReadOnlyList<Ff7BattleBeatRushSprite>? Sprites = null)
{
    public bool Completed => Frame >= 20;
}

public sealed record Ff7BattleBeatRushUpdate(Ff7BattleBeatRushState State, ushort ImpactTargets);

public static class Ff7BattleBeatRushEffect
{
    public static Ff7BattleBeatRushState Start(ushort targetMask)
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

        int frame = state.Frame + 1;
        List<Ff7BattleBeatRushTrail> trails = [];
        foreach (Ff7BattleBeatRushTrail trail in state.Trails ?? [])
        {
            if (frame - trail.StartedAt < trail.Duration * 2)
            {
                List<Ff7BattleBeatRushSample> samples = trail.Samples.ToList();
                if (frame - trail.StartedAt < trail.Duration)
                {
                    samples.Add(Sample(jointPoint));
                }
                else if (frame - trail.StartedAt > trail.Duration && samples.Count > 0)
                {
                    samples.RemoveAt(0);
                }
                trails.Add(trail with { Samples = samples.AsReadOnly() });
            }
        }
        if (frame is 1 or 7)
        {
            trails.Add(new(frame, frame == 1 ? 4 : 6, Array.AsReadOnly(new[] { Sample(jointPoint) })));
        }

        List<Ff7BattleBeatRushSprite> sprites = (state.Sprites ?? []).Where(sprite => frame - sprite.StartedAt < 8).ToList();
        if (frame is 5 or 9 or 12)
        {
            Vector3 point = jointPoint(new(0, 0, -300));
            Ff7BattleBraverPoint anchor = new((short)((short)point.X + nextVisualRandom() % 100 - 50), (short)((short)point.Y + nextVisualRandom() % 100 - 50), (short)((short)point.Z + nextVisualRandom() % 100 - 50));
            int scale = frame == 12 ? nextVisualRandom() % 0x600 + 0x1000 : nextVisualRandom() % 0x800 + 0xA00;
            sprites.Add(new(anchor, frame, 0x1400 * scale >> 12));
        }
        return new(new(state.Target, frame, trails.AsReadOnly(), sprites.AsReadOnly()), frame == 12 ? (ushort)(1 << state.Target) : (ushort)0);
    }

    private static Ff7BattleBeatRushSample Sample(Func<Vector3, Vector3> jointPoint)
    {
        return new(Ff7BattleBraverPoint.At(jointPoint(new(-170, 0, -80))), Ff7BattleBraverPoint.At(jointPoint(new(-110, -120, -80))), Ff7BattleBraverPoint.At(jointPoint(new(-110, 120, -80))));
    }
}
