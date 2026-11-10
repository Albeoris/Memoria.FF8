using System.Numerics;

namespace Memoria.FF7.Formats;

public sealed record Ff7BattleCureTarget(int Slot, int PreviousBone = 0);
public sealed record Ff7BattleCureParticle(short X, short Y, short Z, int Scale, int Frame = 0);
public sealed record Ff7BattleCureState(int Frame, IReadOnlyList<Ff7BattleCureTarget> Targets, IReadOnlyList<Ff7BattleCureParticle> Particles)
{
    public bool Completed => Frame >= 29 + (Targets.Count >= 5 ? (Targets.Count - 1) * 10 : 0) && Particles.Count == 0;
}

public sealed record Ff7BattleCureUpdate(Ff7BattleCureState State, ushort ImpactTargets);

public static class Ff7BattleCureEffect
{
    public static Ff7BattleCureState Start(ushort targetMask)
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
        return new(-1, targets.AsReadOnly(), []);
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
            if (particle.Frame < 7)
            {
                particles.Add(particle with { Frame = particle.Frame + 1 });
            }
        }

        Ff7BattleCureTarget[] targets = state.Targets.ToArray();
        int stagger = targets.Length >= 5 ? 10 : 0;
        ushort impact = 0;
        for (int index = 0; index < targets.Length; index++)
        {
            int localFrame = frame - index * stagger;
            if (localFrame is < 0 or >= 30)
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
                short x = (short)((short)point.X + nextVisualRandom() % 500 - 250);
                short y = (short)((short)point.Y + nextVisualRandom() % 500 - 250);
                short z = (short)((short)point.Z + nextVisualRandom() % 500 - 250);
                particles.Add(new(x, y, z, (nextVisualRandom() & 0xFFF) + 0x1000));
            }
            targets[index] = target with { PreviousBone = bone };
            if (localFrame == 29)
            {
                impact |= (ushort)(1 << target.Slot);
            }
        }
        return new(new(frame, Array.AsReadOnly(targets), particles.AsReadOnly()), impact);
    }
}
