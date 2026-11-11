using System.Numerics;

namespace Memoria.FF7.Formats;

public sealed record Ff7BattlePrimaryBeatRushController(int Source, int Target) : Ff7BattlePrimaryCallback;
public sealed record Ff7BattlePrimaryBeatRushTrail(int Source, int Window, IReadOnlyList<Ff7BattleBeatRushSample> Samples) : Ff7BattlePrimaryCallback;
public sealed record Ff7BattlePrimaryBeatRushSprite(Ff7BattleBeatRushSprite Sprite) : Ff7BattlePrimaryCallback;
public sealed record Ff7BattlePrimaryBeatRushDraw(Ff7BattleBeatRushState State) : Ff7BattlePrimaryDraw;

public static partial class Ff7BattlePrimaryEffects
{
    public static Ff7BattlePrimaryState ScheduleBeatRush(Ff7BattlePrimaryState state, int source, ushort targets, int owner = 0)
    {
        Ff7BattleBeatRushState effect = Ff7BattleBeatRushEffect.CreateState(targets);
        PrimaryPass pass = new(state, null);
        pass.Allocate(new Ff7BattlePrimaryBeatRushController(source, effect.Target) { Owner = owner });
        return pass.Finish(state.Tick, preserveReceipts: true);
    }

    private static Ff7BattlePrimaryCallback? ExecuteBeatRush(Ff7BattlePrimaryCallback callback, PrimaryPass pass)
    {
        Ff7BattlePrimaryContext context = pass.Context ?? throw new InvalidOperationException("FFVII Beat Rush requires original model and random owners.");
        switch (callback)
        {
            case Ff7BattlePrimaryBeatRushController controller:
                if (controller.Age is 5 or 9 or 12)
                {
                    Vector3 point = context.ActorPoint(controller.Source, 32, new(0, 0, -300));
                    Ff7BattleBraverPoint anchor = new((short)((short)point.X + context.NextCrt() % 100 - 50), (short)((short)point.Y + context.NextCrt() % 100 - 50), (short)((short)point.Z + context.NextCrt() % 100 - 50));
                    int scale = controller.Age == 12 ? context.NextCrt() % 0x600 + 0x1000 : context.NextCrt() % 0x800 + 0xA00;
                    pass.Allocate(new Ff7BattlePrimaryBeatRushSprite(new(anchor, 0, 0x1400 * scale >> 12)) { Owner = controller.Owner });
                }
                if (controller.Age is 1 or 7)
                {
                    pass.Allocate(new Ff7BattlePrimaryBeatRushTrail(controller.Source, controller.Age == 1 ? 4 : 6, []) { Owner = controller.Owner });
                }
                if (controller.Age == 12)
                {
                    pass.Contact(controller.Target);
                }
                return controller.Age >= 20 ? null : controller with { Age = controller.Age + 1 };
            case Ff7BattlePrimaryBeatRushTrail trail:
                List<Ff7BattleBeatRushSample> samples = trail.Samples.ToList();
                if (trail.Age < trail.Window)
                {
                    samples.Add(Ff7BattleBeatRushEffect.Sample(offset => context.ActorPoint(trail.Source, 32, offset)));
                }
                if (trail.Age > 0 && samples.Count > 1)
                {
                    pass.Emit(new Ff7BattlePrimaryBeatRushDraw(new(0, trail.Age, Trails: [new(0, trail.Window, samples.ToArray())])));
                }
                if (trail.Age >= trail.Window && samples.Count > 0)
                {
                    samples.RemoveAt(0);
                }
                return samples.Count == 0 ? null : trail with { Age = trail.Age + 1, Samples = samples.ToArray() };
            case Ff7BattlePrimaryBeatRushSprite sprite:
                pass.Emit(new Ff7BattlePrimaryBeatRushDraw(new(0, sprite.Age, Sprites: [sprite.Sprite])));
                return sprite.Age == 7 ? null : sprite with { Age = sprite.Age + 1 };
            default:
                throw new NotSupportedException("FFVII Beat Rush callback has no admitted executor.");
        }
    }

    public static Ff7BattleBeatRushState ProjectBeatRush(Ff7BattlePrimaryState primary, Ff7BattleBeatRushState previous, int owner = 0, bool retainPrimary = false)
    {
        int frame = previous.Frame + 1;
        Ff7BattleBeatRushState[] draws = primary.Draws.OfType<Ff7BattlePrimaryBeatRushDraw>().Where(draw => draw.Owner == owner).Select(draw => draw.State).ToArray();
        Ff7BattleBeatRushTrail[] trails = draws.SelectMany(draw => (draw.Trails ?? []).Select(trail => trail with { StartedAt = frame - draw.Frame })).ToArray();
        trails = [.. trails, .. primary.Slots.OfType<Ff7BattlePrimaryBeatRushTrail>().Where(trail => trail.Owner == owner && trail.Age == 1).Select(trail => new Ff7BattleBeatRushTrail(frame, trail.Window, trail.Samples))];
        Ff7BattleBeatRushSprite[] sprites = draws.SelectMany(draw => (draw.Sprites ?? []).Select(sprite => sprite with { StartedAt = frame - draw.Frame })).ToArray();
        return previous with { Frame = frame, Trails = trails, Sprites = sprites, Primary = retainPrimary ? primary : null };
    }

    public static Ff7BattlePrimaryState ImportBeatRush(Ff7BattlePrimaryState state, Ff7BattleBeatRushState previous, int source, int owner = 0)
    {
        if (previous.Frame < 0)
        {
            return ScheduleBeatRush(state, source, (ushort)(1 << previous.Target), owner);
        }
        PrimaryPass pass = new(state, null);
        if (previous.Frame < 20)
        {
            pass.Allocate(new Ff7BattlePrimaryBeatRushController(source, previous.Target) { Owner = owner, Age = previous.Frame + 1 });
        }
        foreach (Ff7BattleBeatRushTrail trail in previous.Trails ?? [])
        {
            int age = previous.Frame - trail.StartedAt;
            Ff7BattleBeatRushSample[] samples = (age >= trail.Duration ? trail.Samples.Skip(1) : trail.Samples).ToArray();
            if (samples.Length > 0)
            {
                pass.Allocate(new Ff7BattlePrimaryBeatRushTrail(source, trail.Duration, samples) { Owner = owner, Age = age + 1 });
            }
        }
        foreach (Ff7BattleBeatRushSprite sprite in previous.Sprites ?? [])
        {
            int age = previous.Frame - sprite.StartedAt + 1;
            if (age < 8)
            {
                pass.Allocate(new Ff7BattlePrimaryBeatRushSprite(sprite with { StartedAt = 0 }) { Owner = owner, Age = age });
            }
        }
        if ((previous.Trails?.Count ?? 0) > 0 || (previous.Sprites?.Count ?? 0) > 0)
        {
            pass.Emit(new Ff7BattlePrimaryBeatRushDraw(previous with { Primary = null }));
        }
        return pass.Finish(state.Tick, preserveReceipts: true);
    }
}
