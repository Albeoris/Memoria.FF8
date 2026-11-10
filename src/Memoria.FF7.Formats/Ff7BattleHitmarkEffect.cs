using System.Numerics;

namespace Memoria.FF7.Formats;

public sealed record Ff7BattleHitmarkCue(int Frame, int Sprite, int OffsetX, int OffsetY, int Scale, int Palette, int FrameCount);
public sealed record Ff7BattleHitmarkDefinition(int Id, IReadOnlyList<Ff7BattleHitmarkCue> Cues)
{
    public int FrameCount => Cues.Count == 0 ? 0 : Cues.Max(cue => cue.Frame + cue.FrameCount);
}

public sealed record Ff7BattleHitmarkState(int Target, Ff7BattleHitmarkDefinition Definition, int Frame = -1, Ff7BattleBraverPoint? Anchor = null, int CollisionRadius = 0, bool Flipped = false)
{
    public bool Completed => Frame >= Definition.FrameCount;
}

public static class Ff7BattleHitmarkEffect
{
    public static Ff7BattleHitmarkState Start(int target, Ff7BattleHitmarkDefinition definition, bool flipped = false)
    {
        if (target is < 0 or > 9 or 3)
        {
            throw new ArgumentOutOfRangeException(nameof(target));
        }
        ArgumentNullException.ThrowIfNull(definition);
        return new(target, definition, Flipped: flipped);
    }

    public static Ff7BattleHitmarkState Advance(Ff7BattleHitmarkState state, Func<int, Vector3> targetAnchor, Func<int, int> collisionRadius)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(targetAnchor);
        ArgumentNullException.ThrowIfNull(collisionRadius);
        if (state.Completed)
        {
            return state;
        }
        return state.Frame < 0 ? state with { Frame = 0, Anchor = Ff7BattleBraverPoint.At(targetAnchor(state.Target)), CollisionRadius = collisionRadius(state.Target) } : state with { Frame = state.Frame + 1 };
    }
}
