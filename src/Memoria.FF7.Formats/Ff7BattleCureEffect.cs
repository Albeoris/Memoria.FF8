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
        return new(-1, targets.AsReadOnly(), [], kind);
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

        int frame = state.Frame + 1;
        List<Ff7BattleCureParticle> particles = [];
        foreach (Ff7BattleCureParticle particle in state.Particles)
        {
            if (particle.Frame + 1 < particle.FrameCount)
            {
                particles.Add(particle with { Frame = particle.Frame + 1 });
            }
        }

        Ff7BattleCureTarget[] targets = state.Targets.ToArray();
        int stagger = targets.Length >= 5 ? 10 : 0;
        ushort impact = 0;
        int impactFrame = ImpactFrame(state.Kind);
        for (int index = 0; index < targets.Length; index++)
        {
            int localFrame = frame - index * stagger;
            if (localFrame < 0 || localFrame > impactFrame)
            {
                continue;
            }

            Ff7BattleCureTarget target = targets[index];
            int count = boneCount(target.Slot);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
            int bone = target.PreviousBone;
            for (int spawn = 0; spawn < 2; spawn++)
            {
                bone = (bone + (nextVisualRandom() & 3) + 1) % count;
                Vector3 point = bonePosition(target.Slot, bone);
                short x = (short)((short)point.X + Offset(state.Kind, nextVisualRandom()));
                short y = (short)((short)point.Y + Offset(state.Kind, nextVisualRandom()));
                short z = (short)((short)point.Z + Offset(state.Kind, nextVisualRandom()));
                byte atlasY = state.Kind switch
                {
                    Ff7BattleCureKind.Antidote => (byte)(0x20 + (nextVisualRandom() & 1) * 0x60),
                    Ff7BattleCureKind.PhoenixDown => 0x20,
                    _ => 0
                };
                particles.Add(new(x, y, z, (nextVisualRandom() & 0xFFF) + 0x1000, AtlasY: atlasY));
            }
            targets[index] = target with { PreviousBone = bone };
            if (localFrame == impactFrame)
            {
                impact |= (ushort)(1 << target.Slot);
            }
        }
        return new(new(frame, Array.AsReadOnly(targets), particles.AsReadOnly(), state.Kind), impact);
    }
    public static int ImpactFrame(Ff7BattleCureKind kind)
    {
        return kind == Ff7BattleCureKind.Antidote ? 15 : 29;
    }

    private static int Offset(Ff7BattleCureKind kind, int random)
    {
        return (kind == Ff7BattleCureKind.Antidote ? random & 500 : random % 500) - 250;
    }
}
