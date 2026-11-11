using System.Numerics;

namespace Memoria.FF7.Formats;

public sealed record Ff7BattlePrimaryLaserCannonController(int Source, int Target) : Ff7BattlePrimaryCallback;
public sealed record Ff7BattlePrimaryLaserCannonBeam(Ff7BattleBraverPoint Source, Ff7BattleBraverPoint Target) : Ff7BattlePrimaryCallback;
public sealed record Ff7BattlePrimaryLaserCannonImpact(Ff7BattleBraverPoint Position) : Ff7BattlePrimaryCallback;
public sealed record Ff7BattlePrimaryLaserCannonFlare : Ff7BattlePrimaryCallback;
public sealed record Ff7BattlePrimaryLaserCannonBeamDraw(Ff7BattleBraverPoint Source, Ff7BattleBraverPoint Target, int Frame) : Ff7BattlePrimaryDraw;
public sealed record Ff7BattlePrimaryLaserCannonImpactDraw(Ff7BattleBraverPoint Position, int Frame) : Ff7BattlePrimaryDraw;

public static partial class Ff7BattlePrimaryEffects
{
    public static Ff7BattlePrimaryState ScheduleLaserCannon(Ff7BattlePrimaryState state, int source, ushort targets)
    {
        ValidateTargets(targets);
        return ScheduleGroup(state, new(Ff7BattlePrimaryFamily.LaserCannon, targets, 1, Source: source));
    }

    private static Ff7BattlePrimaryCallback? ExecuteLaserCannon(Ff7BattlePrimaryCallback callback, PrimaryPass pass)
    {
        switch (callback)
        {
            case Ff7BattlePrimaryLaserCannonController controller:
                return AdvanceLaserCannonController(controller, pass);
            case Ff7BattlePrimaryLaserCannonBeam beam:
                pass.Emit(new Ff7BattlePrimaryLaserCannonBeamDraw(beam.Source, beam.Target, beam.Age));
                return beam.Age == 7 ? null : beam with { Age = beam.Age + 1 };
            case Ff7BattlePrimaryLaserCannonImpact impact:
                pass.Emit(new Ff7BattlePrimaryLaserCannonImpactDraw(impact.Position, impact.Age));
                return impact.Age == 7 ? null : impact with { Age = impact.Age + 1 };
            case Ff7BattlePrimaryLaserCannonFlare flare:
                // TODO(TD-024): The original flare reads three uninitialized stack words for its world point.
                return flare.Age == 7 ? null : flare with { Age = flare.Age + 1 };
            default:
                throw new InvalidDataException("FFVII Laser Cannon callback has an invalid function identity.");
        }
    }

    private static Ff7BattlePrimaryLaserCannonController? AdvanceLaserCannonController(Ff7BattlePrimaryLaserCannonController controller, PrimaryPass pass)
    {
        if (controller.Age is 3 or 7)
        {
            Func<int, int, Vector3> joint = pass.Context?.JointPoint ?? throw new InvalidOperationException("FFVII Laser Cannon requires its original model joint selectors.");
            if (controller.Age == 3)
            {
                pass.Allocate(new Ff7BattlePrimaryLaserCannonBeam(Ff7BattleBraverPoint.At(joint(controller.Source, 9)), Ff7BattleBraverPoint.At(joint(controller.Target, 0))) { Owner = controller.Owner });
                int flareSlot = pass.Allocate(new Ff7BattlePrimaryLaserCannonFlare { Owner = controller.Owner });
                byte rawPan = unchecked((byte)(0x24 + flareSlot * 0x20));
                pass.PositionedSounds.Add(new(0x3A, -1, rawPan, RawPan: true));
            }
            else
            {
                pass.Allocate(new Ff7BattlePrimaryLaserCannonImpact(Ff7BattleBraverPoint.At(joint(controller.Target, 0))) { Owner = controller.Owner });
                pass.Contact(controller.Target);
                return null;
            }
        }
        return controller with { Age = controller.Age + 1 };
    }
}
