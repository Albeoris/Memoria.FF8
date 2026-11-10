using System.Numerics;

namespace Memoria.FF7.Formats;

public sealed record Ff7BattleSmokeShotTarget(int Slot, Ff7BattleBraverPoint? Position = null, Ff7BattleBraverPoint? Velocity = null);
public sealed record Ff7BattleSmokeShotSprite(Ff7BattleBraverPoint Position, int StartedAt);
public sealed record Ff7BattleSmokeShotState(int Frame, IReadOnlyList<Ff7BattleSmokeShotTarget> Targets, IReadOnlyList<Ff7BattleSmokeShotSprite> Sprites)
{
    public bool Completed => Frame >= (Targets.Count - 1) * 4 + 25;
}

public sealed record Ff7BattleSmokeShotUpdate(Ff7BattleSmokeShotState State, ushort ImpactTargets);

public static class Ff7BattleSmokeShotEffect
{
    public const int SpriteFrameCount = 10;

    public static Ff7BattleSmokeShotState Start(ushort targetMask)
    {
        if (targetMask == 0 || (targetMask & ~0x03F7) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(targetMask));
        }
        List<Ff7BattleSmokeShotTarget> targets = [];
        for (int slot = 0; slot < 10; slot++)
        {
            if ((targetMask & (1 << slot)) != 0)
            {
                targets.Add(new(slot));
            }
        }
        return new(-1, targets.AsReadOnly(), []);
    }

    public static Ff7BattleSmokeShotUpdate Advance(Ff7BattleSmokeShotState state, Func<Vector3> sourceAnchor, Func<int, Vector3> targetAnchor)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(sourceAnchor);
        ArgumentNullException.ThrowIfNull(targetAnchor);
        if (state.Completed)
        {
            return new(state, 0);
        }
        int frame = state.Frame + 1;
        List<Ff7BattleSmokeShotSprite> sprites = state.Sprites.Where(sprite => frame - sprite.StartedAt < SpriteFrameCount).ToList();
        Ff7BattleSmokeShotTarget[] targets = state.Targets.ToArray();
        ushort impact = 0;
        for (int index = 0; index < targets.Length; index++)
        {
            int localFrame = frame - index * 4;
            if (localFrame is < 0 or >= 16)
            {
                continue;
            }
            Ff7BattleSmokeShotTarget target = targets[index];
            if (localFrame == 0)
            {
                Ff7BattleBraverPoint source = Ff7BattleBraverPoint.At(sourceAnchor());
                Ff7BattleBraverPoint destination = Ff7BattleBraverPoint.At(targetAnchor(target.Slot));
                target = target with { Position = source, Velocity = new((short)((destination.X - source.X) / 16), (short)((destination.Y - source.Y) / 16), (short)((destination.Z - source.Z) / 16)) };
            }
            Ff7BattleBraverPoint current = target.Position ?? throw new InvalidDataException("FFVII Smoke Shot has no frozen native source.");
            Ff7BattleBraverPoint velocity = target.Velocity ?? throw new InvalidDataException("FFVII Smoke Shot has no frozen native velocity.");
            Ff7BattleBraverPoint position = new((short)(current.X + velocity.X), (short)(current.Y + velocity.Y), (short)(current.Z + velocity.Z));
            targets[index] = target with { Position = position };
            sprites.Add(new(position, frame));
            if (localFrame == 15)
            {
                impact |= (ushort)(1 << target.Slot);
            }
        }
        return new(new(frame, Array.AsReadOnly(targets), sprites.AsReadOnly()), impact);
    }
}
