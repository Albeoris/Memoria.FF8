using System.Numerics;

namespace Memoria.FF7.Formats;

public enum Ff7BattleCureKind
{
    Cure,
    Ether,
    Antidote,
    PhoenixDown
}

public sealed record Ff7BattleCureTarget(int Slot, int PreviousBone = 0);
public sealed record Ff7BattleCureParticle(short X, short Y, short Z, int Scale, int Frame = 0, byte AtlasY = 0)
{
    public int FrameCount => AtlasY == 0x80 ? 8 : 9;
}
public sealed record Ff7BattleCureState(int Frame, IReadOnlyList<Ff7BattleCureTarget> Targets, IReadOnlyList<Ff7BattleCureParticle> Particles, Ff7BattleCureKind Kind = Ff7BattleCureKind.Cure)
{
    public Ff7BattlePrimaryState? Primary { get; init; }
    public bool Completed => Frame >= Ff7BattleCureEffect.ImpactFrame(Kind) + (Targets.Count >= 5 ? (Targets.Count - 1) * 10 : 0) && Particles.Count == 0;
}

public sealed record Ff7BattleCureUpdate(Ff7BattleCureState State, ushort ImpactTargets);

public static class Ff7BattleCureEffect
{
    public static Ff7BattleCureState Start(ushort targetMask, Ff7BattleCureKind kind = Ff7BattleCureKind.Cure)
    {
        if (targetMask == 0 || (targetMask & ~0x03F7) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(targetMask));
        }

        List<Ff7BattleCureTarget> targets = [];
        for (int slot = 0; slot < 10; slot++)
        {
            if ((targetMask & (1 << slot)) != 0)
            {
                targets.Add(new(slot));
            }
        }
        return new(-1, targets.AsReadOnly(), [], kind) { Primary = Ff7BattlePrimaryEffects.ScheduleCure(new(), targetMask, kind) };
    }

    public static Ff7BattleCureUpdate Advance(Ff7BattleCureState state, Func<int, int> boneCount, Func<int, int, Vector3> bonePosition, Func<int> nextVisualRandom)
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
        Ff7BattlePrimaryState previous = state.Primary ?? Ff7BattlePrimaryEffects.ImportCure(new() { Tick = state.Frame }, state);
        Ff7BattlePrimaryState primary = Ff7BattlePrimaryEffects.Advance(previous, context);
        return new(Ff7BattlePrimaryEffects.ProjectCure(primary, state, retainPrimary: true), primary.ImpactTargets);
    }
    public static int ImpactFrame(Ff7BattleCureKind kind)
    {
        return kind == Ff7BattleCureKind.Antidote ? 15 : 29;
    }

    internal static int Offset(Ff7BattleCureKind kind, int random)
    {
        return (kind == Ff7BattleCureKind.Antidote ? random & 500 : random % 500) - 250;
    }
}
