using System.Numerics;

namespace Memoria.FF7.Formats;

public sealed record Ff7BattlePrimaryBigShotGroup(int Source, int Target, short ChargeRotation, short CounterRotation) : Ff7BattlePrimaryCallback;
public sealed record Ff7BattlePrimaryBigShotController(int Source, int Target, short ChargeRotation, short CounterRotation) : Ff7BattlePrimaryCallback;
public sealed record Ff7BattlePrimaryBigShotCharge(int Source, short ChargeRotation, short CounterRotation) : Ff7BattlePrimaryCallback;
public sealed record Ff7BattlePrimaryBigShotParticle(Ff7BattleBigShotParticle Particle) : Ff7BattlePrimaryCallback;
public sealed record Ff7BattlePrimaryBigShotDraw(Ff7BattleBigShotState State) : Ff7BattlePrimaryDraw;

public static partial class Ff7BattlePrimaryEffects
{
    public static Ff7BattlePrimaryState ScheduleBigShot(Ff7BattlePrimaryState state, int source, ushort targets, short chargeRotation = 0, short counterRotation = 0, int owner = 0)
    {
        Ff7BattleBigShotState effect = Ff7BattleBigShotEffect.CreateState(targets, chargeRotation, counterRotation);
        PrimaryPass pass = new(state, null);
        pass.Allocate(new Ff7BattlePrimaryBigShotGroup(source, effect.Target, chargeRotation, counterRotation) { Owner = owner });
        return pass.Finish(state.Tick, preserveReceipts: true);
    }

    private static Ff7BattlePrimaryCallback? ExecuteBigShot(Ff7BattlePrimaryCallback callback, PrimaryPass pass)
    {
        Ff7BattlePrimaryContext context = pass.Context ?? throw new InvalidOperationException("FFVII Big Shot requires live model and random owners.");
        switch (callback)
        {
            case Ff7BattlePrimaryBigShotGroup group:
                pass.Allocate(new Ff7BattlePrimaryBigShotController(group.Source, group.Target, group.ChargeRotation, group.CounterRotation) { Owner = group.Owner });
                return null;
            case Ff7BattlePrimaryBigShotController controller:
                if (controller.Age == 0)
                {
                    pass.Allocate(new Ff7BattlePrimaryBigShotCharge(controller.Source, controller.ChargeRotation, controller.CounterRotation) { Owner = controller.Owner });
                }
                if (controller.Age < 14 || controller.Age == 22)
                {
                    Func<int, int, Vector3> joint = context.JointPoint ?? throw new InvalidOperationException("FFVII Big Shot requires target joint selectors.");
                    IReadOnlyList<Ff7BattleBigShotParticle> particles = Ff7BattleBigShotEffect.Spawn(controller.Age, controller.Target, offset => Weapon(controller.Source, offset), target => joint(target, 0), context.ActorRotation(controller.Source).Y, context.NextCrt);
                    foreach (Ff7BattleBigShotParticle particle in particles)
                    {
                        pass.Allocate(new Ff7BattlePrimaryBigShotParticle(particle with { StartedAt = 0 }) { Owner = controller.Owner });
                    }
                }
                if (controller.Age == 22)
                {
                    pass.Contact(controller.Target);
                }
                return controller.Age == 44 ? null : controller with { Age = controller.Age + 1 };
            case Ff7BattlePrimaryBigShotCharge charge:
                Vector3 origin = Weapon(charge.Source, Vector3.Zero);
                Ff7BattleBigShotCharge basis = new(Ff7BattleBraverPoint.At(origin), Ff7BattleBraverPoint.At(Weapon(charge.Source, new(4096, 0, 0)) - origin), Ff7BattleBraverPoint.At(Weapon(charge.Source, new(0, 4096, 0)) - origin), Ff7BattleBraverPoint.At(Weapon(charge.Source, new(0, 0, 4096)) - origin));
                pass.Emit(new Ff7BattlePrimaryBigShotDraw(new(0, charge.Age, Charge: basis, InitialChargeRotation: charge.ChargeRotation, InitialCounterRotation: charge.CounterRotation)));
                return charge.Age == 21 ? null : charge with { Age = charge.Age + 1 };
            case Ff7BattlePrimaryBigShotParticle child:
                Ff7BattleBigShotParticle particleState = child.Particle;
                int lifetime = particleState.Kind == Ff7BattleBigShotParticleKind.Charge ? 8 : 22;
                if (child.Age >= lifetime)
                {
                    return null;
                }
                pass.Emit(new Ff7BattlePrimaryBigShotDraw(new(0, child.Age, Particles: [particleState])));
                Ff7BattleBraverPoint position = Ff7BattleBigShotEffect.Add(particleState.Position, particleState.Velocity);
                Ff7BattleBraverPoint velocity = particleState.Velocity with { Y = (short)(particleState.Velocity.Y + (particleState.Kind == Ff7BattleBigShotParticleKind.Fragment ? 10 : 0)) };
                BattleRotationWords rotation = new((short)(particleState.Rotation.X + particleState.Spin.X), (short)(particleState.Rotation.Y + particleState.Spin.Y), (short)(particleState.Rotation.Z + particleState.Spin.Z));
                if (particleState.Kind != Ff7BattleBigShotParticleKind.Charge && position.Y > 0)
                {
                    return null;
                }
                return child with { Age = child.Age + 1, Particle = particleState with { Position = position, Velocity = velocity, Rotation = rotation } };
            default:
                throw new NotSupportedException("FFVII Big Shot callback has no admitted executor.");
        }

        Vector3 Weapon(int source, Vector3 offset)
        {
            return (context.WeaponPoint ?? throw new InvalidOperationException("FFVII Big Shot requires the linked weapon matrix."))(source, offset);
        }
    }

    public static Ff7BattleBigShotState ProjectBigShot(Ff7BattlePrimaryState primary, Ff7BattleBigShotState previous, int owner = 0, bool retainPrimary = false)
    {
        int frame = previous.Frame + 1;
        Ff7BattleBigShotState[] draws = primary.Draws.OfType<Ff7BattlePrimaryBigShotDraw>().Where(draw => draw.Owner == owner).Select(draw => draw.State).ToArray();
        Ff7BattleBigShotParticle[] particles = draws.SelectMany(draw => (draw.Particles ?? []).Select(particle => particle with { StartedAt = frame - draw.Frame })).ToArray();
        return previous with { Frame = frame, Charge = draws.FirstOrDefault(draw => draw.Charge is not null)?.Charge, Particles = particles, Primary = retainPrimary ? primary : null };
    }

    public static Ff7BattlePrimaryState ImportBigShot(Ff7BattlePrimaryState state, Ff7BattleBigShotState previous, int source, int owner = 0)
    {
        if (previous.Frame < 0)
        {
            return ScheduleBigShot(state, source, (ushort)(1 << previous.Target), previous.InitialChargeRotation, previous.InitialCounterRotation, owner);
        }
        PrimaryPass pass = new(state, null);
        if (previous.Frame < 44)
        {
            pass.Allocate(new Ff7BattlePrimaryBigShotController(source, previous.Target, previous.InitialChargeRotation, previous.InitialCounterRotation) { Owner = owner, Age = previous.Frame + 1 });
        }
        if (previous.Frame < 21)
        {
            pass.Allocate(new Ff7BattlePrimaryBigShotCharge(source, previous.InitialChargeRotation, previous.InitialCounterRotation) { Owner = owner, Age = previous.Frame + 1 });
        }
        foreach (Ff7BattleBigShotParticle particle in previous.Particles ?? [])
        {
            int age = previous.Frame - particle.StartedAt + 1;
            int lifetime = particle.Kind == Ff7BattleBigShotParticleKind.Charge ? 8 : 22;
            Ff7BattleBraverPoint position = Ff7BattleBigShotEffect.Add(particle.Position, particle.Velocity);
            if (age <= lifetime && (particle.Kind == Ff7BattleBigShotParticleKind.Charge || position.Y <= 0))
            {
                Ff7BattleBraverPoint velocity = particle.Velocity with { Y = (short)(particle.Velocity.Y + (particle.Kind == Ff7BattleBigShotParticleKind.Fragment ? 10 : 0)) };
                BattleRotationWords rotation = new((short)(particle.Rotation.X + particle.Spin.X), (short)(particle.Rotation.Y + particle.Spin.Y), (short)(particle.Rotation.Z + particle.Spin.Z));
                pass.Allocate(new Ff7BattlePrimaryBigShotParticle(particle with { StartedAt = 0, Position = position, Velocity = velocity, Rotation = rotation }) { Owner = owner, Age = age });
            }
        }
        if (previous.Charge is not null || (previous.Particles?.Count ?? 0) > 0)
        {
            pass.Emit(new Ff7BattlePrimaryBigShotDraw(previous with { Primary = null }));
        }
        return pass.Finish(state.Tick, preserveReceipts: true);
    }
}
