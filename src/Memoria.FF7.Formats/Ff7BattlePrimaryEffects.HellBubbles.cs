using System.Numerics;

namespace Memoria.FF7.Formats;

public sealed record Ff7BattleBubbleParticle(short Radius, short Angle, int Age = 0);
public sealed record Ff7BattleBubblePool
{
    public IReadOnlyList<Ff7BattleBubbleParticle?> Particles { get; init; } = new Ff7BattleBubbleParticle?[200];
    public short RibbonBrightness { get; init; }
    public short RibbonScale { get; init; } = 2560;
    public short CloudBrightness { get; init; }
}
public sealed record Ff7BattlePrimaryBubbleGroup(ushort Targets, int Source, int NextTarget = 0) : Ff7BattlePrimaryCallback;
public sealed record Ff7BattlePrimaryBubbleController(int Target, Ff7BattleBraverPoint Start, Ff7BattleBraverPoint End) : Ff7BattlePrimaryCallback;
public sealed record Ff7BattlePrimaryBubbleStream(Ff7BattleBraverPoint Start, Ff7BattleBraverPoint Delta, short Yaw) : Ff7BattlePrimaryCallback;
public sealed record Ff7BattlePrimaryBubbleCloud(Ff7BattleBraverPoint Start, short Yaw, short Length) : Ff7BattlePrimaryCallback;
public sealed record Ff7BattlePrimaryBubbleBurst(Ff7BattleBraverPoint Position) : Ff7BattlePrimaryCallback;
public sealed record Ff7BattlePrimaryBubbleRibbon(Ff7BattleBraverPoint Position) : Ff7BattlePrimaryCallback;
public sealed record Ff7BattlePrimaryBubbleSpriteDraw(Ff7BattleBraverPoint Position, bool Burst, int Frame, int Scale, byte Brightness) : Ff7BattlePrimaryDraw;
public sealed record Ff7BattlePrimaryBubbleRibbonDraw(Ff7BattleBraverPoint Position, short Scale, short Angle, byte Brightness) : Ff7BattlePrimaryDraw;
public sealed record Ff7BattlePrimaryBubbleCloudDraw(Ff7BattleBraverPoint Position, short Yaw, short Pitch, int Width, short Length, byte Brightness) : Ff7BattlePrimaryDraw;

public static partial class Ff7BattlePrimaryEffects
{
    public static Ff7BattlePrimaryState ScheduleHellBubbles(Ff7BattlePrimaryState state, int source, ushort targets)
    {
        if (targets == 0 || (targets & ~0x03F7) != 0 || source is < 0 or > 9 or 3)
        {
            throw new ArgumentOutOfRangeException(nameof(targets));
        }
        PrimaryPass pass = new(state, null);
        pass.Bubbles = new();
        pass.Allocate(new Ff7BattlePrimaryBubbleGroup(targets, source));
        return pass.Finish(state.Tick, preserveReceipts: true);
    }

    private static Ff7BattlePrimaryCallback? ExecuteHellBubbles(Ff7BattlePrimaryCallback callback, PrimaryPass pass)
    {
        switch (callback)
        {
            case Ff7BattlePrimaryBubbleGroup group:
                if (group.Age != 0)
                {
                    return group with { Age = (group.Age + 1) % 4 };
                }
                int target = BitOperations.TrailingZeroCount((uint)(group.Targets & ~((1 << group.NextTarget) - 1)));
                Func<int, int, Vector3> joint = pass.Context?.JointPoint ?? throw new InvalidOperationException("FFVII Hell Bubbles requires its original joint selectors.");
                pass.Allocate(new Ff7BattlePrimaryBubbleController(target, Ff7BattleBraverPoint.At(joint(group.Source, 8)), Ff7BattleBraverPoint.At(joint(target, 0))));
                return group.Targets < (1 << (target + 1)) ? null : group with { Age = 1, NextTarget = target + 1 };

            case Ff7BattlePrimaryBubbleController controller:
                return AdvanceBubbleController(controller, pass);

            case Ff7BattlePrimaryBubbleStream stream:
                return AdvanceBubbleStream(stream, pass);

            case Ff7BattlePrimaryBubbleCloud cloud:
                pass.Emit(new Ff7BattlePrimaryBubbleCloudDraw(cloud.Start, unchecked((short)(cloud.Yaw - 1024)), (short)(-cloud.Age * 128), (256 + cloud.Age * 8) * 6656 >> 8, cloud.Length, unchecked((byte)(pass.Bubbles.CloudBrightness << 4))));
                pass.Bubbles = pass.Bubbles with { CloudBrightness = (short)(pass.Bubbles.CloudBrightness + (cloud.Age < 8 ? 1 : cloud.Age > 37 ? -1 : 0)) };
                return cloud.Age == 44 ? null : cloud with { Age = cloud.Age + 1 };

            case Ff7BattlePrimaryBubbleRibbon ribbon:
                pass.Emit(new Ff7BattlePrimaryBubbleRibbonDraw(ribbon.Position, pass.Bubbles.RibbonScale, unchecked((short)(ribbon.Age * 256)), unchecked((byte)pass.Bubbles.RibbonBrightness)));
                if (ribbon.Age < 15)
                {
                    pass.Bubbles = pass.Bubbles with { RibbonBrightness = (short)(pass.Bubbles.RibbonBrightness + 16) };
                }
                if (ribbon.Age > 32)
                {
                    pass.Bubbles = pass.Bubbles with { RibbonScale = (short)(pass.Bubbles.RibbonScale - 300), RibbonBrightness = (short)(pass.Bubbles.RibbonBrightness - 30) };
                }
                return ribbon.Age == 39 ? null : ribbon with { Age = ribbon.Age + 1 };

            case Ff7BattlePrimaryBubbleBurst burst:
                pass.Emit(new Ff7BattlePrimaryBubbleSpriteDraw(burst.Position, true, burst.Age, 4096, 128));
                return burst.Age == 11 ? null : burst with { Age = burst.Age + 1 };

            default:
                throw new InvalidOperationException("FFVII Hell Bubbles callback has an invalid function identity.");
        }
    }

    private static Ff7BattlePrimaryBubbleController? AdvanceBubbleController(Ff7BattlePrimaryBubbleController controller, PrimaryPass pass)
    {
        if (controller.Age == 0)
        {
            int dx = controller.End.X - controller.Start.X;
            int dz = controller.End.Z - controller.Start.Z;
            short yaw = (short)(int)((double)(float)Math.Atan2(dx, dz) * (180 / Math.PI) * 11.377778f);
            pass.Allocate(new Ff7BattlePrimaryBubbleStream(controller.Start, new((short)(dx / 20), 0, (short)(dz / 20)), yaw));
            short x = unchecked((short)dx);
            short z = unchecked((short)dz);
            short distance = (short)Math.Sqrt(x * x + z * z);
            pass.Allocate(new Ff7BattlePrimaryBubbleCloud(controller.Start, yaw, (short)(distance * 5376 / 2800)));
        }
        if (controller.Age == 60)
        {
            pass.Contact(controller.Target);
        }
        if (controller.Age == 30)
        {
            pass.Allocate(new Ff7BattlePrimaryBubbleRibbon(controller.End));
            pass.StageRed = 0;
            pass.StageGreen = 0;
            pass.StageBlue = 32;
            pass.FadeTo(4096, 10);
        }
        if (controller.Age is > 20 and < 40)
        {
            Func<int> crt = pass.Context!.NextCrt;
            Ff7BattleBraverPoint position = new(unchecked((short)(controller.End.X + crt() % 1400 - 700)), unchecked((short)(controller.End.Y + crt() % 1400 - 700)), unchecked((short)(controller.End.Z + crt() % 1400 - 700)));
            pass.Allocate(new Ff7BattlePrimaryBubbleBurst(position));
        }
        if (controller.Age == 69)
        {
            pass.FadeTo(0, 10);
            return null;
        }
        return controller with { Age = controller.Age + 1 };
    }

    private static Ff7BattlePrimaryBubbleStream? AdvanceBubbleStream(Ff7BattlePrimaryBubbleStream stream, PrimaryPass pass)
    {
        Ff7BattleBubbleParticle?[] particles = pass.Bubbles.Particles.ToArray();
        Ff7BattleSearchScopeBasis basis = Ff7BattleEffectMatrix.RotateXyz(new(0, stream.Yaw, 0));
        foreach (Ff7BattleBubbleParticle particle in particles.OfType<Ff7BattleBubbleParticle>())
        {
            int age = particle.Age;
            short x = unchecked((short)(((SilkCosine(particle.Angle) * particle.Radius * age / 20) * (256 + stream.Age * 8)) >> 20));
            short y = unchecked((short)(((SilkSine(particle.Angle) * particle.Radius * age / 20) * (256 + stream.Age * 8)) >> 20));
            Ff7BattleBraverPoint center = new(unchecked((short)(stream.Start.X + stream.Delta.X * age)), unchecked((short)(stream.Start.Y + stream.Delta.Y * age)), unchecked((short)(stream.Start.Z + stream.Delta.Z * age)));
            Ff7BattleBraverPoint position = Ff7BattleEffectMatrix.Add(center, Ff7BattleEffectMatrix.Transform(new(x, y, 0), basis));
            pass.Emit(new Ff7BattlePrimaryBubbleSpriteDraw(position, false, 0, 2048 + age * 64, (byte)((age < 17 ? 8 : 25 - age) << 4)));
        }

        if (stream.Age is > 0 and < 25)
        {
            Func<int> crt = pass.Context!.NextCrt;
            for (int count = 0; count < 4; count++)
            {
                int slot = Array.IndexOf(particles, null);
                if (slot < 0)
                {
                    break;
                }
                particles[slot] = new((short)(crt() % 800), (short)(crt() & 4095));
            }
        }
        bool active = false;
        for (int index = 0; index < particles.Length; index++)
        {
            if (particles[index] is not { } particle)
            {
                continue;
            }
            active = true;
            particles[index] = particle.Age == 24 ? null : particle with { Age = particle.Age + 1, Angle = unchecked((short)(particle.Angle - 128)) };
        }
        pass.Bubbles = pass.Bubbles with { Particles = particles };
        return stream.Age >= 25 && !active ? null : stream with { Age = stream.Age + 1 };
    }
}
