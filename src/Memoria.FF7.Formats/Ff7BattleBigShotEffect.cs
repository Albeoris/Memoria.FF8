using System.Numerics;

namespace Memoria.FF7.Formats;

public enum Ff7BattleBigShotParticleKind
{
    Charge,
    Fragment,
    Burst
}

public sealed record Ff7BattleBigShotParticle(Ff7BattleBigShotParticleKind Kind, int StartedAt, int Variant, Ff7BattleBraverPoint Position, Ff7BattleBraverPoint Velocity, int Scale, BattleRotationWords Rotation = default, BattleRotationWords Spin = default);
public sealed record Ff7BattleBigShotCharge(Ff7BattleBraverPoint Origin, Ff7BattleBraverPoint XAxis, Ff7BattleBraverPoint YAxis, Ff7BattleBraverPoint ZAxis);
public sealed record Ff7BattleBigShotState(int Target, int Frame = -1, Ff7BattleBigShotCharge? Charge = null, IReadOnlyList<Ff7BattleBigShotParticle>? Particles = null, short InitialChargeRotation = 0, short InitialCounterRotation = 0)
{
    public Ff7BattlePrimaryState? Primary { get; init; }
    public bool Completed => Frame >= 44;
    public short ChargeRotation => (short)(InitialChargeRotation + Math.Clamp(Frame + 1, 0, 22) * 640);
    public short CounterRotation => (short)(InitialCounterRotation - Math.Clamp(Frame + 1, 0, 22) * 448);
}

public sealed record Ff7BattleBigShotUpdate(Ff7BattleBigShotState State, ushort ImpactTargets);

public static class Ff7BattleBigShotEffect
{
    public static Ff7BattleBigShotState Start(ushort targetMask, short chargeRotation = 0, short counterRotation = 0)
    {
        Ff7BattleBigShotState state = CreateState(targetMask, chargeRotation, counterRotation);
        return state with { Primary = Ff7BattlePrimaryEffects.ScheduleBigShot(new(), 0, targetMask, chargeRotation, counterRotation) };
    }

    internal static Ff7BattleBigShotState CreateState(ushort targetMask, short chargeRotation, short counterRotation)
    {
        if (targetMask == 0 || (targetMask & ~0x03F7) != 0 || (targetMask & (targetMask - 1)) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(targetMask));
        }
        return new(BitOperations.TrailingZeroCount(targetMask), InitialChargeRotation: chargeRotation, InitialCounterRotation: counterRotation);
    }

    public static Ff7BattleBigShotUpdate Advance(Ff7BattleBigShotState state, Func<Vector3, Vector3> weaponPoint, Func<int, Vector3> targetAnchor, int facing, Func<int> nextVisualRandom)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(weaponPoint);
        ArgumentNullException.ThrowIfNull(targetAnchor);
        ArgumentNullException.ThrowIfNull(nextVisualRandom);
        if (state.Completed)
        {
            return new(state, 0);
        }

        Ff7BattlePrimaryContext context = new((_, _, _) => throw new NotSupportedException(), _ => new(0, (short)facing, 0), _ => throw new NotSupportedException(), nextVisualRandom, JointPoint: (target, _) => targetAnchor(target), WeaponPoint: (_, offset) => weaponPoint(offset));
        Ff7BattlePrimaryState primary = state.Primary ?? Ff7BattlePrimaryEffects.ImportBigShot(new(), state, 0);
        primary = Ff7BattlePrimaryEffects.Advance(primary, context);
        return new(Ff7BattlePrimaryEffects.ProjectBigShot(primary, state, retainPrimary: true), primary.ImpactTargets);
    }

    internal static IReadOnlyList<Ff7BattleBigShotParticle> Spawn(int frame, int target, Func<Vector3, Vector3> weaponPoint, Func<int, Vector3> targetAnchor, int facing, Func<int> nextVisualRandom)
    {
        List<Ff7BattleBigShotParticle> particles = [];
        if (frame < 14)
        {
            for (int spawn = 0; spawn < 2; spawn++)
            {
                Vector3 local = new(0, 0, -(nextVisualRandom() % 1000 + 1000));
                BattleRotationWords rotation = new((short)((nextVisualRandom() & 1023) - 512), (short)((nextVisualRandom() & 1023) - 512), 0);
                Vector3 point = weaponPoint(Vector3.Transform(local, BattleRigidPose.Rotation(rotation)) - new Vector3(180));
                Vector3 center = weaponPoint(new(-180));
                Ff7BattleBraverPoint velocity = new((short)(((int)center.X - (int)point.X) / 8), (short)(((int)center.Y - (int)point.Y) / 8), (short)(((int)center.Z - (int)point.Z) / 8));
                particles.Add(new(Ff7BattleBigShotParticleKind.Charge, frame, nextVisualRandom() % 3, Ff7BattleBraverPoint.At(point), velocity, 4096));
            }
        }
        if (frame == 22)
        {
            Ff7BattleBraverPoint anchor = Ff7BattleBraverPoint.At(targetAnchor(target));
            for (int spawn = 0; spawn < 80; spawn++)
            {
                bool fragment = spawn < 50;
                Vector3 local = new(0, 0, -(nextVisualRandom() % (fragment ? 200 : 300) + (fragment ? 100 : 500)));
                BattleRotationWords direction = new((short)((nextVisualRandom() & 2047) - 1024), (short)(facing + (nextVisualRandom() & 2047) + 1024), 0);
                Ff7BattleBraverPoint velocity = Ff7BattleBraverPoint.At(Vector3.Transform(local, BattleRigidPose.Rotation(direction)));
                int scale = nextVisualRandom() % 0x1800 + 0x800;
                int variant = nextVisualRandom() % 3;
                BattleRotationWords rotation = default;
                BattleRotationWords spin = default;
                if (fragment)
                {
                    rotation = new((short)(nextVisualRandom() & 4095), (short)(nextVisualRandom() & 4095), (short)(nextVisualRandom() & 4095));
                    spin = new((short)(nextVisualRandom() % 150 + 50), (short)(nextVisualRandom() % 150 + 50), (short)(nextVisualRandom() % 150 + 50));
                }
                particles.Add(new(fragment ? Ff7BattleBigShotParticleKind.Fragment : Ff7BattleBigShotParticleKind.Burst, frame, variant, anchor, velocity, scale, rotation, spin));
            }
        }
        return particles;
    }

    internal static Ff7BattleBraverPoint Add(Ff7BattleBraverPoint a, Ff7BattleBraverPoint b)
    {
        return new((short)(a.X + b.X), (short)(a.Y + b.Y), (short)(a.Z + b.Z));
    }
}
