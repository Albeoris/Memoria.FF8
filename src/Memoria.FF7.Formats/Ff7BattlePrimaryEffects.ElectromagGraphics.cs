namespace Memoria.FF7.Formats;

public sealed record Ff7BattlePrimaryElectromagCore(Ff7BattleBraverPoint Position) : Ff7BattlePrimaryCallback;
public sealed record Ff7BattlePrimaryElectromagSmoke(Ff7BattleBraverPoint Position) : Ff7BattlePrimaryCallback;
public sealed record Ff7BattlePrimaryElectromagBurst(Ff7BattleBraverPoint Position) : Ff7BattlePrimaryCallback;
public sealed record Ff7BattlePrimaryElectromagGroundRing(Ff7BattleBraverPoint Position) : Ff7BattlePrimaryCallback;
public sealed record Ff7BattlePrimaryElectromagSourceRing(Ff7BattleBraverPoint Position) : Ff7BattlePrimaryCallback;
public sealed record Ff7BattlePrimaryElectromagShell(Ff7BattleBraverPoint Position) : Ff7BattlePrimaryCallback;
public sealed record Ff7BattlePrimaryElectromagRock(Ff7BattleBraverPoint Position, int Variant, short Rotation) : Ff7BattlePrimaryCallback;
public sealed record Ff7BattlePrimaryElectromagExplosion(Ff7BattleBraverPoint Position) : Ff7BattlePrimaryCallback;
public sealed record Ff7BattleElectromagFragment(Ff7BattleBraverPoint Position, int Frame) : Ff7BattleSecondaryCallback;
public sealed record Ff7BattleElectromagSpinningFragment(Ff7BattleBraverPoint Position, Ff7BattleBraverPoint Velocity, int Frame, short Rotation = 0) : Ff7BattleSecondaryCallback;
public sealed record Ff7BattleElectromagMeshDraw(int Resource, Ff7BattleBraverPoint Position, Ff7BattleSearchScopeBasis Basis, byte Weight) : Ff7BattlePrimaryDraw;
public sealed record Ff7BattleElectromagSpriteDraw(int Resource, Ff7BattleBraverPoint Position, int Frame, int Scale, byte Brightness, short Roll = 0, bool Additive = false) : Ff7BattlePrimaryDraw;
public sealed record Ff7BattleElectromagSecondaryStep(Ff7BattleSecondaryCallback? Callback, IReadOnlyList<Ff7BattleElectromagSpriteDraw> Draws);

public static class Ff7BattleElectromagEffect
{
    public static byte Weight(short word, bool inverseFixedPoint = false)
    {
        if (inverseFixedPoint)
        {
            int shifted = word >> 4;
            return unchecked((byte)(shifted >= 256 ? 0 : 255 - shifted));
        }
        return unchecked((byte)(word >= 128 ? 255 : word * 2));
    }

    public static Ff7BattleElectromagSecondaryStep AdvanceSecondary(Ff7BattleSecondaryCallback callback)
    {
        switch (callback)
        {
            case Ff7BattleElectromagFragment fragment:
                Ff7BattleElectromagSpriteDraw draw = new(16, fragment.Position, fragment.Frame, 8192, (byte)(128 - fragment.Age * 8));
                return new(fragment.Age == 14 ? null : fragment with { Age = fragment.Age + 1, Position = fragment.Position with { Y = unchecked((short)(fragment.Position.Y - 300)) } }, [draw]);
            case Ff7BattleElectromagSpinningFragment spinning:
                int strength = spinning.Age < 7 ? 8 : 15 - spinning.Age;
                Ff7BattleElectromagSpriteDraw rotated = new(16, spinning.Position, spinning.Frame, spinning.Frame < 4 ? 5120 : 8192, (byte)(strength << 4), spinning.Rotation, strength != 8);
                Ff7BattleBraverPoint position = Ff7BattleEffectMatrix.Add(spinning.Position, spinning.Velocity);
                short velocityY = spinning.Velocity.Y;
                if (position.Y > 0)
                {
                    position = position with { Y = 0 };
                    velocityY = unchecked((short)-velocityY);
                }
                return new(spinning.Age == 14 ? null : spinning with { Age = spinning.Age + 1, Position = position, Velocity = spinning.Velocity with { Y = unchecked((short)(velocityY + 20)) }, Rotation = unchecked((short)(spinning.Rotation + 512)) }, [rotated]);
            default:
                throw new InvalidDataException("FFVII Electromag has an invalid secondary callback identity.");
        }
    }
}

public static partial class Ff7BattlePrimaryEffects
{
    private static Ff7BattlePrimaryElectromagController InitializeElectromagGraphics(Ff7BattlePrimaryElectromagController controller, PrimaryPass pass)
    {
        pass.PositionedSounds.Add(new(371, -1, 64) { PlayCached = true });
        pass.PositionedSounds.Add(new(124, -1, 64) { PlayCached = true });
        pass.StageRed = 0;
        pass.StageGreen = 0;
        pass.StageBlue = 0;
        pass.FadeTo(4096, 16);
        return controller;
    }

    private static Ff7BattlePrimaryElectromagController ExecuteElectromagVisual(Ff7BattlePrimaryElectromagController controller, PrimaryPass pass)
    {
        Ff7BattlePrimaryContext context = pass.Context ?? throw new InvalidOperationException("FFVII Electromag requires its original actor and presentation-random owners.");
        int age = controller.Age;
        int owner = controller.Owner;
        Func<int> random = context.NextCrt;
        if (age < 5 && age % 2 == 0)
        {
            pass.Allocate(new Ff7BattlePrimaryElectromagSourceRing(new(0, -800, 0)) { Owner = owner });
        }
        if (age == 5)
        {
            pass.Allocate(new Ff7BattlePrimaryElectromagCore(new(0, 0, 0)) { Owner = owner });
            pass.Allocate(new Ff7BattlePrimaryElectromagShell(new(0, 0, 0)) { Owner = owner });
        }
        if (age >= 5 && age < 20 && age % 5 == 0)
        {
            pass.Allocate(new Ff7BattlePrimaryElectromagGroundRing(new(0, 0, 0)) { Owner = owner });
        }
        if (age >= 5 && age < 20)
        {
            short radius = unchecked((short)(age * 360));
            Ff7BattleBraverPoint rock = ElectromagCircle(radius, random() % 4096);
            int variant = random() % 4;
            short rotation = (short)(random() % 4096);
            pass.Allocate(new Ff7BattlePrimaryElectromagRock(rock, variant, rotation) { Owner = owner });
            Ff7BattleBraverPoint fragment = new(0, 0, 0);
            int frame = 0;
            for (int overwrite = 0; overwrite < 2; overwrite++)
            {
                fragment = ElectromagCircle(radius, random() % 4096);
                frame = random() % 6;
            }
            pass.TailLaserSecondaries.Add(new Ff7BattleElectromagFragment(fragment, frame));
            for (int index = 0; index < 4; index++)
            {
                pass.Allocate(new Ff7BattlePrimaryElectromagSmoke(ElectromagCircle(radius, random() % 4096)) { Owner = owner });
            }
            pass.CameraFocusYDelta = unchecked((short)(pass.CameraFocusYDelta + ((age & 1) == 0 ? 32 : -32)));
        }
        if (age == 25)
        {
            foreach (int target in ElectromagTargets(controller.Targets))
            {
                Ff7BattleBraverPoint point = ElectromagTarget(context, target);
                pass.Allocate(new Ff7BattlePrimaryElectromagExplosion(point with { Y = 0 }) { Owner = owner });
                for (int index = 0; index < 8; index++)
                {
                    Ff7BattleBraverPoint fragmentPoint = ElectromagTarget(context, target);
                    short lift = unchecked((short)(-200 + 2 * (random() % 100) - 100));
                    BattleRotationWords turn = new((short)(random() % 800 - 400), 0, (short)(random() % 800 - 400));
                    Ff7BattleBraverPoint velocity = Ff7BattleEffectMatrix.Transform(new(0, lift, 0), Ff7BattleEffectMatrix.RotateXyz(turn));
                    pass.TailLaserSecondaries.Add(new Ff7BattleElectromagSpinningFragment(fragmentPoint, velocity, random() % 6));
                }
            }
            pass.PositionedSounds.Add(new(250, -1, 64));
        }
        if (age > 25 && age < 33)
        {
            foreach (int target in ElectromagTargets(controller.Targets))
            {
                Ff7BattleBraverPoint point = ElectromagTarget(context, target);
                Ff7BattleBraverPoint jitter = new(unchecked((short)(point.X + random() % 1200 - 600)), unchecked((short)(point.Y + (-random()) % 500)), unchecked((short)(point.Z + random() % 1200 - 600)));
                pass.Allocate(new Ff7BattlePrimaryElectromagBurst(jitter) { Owner = owner });
            }
        }
        if (age > 33 && age < 43)
        {
            foreach (int target in ElectromagTargets(controller.Targets))
            {
                Ff7BattleBraverPoint point = ElectromagTarget(context, target);
                Ff7BattleBraverPoint jitter = new(unchecked((short)(point.X + random() % 1200 - 600)), unchecked((short)(point.Y + random() % 1200 - 600)), unchecked((short)(point.Z + random() % 1200 - 600)));
                pass.Allocate(new Ff7BattlePrimaryElectromagSmoke(jitter) { Owner = owner });
            }
        }
        if (age + 1 == 55)
        {
            pass.ReleaseSound(371, 15);
            pass.ReleaseSound(124, 15);
            pass.ReleaseSound(250, 15);
        }
        return controller;
    }

    private static Ff7BattlePrimaryCallback? ExecuteElectromagChild(Ff7BattlePrimaryCallback callback, PrimaryPass pass)
    {
        switch (callback)
        {
            case Ff7BattlePrimaryElectromagCore core:
                Ff7BattleSearchScopeBasis coreBasis = ElectromagBasis(new(0, 0, 0), core.Age * 640, core.Age * 640, core.Age * 640);
                pass.Emit(new Ff7BattleElectromagMeshDraw(1 + core.Age % 4, core.Position, coreBasis, Ff7BattleElectromagEffect.Weight((short)(128 - core.Age * 8))));
                pass.Emit(new Ff7BattleElectromagMeshDraw(1 + (core.Age + 1) % 4, core.Position, coreBasis, Ff7BattleElectromagEffect.Weight((short)(64 - core.Age * 4))));
                pass.Emit(new Ff7BattleElectromagSpriteDraw(18, core.Position, 0, 8192, (byte)(64 - core.Age * 4), Additive: true));
                return core.Age == 14 ? null : core with { Age = core.Age + 1 };
            case Ff7BattlePrimaryElectromagSmoke smoke:
                pass.Emit(new Ff7BattleElectromagSpriteDraw(15, smoke.Position, smoke.Age, 8192, 128, Additive: true));
                return smoke.Age == 7 ? null : smoke with { Age = smoke.Age + 1 };
            case Ff7BattlePrimaryElectromagBurst burst:
                pass.Emit(new Ff7BattleElectromagSpriteDraw(19, burst.Position, burst.Age, 8192, 128, Additive: true));
                return burst.Age == 7 ? null : burst with { Age = burst.Age + 1 };
            case Ff7BattlePrimaryElectromagGroundRing ring:
                pass.Emit(new Ff7BattleElectromagMeshDraw(0, ring.Position, ElectromagBasis(new(0, 0, 0), ring.Age * 1536, 8192, ring.Age * 1536), Ff7BattleElectromagEffect.Weight((short)(128 - ring.Age * 8))));
                return ring.Age == 14 ? null : ring with { Age = ring.Age + 1 };
            case Ff7BattlePrimaryElectromagSourceRing sourceRing:
                pass.Emit(new Ff7BattleElectromagMeshDraw(14, sourceRing.Position, ElectromagBasis(new(0, 0, 0), sourceRing.Age * 3200, 8192, sourceRing.Age * 3200), Ff7BattleElectromagEffect.Weight((short)(sourceRing.Age * 409), true)));
                return sourceRing.Age == 9 ? null : sourceRing with { Age = sourceRing.Age + 1 };
            case Ff7BattlePrimaryElectromagShell shell:
                pass.Emit(new Ff7BattleElectromagMeshDraw(5 + shell.Age % 4, shell.Position, ElectromagBasis(new(0, 0, 0), shell.Age * 512, 8192, shell.Age * 512), Ff7BattleElectromagEffect.Weight((short)(128 - shell.Age * 6))));
                return shell.Age == 14 ? null : shell with { Age = shell.Age + 1 };
            case Ff7BattlePrimaryElectromagRock rock:
                pass.Emit(new Ff7BattleElectromagMeshDraw(9 + rock.Variant, rock.Position, Ff7BattleEffectMatrix.RotateXyz(new(0, 0, rock.Rotation)), Ff7BattleElectromagEffect.Weight((short)(128 - rock.Age * 8))));
                return rock.Age == 14 ? null : rock with { Age = rock.Age + 1, Position = rock.Position with { Y = unchecked((short)(rock.Position.Y - 100)) }, Rotation = unchecked((short)(rock.Rotation + 256)) };
            case Ff7BattlePrimaryElectromagExplosion explosion:
                pass.Emit(new Ff7BattleElectromagMeshDraw(13, explosion.Position, ElectromagBasis(new(0, unchecked((short)(explosion.Age * 64)), 0), 12288 + explosion.Age * 256, 8192 + explosion.Age * 256, 12288 + explosion.Age * 256), Ff7BattleElectromagEffect.Weight((short)(128 - explosion.Age * 4))));
                return explosion.Age == 14 ? null : explosion with { Age = explosion.Age + 1 };
            default:
                throw new InvalidDataException("FFVII Electromag has an invalid primary child callback identity.");
        }
    }

    private static Ff7BattleSearchScopeBasis ElectromagBasis(BattleRotationWords turn, int x, int y, int z) => Ff7BattleEffectMatrix.Scale(Ff7BattleEffectMatrix.RotateZyx(turn), x, y, z);

    private static Ff7BattleBraverPoint ElectromagCircle(short radius, int angle) => new(unchecked((short)(SilkSine(angle) * radius >> 12)), 0, unchecked((short)(SilkCosine(angle) * radius >> 12)));

    private static Ff7BattleBraverPoint ElectromagTarget(Ff7BattlePrimaryContext context, int target) => Ff7BattleBraverPoint.At((context.JointPoint ?? throw new InvalidOperationException("FFVII Electromag requires current original target joint selectors."))(target, 0));

    private static IEnumerable<int> ElectromagTargets(ushort targets)
    {
        for (int slot = 0; slot < 10; slot++)
        {
            if ((targets & 1 << slot) != 0)
            {
                yield return slot;
            }
        }
    }
}
