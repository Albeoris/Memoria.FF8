using System.Numerics;

namespace Memoria.FF7.Formats;

public sealed record Ff7BattlePrimaryBigBomberController(int Source, int Target, Ff7BattleBraverPoint Start, Ff7BattleBraverPoint End) : Ff7BattlePrimaryCallback;
public sealed record Ff7BattlePrimaryBigBomberMuzzle(Ff7BattleBraverPoint Position, short Yaw) : Ff7BattlePrimaryCallback;
public sealed record Ff7BattlePrimaryBigBomberDebris(Ff7BattleBraverPoint Position, Ff7BattleBraverPoint Velocity, int Sprite, short Roll = 0) : Ff7BattlePrimaryCallback;
public sealed record Ff7BattlePrimaryBigBomberSmoke(Ff7BattleBraverPoint Position, short VerticalStep, short Scale, short Yaw) : Ff7BattlePrimaryCallback;
public sealed record Ff7BattlePrimaryBigBomberWave(Ff7BattleBraverPoint Position) : Ff7BattlePrimaryCallback;
public sealed record Ff7BattlePrimaryBigBomberMeshDraw(int Model, Ff7BattleBraverPoint Position, short Yaw, int Width, int Height, byte Weight) : Ff7BattlePrimaryDraw;
public sealed record Ff7BattlePrimaryBigBomberSpriteDraw(int Sprite, Ff7BattleBraverPoint Position, int Frame, byte Brightness, short Angle, int Scale) : Ff7BattlePrimaryDraw;

public static partial class Ff7BattlePrimaryEffects
{
    private static readonly short[] BigBomberSmokeWidths = [1536, 2560, 3328, 3840, 4096];

    public static Ff7BattlePrimaryState ScheduleBigBomber(Ff7BattlePrimaryState state, int source, ushort targets)
    {
        ValidateTargets(targets);
        return ScheduleGroup(state, new(Ff7BattlePrimaryFamily.BigBomber, targets, 4, Source: source));
    }

    private static void InitializeBigBomberTarget(Ff7BattlePrimaryTargetGroup group, int target, PrimaryPass pass, Ff7BattlePrimaryContext context)
    {
        Func<int, int, Vector3> joint = context.JointPoint ?? throw new InvalidOperationException("FFVII Big Bomber requires original model joint selectors.");
        pass.Allocate(new Ff7BattlePrimaryBigBomberController(group.Source, target, Ff7BattleBraverPoint.At(joint(group.Source, 0)), Ff7BattleBraverPoint.At(joint(target, 0))) { Owner = group.Owner });
        pass.PositionedSounds.Add(new(20, group.Source, 0));
    }

    private static Ff7BattlePrimaryCallback? ExecuteBigBomber(Ff7BattlePrimaryCallback callback, PrimaryPass pass)
    {
        switch (callback)
        {
            case Ff7BattlePrimaryBigBomberController controller:
                return AdvanceBigBomberController(controller, pass);
            case Ff7BattlePrimaryBigBomberMuzzle muzzle:
                pass.Emit(new Ff7BattlePrimaryBigBomberSpriteDraw(7, muzzle.Position, muzzle.Age, 255, muzzle.Yaw, 12288));
                return muzzle.Age == 7 ? null : muzzle with { Age = muzzle.Age + 1 };
            case Ff7BattlePrimaryBigBomberDebris debris:
                int brightness = debris.Age < 12 ? 255 : Math.Min(255, (20 - debris.Age) << 5);
                pass.Emit(new Ff7BattlePrimaryBigBomberSpriteDraw(debris.Sprite / 3 == 2 ? 5 : 6, debris.Position, debris.Sprite % 3, (byte)brightness, debris.Roll, 8192));
                return debris.Age == 19 ? null : debris with { Age = debris.Age + 1, Position = Ff7BattleEffectMatrix.Add(debris.Position, debris.Velocity), Velocity = debris.Velocity with { Y = (short)(debris.Velocity.Y + 20) }, Roll = (short)(debris.Roll + 512) };
            case Ff7BattlePrimaryBigBomberSmoke smoke:
                pass.Emit(new Ff7BattlePrimaryBigBomberMeshDraw(3, smoke.Position, smoke.Yaw, smoke.Scale, 4096, Ff7BattleStageFade.Alpha((short)(smoke.Age * 200))));
                return smoke.Age == 19 ? null : smoke with { Age = smoke.Age + 1, Position = smoke.Position with { Y = (short)(smoke.Position.Y + smoke.VerticalStep) }, Scale = (short)(smoke.Scale + 512), Yaw = (short)(smoke.Yaw + 128) };
            case Ff7BattlePrimaryBigBomberWave wave:
                int scale = 1536 + wave.Age * 240;
                int ringScale = 2048 + wave.Age * 320;
                short yaw = (short)(wave.Age * 64);
                byte weight = (byte)Math.Min(255, (128 - wave.Age * 6) * 2);
                pass.Emit(new Ff7BattlePrimaryBigBomberMeshDraw(0, wave.Position, yaw, scale, scale, weight));
                pass.Emit(new Ff7BattlePrimaryBigBomberMeshDraw(2, wave.Position, yaw, ringScale, ringScale, weight));
                pass.Emit(new Ff7BattlePrimaryBigBomberMeshDraw(1, wave.Position, yaw, scale, scale, Ff7BattleStageFade.Alpha((short)(3072 + wave.Age * 48))));
                pass.Emit(new Ff7BattlePrimaryBigBomberMeshDraw(4, wave.Position, yaw, scale, scale, Ff7BattleStageFade.Alpha((short)(wave.Age * 96))));
                return wave.Age == 19 ? null : wave with { Age = wave.Age + 1 };
            default:
                throw new InvalidDataException("FFVII Big Bomber callback has an invalid function identity.");
        }
    }

    private static Ff7BattlePrimaryBigBomberController? AdvanceBigBomberController(Ff7BattlePrimaryBigBomberController controller, PrimaryPass pass)
    {
        Ff7BattlePrimaryContext context = pass.Context!;
        if (controller.Age == 0)
        {
            short yaw = (short)((int)((double)(float)Math.Atan2(controller.End.X - controller.Start.X, controller.End.Z - controller.Start.Z) * (180 / Math.PI) * 11.377778f) + 1024);
            Func<int, int, Vector3> joint = context.JointPoint ?? throw new InvalidOperationException("FFVII Big Bomber requires its two muzzle joints.");
            pass.Allocate(new Ff7BattlePrimaryBigBomberMuzzle(Ff7BattleBraverPoint.At(joint(controller.Source, 11)), yaw) { Owner = controller.Owner });
            pass.Allocate(new Ff7BattlePrimaryBigBomberMuzzle(Ff7BattleBraverPoint.At(joint(controller.Source, 12)), yaw) { Owner = controller.Owner });
        }
        if (controller.Age == 20)
        {
            Func<int> crt = context.NextCrt;
            for (int side = 0; side < 2; side++)
            {
                int angle = 512 + side * 2048;
                Ff7BattleBraverPoint center = new((short)(controller.End.X + (SilkCosine(angle) * 800 >> 12)), 0, (short)(controller.End.Z + (SilkSine(angle) * 800 >> 12)));
                for (int fragment = 0; fragment < 24; fragment++)
                {
                    Ff7BattleBraverPoint point = new((short)(center.X + crt() % 400 - 200), 0, (short)(center.Z + crt() % 400 - 200));
                    short lift = (short)(-350 + 2 * (crt() % 50));
                    BattleRotationWords rotation = new((short)(crt() % 800 - 400), 0, (short)(crt() % 800 - 400));
                    Ff7BattleBraverPoint velocity = Ff7BattleEffectMatrix.Transform(new(0, lift, 0), Ff7BattleEffectMatrix.RotateXyz(rotation));
                    pass.Allocate(new Ff7BattlePrimaryBigBomberDebris(point, velocity, crt() % 9) { Owner = controller.Owner });
                }
                pass.Allocate(new Ff7BattlePrimaryBigBomberWave(center) { Owner = controller.Owner });
                for (int layer = 0; layer < BigBomberSmokeWidths.Length; layer++)
                {
                    int offset = layer - 4;
                    pass.Allocate(new Ff7BattlePrimaryBigBomberSmoke(center with { Y = (short)(offset * 80) }, (short)(offset * 10), BigBomberSmokeWidths[layer], (short)(crt() & 4095)) { Owner = controller.Owner });
                }
            }
            pass.PositionedSounds.Add(new(250, controller.Target, 0));
        }
        if (controller.Age == 35)
        {
            pass.Contact(controller.Target);
        }
        return controller.Age == 44 ? null : controller with { Age = controller.Age + 1 };
    }
}
