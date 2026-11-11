using System.Numerics;

namespace Memoria.FF7.Formats;

public sealed record Ff7BattleSilkLine(int Age, Ff7BattleBraverPoint Start, Ff7BattleBraverPoint End);
public sealed record Ff7BattleSilkLinePool
{
    public IReadOnlyList<Ff7BattleSilkLine?> Slots { get; init; } = new Ff7BattleSilkLine?[200];
    public short Direction { get; init; } = 1;
}

public sealed record Ff7BattlePrimarySilkGroup(ushort Targets, int Attacker, int PrimaryTarget, int NextTarget = 0) : Ff7BattlePrimaryCallback;
public sealed record Ff7BattlePrimarySilkController(int Target, int PrimaryTarget, Ff7BattleBraverPoint Source, Ff7BattleBraverPoint Destination) : Ff7BattlePrimaryCallback;
public sealed record Ff7BattlePrimarySilkLines : Ff7BattlePrimaryCallback;
public sealed record Ff7BattlePrimarySilkSprite(Ff7BattleBraverPoint Position, int Kind) : Ff7BattlePrimaryCallback;
public sealed record Ff7BattlePrimarySilkStrand(Ff7BattleBraverPoint Position, Ff7BattleBraverPoint Velocity, short Angle, short AngularVelocity, short Radius, short MaximumRadius) : Ff7BattlePrimaryCallback;
public sealed record Ff7BattlePrimarySilkLineDraw(Ff7BattleBraverPoint Start, Ff7BattleBraverPoint End, byte Brightness) : Ff7BattlePrimaryDraw;
public sealed record Ff7BattlePrimarySilkSpriteDraw(Ff7BattleBraverPoint Position, int Kind, int Frame) : Ff7BattlePrimaryDraw;

public static partial class Ff7BattlePrimaryEffects
{
    public static Ff7BattlePrimaryState ScheduleSilk(Ff7BattlePrimaryState state, ushort targets, int attacker, int primaryTarget, int owner = 0)
    {
        if (targets == 0 || (targets & ~0x03F7) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(targets));
        }
        if (primaryTarget is < 0 or > 9 || (targets & (1 << primaryTarget)) == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(primaryTarget));
        }
        PrimaryPass pass = new(state, null);
        pass.SilkLines = pass.SilkLines with { Slots = new Ff7BattleSilkLine?[200] };
        pass.Allocate(new Ff7BattlePrimarySilkGroup(targets, attacker, primaryTarget) { Owner = owner });
        return pass.Finish(state.Tick, preserveReceipts: true);
    }

    private static Ff7BattlePrimaryCallback? ExecuteSilk(Ff7BattlePrimaryCallback callback, PrimaryPass pass)
    {
        Ff7BattlePrimaryContext context = pass.Context ?? throw new InvalidOperationException("FFVII Silk requires the original actor and random owners.");
        Func<int, int, Vector3> joint = context.JointPoint ?? throw new InvalidOperationException("FFVII Silk requires the original model joint selectors.");
        Func<int, int, int> selector = context.JointSelector ?? throw new InvalidOperationException("FFVII Silk requires the original primary-target joint selector table.");
        switch (callback)
        {
            case Ff7BattlePrimarySilkGroup group:
                if (group.Age != 0)
                {
                    return group with { Age = (group.Age + 1) % 4 };
                }
                int target = group.NextTarget;
                while ((group.Targets & (1 << target)) == 0)
                {
                    target++;
                }
                Ff7BattleBraverPoint source = Ff7BattleBraverPoint.At(joint(group.Attacker, 8));
                Ff7BattleBraverPoint destination = Ff7BattleBraverPoint.At(joint(target, 0));
                pass.Allocate(new Ff7BattlePrimarySilkController(target, group.PrimaryTarget, source, destination) { Owner = group.Owner });
                pass.Allocate(new Ff7BattlePrimarySilkLines { Owner = group.Owner });
                if (source.Z > destination.Z)
                {
                    pass.SilkLines = pass.SilkLines with { Direction = -1 };
                }
                return group.Targets < (1 << (target + 1)) ? null : group with { NextTarget = target + 1, Age = 1 };
            case Ff7BattlePrimarySilkController controller:
                if (controller.Age == 0)
                {
                    short yaw = (short)(int)((double)(float)Math.Atan2(controller.Destination.X - controller.Source.X, controller.Destination.Z - controller.Source.Z) * (180 / Math.PI) * 11.377778f);
                    Vector3 offset = Vector3.Transform(new(0, 0, 400), Ff7BattleRocketEffect.YawMatrix(yaw));
                    Ff7BattleBraverPoint adjusted = Add(controller.Source, Ff7BattleBraverPoint.At(offset));
                    controller = controller with { Source = adjusted };
                    pass.Allocate(new Ff7BattlePrimarySilkSprite(adjusted, 3) { Owner = controller.Owner });
                    short targetHeight = (short)((short)context.ActorPoint(controller.Target, selector(controller.PrimaryTarget, 10), Vector3.Zero).Y - 200);
                    for (int index = 0; index < 20; index++)
                    {
                        Ff7BattleBraverPoint velocity = new((short)((controller.Destination.X - adjusted.X) / 10), (short)((targetHeight / 20 * index - adjusted.Y) / 10), (short)((controller.Destination.Z - adjusted.Z) / 10));
                        bool even = (index & 1) == 0;
                        short angle = (short)(context.NextCrt() % 512 + (even ? -256 : 1792));
                        short spin = (short)(pass.SilkLines.Direction * (even ? 384 : -384));
                        short radius = (short)((SilkSine(index * 102) * 600 >> 12) + (SilkSine(index * 800) * 200 >> 12) + 600);
                        pass.Allocate(new Ff7BattlePrimarySilkStrand(adjusted, velocity, angle, spin, 0, radius) { Owner = controller.Owner });
                    }
                }
                if (controller.Age == 36)
                {
                    pass.Hitmarks.Add(new(controller.Target, 0x33));
                    pass.Contact(controller.Target);
                }
                if (controller.Age is > 16 and < 31)
                {
                    Ff7BattleBraverPoint anchor = Ff7BattleBraverPoint.At(context.ActorPoint(controller.Target, selector(controller.PrimaryTarget, 0), Vector3.Zero));
                    Ff7BattleBraverPoint jitter = new((short)(context.NextCrt() % 1000 - 500), (short)(context.NextCrt() % 1000 - 500), (short)(context.NextCrt() % 1000 - 500));
                    pass.Allocate(new Ff7BattlePrimarySilkSprite(Add(anchor, jitter), context.NextCrt() & 1) { Owner = controller.Owner });
                }
                return controller.Age == 49 ? null : controller with { Age = controller.Age + 1 };
            case Ff7BattlePrimarySilkLines lines:
                Ff7BattleSilkLine?[] slots = pass.SilkLines.Slots.ToArray();
                byte brightness = (byte)((lines.Age < 32 ? 128 : (40 - lines.Age) * 16) >> 1);
                for (int index = 0; index < slots.Length; index++)
                {
                    if (slots[index] is not { } line)
                    {
                        continue;
                    }
                    pass.Emit(new Ff7BattlePrimarySilkLineDraw(line.Start, line.End, brightness));
                    slots[index] = line.Age == 9 ? null : line with { Age = line.Age + 1 };
                }
                pass.SilkLines = pass.SilkLines with { Slots = slots };
                return lines.Age == 39 ? null : lines with { Age = lines.Age + 1 };
            case Ff7BattlePrimarySilkStrand strand:
                Ff7BattleSilkLine?[] segments = pass.SilkLines.Slots.ToArray();
                int free = Array.FindIndex(segments, line => line is null);
                Ff7BattleBraverPoint start = SilkStrandPoint(strand);
                if (strand.Age < 10)
                {
                    strand = strand with { Position = Add(strand.Position, strand.Velocity), Radius = (short)(SilkSine(strand.Age * 200) * strand.MaximumRadius >> 12) };
                }
                else
                {
                    strand = strand with { Angle = (short)(strand.Angle + strand.AngularVelocity), Position = strand.Position with { Y = (short)(strand.Position.Y - 5) } };
                }
                if (strand.Age > 20)
                {
                    strand = strand with { Radius = (short)(strand.Radius + strand.Radius / 64) };
                }
                if (free >= 0)
                {
                    segments[free] = new(0, start, SilkStrandPoint(strand));
                    pass.SilkLines = pass.SilkLines with { Slots = segments };
                }
                return strand.Age == 39 ? null : strand with { Age = strand.Age + 1 };
            case Ff7BattlePrimarySilkSprite sprite:
                pass.Emit(new Ff7BattlePrimarySilkSpriteDraw(sprite.Position, sprite.Kind, sprite.Age));
                int frameCount = sprite.Kind == 3 ? 10 : 7;
                return sprite.Age + 1 == frameCount ? null : sprite with { Age = sprite.Age + 1 };
            default:
                throw new NotSupportedException("The FFVII primary callback is outside the Silk family.");
        }
    }

    private static Ff7BattleBraverPoint SilkStrandPoint(Ff7BattlePrimarySilkStrand strand)
    {
        return new((short)(strand.Position.X + (SilkCosine(strand.Angle) * strand.Radius >> 12)), strand.Position.Y, (short)(strand.Position.Z + (SilkSine(strand.Angle) * strand.Radius >> 12)));
    }

    private static Ff7BattleBraverPoint Add(Ff7BattleBraverPoint a, Ff7BattleBraverPoint b) => new((short)(a.X + b.X), (short)(a.Y + b.Y), (short)(a.Z + b.Z));
    private static int SilkCosine(int angle) => (int)(Math.Cos(angle * 0.087890625f * (Math.PI / 180)) * 4096);
    private static int SilkSine(int angle) => (int)(Math.Sin(angle * 0.087890625f * (Math.PI / 180)) * 4096);
}
