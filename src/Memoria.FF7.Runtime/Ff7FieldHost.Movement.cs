using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7FieldHost
{
    private FieldHostReply MoveActor(FieldHostRequest request, FieldActorMove movement)
    {
        Ff7Actor actor = GetActor(request.Entity);
        if (movement.Animated)
        {
            int threshold = unchecked(1536 * field.Scripts.Scale) / 512;
            int animation = actor.MoveSpeed > threshold ? 2 : 1;
            if (actor.Animation != animation)
            {
                int count = animationLength(actor.Model, animation);
                if (count < 1)
                {
                    throw new InvalidDataException("FFVII movement animation has no native frames.");
                }
                actor = actor with { Animation = animation, AnimationSpeed = 16, AnimationFrame16 = 0, LastAnimationFrame = count - 1, AnimationLoop = true, AnimationStopped = false, AnimationFinished = false };
            }
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
}
