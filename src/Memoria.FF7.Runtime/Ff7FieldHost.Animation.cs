using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7FieldHost
{
    private FieldHostReply Animate(FieldHostRequest request, FieldAnimation animation)
    {
        Ff7Actor actor = GetActor(request.Entity);
        if (animation.Speed <= 0)
        {
            throw new InvalidDataException("FFVII animation divisor must be positive.");
        }
        if (animation.Default)
        {
            SetActor(request.Entity, current => current with
            {
                DefaultAnimation = animation.Animation,
                DefaultAnimationSpeed = current.BaseAnimationSpeed / animation.Speed,
                AnimationStatus = current.AnimationStatus == 3 ? 0 : current.AnimationStatus,
                AnimationStopped = false
            });
            return new(true);
        }
        if (actor.AnimationStatus is 0 or 1 or 3)
        {
            int count = animationLength(actor.Model, animation.Animation);
            if (count < 1)
            {
                throw new InvalidDataException("FFVII animation has no native frames.");
            }
            int first = animation.FirstFrame ?? 0;
            int initial = animation.DivideFirstFrameBySpeed ? 16 * (first / animation.Speed) : 16 * first;
            int last = animation.LastFrame is int requested ? Math.Min(count - 1, requested / animation.Speed) : count - 1;
            SetActor(request.Entity, current => current with
            {
                Animation = animation.Animation,
                AnimationSpeed = current.BaseAnimationSpeed / animation.Speed,
                AnimationFrame16 = initial,
                FirstAnimationFrame = first,
                LastAnimationFrame = last,
                AnimationStatus = animation.Blocking ? 2 : animation.HoldLast ? 6 : 5,
                HoldAnimation = animation.HoldLast,
                AnimationLoop = false,
                AnimationStopped = false,
                AnimationFinished = false
            });
            Process = Process with { AnimationRequests = [.. Process.AnimationRequests.Where(item => item.RequestId != request.Id), new Ff7AnimationRequest(request.Id, request.Entity)] };
            return new(!animation.Blocking);
        }
        if (actor.AnimationStatus == 4)
        {
            SetActor(request.Entity, current => current with { AnimationStatus = animation.HoldLast ? 3 : 0, HoldAnimation = animation.HoldLast, AnimationFinished = true });
            return new(true);
        }
        return new(false);
    }

    private FieldHostReply WaitForAnimation(int entity)
    {
        Ff7Actor actor = GetActor(entity);
        if (actor.AnimationStatus is 2 or 5 or 6)
        {
            return new(false);
        }
        if (actor.AnimationStatus == 4)
        {
            SetActor(entity, current => current with { AnimationStatus = 0 });
        }
        return new(true);
    }

    private FieldHostReply StopAnimation(int entity)
    {
        SetActor(entity, current => current with { LastAnimationFrame = current.AnimationFrame16 >> 4, AnimationStatus = 3, HoldAnimation = true, AnimationStopped = true });
        return new(true);
    }

    private void AdvanceAnimation()
    {
        foreach (Ff7Actor actor in Process.Actors.Where(actor => actor.Ladder is null && actor.Jump is null))
        {
            int count = animationLength(actor.Model, actor.Animation);
            if (count < 1)
            {
                throw new InvalidDataException("FFVII actor animation has no native frames.");
            }
            Ff7Actor next = actor;
            int last = actor.LastAnimationFrame < 0 ? count - 1 : actor.LastAnimationFrame;
            bool scriptControlled = !actor.PlayerControlled || Process.PlayerLocked || Process.Join is not null;
            if (scriptControlled)
            {
                switch (actor.AnimationStatus)
                {
                    case 0:
                        if (actor.Animation != actor.DefaultAnimation)
                        {
                            int defaultCount = animationLength(actor.Model, actor.DefaultAnimation);
                            if (defaultCount < 1)
                            {
                                throw new InvalidDataException("FFVII default animation has no native frames.");
                            }
                            last = defaultCount - 1;
                            next = next with { Animation = actor.DefaultAnimation, AnimationSpeed = actor.DefaultAnimationSpeed, AnimationFrame16 = 0, LastAnimationFrame = last, AnimationLoop = true, AnimationStopped = false, AnimationFinished = false };
                        }
                        else
                        {
                            last = count - 1;
                            next = next with { LastAnimationFrame = last, AnimationLoop = true };
                            if ((actor.AnimationFrame16 >> 4) >= last)
                            {
                                next = next with { AnimationFrame16 = 0 };
                            }
                        }
                        break;
                    case 1:
                        if ((actor.AnimationFrame16 >> 4) >= last)
                        {
                            next = next with { AnimationFrame16 = 0 };
                        }
                        break;
                    case 2:
                        if ((actor.AnimationFrame16 >> 4) >= last)
                        {
                            next = next with { AnimationStatus = 4, AnimationFrame16 = last << 4, AnimationFinished = true };
                        }
                        break;
                    case 3:
                    case 4:
                        next = next with { AnimationFrame16 = last << 4 };
                        break;
                    case 5:
                        if ((actor.AnimationFrame16 >> 4) >= last)
                        {
                            next = next with { AnimationStatus = 0, AnimationFinished = true };
                        }
                        break;
                    case 6:
                        if ((actor.AnimationFrame16 >> 4) >= last)
                        {
                            next = next with { AnimationStatus = 3, AnimationFinished = true };
                        }
                        break;
                    default:
                        throw new InvalidDataException("FFVII actor has an invalid native animation status.");
                }
            }
            int frame = unchecked((short)(next.AnimationFrame16 + next.AnimationSpeed));
            if (!scriptControlled)
            {
                last = animationLength(next.Model, next.Animation) - 1;
                if (frame > (last << 4))
                {
                    frame = 0;
                }
            }
            else if (frame > (last << 4))
            {
                frame = last << 4;
            }
            if (frame < 0)
            {
                throw new NotSupportedException("Negative FFVII animation frame progression is outside the admitted playback contract.");
            }
            next = next with { AnimationFrame16 = frame };
            SetActorSlot(actor.Slot, _ => next);
        }
    }
}
