using System.Numerics;

namespace Memoria.FF7.Formats;

public sealed record Ff7BattleEnergyBallGlobals(Ff7BattleSearchScopeBasis? Basis = null, short ArcStrength = 8, short OrbStrength = 8);
public sealed record Ff7BattlePrimaryEnergyBallController(int Target, Ff7BattleBraverPoint Source, Ff7BattleBraverPoint Destination, short Length) : Ff7BattlePrimaryCallback;
public sealed record Ff7BattlePrimaryEnergyBallSourceLight(Ff7BattleBraverPoint Position) : Ff7BattlePrimaryCallback;
public sealed record Ff7BattlePrimaryEnergyBallCharge(Ff7BattleBraverPoint Position) : Ff7BattlePrimaryCallback;
public sealed record Ff7BattlePrimaryEnergyBallRing(Ff7BattleBraverPoint Position, int Variant, short Rotation) : Ff7BattlePrimaryCallback;
public sealed record Ff7BattlePrimaryEnergyBallArc(Ff7BattleBraverPoint Position, short LengthScale) : Ff7BattlePrimaryCallback;
public sealed record Ff7BattlePrimaryEnergyBallOrb(Ff7BattleBraverPoint Source, short Length) : Ff7BattlePrimaryCallback;
public sealed record Ff7BattlePrimaryEnergyBallBlast(Ff7BattleBraverPoint Position) : Ff7BattlePrimaryCallback;
public sealed record Ff7BattlePrimaryEnergyBallMeshDraw(int Model, Ff7BattleBraverPoint Position, Ff7BattleSearchScopeBasis Basis, byte Alpha) : Ff7BattlePrimaryDraw;
public sealed record Ff7BattlePrimaryEnergyBallSpriteDraw(int Resource, Ff7BattleBraverPoint Position, int Frame, int Scale, byte Brightness) : Ff7BattlePrimaryDraw;

public static partial class Ff7BattlePrimaryEffects
{
    private static readonly Ff7BattleSearchScopeBasis EnergyBallTurn = Ff7BattleEffectMatrix.RotateZyx(new(-1024, 0, 0));

    public static Ff7BattlePrimaryState ScheduleEnergyBall(Ff7BattlePrimaryState state, int source, ushort targets, int owner = 0)
    {
        ValidateTargets(targets);
        return ScheduleGroup(state with { EnergyBallGlobals = new(state.EnergyBallGlobals.Basis) }, new(Ff7BattlePrimaryFamily.EnergyBall, targets, 4, Source: source) { Owner = owner });
    }

    private static void InitializeEnergyBallTarget(int source, int target, int owner, PrimaryPass pass, ref Ff7BattleEnergyBallGlobals globals)
    {
        Ff7BattlePrimaryContext context = pass.Context ?? throw new InvalidOperationException("FFVII Energy Ball requires original actor owners.");
        Func<int, int, Vector3> joint = context.JointPoint ?? throw new InvalidOperationException("FFVII Energy Ball requires original joint selectors.");
        Ff7BattleBraverPoint origin = Ff7BattleBraverPoint.At(joint(source, 9));
        Ff7BattleBraverPoint destination = Ff7BattleBraverPoint.At(joint(target, 0));
        Ff7BattleSearchScopeBasis basis = Ff7BattleSearchScopeEffect.CreateState((ushort)(1 << target), origin.Position, destination.Position, 0).Basis;
        globals = globals with { Basis = basis };
        pass.Allocate(new Ff7BattlePrimaryEnergyBallController(target, origin, destination, Length(origin, destination)) { Owner = owner });
        pass.StageRed = 0;
        pass.StageGreen = 0;
        pass.StageBlue = 0;
        pass.FadeTo(4096, 20);
        pass.PositionedSounds.Add(new(0x1C8, source, 0x40));
    }

    private static Ff7BattlePrimaryCallback? ExecuteEnergyBall(Ff7BattlePrimaryCallback callback, PrimaryPass pass, ref Ff7BattleEnergyBallGlobals globals)
    {
        switch (callback)
        {
            case Ff7BattlePrimaryEnergyBallController controller:
                if (controller.Age == 0)
                {
                    pass.Allocate(new Ff7BattlePrimaryEnergyBallSourceLight(controller.Source) { Owner = controller.Owner });
                }
                if (controller.Age < 12 && (controller.Age & 3) == 0)
                {
                    pass.Allocate(new Ff7BattlePrimaryEnergyBallCharge(controller.Source) { Owner = controller.Owner });
                }
                if (controller.Age is >= 20 and < 28)
                {
                    Func<int> random = EnergyBallRandom(pass);
                    pass.Allocate(new Ff7BattlePrimaryEnergyBallRing(controller.Source, random() & 3, (short)(random() & 4095)) { Owner = controller.Owner });
                }
                if (controller.Age == 20)
                {
                    pass.Allocate(new Ff7BattlePrimaryEnergyBallArc(controller.Source, (short)(controller.Length * 0x1200 / 3000)) { Owner = controller.Owner });
                    pass.Allocate(new Ff7BattlePrimaryEnergyBallOrb(controller.Source, controller.Length) { Owner = controller.Owner });
                }
                if (controller.Age is > 27 and < 43)
                {
                    Func<int> random = EnergyBallRandom(pass);
                    for (int spawn = 0; spawn < 2; spawn++)
                    {
                        Ff7BattleBraverPoint target = controller.Destination;
                        Ff7BattleBraverPoint position = new((short)(target.X + random() % 1200 - 600), (short)(target.Y + random() % 1200 - 600), (short)(target.Z + random() % 1200 - 600));
                        pass.Allocate(new Ff7BattlePrimaryEnergyBallBlast(position) { Owner = controller.Owner });
                    }
                }
                if (controller.Age == 35)
                {
                    pass.Contact(controller.Target);
                    pass.PositionedSounds.Add(new(0xFA, controller.Target, 0x40));
                }
                if (controller.Age == 44)
                {
                    pass.FadeTo(0, 10);
                    return null;
                }
                return controller with { Age = controller.Age + 1 };
            case Ff7BattlePrimaryEnergyBallSourceLight light:
                int brightness = light.Age < 28 ? 8 : 36 - light.Age;
                pass.Emit(new Ff7BattlePrimaryEnergyBallSpriteDraw(20, light.Position, light.Age & 1, 8192 + light.Age * 200, ExpandEffectBrightness(brightness << 4)));
                return light.Age == 35 ? null : light with { Age = light.Age + 1 };
            case Ff7BattlePrimaryEnergyBallCharge charge:
                short x = (short)(charge.Age << 6);
                short y = (short)(charge.Age << 7);
                int outerScale = 8192 - charge.Age * 409;
                int innerScale = 6144 - charge.Age * 307;
                Ff7BattleSearchScopeBasis outer = Ff7BattleEffectMatrix.Scale(Ff7BattleEffectMatrix.RotateZyx(new(x, y, 0)), outerScale, outerScale, outerScale);
                Ff7BattleSearchScopeBasis inner = Ff7BattleEffectMatrix.Scale(Ff7BattleEffectMatrix.RotateZyx(new(x, (short)-y, 0)), innerScale, innerScale, innerScale);
                byte chargeAlpha = ExpandEffectBrightness(charge.Age * 6);
                pass.Emit(new Ff7BattlePrimaryEnergyBallMeshDraw(0, charge.Position, outer, chargeAlpha));
                pass.Emit(new Ff7BattlePrimaryEnergyBallMeshDraw(0, charge.Position, inner, chargeAlpha));
                return charge.Age == 19 ? null : charge with { Age = charge.Age + 1 };
            case Ff7BattlePrimaryEnergyBallRing ring:
                Ff7BattleSearchScopeBasis ringBasis = Ff7BattleEffectMatrix.Compose(Ff7BattleEffectMatrix.RotateXyz(new(-1024, ring.Rotation, 0)), EnergyBallBasis(globals));
                ringBasis = Ff7BattleEffectMatrix.Scale(ringBasis, 12288, 12288, 12288);
                pass.Emit(new Ff7BattlePrimaryEnergyBallMeshDraw(1 + ring.Variant, ring.Position, ringBasis, ExpandEffectBrightness(128 - (ring.Age << 5))));
                return ring.Age == 3 ? null : ring with { Age = ring.Age + 1 };
            case Ff7BattlePrimaryEnergyBallArc arc:
                Ff7BattleSearchScopeBasis arcBasis = Ff7BattleEffectMatrix.Compose(EnergyBallTurn, EnergyBallBasis(globals));
                arcBasis = Ff7BattleEffectMatrix.Scale(arcBasis, 8192, arc.LengthScale * arc.Age / 10, 8192);
                pass.Emit(new Ff7BattlePrimaryEnergyBallMeshDraw(7 + arc.Age % 7, arc.Position, arcBasis, ExpandEffectBrightness(globals.ArcStrength << 4)));
                if (arc.Age > 8)
                {
                    globals = globals with { ArcStrength = (short)(globals.ArcStrength - 1) };
                }
                return arc.Age == 15 ? null : arc with { Age = arc.Age + 1 };
            case Ff7BattlePrimaryEnergyBallOrb orb:
                Ff7BattleSearchScopeBasis aim = EnergyBallBasis(globals);
                Ff7BattleBraverPoint offset = Ff7BattleEffectMatrix.Transform(new(0, 0, (short)(orb.Length * orb.Age / 10)), aim);
                Ff7BattleBraverPoint orbPosition = Ff7BattleEffectMatrix.Add(orb.Source, offset);
                Ff7BattleSearchScopeBasis orbBasis = Ff7BattleEffectMatrix.Scale(Ff7BattleEffectMatrix.Compose(EnergyBallTurn, aim), 8192, 8192, 8192);
                byte orbAlpha = ExpandEffectBrightness(globals.OrbStrength << 4);
                for (int model = 17; model <= 19; model++)
                {
                    pass.Emit(new Ff7BattlePrimaryEnergyBallMeshDraw(model, orbPosition, orbBasis, orbAlpha));
                }
                pass.Emit(new Ff7BattlePrimaryEnergyBallSpriteDraw(20, orbPosition, orb.Age & 1, 12288, ExpandEffectBrightness((byte)(globals.OrbStrength * 20))));
                if (orb.Age > 8)
                {
                    globals = globals with { OrbStrength = (short)(globals.OrbStrength - 1) };
                }
                return orb.Age == 15 ? null : orb with { Age = orb.Age + 1 };
            case Ff7BattlePrimaryEnergyBallBlast blast:
                pass.Emit(new Ff7BattlePrimaryEnergyBallSpriteDraw(21, blast.Position, blast.Age, 8192, 255));
                return blast.Age == 15 ? null : blast with { Age = blast.Age + 1 };
            default:
                throw new NotSupportedException("FFVII Energy Ball callback has no admitted executor.");
        }
    }

    private static Ff7BattleSearchScopeBasis EnergyBallBasis(Ff7BattleEnergyBallGlobals globals)
    {
        return globals.Basis ?? throw new InvalidDataException("FFVII Energy Ball has no original shared aiming matrix.");
    }

    private static Func<int> EnergyBallRandom(PrimaryPass pass)
    {
        return pass.Context?.NextCrt ?? throw new InvalidOperationException("FFVII Energy Ball requires its original random owner.");
    }
}
