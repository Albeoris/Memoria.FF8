using System.Numerics;

namespace Memoria.FF7.Formats;

public sealed record Ff7BattleSmokeShotTarget(int Slot, Ff7BattleBraverPoint? Position = null, Ff7BattleBraverPoint? Velocity = null);
public sealed record Ff7BattleSmokeShotSprite(Ff7BattleBraverPoint Position, int StartedAt);
public sealed record Ff7BattleSmokeShotState(int Frame, IReadOnlyList<Ff7BattleSmokeShotTarget> Targets, IReadOnlyList<Ff7BattleSmokeShotSprite> Sprites)
{
    public Ff7BattlePrimaryState? Primary { get; init; }
    public bool Completed => Frame >= (Targets.Count - 1) * 4 + 25;
}

public sealed record Ff7BattleSmokeShotUpdate(Ff7BattleSmokeShotState State, ushort ImpactTargets);

public static class Ff7BattleSmokeShotEffect
{
    public const int SpriteFrameCount = 10;

    public static Ff7BattleSmokeShotState Start(ushort targetMask)
    {
        Ff7BattleSmokeShotState state = CreateState(targetMask);
        return state with { Primary = Ff7BattlePrimaryEffects.ScheduleSmokeShot(new(), 0, targetMask) };
    }

    internal static Ff7BattleSmokeShotState CreateState(ushort targetMask)
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
        Ff7BattlePrimaryContext context = new((_, _, _) => throw new NotSupportedException(), _ => default, _ => throw new NotSupportedException(), () => throw new NotSupportedException(), JointPoint: (target, selector) => selector == 8 ? sourceAnchor() : targetAnchor(target));
        Ff7BattlePrimaryState primary = state.Primary ?? Ff7BattlePrimaryEffects.ImportSmokeShot(new(), state, 0);
        primary = Ff7BattlePrimaryEffects.Advance(primary, context);
        return new(Ff7BattlePrimaryEffects.ProjectSmokeShot(primary, state, retainPrimary: true), primary.ImpactTargets);
    }
}
