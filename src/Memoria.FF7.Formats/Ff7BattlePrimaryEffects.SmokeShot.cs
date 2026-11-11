using System.Numerics;

namespace Memoria.FF7.Formats;

public sealed record Ff7BattlePrimarySmokeShotController(int Target, Ff7BattleBraverPoint Position, Ff7BattleBraverPoint Velocity) : Ff7BattlePrimaryCallback;
public sealed record Ff7BattlePrimarySmokeShotSprite(Ff7BattleBraverPoint Position) : Ff7BattlePrimaryCallback;
public sealed record Ff7BattlePrimarySmokeShotDraw(Ff7BattleSmokeShotSprite Sprite, int Frame) : Ff7BattlePrimaryDraw;

public static partial class Ff7BattlePrimaryEffects
{
    public static Ff7BattlePrimaryState ScheduleSmokeShot(Ff7BattlePrimaryState state, int source, ushort targets, int owner = 0)
    {
        ValidateTargets(targets);
        return ScheduleGroup(state, new(Ff7BattlePrimaryFamily.SmokeShot, targets, 4, Source: source) { Owner = owner });
    }

    private static Ff7BattlePrimaryCallback? ExecuteSmokeShot(Ff7BattlePrimaryCallback callback, PrimaryPass pass)
    {
        switch (callback)
        {
            case Ff7BattlePrimarySmokeShotController controller:
                Ff7BattleBraverPoint position = new((short)(controller.Position.X + controller.Velocity.X), (short)(controller.Position.Y + controller.Velocity.Y), (short)(controller.Position.Z + controller.Velocity.Z));
                pass.Allocate(new Ff7BattlePrimarySmokeShotSprite(position) { Owner = controller.Owner });
                if (controller.Age == 15)
                {
                    pass.Hitmarks.Add(new(controller.Target, 0x32));
                    pass.Contact(controller.Target);
                    return null;
                }
                return controller with { Age = controller.Age + 1, Position = position };
            case Ff7BattlePrimarySmokeShotSprite sprite:
                pass.Emit(new Ff7BattlePrimarySmokeShotDraw(new(sprite.Position, 0), sprite.Age));
                return sprite.Age == 9 ? null : sprite with { Age = sprite.Age + 1 };
            default:
                throw new NotSupportedException("FFVII Smoke Shot callback has no admitted executor.");
        }
    }

    public static Ff7BattleSmokeShotState ProjectSmokeShot(Ff7BattlePrimaryState primary, Ff7BattleSmokeShotState previous, int owner = 0, bool retainPrimary = false)
    {
        int frame = previous.Frame + 1;
        Ff7BattleSmokeShotTarget[] targets = previous.Targets.Select(target => primary.Slots.OfType<Ff7BattlePrimarySmokeShotController>().FirstOrDefault(controller => controller.Owner == owner && controller.Target == target.Slot) is { } controller ? target with { Position = controller.Position, Velocity = controller.Velocity } : target).ToArray();
        Ff7BattleSmokeShotSprite[] sprites = primary.Draws.OfType<Ff7BattlePrimarySmokeShotDraw>().Where(draw => draw.Owner == owner).Select(draw => draw.Sprite with { StartedAt = frame - draw.Frame }).ToArray();
        return previous with { Frame = frame, Targets = targets, Sprites = sprites, Primary = retainPrimary ? primary : null };
    }

    public static Ff7BattlePrimaryState ImportSmokeShot(Ff7BattlePrimaryState state, Ff7BattleSmokeShotState previous, int source, int owner = 0)
    {
        ushort mask = TargetMask(previous.Targets.Select(target => target.Slot));
        if (previous.Frame < 0)
        {
            return ScheduleSmokeShot(state, source, mask, owner);
        }
        PrimaryPass pass = new(state, null) { CurrentOwner = owner };
        bool pending = false;
        for (int ordinal = 0; ordinal < previous.Targets.Count; ordinal++)
        {
            Ff7BattleSmokeShotTarget target = previous.Targets[ordinal];
            int age = previous.Frame - ordinal * 4 + 1;
            if (age <= 0)
            {
                if (!pending)
                {
                    pass.Allocate(new Ff7BattlePrimaryTargetGroup(Ff7BattlePrimaryFamily.SmokeShot, mask, 4, NextTarget: target.Slot, Source: source) { Owner = owner, Age = (previous.Frame + 1) % 4 });
                    pending = true;
                }
            }
            else if (age < 16)
            {
                pass.Allocate(new Ff7BattlePrimarySmokeShotController(target.Slot, target.Position ?? throw new InvalidDataException("FFVII Smoke Shot import has no frozen source."), target.Velocity ?? throw new InvalidDataException("FFVII Smoke Shot import has no frozen velocity.")) { Owner = owner, Age = age });
            }
        }
        foreach (Ff7BattleSmokeShotSprite sprite in previous.Sprites)
        {
            int age = previous.Frame - sprite.StartedAt;
            pass.Emit(new Ff7BattlePrimarySmokeShotDraw(sprite with { StartedAt = 0 }, age));
            if (age < 9)
            {
                pass.Allocate(new Ff7BattlePrimarySmokeShotSprite(sprite.Position) { Owner = owner, Age = age + 1 });
            }
        }
        return pass.Finish(state.Tick, preserveReceipts: true);
    }
}
