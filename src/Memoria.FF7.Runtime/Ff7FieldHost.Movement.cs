using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7FieldHost
{
    private FieldHostReply MoveActor(FieldHostRequest request, FieldActorMove movement)
    {
        if (movement.TargetEntity is int targetEntity)
        {
            return MoveToActor(request, movement, targetEntity);
        }
        Ff7Actor actor = GetActor(request.Entity);
        if (movement.Animated)
        {
            actor = SelectMovementAnimation(actor);
        }
        bool complete = actor.X4096 == movement.X * 4096 && actor.Y4096 == movement.Y * 4096;
        actor = actor with { AnimationStatus = complete ? 0 : 1 };
        SetActor(request.Entity, _ => actor);
        if (!complete && !Process.Motions.Any(motion => motion.RequestId == request.Id))
        {
            Process = Process with { Motions = [.. Process.Motions, new Ff7Motion(request.Id, request.Entity, movement.X, movement.Y, movement.Turn, movement.Animated)] };
        }
        return new(complete);
    }

    private FieldHostReply MoveToActor(FieldHostRequest request, FieldActorMove movement, int targetEntity)
    {
        Ff7Actor actor = GetActor(request.Entity);
        Ff7Actor target = GetActor(targetEntity);
        Ff7Motion? motion = Process.Motions.SingleOrDefault(item => item.RequestId == request.Id);
        if (motion is { Completed: true })
        {
            Process = Process with { Motions = Process.Motions.Where(item => item.RequestId != request.Id).ToArray() };
            SetActor(request.Entity, current => current with { AnimationStatus = 0 });
            return new(true);
        }
        var goal = new Ff7EntityMotionTarget(targetEntity, target.X4096, target.Y4096, unchecked((short)target.CollisionRadius));
        if (motion is null)
        {
            Process = Process with { Motions = [.. Process.Motions, new Ff7Motion(request.Id, request.Entity, target.X4096 >> 12, target.Y4096 >> 12, movement.Turn, movement.Animated, goal)] };
            SetActor(request.Entity, current => current with { AnimationStatus = 1 });
        }
        else
        {
            Process = Process with { Motions = Process.Motions.Select(item => item.RequestId == request.Id ? item with { TargetX = target.X4096 >> 12, TargetY = target.Y4096 >> 12, EntityTarget = goal } : item).ToArray() };
            SetActor(request.Entity, _ => SelectMovementAnimation(actor) with { AnimationStatus = 1 });
        }
        return new(false);
    }

    private Ff7Actor SelectMovementAnimation(Ff7Actor actor)
    {
        int threshold = unchecked(1536 * field.Scripts.Scale) / 512;
        int animation = actor.MoveSpeed > threshold ? 2 : 1;
        if (actor.Animation == animation)
        {
            return actor;
        }
        int count = animationLength(actor.Model, animation);
        if (count < 1)
        {
            throw new InvalidDataException("FFVII movement animation has no native frames.");
        }
        return actor with { Animation = animation, AnimationSpeed = 16, AnimationFrame16 = 0, LastAnimationFrame = count - 1, AnimationLoop = true, AnimationStopped = false, AnimationFinished = false };
    }
}
