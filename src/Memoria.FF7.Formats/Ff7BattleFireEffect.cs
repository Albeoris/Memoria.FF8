using System.Numerics;

namespace Memoria.FF7.Formats;

public sealed record Ff7BattleFireTarget(int Slot, int PreviousBone = 0);
public sealed record Ff7BattleFireSprite(short X, short Y, short Z, int CollisionRadius, int Frame = 0);
public sealed record Ff7BattleFireState(int Callback, IReadOnlyList<Ff7BattleFireTarget> Targets, IReadOnlyList<Ff7BattleFireSprite> Sprites)
{
    public bool Completed => Callback >= 5 && Sprites.Count == 0;
}

public sealed record Ff7BattleFireUpdate(Ff7BattleFireState State, ushort ImpactTargets);

public static class Ff7BattleFireEffect
{
    public const int SpriteFrameCount = 14;

    public static Ff7BattleFireState Start(ushort targetMask)
    {
        if (targetMask == 0 || (targetMask & ~0x03F7) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(targetMask));
        }

        List<Ff7BattleFireTarget> targets = [];
        for (int slot = 0; slot < 10; slot++)
        {
            if ((targetMask & (1 << slot)) != 0)
            {
                targets.Add(new(slot));
            }
        }
        return new(0, targets.AsReadOnly(), []);
    }

    public static Ff7BattleFireUpdate Advance(Ff7BattleFireState state, Func<int, int> boneCount, Func<int, int, Vector3> bonePosition, Func<int, int> collisionRadius, Func<int> nextVisualRandom)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(boneCount);
        ArgumentNullException.ThrowIfNull(bonePosition);
        ArgumentNullException.ThrowIfNull(collisionRadius);
        ArgumentNullException.ThrowIfNull(nextVisualRandom);
        if (state.Completed)
        {
            return new(state, 0);
        }

        List<Ff7BattleFireSprite> sprites = [];
        foreach (Ff7BattleFireSprite sprite in state.Sprites)
        {
            if (sprite.Frame + 1 < SpriteFrameCount)
            {
                sprites.Add(sprite with { Frame = sprite.Frame + 1 });
            }
        }

        IReadOnlyList<Ff7BattleFireTarget> targets = state.Targets;
        ushort impact = 0;
        if (state.Callback is 0 or 2 or 4)
        {
            Ff7BattleFireTarget[] updated = new Ff7BattleFireTarget[targets.Count];
            for (int index = 0; index < targets.Count; index++)
            {
                Ff7BattleFireTarget target = targets[index];
                int count = boneCount(target.Slot);
                ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
                int bone = (target.PreviousBone + (nextVisualRandom() & 15) + 1) % count;
                Vector3 position = bonePosition(target.Slot, bone);
                sprites.Add(new((short)position.X, (short)position.Y, (short)position.Z, collisionRadius(target.Slot)));
                updated[index] = target with { PreviousBone = bone };
                if (state.Callback == 0)
                {
                    impact |= (ushort)(1 << target.Slot);
                }
            }
            targets = Array.AsReadOnly(updated);
        }
        return new(new(state.Callback + 1, targets, sprites.AsReadOnly()), impact);
    }
}
