using System.Numerics;

namespace Memoria.FF7.Formats;

public sealed record Ff7BattlePrimaryFireController(int Target, int CollisionRadius, int PreviousBone = 0) : Ff7BattlePrimaryCallback;
public sealed record Ff7BattlePrimaryFireSprite(Ff7BattleFireSprite Sprite) : Ff7BattlePrimaryCallback;
public sealed record Ff7BattlePrimaryIceInitializer(int Target) : Ff7BattlePrimaryCallback;
public sealed record Ff7BattlePrimaryIceMesh(Ff7BattleIceCrystal Crystal) : Ff7BattlePrimaryCallback;
public sealed record Ff7BattlePrimaryFireDraw(Ff7BattleFireSprite Sprite) : Ff7BattlePrimaryDraw;
public sealed record Ff7BattlePrimaryIceDraw(Ff7BattleIceCrystal Crystal, int Frame) : Ff7BattlePrimaryDraw;

public static partial class Ff7BattlePrimaryEffects
{
    public static Ff7BattlePrimaryState ScheduleFire(Ff7BattlePrimaryState state, ushort targets, int owner = 0)
    {
        ValidateTargets(targets);
        return ScheduleGroup(state, new(Ff7BattlePrimaryFamily.Fire, targets, 0) { Owner = owner });
    }

    public static Ff7BattlePrimaryState ScheduleIce(Ff7BattlePrimaryState state, ushort targets, int owner = 0)
    {
        ValidateTargets(targets);
        return ScheduleGroup(state, new(Ff7BattlePrimaryFamily.Ice, targets, 4) { Owner = owner });
    }

    private static Ff7BattlePrimaryCallback? ExecuteFireIce(Ff7BattlePrimaryCallback callback, PrimaryPass pass)
    {
        switch (callback)
        {
            case Ff7BattlePrimaryFireController fire:
                Ff7BattlePrimaryContext context = pass.Context ?? throw new InvalidOperationException("FFVII Fire requires original model and random owners.");
                if (fire.Age == 0)
                {
                    pass.Contact(fire.Target);
                }
                int bone = fire.PreviousBone;
                if ((fire.Age & 1) == 0)
                {
                    bone = (bone + (context.NextCrt() & 15) + 1) % BoneCount(context, fire.Target);
                    Ff7BattleBraverPoint position = Ff7BattleBraverPoint.At(context.ActorPoint(fire.Target, bone, Vector3.Zero));
                    pass.Allocate(new Ff7BattlePrimaryFireSprite(new(position.X, position.Y, position.Z, fire.CollisionRadius)) { Owner = fire.Owner });
                }
                return fire.Age == 4 ? null : fire with { Age = fire.Age + 1, PreviousBone = bone };
            case Ff7BattlePrimaryFireSprite sprite:
                pass.Emit(new Ff7BattlePrimaryFireDraw(sprite.Sprite with { Frame = sprite.Age }));
                return sprite.Age + 1 == Ff7BattleFireEffect.SpriteFrameCount ? null : sprite with { Age = sprite.Age + 1 };
            case Ff7BattlePrimaryIceInitializer ice:
                Ff7BattlePrimaryContext iceContext = pass.Context ?? throw new InvalidOperationException("FFVII Ice requires original target model owners.");
                Func<int, int, Vector3> joint = iceContext.JointPoint ?? throw new InvalidOperationException("FFVII Ice requires original target joint selectors.");
                Ff7BattleBraverPoint point = Ff7BattleBraverPoint.At(joint(ice.Target, 0));
                pass.Allocate(new Ff7BattlePrimaryIceMesh(new(point.X, point.Y, point.Z, iceContext.CollisionRadius(ice.Target), pass.Tick)) { Owner = ice.Owner });
                pass.Contact(ice.Target);
                return null;
            case Ff7BattlePrimaryIceMesh mesh:
                pass.Emit(new Ff7BattlePrimaryIceDraw(mesh.Crystal, mesh.Age));
                return mesh.Age + 1 == Ff7BattleIceEffect.FrameCount ? null : mesh with { Age = mesh.Age + 1 };
            default:
                throw new NotSupportedException("FFVII Fire/Ice callback has no admitted executor.");
        }
    }

    public static Ff7BattleFireState ProjectFire(Ff7BattlePrimaryState primary, Ff7BattleFireState previous, int owner = 0, bool retainPrimary = false)
    {
        Ff7BattleFireTarget[] targets = previous.Targets.Select(target => primary.Slots.OfType<Ff7BattlePrimaryFireController>().FirstOrDefault(controller => controller.Owner == owner && controller.Target == target.Slot) is { } controller ? target with { PreviousBone = controller.PreviousBone } : target).ToArray();
        Ff7BattleFireSprite[] sprites = primary.Draws.OfType<Ff7BattlePrimaryFireDraw>().Where(draw => draw.Owner == owner).Select(draw => draw.Sprite).ToArray();
        return previous with { Callback = previous.Callback + 1, Targets = targets, Sprites = sprites, Primary = retainPrimary ? primary : null };
    }

    public static Ff7BattleIceState ProjectIce(Ff7BattlePrimaryState primary, Ff7BattleIceState previous, int owner = 0, bool retainPrimary = false)
    {
        int frame = previous.Frame + 1;
        Ff7BattleIceCrystal[] crystals = primary.Draws.OfType<Ff7BattlePrimaryIceDraw>().Where(draw => draw.Owner == owner).Select(draw => draw.Crystal with { StartedAt = frame - draw.Frame }).ToArray();
        return previous with { Frame = frame, Crystals = crystals, Primary = retainPrimary ? primary : null };
    }

    public static Ff7BattlePrimaryState ImportFire(Ff7BattlePrimaryState state, Ff7BattleFireState previous, Ff7BattlePrimaryContext context, int owner = 0)
    {
        ushort targets = TargetMask(previous.Targets.Select(target => target.Slot));
        if (previous.Callback == 0)
        {
            return ScheduleFire(state, targets, owner);
        }
        PrimaryPass pass = new(state, context) { CurrentOwner = owner };
        if (previous.Callback < 5)
        {
            foreach (Ff7BattleFireTarget target in previous.Targets)
            {
                pass.Allocate(new Ff7BattlePrimaryFireController(target.Slot, context.CollisionRadius(target.Slot), target.PreviousBone) { Owner = owner, Age = previous.Callback });
            }
        }
        foreach (Ff7BattleFireSprite sprite in previous.Sprites)
        {
            pass.Emit(new Ff7BattlePrimaryFireDraw(sprite));
            if (sprite.Frame + 1 < Ff7BattleFireEffect.SpriteFrameCount)
            {
                pass.Allocate(new Ff7BattlePrimaryFireSprite(sprite with { Frame = 0 }) { Owner = owner, Age = sprite.Frame + 1 });
            }
        }
        return pass.Finish(state.Tick, preserveReceipts: true);
    }

    public static Ff7BattlePrimaryState ImportIce(Ff7BattlePrimaryState state, Ff7BattleIceState previous, Ff7BattlePrimaryContext context, int owner = 0)
    {
        if (previous.Frame < 0)
        {
            return ScheduleIce(state, previous.TargetMask, owner);
        }
        PrimaryPass pass = new(state, context) { CurrentOwner = owner };
        int ordinal = 0;
        bool pending = false;
        for (int slot = 0; slot < 10; slot++)
        {
            if ((previous.TargetMask & (1 << slot)) == 0)
            {
                continue;
            }
            int age = previous.Frame - ordinal * 4;
            if (age < 0)
            {
                if (!pending)
                {
                    pass.Allocate(new Ff7BattlePrimaryTargetGroup(Ff7BattlePrimaryFamily.Ice, previous.TargetMask, 4, NextTarget: slot) { Owner = owner, Age = (previous.Frame + 1) % 4 });
                    pending = true;
                }
            }
            else if (age < Ff7BattleIceEffect.FrameCount)
            {
                Ff7BattleIceCrystal crystal;
                Ff7BattleIceCrystal? saved = previous.Crystals?.FirstOrDefault(crystal => crystal.StartedAt == ordinal * 4);
                if (saved is null && previous.Crystals is { } oldCrystals && oldCrystals.All(crystal => crystal.StartedAt == 0) && ordinal < oldCrystals.Count)
                {
                    saved = oldCrystals[ordinal];
                }
                if (saved is not null)
                {
                    crystal = saved;
                }
                else
                {
                    Func<int, int, Vector3> joint = context.JointPoint ?? throw new InvalidOperationException("FFVII Ice presentation import requires its saved or selected-resource target anchor.");
                    Ff7BattleBraverPoint anchor = Ff7BattleBraverPoint.At(joint(slot, 0));
                    crystal = new(anchor.X, anchor.Y, anchor.Z, context.CollisionRadius(slot), ordinal * 4);
                }
                pass.Emit(new Ff7BattlePrimaryIceDraw(crystal, age));
                if (age + 1 < Ff7BattleIceEffect.FrameCount)
                {
                    pass.Allocate(new Ff7BattlePrimaryIceMesh(crystal) { Owner = owner, Age = age + 1 });
                }
            }
            ordinal++;
        }
        return pass.Finish(state.Tick, preserveReceipts: true);
    }
}
