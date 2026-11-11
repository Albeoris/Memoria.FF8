using System.Numerics;

namespace Memoria.FF7.Formats;

public abstract record Ff7BattleTailLaserCallback : Ff7BattlePrimaryCallback;
public sealed record Ff7BattleTailLaserStage(Ff7BattleBraverPoint Source, Ff7BattleBraverPoint Center, ushort Targets) : Ff7BattleTailLaserCallback;
public sealed record Ff7BattleTailLaserSweep(Ff7BattleBraverPoint Source, Ff7BattleBraverPoint Sweep, ushort Targets) : Ff7BattleTailLaserCallback;
public sealed record Ff7BattleTailLaserBeam(Ff7BattleBraverPoint Source, Ff7BattleBraverPoint End) : Ff7BattleTailLaserCallback;
public sealed record Ff7BattleTailLaserFragment(Ff7BattleBraverPoint Position, Ff7BattleBraverPoint Velocity, int Frame, short Roll = 0) : Ff7BattleTailLaserCallback;
public sealed record Ff7BattleTailLaserImpact(int Target, Ff7BattleBraverPoint Position) : Ff7BattleTailLaserCallback;
public sealed record Ff7BattleTailLaserBlast(Ff7BattleBraverPoint Position) : Ff7BattleTailLaserCallback;
public sealed record Ff7BattleTailLaserSmoke(Ff7BattleBraverPoint Position) : Ff7BattleTailLaserCallback;

public sealed record Ff7BattleTailLaserSprite(int Resource, Ff7BattleBraverPoint Position, int Frame, int Scale, byte Brightness = 128, bool Additive = false, short Roll = 0, Ff7BattleSearchScopeBasis? Basis = null);
public sealed record Ff7BattleTailLaserSpriteDraw(Ff7BattleTailLaserSprite Sprite) : Ff7BattlePrimaryDraw;
public sealed record Ff7BattleTailLaserMeshDraw(int Resource, Ff7BattleBraverPoint Position, Ff7BattleSearchScopeBasis Basis, byte ColorWeight) : Ff7BattlePrimaryDraw;

public static partial class Ff7BattlePrimaryEffects
{
    private static readonly Ff7BattleSearchScopeBasis TailLaserIdentity = Ff7BattleEffectMatrix.RotateXyz(default);
    private static readonly Ff7BattleSearchScopeBasis TailLaserBeamTurn = Ff7BattleEffectMatrix.RotateXyz(new(-1024, 0, 0));

    public static Ff7BattlePrimaryState ScheduleTailLaser(Ff7BattlePrimaryState state, Ff7BattleBraverPoint source, Ff7BattleBraverPoint targetCenter, ushort targets, int owner = 0)
    {
        ValidateTargets(targets);
        return Schedule(state, new Ff7BattleTailLaserStage(source, targetCenter, targets) { Owner = owner });
    }

    private static Ff7BattlePrimaryCallback? ExecuteTailLaser(Ff7BattleTailLaserCallback callback, PrimaryPass pass)
    {
        switch (callback)
        {
            case Ff7BattleTailLaserStage stage:
                if (stage.Age == 0)
                {
                    Ff7BattleBraverPoint start = stage.Center with { X = (short)(stage.Center.X + 4096), Y = 0 };
                    pass.Allocate(new Ff7BattleTailLaserSweep(stage.Source, start, stage.Targets) { Owner = stage.Owner });
                    pass.StageRed = pass.StageGreen = pass.StageBlue = 0;
                    pass.FadeTo(3072, 10);
                    pass.PositionedSounds.Add(new(0x173, -1, 64));
                }
                if (stage.Age == 44)
                {
                    pass.FadeTo(0, 10);
                    return null;
                }
                return stage with { Age = stage.Age + 1 };
            case Ff7BattleTailLaserSweep sweep:
                return AdvanceTailLaserSweep(sweep, pass);
            case Ff7BattleTailLaserBeam beam:
                Ff7BattleSearchScopeBasis aim = Ff7BattleSearchScopeEffect.CreateState(1, beam.Source.Position, beam.End.Position, 0).Basis;
                Ff7BattleSearchScopeBasis aimed = Ff7BattleEffectMatrix.Compose(TailLaserBeamTurn, aim);
                int lengthScale = (Length(beam.Source, beam.End) << 12) / 2600;
                pass.Emit(new Ff7BattleTailLaserMeshDraw(2, beam.Source, Ff7BattleEffectMatrix.Scale(aimed, 4096, lengthScale, 4096), (byte)(255 - (beam.Age << 6))));
                return beam.Age == 3 ? null : beam with { Age = beam.Age + 1 };
            case Ff7BattleTailLaserFragment fragment:
                int brightness = Math.Min(8, 15 - fragment.Age) << 4;
                pass.Emit(new Ff7BattleTailLaserSpriteDraw(new(4, fragment.Position, fragment.Frame, 8192, (byte)brightness, brightness != 128, fragment.Roll)));
                Ff7BattleBraverPoint point = Ff7BattleEffectMatrix.Add(fragment.Position, fragment.Velocity);
                short y = point.Y > 0 ? (short)-fragment.Velocity.Y : fragment.Velocity.Y;
                return fragment.Age == 14 ? null : fragment with { Age = fragment.Age + 1, Position = point with { Y = Math.Min((short)0, point.Y) }, Velocity = fragment.Velocity with { Y = (short)(y + 20) }, Roll = (short)(fragment.Roll + 512) };
            case Ff7BattleTailLaserImpact impact:
                return AdvanceTailLaserImpact(impact, pass);
            case Ff7BattleTailLaserBlast blast:
                int spread = 8192 + (SilkSine(blast.Age * 34) << 1);
                byte weight = (byte)Math.Min(255, (SilkCosine(blast.Age * 34) * 128 >> 12) * 2);
                pass.Emit(new Ff7BattleTailLaserMeshDraw(0, blast.Position, Ff7BattleEffectMatrix.Scale(TailLaserIdentity, spread, 8192, spread), weight));
                return blast.Age == 29 ? null : blast with { Age = blast.Age + 1 };
            case Ff7BattleTailLaserSmoke smoke:
                pass.Emit(new Ff7BattleTailLaserSpriteDraw(new(12, smoke.Position, Math.Min(smoke.Age, 7), 8192 + 192 * smoke.Age, (byte)(96 - 4 * smoke.Age), true)));
                return smoke.Age == 23 ? null : smoke with { Age = smoke.Age + 1 };
            default:
                throw new InvalidDataException("Unknown FFVII Tail Laser callback.");
        }
    }

    private static Ff7BattlePrimaryCallback? AdvanceTailLaserSweep(Ff7BattleTailLaserSweep sweep, PrimaryPass pass)
    {
        Ff7BattlePrimaryContext context = TailLaserContext(pass);
        Func<int, int, Vector3> joint = context.JointPoint ?? throw new InvalidOperationException("FFVII Tail Laser requires cached original target joints.");
        Func<int> random = context.NextCrt;
        pass.Emit(new Ff7BattleTailLaserSpriteDraw(new(3, sweep.Source, sweep.Age & 1, 12288, Additive: true)));
        pass.Emit(new Ff7BattleTailLaserMeshDraw(1, sweep.Source, TailLaserIdentity, 247));
        pass.Allocate(new Ff7BattleTailLaserBeam(sweep.Source, sweep.Sweep) { Owner = sweep.Owner });
        Ff7BattleBraverPoint next = sweep.Sweep with { X = (short)(sweep.Sweep.X - 546) };
        if ((sweep.Age & 1) == 0)
        {
            int angle = random() % 4096;
            Ff7BattleBraverPoint velocity = new((short)(SilkCosine(angle) * 100 >> 12), (short)(-random() % 200 - 100), (short)(SilkSine(angle) * 100 >> 12));
            pass.Allocate(new Ff7BattleTailLaserFragment(next, velocity, 4 + random() % 16) { Owner = sweep.Owner });
        }
        Ff7BattleBraverPoint smoke = new((short)(next.X + random() % 200 - 100), 0, (short)(next.Z + random() % 200 - 100));
        pass.TailLaserSecondaries.Add(new Ff7BattleTailLaserSecondarySprite(10, smoke));
        ushort remaining = sweep.Targets;
        for (int target = 0; target < 10; target++)
        {
            if ((remaining & (1 << target)) == 0)
            {
                continue;
            }
            Ff7BattleBraverPoint targetPoint = Ff7BattleBraverPoint.At(joint(target, 0));
            if (Math.Abs(targetPoint.X - next.X) >= 600)
            {
                continue;
            }
            remaining ^= (ushort)(1 << target);
            pass.Allocate(new Ff7BattleTailLaserImpact(target, targetPoint) { Owner = sweep.Owner });
            pass.PositionedSounds.Add(new(0x176, -1, 64, Channel: 2));
        }
        return sweep.Age == 14 ? null : sweep with { Age = sweep.Age + 1, Sweep = next, Targets = remaining };
    }

    private static Ff7BattlePrimaryCallback? AdvanceTailLaserImpact(Ff7BattleTailLaserImpact impact, PrimaryPass pass)
    {
        Ff7BattlePrimaryContext context = TailLaserContext(pass);
        Func<int> random = context.NextCrt;
        Ff7BattleBraverPoint target = impact.Position;
        if (impact.Age == 0)
        {
            for (int index = 0; index < 12; index++)
            {
                short pitch = (short)(-random() % 1024);
                short yaw = (short)(index << 8);
                Ff7BattleBraverPoint local = new((short)(SilkCosine(pitch) * 160 >> 12), (short)(SilkSine(pitch) * 160 >> 12), 0);
                Ff7BattleBraverPoint velocity = Ff7BattleEffectMatrix.Transform(local, Ff7BattleEffectMatrix.RotateXyz(new(0, yaw, 0)));
                pass.TailLaserSecondaries.Add(new Ff7BattleTailLaserSecondarySpark(target with { Y = 0 }, velocity, yaw, pitch));
            }
        }
        if (impact.Age < 12 && (impact.Age & 3) == 0)
        {
            int rotation = (impact.Age >> 2) * 1365;
            int radius = context.CollisionRadius(impact.Target);
            Ff7BattleBraverPoint blast = new((short)(target.X + (SilkSine(rotation) * radius >> 12)), 0, (short)(target.Z + (SilkCosine(rotation) * radius >> 12)));
            pass.Allocate(new Ff7BattleTailLaserBlast(blast) { Owner = impact.Owner });
        }
        if (impact.Age < 6)
        {
            for (int index = 0; index < 2; index++)
            {
                Ff7BattleBraverPoint smoke = new((short)(target.X + random() % 1600 - 800), (short)(-random() % 500), (short)(target.Z + random() % 1600 - 800));
                pass.Allocate(new Ff7BattleTailLaserSmoke(smoke) { Owner = impact.Owner });
            }
        }
        if (impact.Age >= 19)
        {
            pass.TailLaserSecondaries.Add(TailLaserFlash(target, 7, random));
            pass.TailLaserSecondaries.Add(TailLaserFlash(target, 5, random));
        }
        if (impact.Age == 20)
        {
            pass.Contact(impact.Target);
        }
        return impact.Age == 29 ? null : impact with { Age = impact.Age + 1 };
    }

    private static Ff7BattleTailLaserSecondarySprite TailLaserFlash(Ff7BattleBraverPoint target, int resource, Func<int> random)
    {
        Ff7BattleBraverPoint flash = new((short)(target.X + random() % 1200 - 600), (short)(target.Y - random() % 500 - 200), (short)(target.Z + random() % 1200 - 600));
        return new(resource + random() % 2, flash);
    }

    private static Ff7BattlePrimaryContext TailLaserContext(PrimaryPass pass) => pass.Context ?? throw new InvalidOperationException("FFVII Tail Laser requires its original actor owners.");
}
