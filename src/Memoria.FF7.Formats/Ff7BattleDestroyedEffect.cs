using System.Numerics;

namespace Memoria.FF7.Formats;

public sealed record Ff7BattleDestroyedSprite(Ff7BattleBraverPoint Position, int StartedAt, int Scale, int Variant);
public sealed record Ff7BattleDestroyedState(ushort TargetMask, int Frame = -1, IReadOnlyList<Ff7BattleDestroyedSprite>? Sprites = null)
{
    public Ff7BattlePrimaryState? Primary { get; init; }
    public bool Completed => Frame >= (BitOperations.PopCount(TargetMask) - 1) * 4 + 33;
    public byte ScreenBlue => Frame >= 0 && Frame < (BitOperations.PopCount(TargetMask) - 1) * 4 + 30 ? (byte)((Frame & 1) * 32 + 32) : (byte)0;
}

public sealed record Ff7BattleDestroyedUpdate(Ff7BattleDestroyedState State, ushort ImpactTargets);

public static class Ff7BattleDestroyedEffect
{
    public const int SpriteFrameCount = 12;

    public static Ff7BattleDestroyedState Start(ushort targetMask)
    {
        if (targetMask == 0 || (targetMask & ~0x03F7) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(targetMask));
        }
        return new(targetMask) { Primary = Ff7BattlePrimaryEffects.ScheduleDestroyed(new(), targetMask) };
    }

    public static Ff7BattleDestroyedUpdate Advance(Ff7BattleDestroyedState state, Func<int, int> boneCount, Func<int, int, Vector3> bonePosition, Func<int> nextVisualRandom)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(boneCount);
        ArgumentNullException.ThrowIfNull(bonePosition);
        ArgumentNullException.ThrowIfNull(nextVisualRandom);
        if (state.Completed)
        {
            return new(state, 0);
        }
        Ff7BattlePrimaryContext context = new((actor, bone, point) => bonePosition(actor, bone) + point, _ => default, _ => 0, nextVisualRandom, boneCount);
        Ff7BattlePrimaryState previous = state.Primary ?? Ff7BattlePrimaryEffects.ImportDestroyed(new() { Tick = state.Frame }, state);
        Ff7BattlePrimaryState primary = Ff7BattlePrimaryEffects.Advance(previous, context);
        return new(Ff7BattlePrimaryEffects.ProjectDestroyed(primary, state, retainPrimary: true), primary.ImpactTargets);
    }
}
