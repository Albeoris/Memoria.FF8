using System.Numerics;

namespace Memoria.FF7.Formats;

public sealed record Ff7BattlePrimaryRocketController(int Target, Ff7BattleRocketProjectile InitialProjectile) : Ff7BattlePrimaryCallback;
public sealed record Ff7BattlePrimaryRocketMissile(int Target, Ff7BattleRocketProjectile Projectile) : Ff7BattlePrimaryCallback;
public sealed record Ff7BattlePrimaryRocketExplosion(Ff7BattleBraverPoint Position) : Ff7BattlePrimaryCallback;
public sealed record Ff7BattlePrimaryRocketDraw(Ff7BattleRocketState State) : Ff7BattlePrimaryDraw;

public static partial class Ff7BattlePrimaryEffects
{
    public static Ff7BattlePrimaryState ScheduleRocket(Ff7BattlePrimaryState state, int source, ushort targets, int owner = 0)
    {
        ValidateTargets(targets);
        return ScheduleGroup(state, new(Ff7BattlePrimaryFamily.Rocket, targets, 4, Source: source) { Owner = owner });
    }

    private static Ff7BattlePrimaryCallback? ExecuteRocket(Ff7BattlePrimaryCallback callback, PrimaryPass pass)
    {
        Ff7BattlePrimaryContext context = pass.Context ?? throw new InvalidOperationException("FFVII rocket requires original actor and random owners.");
        switch (callback)
        {
            case Ff7BattlePrimaryRocketController controller:
                if (controller.Age == 0)
                {
                    pass.Allocate(new Ff7BattlePrimaryRocketMissile(controller.Target, controller.InitialProjectile) { Owner = controller.Owner });
                }
                if (controller.Age == 20)
                {
                    pass.Contact(controller.Target);
                }
                if (controller.Age is > 20 and < 24)
                {
                    Func<int, int, Vector3> joint = context.JointPoint ?? throw new InvalidOperationException("FFVII rocket requires original target joint selectors.");
                    for (int spawn = 0; spawn < 2; spawn++)
                    {
                        Ff7BattleBraverPoint anchor = Ff7BattleBraverPoint.At(joint(controller.Target, 0));
                        Ff7BattleBraverPoint position = new((short)(anchor.X + context.NextCrt() % 1000 - 500), (short)(anchor.Y + context.NextCrt() % 1000 - 500), (short)(anchor.Z + context.NextCrt() % 1000 - 500));
                        pass.Allocate(new Ff7BattlePrimaryRocketExplosion(position) { Owner = controller.Owner });
                    }
                }
                return controller.Age == 39 ? null : controller with { Age = controller.Age + 1 };
            case Ff7BattlePrimaryRocketMissile missile:
                Ff7BattleRocketState draw = Ff7BattleRocketEffect.AdvanceProjectile(missile.Projectile, missile.Age, context.NextCrt);
                draw = draw with { Targets = [draw.Targets[0] with { Slot = missile.Target }] };
                pass.Emit(new Ff7BattlePrimaryRocketDraw(draw));
                return missile.Age == 19 ? null : missile with { Age = missile.Age + 1, Projectile = draw.Targets[0].Projectile! };
            case Ff7BattlePrimaryRocketExplosion explosion:
                pass.Emit(new Ff7BattlePrimaryRocketDraw(new(explosion.Age, [], [new(explosion.Position, 0)]) { Sprites = [new(explosion.Position, 2, explosion.Age)] }));
                return explosion.Age == 15 ? null : explosion with { Age = explosion.Age + 1 };
            default:
                throw new NotSupportedException("FFVII rocket callback has no admitted executor.");
        }
    }

    public static Ff7BattleRocketState ProjectRocket(Ff7BattlePrimaryState primary, Ff7BattleRocketState previous, int owner = 0, bool retainPrimary = false)
    {
        int frame = previous.Frame + 1;
        Ff7BattleRocketState[] draws = primary.Draws.OfType<Ff7BattlePrimaryRocketDraw>().Where(draw => draw.Owner == owner).Select(draw => draw.State).ToArray();
        Ff7BattleRocketTarget[] targets = previous.Targets.Select(target => draws.SelectMany(draw => draw.Targets).FirstOrDefault(value => value.Slot == target.Slot) is { Projectile: { } projectile } ? target with { Projectile = projectile } : target).ToArray();
        return previous with { Frame = frame, Targets = targets, Explosions = draws.SelectMany(draw => draw.Explosions.Select(explosion => explosion with { StartedAt = frame - draw.Frame })).ToArray(), Missiles = draws.SelectMany(draw => draw.Missiles).ToArray(), Sprites = draws.SelectMany(draw => draw.Sprites).ToArray(), Points = draws.SelectMany(draw => draw.Points).ToArray(), Primary = retainPrimary ? primary : null };
    }

    public static Ff7BattlePrimaryState ImportRocket(Ff7BattlePrimaryState state, Ff7BattleRocketState previous, int source, int owner = 0)
    {
        ushort mask = TargetMask(previous.Targets.Select(target => target.Slot));
        if (previous.Frame < 0)
        {
            return ScheduleRocket(state, source, mask, owner);
        }
        PrimaryPass pass = new(state, null) { CurrentOwner = owner };
        bool pending = false;
        foreach (Ff7BattleRocketTarget target in previous.Targets)
        {
            int age = previous.Frame - target.StartedAt;
            if (age < 0)
            {
                if (!pending)
                {
                    pass.Allocate(new Ff7BattlePrimaryTargetGroup(Ff7BattlePrimaryFamily.Rocket, mask, 4, NextTarget: target.Slot, Source: source) { Owner = owner, Age = (previous.Frame + 1) % 4 });
                    pending = true;
                }
            }
            else
            {
                Ff7BattleRocketProjectile projectile = target.Projectile ?? throw new InvalidDataException("FFVII rocket import has no frozen projectile.");
                if (age < 39)
                {
                    pass.Allocate(new Ff7BattlePrimaryRocketController(target.Slot, projectile) { Owner = owner, Age = age + 1 });
                }
                if (age < 19)
                {
                    pass.Allocate(new Ff7BattlePrimaryRocketMissile(target.Slot, projectile) { Owner = owner, Age = age + 1 });
                }
            }
        }
        foreach (Ff7BattleRocketExplosion explosion in previous.Explosions)
        {
            int age = previous.Frame - explosion.StartedAt;
            if (age < 15)
            {
                pass.Allocate(new Ff7BattlePrimaryRocketExplosion(explosion.Position) { Owner = owner, Age = age + 1 });
            }
        }
        if (previous.Missiles.Count > 0 || previous.Sprites.Count > 0 || previous.Points.Count > 0)
        {
            pass.Emit(new Ff7BattlePrimaryRocketDraw(previous with { Primary = null }));
        }
        return pass.Finish(state.Tick, preserveReceipts: true);
    }
}
