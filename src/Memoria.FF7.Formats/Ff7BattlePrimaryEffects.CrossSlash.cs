using System.Numerics;

namespace Memoria.FF7.Formats;

public sealed record Ff7BattlePrimaryCrossSlashGroup(ushort Targets) : Ff7BattlePrimaryCallback;
public sealed record Ff7BattlePrimaryCrossSlashController(int Target, Ff7BattleBraverPoint Position, int Radius) : Ff7BattlePrimaryCallback;
public sealed record Ff7BattlePrimaryCrossSlashSprite(int Sprite, int Birth, Ff7BattleBraverPoint Position, int Radius, int Fade = -1, int BrightnessStep = 0) : Ff7BattlePrimaryCallback;
public sealed record Ff7BattlePrimaryCrossSlashDraw(int Sprite, Ff7BattleBraverPoint Position, int Radius, int Scale, int Brightness, int Rotation = 0) : Ff7BattlePrimaryDraw;

public static partial class Ff7BattlePrimaryEffects
{
    public static Ff7BattlePrimaryState ScheduleCrossSlash(Ff7BattlePrimaryState state, ushort targets, int owner = 0)
    {
        if (targets == 0)
        {
            throw new InvalidDataException("FFVII Cross-slash requires a target mask.");
        }
        return Schedule(state, new Ff7BattlePrimaryCrossSlashGroup(targets) { Owner = owner });
    }

    private static Ff7BattlePrimaryCallback? ExecuteCrossSlash(Ff7BattlePrimaryCallback callback, PrimaryPass pass)
    {
        switch (callback)
        {
            case Ff7BattlePrimaryCrossSlashGroup group:
                Ff7BattlePrimaryContext context = pass.Context ?? throw new InvalidOperationException("FFVII Cross-slash requires its target model.");
                Func<int, int, Vector3> joint = context.JointPoint ?? throw new InvalidOperationException("FFVII Cross-slash requires the target joint selector.");
                int target = BitOperations.TrailingZeroCount(group.Targets);
                pass.Allocate(new Ff7BattlePrimaryCrossSlashController(target, Ff7BattleBraverPoint.At(joint(target, 0)), context.CollisionRadius(target)) { Owner = group.Owner });
                ushort remaining = (ushort)(group.Targets & ~(1 << target));
                return remaining == 0 ? null : group with { Targets = remaining };
            case Ff7BattlePrimaryCrossSlashController controller:
                int sprite = controller.Age switch { 5 => 0, 15 => 1, 26 => 2, _ => -1 };
                if (sprite >= 0)
                {
                    pass.Allocate(new Ff7BattlePrimaryCrossSlashSprite(sprite, controller.Age, controller.Position, controller.Radius) { Owner = controller.Owner });
                }
                ushort sound = controller.Age switch { 9 or 19 => 18, 30 => 27, 37 => 33, _ => 0 };
                if (sound != 0)
                {
                    pass.PositionedSounds.Add(new(sound, -1, 64, Point: controller.Position));
                }
                if (controller.Age == 37)
                {
                    pass.Contact(controller.Target);
                    return null;
                }
                return controller with { Age = controller.Age + 1 };
            case Ff7BattlePrimaryCrossSlashSprite spriteCallback:
                if (spriteCallback.Fade >= 0)
                {
                    int scale = 0x1000 + spriteCallback.Fade * 0x6FFF / 8;
                    int brightness = 0x80 - spriteCallback.BrightnessStep * 16;
                    pass.Emit(new Ff7BattlePrimaryCrossSlashDraw(spriteCallback.Sprite, spriteCallback.Position, spriteCallback.Radius, scale, brightness, spriteCallback.Fade << 8));
                    return spriteCallback.BrightnessStep == 7 ? null : spriteCallback with { BrightnessStep = spriteCallback.BrightnessStep + 1 };
                }
                if (spriteCallback.Age < 4)
                {
                    pass.Emit(new Ff7BattlePrimaryCrossSlashDraw(spriteCallback.Sprite, spriteCallback.Position, spriteCallback.Radius, 0x4000 - spriteCallback.Age * 0xC00, spriteCallback.Age * 32));
                }
                else if (spriteCallback.Birth + spriteCallback.Age < 37)
                {
                    pass.Emit(new Ff7BattlePrimaryCrossSlashDraw(spriteCallback.Sprite, spriteCallback.Position, spriteCallback.Radius, 0x1000, 0x80));
                }
                else if (spriteCallback.Birth + spriteCallback.Age < 45)
                {
                    int fade = spriteCallback.Birth + spriteCallback.Age - 37;
                    pass.Allocate(spriteCallback with { Fade = fade, BrightnessStep = fade * 3 / 4 });
                }
                else
                {
                    return null;
                }
                return spriteCallback with { Age = spriteCallback.Age + 1 };
            default:
                throw new NotSupportedException("FFVII Cross-slash callback has no admitted executor.");
        }
    }
}
