using System.Numerics;

namespace Memoria.FF7.Formats;

public sealed record Ff7BattlePrimaryMindblowGroup(int Source, ushort Targets) : Ff7BattlePrimaryCallback;
public sealed record Ff7BattlePrimaryMindblowController(int Source, int Target) : Ff7BattlePrimaryCallback;
public sealed record Ff7BattlePrimaryMindblowMesh(int Source) : Ff7BattlePrimaryCallback;
public sealed record Ff7BattlePrimaryMindblowParticle(Ff7BattleBraverPoint Point, Ff7BattleBraverPoint Velocity, int Frame, int Scale, bool Impact) : Ff7BattlePrimaryCallback;
public sealed record Ff7BattlePrimaryMindblowMeshDraw(Ff7BattleBraverPoint Point, Ff7BattleSearchScopeBasis Basis, bool Inner) : Ff7BattlePrimaryDraw;
public sealed record Ff7BattlePrimaryMindblowPulseDraw(byte Brightness) : Ff7BattlePrimaryDraw;
public sealed record Ff7BattlePrimaryMindblowParticleDraw(Ff7BattleBraverPoint Point, int Frame, int Scale, int Brightness) : Ff7BattlePrimaryDraw;

public static partial class Ff7BattlePrimaryEffects
{
    public static Ff7BattlePrimaryState ScheduleMindblow(Ff7BattlePrimaryState state, int source, ushort targets)
    {
        if (targets == 0)
        {
            throw new InvalidDataException("FFVII Mindblow requires its original target mask.");
        }
        return Schedule(state, new Ff7BattlePrimaryMindblowGroup(source, targets));
    }

    private static Ff7BattlePrimaryCallback? ExecuteMindblow(Ff7BattlePrimaryCallback callback, PrimaryPass pass)
    {
        Ff7BattlePrimaryContext context = pass.Context ?? throw new InvalidOperationException("FFVII Mindblow requires its actor geometry and visual RNG.");
        Func<int, Vector3, Vector3> weapon = context.WeaponPoint ?? throw new InvalidOperationException("FFVII Mindblow requires the source weapon matrix.");
        Func<int, int, Vector3> joint = context.JointPoint ?? throw new InvalidOperationException("FFVII Mindblow requires target joint selector zero.");
        switch (callback)
        {
            case Ff7BattlePrimaryMindblowGroup group:
                int target = BitOperations.TrailingZeroCount(group.Targets);
                pass.Allocate(new Ff7BattlePrimaryMindblowController(group.Source, target) { Owner = group.Owner });
                ushort remaining = (ushort)(group.Targets & ~(1 << target));
                return remaining == 0 ? null : group with { Targets = remaining };
            case Ff7BattlePrimaryMindblowController controller:
                if (controller.Age == 0)
                {
                    pass.Allocate(new Ff7BattlePrimaryMindblowMesh(controller.Source) { Owner = controller.Owner });
                    Ff7BattleBraverPoint root = Ff7BattleBraverPoint.At(context.ActorPoint(controller.Source, 0, Vector3.Zero));
                    pass.PositionedSounds.Add(new(40, -1, 64, Point: root));
                }
                if (controller.Age < 14)
                {
                    Ff7BattleBraverPoint destination = Ff7BattleBraverPoint.At(weapon(controller.Source, new(0, 0, -180)));
                    for (int index = 0; index < 2; index++)
                    {
                        int radius = -1000 - context.NextCrt() % 1000;
                        BattleRotationWords rotation = new((short)((context.NextCrt() & 0x3FF) - 0x200), (short)((context.NextCrt() & 0x3FF) - 0x200), 0);
                        Ff7BattleBraverPoint local = Ff7BattleEffectMatrix.Transform(new(0, 0, (short)radius), Ff7BattleEffectMatrix.RotateXyz(rotation));
                        Ff7BattleBraverPoint position = Ff7BattleBraverPoint.At(weapon(controller.Source, local.Position + new Vector3(0, 0, -180)));
                        Ff7BattleBraverPoint velocity = new((short)((destination.X - position.X) / 8), (short)((destination.Y - position.Y) / 8), (short)((destination.Z - position.Z) / 8));
                        pass.Allocate(new Ff7BattlePrimaryMindblowParticle(position, velocity, context.NextCrt() % 3, 4096, false) { Owner = controller.Owner });
                    }
                }
                if (controller.Age == 22)
                {
                    pass.Contact(controller.Target);
                    pass.Emit(new Ff7BattlePrimaryMindblowPulseDraw(255));
                }
                else if (controller.Age == 23)
                {
                    pass.Emit(new Ff7BattlePrimaryMindblowPulseDraw(127));
                }
                if (controller.Age is >= 22 and < 25)
                {
                    Ff7BattleBraverPoint position = Ff7BattleBraverPoint.At(joint(controller.Target, 0));
                    for (int index = 0; index < 20; index++)
                    {
                        int radius = -300 - context.NextCrt() % 500;
                        BattleRotationWords rotation = new((short)((context.NextCrt() & 0x3FF) - 0x200), (short)(context.ActorRotation(controller.Source).Y + (context.NextCrt() & 0x3FF) - 0x200), 0);
                        Ff7BattleBraverPoint velocity = Ff7BattleEffectMatrix.Transform(new(0, 0, (short)radius), Ff7BattleEffectMatrix.RotateXyz(rotation));
                        int scale = (context.NextCrt() & 0x7FF) + 2048;
                        pass.Allocate(new Ff7BattlePrimaryMindblowParticle(position, velocity, context.NextCrt() % 3, scale, true) { Owner = controller.Owner });
                    }
                }
                return controller.Age == 44 ? null : controller with { Age = controller.Age + 1 };
            case Ff7BattlePrimaryMindblowMesh mesh:
                int scaleWord = 64 + mesh.Age * 448 / 22;
                Ff7BattleBraverPoint origin = Ff7BattleBraverPoint.At(weapon(mesh.Source, new(0, 0, -180)));
                Ff7BattleSearchScopeBasis weaponBasis = new(Axis(Vector3.UnitX), Axis(Vector3.UnitY), Axis(Vector3.UnitZ));
                EmitMesh((short)((mesh.Age + 1) * 640), scaleWord, false);
                EmitMesh((short)(-(mesh.Age + 1) * 448), scaleWord * 0x1200 / 4096, true);
                return mesh.Age == 21 ? null : mesh with { Age = mesh.Age + 1 };

                Ff7BattleBraverPoint Axis(Vector3 axis)
                {
                    Vector3 end = weapon(mesh.Source, axis * 4096 + new Vector3(0, 0, -180));
                    return Ff7BattleBraverPoint.At(end - origin.Position);
                }

                void EmitMesh(short spin, int scale, bool inner)
                {
                    Ff7BattleSearchScopeBasis local = Ff7BattleEffectMatrix.Scale(Ff7BattleEffectMatrix.RotateXyz(new(0, spin, 0)), scale, scale, scale);
                    pass.Emit(new Ff7BattlePrimaryMindblowMeshDraw(origin, Ff7BattleEffectMatrix.Compose(local, weaponBasis), inner));
                }
            case Ff7BattlePrimaryMindblowParticle particle:
                if (particle.Age >= (particle.Impact ? 15 : 8))
                {
                    return null;
                }
                int brightness = particle.Impact ? 128 - Math.Max(0, particle.Age - 5) * 128 / 10 : Math.Min(128, particle.Age * 32);
                pass.Emit(new Ff7BattlePrimaryMindblowParticleDraw(particle.Point, particle.Frame, particle.Scale, brightness));
                Ff7BattleBraverPoint nextVelocity = particle.Impact ? new((short)(particle.Velocity.X * 3840 >> 12), (short)(particle.Velocity.Y * 3840 >> 12), (short)(particle.Velocity.Z * 3840 >> 12)) : particle.Velocity;
                return particle with { Point = Ff7BattleEffectMatrix.Add(particle.Point, particle.Velocity), Velocity = nextVelocity, Age = particle.Age + 1 };
            default:
                throw new NotSupportedException("FFVII Mindblow callback has no admitted executor.");
        }
    }
}
