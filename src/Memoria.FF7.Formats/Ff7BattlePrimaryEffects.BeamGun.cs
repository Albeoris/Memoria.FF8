using System.Numerics;

namespace Memoria.FF7.Formats;

public sealed record Ff7BattlePrimaryBeamGunController(int Target, int PrimaryTarget, Ff7BattleBraverPoint Source, Ff7BattleBraverPoint Destination) : Ff7BattlePrimaryCallback;
public sealed record Ff7BattlePrimaryBeamGunFlash(Ff7BattleBraverPoint Position) : Ff7BattlePrimaryCallback;
public sealed record Ff7BattlePrimaryBeamGunShot(Ff7BattleBraverPoint Position, Ff7BattleBraverPoint Delta) : Ff7BattlePrimaryCallback;
public sealed record Ff7BattlePrimaryBeamGunBurst(Ff7BattleBraverPoint Position, int Radius) : Ff7BattlePrimaryCallback;
public sealed record Ff7BattlePrimaryBeamGunMeshDraw(Ff7BattleBraverPoint Position, Ff7BattleSearchScopeBasis Basis) : Ff7BattlePrimaryDraw;
public sealed record Ff7BattlePrimaryBeamGunSpriteDraw(Ff7BattleBraverPoint Position, int Frame, int Radius = 0, Ff7BattleSearchScopeBasis? FlashBasis = null) : Ff7BattlePrimaryDraw;

public static partial class Ff7BattlePrimaryEffects
{
    private static readonly Ff7BattleSearchScopeBasis BeamGunFlashRotation = Ff7BattleEffectMatrix.RotateXyz(new(0, -1024, 0));
    private static readonly Ff7BattleSearchScopeBasis BeamGunShotRotation = Ff7BattleEffectMatrix.RotateXyz(new(-1024, 0, 0));

    public static Ff7BattlePrimaryState ScheduleBeamGun(Ff7BattlePrimaryState state, int source, ushort targets)
    {
        ValidateTargets(targets);
        return ScheduleGroup(state, new(Ff7BattlePrimaryFamily.BeamGun, targets, 4, Source: source));
    }

    private static void InitializeBeamGunTarget(Ff7BattlePrimaryTargetGroup group, int target, PrimaryPass pass, Ff7BattlePrimaryContext context)
    {
        Func<int, int, Vector3> joint = context.JointPoint ?? throw new InvalidOperationException("FFVII Beam Gun requires original model joint selectors.");
        Ff7BattleBraverPoint start = Ff7BattleBraverPoint.At(joint(group.Source, 6));
        Ff7BattleBraverPoint end = Ff7BattleBraverPoint.At(joint(target, 0));
        Ff7BattleBraverPoint delta = new((short)((end.X - start.X) / 8), (short)((end.Y - start.Y) / 8), (short)((end.Z - start.Z) / 8));
        pass.BeamGunBasis = Ff7BattleSearchScopeEffect.AimFromDirection(delta);
        pass.Allocate(new Ff7BattlePrimaryBeamGunController(target, BitOperations.TrailingZeroCount(group.Targets), start, end) { Owner = group.Owner });
    }

    private static Ff7BattlePrimaryCallback? ExecuteBeamGun(Ff7BattlePrimaryCallback callback, PrimaryPass pass)
    {
        Ff7BattleSearchScopeBasis basis = pass.BeamGunBasis ?? throw new InvalidDataException("FFVII Beam Gun has no original aiming matrix.");
        switch (callback)
        {
            case Ff7BattlePrimaryBeamGunController controller:
                return AdvanceBeamGunController(controller, pass, basis);

            case Ff7BattlePrimaryBeamGunFlash flash:
                Ff7BattleSearchScopeBasis flashBasis = Ff7BattleEffectMatrix.Scale(Ff7BattleEffectMatrix.Compose(BeamGunFlashRotation, basis), 8192, 8192, 8192);
                pass.Emit(new Ff7BattlePrimaryBeamGunSpriteDraw(flash.Position, flash.Age, FlashBasis: flashBasis));
                return flash.Age == 3 ? null : flash with { Age = flash.Age + 1 };

            case Ff7BattlePrimaryBeamGunShot shot:
                Ff7BattleSearchScopeBasis shotBasis = Ff7BattleEffectMatrix.Scale(Ff7BattleEffectMatrix.Compose(BeamGunShotRotation, basis), 1280, 1280, 1280);
                pass.Emit(new Ff7BattlePrimaryBeamGunMeshDraw(shot.Position, shotBasis));
                return shot.Age == 6 ? null : shot with { Age = shot.Age + 1, Position = Ff7BattleEffectMatrix.Add(shot.Position, shot.Delta) };

            case Ff7BattlePrimaryBeamGunBurst burst:
                pass.Emit(new Ff7BattlePrimaryBeamGunSpriteDraw(burst.Position, burst.Age, burst.Radius));
                return burst.Age == 7 ? null : burst with { Age = burst.Age + 1 };

            default:
                throw new InvalidDataException("FFVII Beam Gun callback has an invalid function identity.");
        }
    }

    private static Ff7BattlePrimaryBeamGunController? AdvanceBeamGunController(Ff7BattlePrimaryBeamGunController controller, PrimaryPass pass, Ff7BattleSearchScopeBasis basis)
    {
        if (controller.Age == 0)
        {
            controller = controller with { Source = Ff7BattleEffectMatrix.Add(controller.Source, Ff7BattleEffectMatrix.Transform(new(0, 0, 500), basis)) };
            pass.Allocate(new Ff7BattlePrimaryBeamGunFlash(controller.Source) { Owner = controller.Owner });
        }
        Ff7BattlePrimaryContext context = pass.Context!;
        Func<int> crt = context.NextCrt;
        if (controller.Age < 15)
        {
            Ff7BattleBraverPoint delta = new((short)((controller.Destination.X - controller.Source.X) / 8), (short)((controller.Destination.Y - controller.Source.Y) / 8), (short)((controller.Destination.Z - controller.Source.Z) / 8));
            for (int index = 0; index < 3; index++)
            {
                int angle = controller.Age * 512 + index * 1365;
                Ff7BattleBraverPoint offset = new((short)(SilkCosine(angle) >> 5), (short)(SilkSine(angle) >> 5), (short)(crt() % 100 - 50));
                Ff7BattleBraverPoint point = Ff7BattleEffectMatrix.Add(controller.Source, Ff7BattleEffectMatrix.Transform(offset, basis));
                pass.Allocate(new Ff7BattlePrimaryBeamGunShot(point, delta) { Owner = controller.Owner });
            }
        }
        if (controller.Age is >= 9 and < 28)
        {
            int bone = context.JointSelector?.Invoke(controller.PrimaryTarget, 0) ?? throw new InvalidOperationException("FFVII Beam Gun requires its primary target's original joint selector.");
            Ff7BattleBraverPoint anchor = Ff7BattleBraverPoint.At(context.ActorPoint(controller.Target, bone, Vector3.Zero));
            Ff7BattleBraverPoint jitter = new((short)(crt() % 800 - 400), (short)(crt() % 800 - 400), (short)(crt() % 800 - 400));
            pass.Allocate(new Ff7BattlePrimaryBeamGunBurst(Ff7BattleEffectMatrix.Add(anchor, jitter), context.CollisionRadius(controller.PrimaryTarget)) { Owner = controller.Owner });
        }
        if (controller.Age == 15)
        {
            pass.Contact(controller.Target);
        }
        return controller.Age == 39 ? null : controller with { Age = controller.Age + 1 };
    }
}
