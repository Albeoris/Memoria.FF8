using System.Numerics;

namespace Memoria.FF7.Formats;

public sealed record Ff7BattleFireTarget(int Slot, int PreviousBone = 0);
public sealed record Ff7BattleFireSprite(short X, short Y, short Z, int CollisionRadius, int Frame = 0);
public sealed record Ff7BattleFireState(int Callback, IReadOnlyList<Ff7BattleFireTarget> Targets, IReadOnlyList<Ff7BattleFireSprite> Sprites)
{
    public Ff7BattlePrimaryState? Primary { get; init; }
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
        return new(0, targets.AsReadOnly(), []) { Primary = Ff7BattlePrimaryEffects.ScheduleFire(new(), targetMask) };
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
        Ff7BattlePrimaryContext context = new((actor, bone, point) => bonePosition(actor, bone) + point, _ => default, collisionRadius, nextVisualRandom, boneCount);
        Ff7BattlePrimaryState previous = state.Primary ?? Ff7BattlePrimaryEffects.ImportFire(new() { Tick = state.Callback - 1 }, state, context);
        Ff7BattlePrimaryState primary = Ff7BattlePrimaryEffects.Advance(previous, context);
        return new(Ff7BattlePrimaryEffects.ProjectFire(primary, state, retainPrimary: true), primary.ImpactTargets);
    }
}
