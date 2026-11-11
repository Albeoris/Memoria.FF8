using System.Numerics;

namespace Memoria.FF7.Formats;

public enum Ff7BattlePrimaryFamily
{
    Cure,
    Bolt,
    Destroyed
}

public sealed record Ff7BattlePrimaryTargetGroup(Ff7BattlePrimaryFamily Family, ushort Targets, int Step, Ff7BattleCureKind CureKind = Ff7BattleCureKind.Cure, int NextTarget = 0) : Ff7BattlePrimaryCallback;
public sealed record Ff7BattlePrimaryCureController(int Target, Ff7BattleCureKind Kind, int PreviousBone = 0) : Ff7BattlePrimaryCallback;
public sealed record Ff7BattlePrimaryCureParticle(Ff7BattleCureKind Kind, Ff7BattleCureParticle Particle) : Ff7BattlePrimaryCallback;
public sealed record Ff7BattlePrimaryBoltController(int Target, Ff7BattleBoltStrike Strike) : Ff7BattlePrimaryCallback;
public sealed record Ff7BattlePrimaryBoltSpark(Ff7BattleBoltSpark Spark) : Ff7BattlePrimaryCallback;
public sealed record Ff7BattlePrimaryDestroyedController(int Target) : Ff7BattlePrimaryCallback;
public sealed record Ff7BattlePrimaryDestroyedSprite(Ff7BattleDestroyedSprite Sprite) : Ff7BattlePrimaryCallback;
public sealed record Ff7BattlePrimaryCureDraw(Ff7BattleCureKind Kind, Ff7BattleCureParticle Particle) : Ff7BattlePrimaryDraw;
public sealed record Ff7BattlePrimaryBoltStrikeDraw(Ff7BattleBoltStrike Strike, int Frame) : Ff7BattlePrimaryDraw;
public sealed record Ff7BattlePrimaryBoltSparkDraw(Ff7BattleBoltSpark Spark) : Ff7BattlePrimaryDraw;
public sealed record Ff7BattlePrimaryDestroyedSpriteDraw(Ff7BattleDestroyedSprite Sprite, int Frame) : Ff7BattlePrimaryDraw;
public sealed record Ff7BattlePrimaryDestroyedPulseDraw(byte Blue) : Ff7BattlePrimaryDraw;

public static partial class Ff7BattlePrimaryEffects
{
    public static Ff7BattlePrimaryState ScheduleCure(Ff7BattlePrimaryState state, ushort targets, Ff7BattleCureKind kind = Ff7BattleCureKind.Cure, int owner = 0)
    {
        ValidateTargets(targets);
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }
        int step = BitOperations.PopCount(targets) >= 5 ? 10 : 0;
        return ScheduleGroup(state, new(Ff7BattlePrimaryFamily.Cure, targets, step, kind) { Owner = owner });
    }

    public static Ff7BattlePrimaryState ScheduleBolt(Ff7BattlePrimaryState state, ushort targets, int owner = 0)
    {
        ValidateTargets(targets);
        return ScheduleGroup(state, new(Ff7BattlePrimaryFamily.Bolt, targets, 2) { Owner = owner });
    }

    public static Ff7BattlePrimaryState ScheduleDestroyed(Ff7BattlePrimaryState state, ushort targets, int owner = 0)
    {
        ValidateTargets(targets);
        return ScheduleGroup(state, new(Ff7BattlePrimaryFamily.Destroyed, targets, 4) { Owner = owner });
    }

    private static Ff7BattlePrimaryState ScheduleGroup(Ff7BattlePrimaryState state, Ff7BattlePrimaryTargetGroup group)
    {
        PrimaryPass pass = new(state, null);
        pass.Allocate(group);
        return pass.Finish(state.Tick, preserveReceipts: true);
    }

    private static Ff7BattlePrimaryCallback? ExecuteParticles(Ff7BattlePrimaryCallback callback, PrimaryPass pass)
    {
        Ff7BattlePrimaryContext context = pass.Context ?? throw new InvalidOperationException("FFVII primary particles require their original actor and random owners.");
        switch (callback)
        {
            case Ff7BattlePrimaryTargetGroup group:
                if (group.Age != 0)
                {
                    return group with { Age = group.Age + 1 >= group.Step ? 0 : group.Age + 1 };
                }
                int next = group.NextTarget;
                while (true)
                {
                    while ((group.Targets & (1 << next)) == 0)
                    {
                        next++;
                    }
                    switch (group.Family)
                    {
                        case Ff7BattlePrimaryFamily.Cure:
                            pass.Allocate(new Ff7BattlePrimaryCureController(next, group.CureKind) { Owner = group.Owner });
                            break;
                        case Ff7BattlePrimaryFamily.Bolt:
                            Func<int, int, Vector3> joint = context.JointPoint ?? throw new InvalidOperationException("FFVII Bolt requires original target joint selectors.");
                            Ff7BattleBraverPoint point = Ff7BattleBraverPoint.At(joint(next, 0));
                            pass.Allocate(new Ff7BattlePrimaryBoltController(next, new(point.X, point.Y, point.Z, context.CollisionRadius(next), pass.Tick)) { Owner = group.Owner });
                            break;
                        case Ff7BattlePrimaryFamily.Destroyed:
                            pass.Allocate(new Ff7BattlePrimaryDestroyedController(next) { Owner = group.Owner });
                            break;
                        default:
                            throw new NotSupportedException("FFVII primary target driver has an unsupported initializer.");
                    }
                    next++;
                    if (group.Targets < (1 << next))
                    {
                        return null;
                    }
                    if (group.Step != 0)
                    {
                        return group with { NextTarget = next, Age = 1 };
                    }
                }
            case Ff7BattlePrimaryCureController cure:
                int count = BoneCount(context, cure.Target);
                int bone = cure.PreviousBone;
                for (int spawn = 0; spawn < 2; spawn++)
                {
                    bone = (bone + (context.NextCrt() & 3) + 1) % count;
                    Ff7BattleBraverPoint anchor = Ff7BattleBraverPoint.At(context.ActorPoint(cure.Target, bone, Vector3.Zero));
                    short x = (short)(anchor.X + Ff7BattleCureEffect.Offset(cure.Kind, context.NextCrt()));
                    short y = (short)(anchor.Y + Ff7BattleCureEffect.Offset(cure.Kind, context.NextCrt()));
                    short z = (short)(anchor.Z + Ff7BattleCureEffect.Offset(cure.Kind, context.NextCrt()));
                    byte atlasY = cure.Kind switch
                    {
                        Ff7BattleCureKind.Antidote => (byte)(0x20 + (context.NextCrt() & 1) * 0x60),
                        Ff7BattleCureKind.PhoenixDown => 0x20,
                        _ => 0
                    };
                    int scale = (context.NextCrt() & 4095) + 4096;
                    pass.Allocate(new Ff7BattlePrimaryCureParticle(cure.Kind, new(x, y, z, scale, AtlasY: atlasY)) { Owner = cure.Owner });
                }
                if (cure.Age == Ff7BattleCureEffect.ImpactFrame(cure.Kind))
                {
                    pass.Contact(cure.Target);
                    return null;
                }
                return cure with { Age = cure.Age + 1, PreviousBone = bone };
            case Ff7BattlePrimaryCureParticle cureParticle:
                pass.Emit(new Ff7BattlePrimaryCureDraw(cureParticle.Kind, cureParticle.Particle with { Frame = cureParticle.Age }));
                return cureParticle.Age + 1 == cureParticle.Particle.FrameCount ? null : cureParticle with { Age = cureParticle.Age + 1 };
            case Ff7BattlePrimaryBoltController bolt:
                pass.Emit(new Ff7BattlePrimaryBoltStrikeDraw(bolt.Strike, bolt.Age));
                if (bolt.Age == 0)
                {
                    pass.Contact(bolt.Target);
                }
                if (bolt.Age >= 2)
                {
                    short x = (short)(bolt.Strike.X + context.NextCrt() % 1000 - 500);
                    short y = (short)(bolt.Strike.Y + context.NextCrt() % 1000 - 500);
                    short z = (short)(bolt.Strike.Z + context.NextCrt() % 1000 - 500);
                    int flip = context.NextCrt() & 3;
                    pass.Allocate(new Ff7BattlePrimaryBoltSpark(new(x, y, z, bolt.Strike.CollisionRadius, flip)) { Owner = bolt.Owner });
                }
                return bolt.Age == 15 ? null : bolt with { Age = bolt.Age + 1 };
            case Ff7BattlePrimaryBoltSpark spark:
                pass.Emit(new Ff7BattlePrimaryBoltSparkDraw(spark.Spark with { Frame = spark.Age }));
                return spark.Age == 7 ? null : spark with { Age = spark.Age + 1 };
            case Ff7BattlePrimaryDestroyedController destroyed:
                pass.Emit(new Ff7BattlePrimaryDestroyedPulseDraw((byte)((destroyed.Age & 1) * 32 + 32)));
                if (destroyed.Age < 22)
                {
                    int selectedBone = context.NextCrt() % BoneCount(context, destroyed.Target);
                    Ff7BattleBraverPoint anchor = Ff7BattleBraverPoint.At(context.ActorPoint(destroyed.Target, selectedBone, Vector3.Zero));
                    Ff7BattleBraverPoint position = new((short)(anchor.X + context.NextCrt() % 400 - 200), (short)(anchor.Y + context.NextCrt() % 400 - 200), (short)(anchor.Z + context.NextCrt() % 400 - 200));
                    int scale = 6144 + (context.NextCrt() & 4095);
                    int variant = context.NextCrt() & 1;
                    pass.Allocate(new Ff7BattlePrimaryDestroyedSprite(new(position, pass.Tick, scale, variant)) { Owner = destroyed.Owner });
                }
                if (destroyed.Age == 29)
                {
                    pass.Contact(destroyed.Target);
                    return null;
                }
                return destroyed with { Age = destroyed.Age + 1 };
            case Ff7BattlePrimaryDestroyedSprite destroyedSprite:
                pass.Emit(new Ff7BattlePrimaryDestroyedSpriteDraw(destroyedSprite.Sprite, destroyedSprite.Age));
                return destroyedSprite.Age + 1 == Ff7BattleDestroyedEffect.SpriteFrameCount ? null : destroyedSprite with { Age = destroyedSprite.Age + 1 };
            default:
                throw new NotSupportedException("FFVII particle callback has no admitted executor.");
        }
    }

    private static int BoneCount(Ff7BattlePrimaryContext context, int target)
    {
        Func<int, int> count = context.BoneCount ?? throw new InvalidOperationException("FFVII particle placement requires the original model bone count.");
        int value = count(target);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        return value;
    }

    private static void ValidateTargets(ushort targets)
    {
        if (targets == 0 || (targets & ~0x03F7) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(targets));
        }
    }
}
