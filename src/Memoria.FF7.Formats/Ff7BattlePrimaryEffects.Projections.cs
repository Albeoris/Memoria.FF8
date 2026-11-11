using System.Numerics;

namespace Memoria.FF7.Formats;

public static partial class Ff7BattlePrimaryEffects
{
    public static Ff7BattleCureState ProjectCure(Ff7BattlePrimaryState primary, Ff7BattleCureState previous, int owner = 0, bool retainPrimary = false)
    {
        Ff7BattleCureTarget[] targets = previous.Targets.Select(target => primary.Slots.OfType<Ff7BattlePrimaryCureController>().FirstOrDefault(controller => controller.Owner == owner && controller.Target == target.Slot) is { } controller ? target with { PreviousBone = controller.PreviousBone } : target).ToArray();
        Ff7BattleCureParticle[] particles = primary.Draws.OfType<Ff7BattlePrimaryCureDraw>().Where(draw => draw.Owner == owner).Select(draw => draw.Particle).ToArray();
        return previous with { Frame = previous.Frame + 1, Targets = targets, Particles = particles, Primary = retainPrimary ? primary : null };
    }

    public static Ff7BattleBoltState ProjectBolt(Ff7BattlePrimaryState primary, Ff7BattleBoltState previous, int owner = 0, bool retainPrimary = false)
    {
        int frame = previous.Frame + 1;
        Ff7BattleBoltStrike[] strikes = primary.Draws.OfType<Ff7BattlePrimaryBoltStrikeDraw>().Where(draw => draw.Owner == owner).Select(draw => draw.Strike with { StartedAt = frame - draw.Frame }).ToArray();
        Ff7BattleBoltSpark[] sparks = primary.Draws.OfType<Ff7BattlePrimaryBoltSparkDraw>().Where(draw => draw.Owner == owner).Select(draw => draw.Spark).ToArray();
        return previous with { Frame = frame, Strikes = strikes, Sparks = sparks, Primary = retainPrimary ? primary : null };
    }

    public static Ff7BattleDestroyedState ProjectDestroyed(Ff7BattlePrimaryState primary, Ff7BattleDestroyedState previous, int owner = 0, bool retainPrimary = false)
    {
        int frame = previous.Frame + 1;
        Ff7BattleDestroyedSprite[] sprites = primary.Draws.OfType<Ff7BattlePrimaryDestroyedSpriteDraw>().Where(draw => draw.Owner == owner).Select(draw => draw.Sprite with { StartedAt = frame - draw.Frame }).ToArray();
        return previous with { Frame = frame, Sprites = sprites, Primary = retainPrimary ? primary : null };
    }

    public static Ff7BattleSearchScopeState ProjectSearchScope(Ff7BattlePrimaryState primary, Ff7BattleSearchScopeState previous, int owner = 0, bool retainPrimary = false)
    {
        int frame = previous.Frame + 1;
        Ff7BattleSearchScopeMesh[] meshes = primary.Draws.OfType<Ff7BattlePrimaryScopeMeshDraw>().Where(draw => draw.Owner == owner).Select(draw => draw.Mesh).ToArray();
        Ff7BattleSearchScopeSprite[] sprites = primary.Draws.OfType<Ff7BattlePrimaryScopeSpriteDraw>().Where(draw => draw.Owner == owner).Select(draw => new Ff7BattleSearchScopeSprite(frame - draw.Frame - (draw.Stage == 0 ? 0 : Ff7BattleSearchScopeEffect.FirstSpriteFrames), draw.Position)).ToArray();
        return previous with { Frame = frame, ScaleCounter = primary.SearchScopeCounter, Meshes = meshes, Sprites = sprites, Primary = retainPrimary ? primary : null };
    }

    public static Ff7BattlePrimaryState ImportCure(Ff7BattlePrimaryState state, Ff7BattleCureState previous, int owner = 0)
    {
        if (previous.Frame < 0)
        {
            return ScheduleCure(state, TargetMask(previous.Targets.Select(target => target.Slot)), previous.Kind, owner);
        }
        PrimaryPass pass = new(state, null) { CurrentOwner = owner };
        ushort targets = TargetMask(previous.Targets.Select(target => target.Slot));
        int step = previous.Targets.Count >= 5 ? 10 : 0;
        bool pending = false;
        for (int index = 0; index < previous.Targets.Count; index++)
        {
            Ff7BattleCureTarget target = previous.Targets[index];
            int age = previous.Frame - index * step + 1;
            if (age <= 0)
            {
                if (!pending)
                {
                    pass.Allocate(new Ff7BattlePrimaryTargetGroup(Ff7BattlePrimaryFamily.Cure, targets, step, previous.Kind, target.Slot) { Owner = owner, Age = (previous.Frame + 1) % step });
                    pending = true;
                }
            }
            else if (age <= Ff7BattleCureEffect.ImpactFrame(previous.Kind))
            {
                pass.Allocate(new Ff7BattlePrimaryCureController(target.Slot, previous.Kind, target.PreviousBone) { Owner = owner, Age = age });
            }
        }
        foreach (Ff7BattleCureParticle particle in previous.Particles)
        {
            pass.Emit(new Ff7BattlePrimaryCureDraw(previous.Kind, particle));
            if (particle.Frame + 1 < particle.FrameCount)
            {
                pass.Allocate(new Ff7BattlePrimaryCureParticle(previous.Kind, particle with { Frame = 0 }) { Owner = owner, Age = particle.Frame + 1 });
            }
        }
        return pass.Finish(state.Tick, preserveReceipts: true);
    }

    public static Ff7BattlePrimaryState ImportBolt(Ff7BattlePrimaryState state, Ff7BattleBoltState previous, Ff7BattlePrimaryContext context, int owner = 0)
    {
        if (previous.Frame < 0)
        {
            return ScheduleBolt(state, previous.TargetMask, owner);
        }
        PrimaryPass pass = new(state, context) { CurrentOwner = owner };
        int ordinal = 0;
        bool pending = false;
        for (int slot = 0; slot < 10; slot++)
        {
            if ((previous.TargetMask & (1 << slot)) == 0)
            {
                continue;
            }
            int age = previous.Frame - ordinal * 2;
            if (age < 0)
            {
                if (!pending)
                {
                    pass.Allocate(new Ff7BattlePrimaryTargetGroup(Ff7BattlePrimaryFamily.Bolt, previous.TargetMask, 2, NextTarget: slot) { Owner = owner, Age = (previous.Frame + 1) % 2 });
                    pending = true;
                }
            }
            else if (age < 16)
            {
                Ff7BattleBoltStrike strike;
                if (previous.Strikes is { } strikes && ordinal < strikes.Count)
                {
                    strike = strikes[ordinal];
                }
                else
                {
                    Func<int, int, Vector3> joint = context.JointPoint ?? throw new InvalidOperationException("FFVII Bolt presentation import requires its saved or selected-resource target anchor.");
                    Ff7BattleBraverPoint anchor = Ff7BattleBraverPoint.At(joint(slot, 0));
                    strike = new(anchor.X, anchor.Y, anchor.Z, context.CollisionRadius(slot), ordinal * 2);
                }
                pass.Emit(new Ff7BattlePrimaryBoltStrikeDraw(strike, age));
                if (age + 1 < 16)
                {
                    pass.Allocate(new Ff7BattlePrimaryBoltController(slot, strike) { Owner = owner, Age = age + 1 });
                }
            }
            ordinal++;
        }
        foreach (Ff7BattleBoltSpark spark in previous.Sparks ?? [])
        {
            pass.Emit(new Ff7BattlePrimaryBoltSparkDraw(spark));
            if (spark.Frame < 7)
            {
                pass.Allocate(new Ff7BattlePrimaryBoltSpark(spark with { Frame = 0 }) { Owner = owner, Age = spark.Frame + 1 });
            }
        }
        return pass.Finish(state.Tick, preserveReceipts: true);
    }

    public static Ff7BattlePrimaryState ImportDestroyed(Ff7BattlePrimaryState state, Ff7BattleDestroyedState previous, int owner = 0)
    {
        if (previous.Frame < 0)
        {
            return ScheduleDestroyed(state, previous.TargetMask, owner);
        }
        PrimaryPass pass = new(state, null) { CurrentOwner = owner };
        int ordinal = 0;
        bool pending = false;
        for (int slot = 0; slot < 10; slot++)
        {
            if ((previous.TargetMask & (1 << slot)) == 0)
            {
                continue;
            }
            int age = previous.Frame - ordinal++ * 4;
            if (age < 0)
            {
                if (!pending)
                {
                    pass.Allocate(new Ff7BattlePrimaryTargetGroup(Ff7BattlePrimaryFamily.Destroyed, previous.TargetMask, 4, NextTarget: slot) { Owner = owner, Age = (previous.Frame + 1) % 4 });
                    pending = true;
                }
            }
            else if (age < 30)
            {
                pass.Emit(new Ff7BattlePrimaryDestroyedPulseDraw((byte)((age & 1) * 32 + 32)));
                if (age + 1 < 30)
                {
                    pass.Allocate(new Ff7BattlePrimaryDestroyedController(slot) { Owner = owner, Age = age + 1 });
                }
            }
        }
        foreach (Ff7BattleDestroyedSprite sprite in previous.Sprites ?? [])
        {
            int age = previous.Frame - sprite.StartedAt;
            pass.Emit(new Ff7BattlePrimaryDestroyedSpriteDraw(sprite, age));
            if (age + 1 < Ff7BattleDestroyedEffect.SpriteFrameCount)
            {
                pass.Allocate(new Ff7BattlePrimaryDestroyedSprite(sprite) { Owner = owner, Age = age + 1 });
            }
        }
        return pass.Finish(state.Tick, preserveReceipts: true);
    }

    private static ushort TargetMask(IEnumerable<int> targets)
    {
        ushort mask = 0;
        foreach (int target in targets)
        {
            mask |= (ushort)(1 << target);
        }
        return mask;
    }
}
