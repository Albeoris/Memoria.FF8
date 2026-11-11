using System.Numerics;

namespace Memoria.FF7.Formats;

public sealed record Ff7BattlePrimarySmogController(int Target, Ff7BattleBraverPoint Source, Ff7BattleBraverPoint Velocity) : Ff7BattlePrimaryCallback;
public sealed record Ff7BattlePrimarySmogCloud(int Target, Ff7BattleBraverPoint Position) : Ff7BattlePrimaryCallback;
public sealed record Ff7BattlePrimarySmogSprite(Ff7BattleSmogSprite Sprite) : Ff7BattlePrimaryCallback;
public sealed record Ff7BattlePrimarySmogSpriteDraw(Ff7BattleSmogSprite Sprite, int Frame) : Ff7BattlePrimaryDraw;
public sealed record Ff7BattlePrimarySmogCloudDraw(int Target, Ff7BattleBraverPoint Position, int Frame) : Ff7BattlePrimaryDraw;

public static partial class Ff7BattlePrimaryEffects
{
    public static Ff7BattlePrimaryState ScheduleSmog(Ff7BattlePrimaryState state, int source, ushort targets, int owner = 0)
    {
        ValidateTargets(targets);
        return ScheduleGroup(state, new(Ff7BattlePrimaryFamily.Smog, targets, 4, Source: source) { Owner = owner });
    }

    private static Ff7BattlePrimaryCallback? ExecuteSmog(Ff7BattlePrimaryCallback callback, PrimaryPass pass)
    {
        switch (callback)
        {
            case Ff7BattlePrimarySmogController controller:
                if (controller.Age < 12 && controller.Age % 4 != 3)
                {
                    int[] scales = [0x1000, 0x1800, 0x1400, 0x1100, 0x0980, 0x0600];
                    for (int spawn = 0; spawn < 2; spawn++)
                    {
                        Ff7BattleBraverPoint position = new((short)(controller.Source.X - controller.Velocity.X / 2 * spawn), (short)(controller.Source.Y - controller.Velocity.Y / 2 * spawn), (short)(controller.Source.Z - controller.Velocity.Z / 2 * spawn));
                        int variant = controller.Age % 4 * 2 + spawn;
                        pass.Allocate(new Ff7BattlePrimarySmogSprite(new(position, 0, variant, scales[variant] * 6 >> 2, controller.Velocity)) { Owner = controller.Owner });
                    }
                }
                if (controller.Age == 12)
                {
                    Ff7BattlePrimaryContext context = pass.Context ?? throw new InvalidOperationException("FFVII Smog requires original actor owners.");
                    Func<int, int, Vector3> joint = context.JointPoint ?? throw new InvalidOperationException("FFVII Smog requires original target joint selectors.");
                    Ff7BattleBraverPoint point = Ff7BattleBraverPoint.At(joint(controller.Target, 0));
                    pass.Contact(controller.Target);
                    pass.Allocate(new Ff7BattlePrimarySmogCloud(controller.Target, point) { Owner = controller.Owner });
                }
                return controller.Age == 39 ? null : controller with { Age = controller.Age + 1 };
            case Ff7BattlePrimarySmogCloud cloud:
                pass.Emit(new Ff7BattlePrimarySmogCloudDraw(cloud.Target, cloud.Position, cloud.Age));
                Ff7BattlePrimaryContext cloudContext = pass.Context ?? throw new InvalidOperationException("FFVII Smog requires its original random owner.");
                Ff7BattleSmogSprite child = Ff7BattleSmogEffect.CreateCloudSprite(cloud.Position, cloud.Age, cloudContext.NextCrt);
                pass.Allocate(new Ff7BattlePrimarySmogSprite(child) { Owner = cloud.Owner });
                return cloud.Age == 29 ? null : cloud with { Age = cloud.Age + 1 };
            case Ff7BattlePrimarySmogSprite sprite:
                pass.Emit(new Ff7BattlePrimarySmogSpriteDraw(sprite.Sprite, sprite.Age));
                int lifetime = sprite.Sprite.Velocity is null ? 8 : 12;
                if (sprite.Age + 1 == lifetime)
                {
                    return null;
                }
                Ff7BattleSmogSprite moved = sprite.Sprite;
                if (moved.Velocity is { } velocity)
                {
                    moved = moved with { Position = new((short)(moved.Position.X + velocity.X), (short)(moved.Position.Y + velocity.Y), (short)(moved.Position.Z + velocity.Z)) };
                }
                return sprite with { Age = sprite.Age + 1, Sprite = moved };
            default:
                throw new NotSupportedException("FFVII Smog callback has no admitted executor.");
        }
    }

    public static Ff7BattleSmogState ProjectSmog(Ff7BattlePrimaryState primary, Ff7BattleSmogState previous, int owner = 0, bool retainPrimary = false)
    {
        int frame = previous.Frame + 1;
        Ff7BattleSmogTarget[] targets = previous.Targets.Select(target =>
        {
            Ff7BattlePrimarySmogController? controller = primary.Slots.OfType<Ff7BattlePrimarySmogController>().FirstOrDefault(value => value.Owner == owner && value.Target == target.Slot);
            Ff7BattlePrimarySmogCloudDraw? cloud = primary.Draws.OfType<Ff7BattlePrimarySmogCloudDraw>().FirstOrDefault(value => value.Owner == owner && value.Target == target.Slot);
            return target with { Source = controller?.Source ?? target.Source, Velocity = controller?.Velocity ?? target.Velocity, Cloud = cloud?.Position ?? target.Cloud };
        }).ToArray();
        Ff7BattleSmogSprite[] sprites = primary.Draws.OfType<Ff7BattlePrimarySmogSpriteDraw>().Where(draw => draw.Owner == owner).Select(draw => draw.Sprite with { StartedAt = frame - draw.Frame }).ToArray();
        return previous with { Frame = frame, Targets = targets, Sprites = sprites, Primary = retainPrimary ? primary : null };
    }

    public static Ff7BattlePrimaryState ImportSmog(Ff7BattlePrimaryState state, Ff7BattleSmogState previous, int source, int owner = 0)
    {
        ushort mask = TargetMask(previous.Targets.Select(target => target.Slot));
        if (previous.Frame < 0)
        {
            return ScheduleSmog(state, source, mask, owner);
        }
        PrimaryPass pass = new(state, null) { CurrentOwner = owner };
        bool pending = false;
        for (int ordinal = 0; ordinal < previous.Targets.Count; ordinal++)
        {
            Ff7BattleSmogTarget target = previous.Targets[ordinal];
            int age = previous.Frame - ordinal * 4;
            if (age < 0)
            {
                if (!pending)
                {
                    pass.Allocate(new Ff7BattlePrimaryTargetGroup(Ff7BattlePrimaryFamily.Smog, mask, 4, NextTarget: target.Slot, Source: source) { Owner = owner, Age = (previous.Frame + 1) % 4 });
                    pending = true;
                }
            }
            else if (age < 39)
            {
                pass.Allocate(new Ff7BattlePrimarySmogController(target.Slot, target.Source ?? throw new InvalidDataException("FFVII Smog import has no frozen source."), target.Velocity ?? throw new InvalidDataException("FFVII Smog import has no frozen velocity.")) { Owner = owner, Age = age + 1 });
            }
            if (age is >= 12 and < 42 && target.Cloud is { } cloud)
            {
                pass.Emit(new Ff7BattlePrimarySmogCloudDraw(target.Slot, cloud, age - 12));
                if (age < 41)
                {
                    pass.Allocate(new Ff7BattlePrimarySmogCloud(target.Slot, cloud) { Owner = owner, Age = age - 11 });
                }
            }
        }
        foreach (Ff7BattleSmogSprite sprite in previous.Sprites)
        {
            int age = previous.Frame - sprite.StartedAt;
            pass.Emit(new Ff7BattlePrimarySmogSpriteDraw(sprite with { StartedAt = 0 }, age));
            if (age + 1 < (sprite.Velocity is null ? 8 : 12))
            {
                Ff7BattleSmogSprite moved = sprite;
                if (sprite.Velocity is { } velocity)
                {
                    moved = moved with { Position = new((short)(sprite.Position.X + velocity.X), (short)(sprite.Position.Y + velocity.Y), (short)(sprite.Position.Z + velocity.Z)) };
                }
                pass.Allocate(new Ff7BattlePrimarySmogSprite(moved with { StartedAt = 0 }) { Owner = owner, Age = age + 1 });
            }
        }
        return pass.Finish(state.Tick, preserveReceipts: true);
    }
}
