using System.Numerics;

namespace Memoria.FF7.Formats;

public sealed record Ff7BattlePrimaryBraverGroup(int Source, int Target) : Ff7BattlePrimaryCallback;
public sealed record Ff7BattlePrimaryBraverController(int Source, int Target) : Ff7BattlePrimaryCallback;
public sealed record Ff7BattlePrimaryBraverShine(int Source) : Ff7BattlePrimaryCallback;
public sealed record Ff7BattlePrimaryBraverTrail(int Source, Ff7BattleBraverTrail? Previous = null) : Ff7BattlePrimaryCallback;
public sealed record Ff7BattlePrimaryBraverImpact(Ff7BattleBraverImpact Impact) : Ff7BattlePrimaryCallback;
public sealed record Ff7BattlePrimaryBraverDraw(Ff7BattleBraverState State) : Ff7BattlePrimaryDraw;

public static partial class Ff7BattlePrimaryEffects
{
    public static Ff7BattlePrimaryState ScheduleBraver(Ff7BattlePrimaryState state, int source, ushort targets, int owner = 0)
    {
        Ff7BattleBraverState braver = Ff7BattleBraverEffect.CreateState(targets);
        PrimaryPass pass = new(state, null);
        pass.Allocate(new Ff7BattlePrimaryBraverGroup(source, braver.Target) { Owner = owner });
        return pass.Finish(state.Tick, preserveReceipts: true);
    }

    private static Ff7BattlePrimaryCallback? ExecuteBraver(Ff7BattlePrimaryCallback callback, PrimaryPass pass)
    {
        Ff7BattlePrimaryContext context = pass.Context ?? throw new InvalidOperationException("FFVII Braver requires live weapon and target owners.");
        switch (callback)
        {
            case Ff7BattlePrimaryBraverGroup group:
                pass.Allocate(new Ff7BattlePrimaryBraverController(group.Source, group.Target) { Owner = group.Owner });
                return null;
            case Ff7BattlePrimaryBraverController controller:
                if (controller.Age == 10)
                {
                    pass.Allocate(new Ff7BattlePrimaryBraverShine(controller.Source) { Owner = controller.Owner });
                }
                if (controller.Age == 17)
                {
                    pass.Allocate(new Ff7BattlePrimaryBraverTrail(controller.Source) { Owner = controller.Owner });
                }
                if (controller.Age == 26)
                {
                    Func<int, int, Vector3> joint = context.JointPoint ?? throw new InvalidOperationException("FFVII Braver requires target joint selectors.");
                    pass.Allocate(new Ff7BattlePrimaryBraverImpact(new(Ff7BattleBraverPoint.At(joint(controller.Target, 0)), context.CollisionRadius(controller.Target))) { Owner = controller.Owner });
                    pass.Contact(controller.Target);
                }
                return controller.Age == 27 ? null : controller with { Age = controller.Age + 1 };
            case Ff7BattlePrimaryBraverShine shine:
                if (shine.Age == 10)
                {
                    return null;
                }
                Vector3 shinePosition = Weapon(shine.Source, new(0, 0, -400));
                pass.Emit(new Ff7BattlePrimaryBraverDraw(new(0, shine.Age + 10, Shine: Ff7BattleBraverPoint.At(shinePosition))));
                return shine with { Age = shine.Age + 1 };
            case Ff7BattlePrimaryBraverTrail trail:
                Ff7BattleBraverTrail current = new(Ff7BattleBraverPoint.At(Weapon(trail.Source, Vector3.Zero)), Ff7BattleBraverPoint.At(Weapon(trail.Source, new(0, 0, -800))));
                if (trail.Age == 20)
                {
                    return null;
                }
                if (trail.Age >= 1 && trail.Previous is { } previous)
                {
                    pass.Emit(new Ff7BattlePrimaryBraverDraw(new(0, trail.Age + 17, Trail: current, PreviousTrail: previous)));
                }
                return trail with { Age = trail.Age + 1, Previous = current };
            case Ff7BattlePrimaryBraverImpact impact:
                if (impact.Age == 7)
                {
                    return null;
                }
                pass.Emit(new Ff7BattlePrimaryBraverDraw(new(0, impact.Age + 26, Impact: impact.Impact)));
                return impact with { Age = impact.Age + 1 };
            default:
                throw new NotSupportedException("FFVII Braver callback has no admitted executor.");
        }

        Vector3 Weapon(int source, Vector3 offset)
        {
            return (context.WeaponPoint ?? throw new InvalidOperationException("FFVII Braver requires the linked weapon matrix."))(source, offset);
        }
    }

    public static Ff7BattleBraverState ProjectBraver(Ff7BattlePrimaryState primary, Ff7BattleBraverState previous, int owner = 0, bool retainPrimary = false)
    {
        Ff7BattleBraverState[] draws = primary.Draws.OfType<Ff7BattlePrimaryBraverDraw>().Where(draw => draw.Owner == owner).Select(draw => draw.State).ToArray();
        Ff7BattleBraverState? trail = draws.FirstOrDefault(draw => draw.Trail is not null);
        return previous with { Frame = previous.Frame + 1, Shine = draws.FirstOrDefault(draw => draw.Shine is not null)?.Shine, Trail = trail?.Trail, PreviousTrail = trail?.PreviousTrail, Impact = draws.FirstOrDefault(draw => draw.Impact is not null)?.Impact, Primary = retainPrimary ? primary : null };
    }

    public static Ff7BattlePrimaryState ImportBraver(Ff7BattlePrimaryState state, Ff7BattleBraverState previous, int source, int owner = 0)
    {
        if (previous.Frame < 0)
        {
            return ScheduleBraver(state, source, (ushort)(1 << previous.Target), owner);
        }
        PrimaryPass pass = new(state, null);
        if (previous.Frame < 27)
        {
            pass.Allocate(new Ff7BattlePrimaryBraverController(source, previous.Target) { Owner = owner, Age = previous.Frame + 1 });
        }
        if (previous.Frame is >= 10 and < 20)
        {
            pass.Allocate(new Ff7BattlePrimaryBraverShine(source) { Owner = owner, Age = previous.Frame - 9 });
        }
        if (previous.Frame is >= 17 and < 37)
        {
            pass.Allocate(new Ff7BattlePrimaryBraverTrail(source, previous.Trail) { Owner = owner, Age = previous.Frame - 16 });
        }
        if (previous.Frame is >= 26 and < 33 && previous.Impact is { } impact)
        {
            pass.Allocate(new Ff7BattlePrimaryBraverImpact(impact) { Owner = owner, Age = previous.Frame - 25 });
        }
        if (previous.Shine is not null || previous.Trail is not null || previous.Impact is not null)
        {
            pass.Emit(new Ff7BattlePrimaryBraverDraw(previous with { Primary = null }));
        }
        return pass.Finish(state.Tick, preserveReceipts: true);
    }
}
