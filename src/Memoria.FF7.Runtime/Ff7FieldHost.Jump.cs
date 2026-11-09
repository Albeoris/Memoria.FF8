using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7FieldHost
{
    private FieldHostReply Jump(long requestId, int entity, FieldActorJump command)
    {
        Ff7Actor actor = GetActor(entity);
        if (actor.Jump is Ff7JumpProcess active)
        {
            if (active.RequestId != requestId)
            {
                throw new NotSupportedException("FFVII jump preemption requires its native request ownership contract.");
            }
            if (!active.Completed)
            {
                return new(false);
            }
            SetActor(entity, current => current with { Jump = null });
            return new(true);
        }
        if (actor.Ladder is not null || command.Duration <= 0 || command.Triangle < 0 || command.Triangle >= field.Walkmesh.Count)
        {
            throw new NotSupportedException("FFVII jump requires a positive native duration, destination triangle and unoccupied movement owner.");
        }
        SetActor(entity, current => current with { Jump = new(requestId, command.X << 12, command.Y << 12, command.Triangle, command.Duration) });
        return new(false);
    }

    private void AdvanceJumps()
    {
        foreach (Ff7Actor actor in Process.Actors.Where(actor => actor.Jump is not null))
        {
            Ff7JumpProcess state = actor.Jump!;
            Ff7NativeMovement movement = Movement ?? throw new NotSupportedException("FFVII jump requires native height resolution and signed interpolation.");
            Ff7Actor updated = actor;
            if (!state.Initialized)
            {
                int targetZ = unchecked(movement.ResolveHeight(state.TargetX / 4096, state.TargetY / 4096, state.TargetTriangle) << 12);
                int velocity = unchecked((targetZ - actor.Z4096) / state.Duration - unchecked(-16000 * state.Duration) / 2);
                state = state with { StartX = actor.X4096, StartY = actor.Y4096, StartZ = actor.Z4096, TargetZ = targetZ, VerticalVelocity = velocity, Initialized = true };
            }
            else if (state.Step == state.Duration)
            {
                state = state with { Completed = true };
                updated = updated with { Triangle = state.TargetTriangle };
            }
            else
            {
                state = state with { Step = state.Step + 1 };
                int z = unchecked(state.StartZ + unchecked(state.VerticalVelocity * state.Step) + unchecked(-16000 * state.Step * state.Step) / 2);
                updated = updated with
                {
                    X4096 = movement.Interpolate(state.StartX, state.TargetX, state.Duration, state.Step, false),
                    Y4096 = movement.Interpolate(state.StartY, state.TargetY, state.Duration, state.Step, false),
                    Z4096 = z
                };
            }
            int count = animationLength(actor.Model, actor.Animation);
            if (count < 1)
            {
                throw new InvalidDataException("FFVII jump's existing animation has no native frames.");
            }
            bool freePlayer = actor.PlayerControlled && !Process.PlayerLocked;
            int last = freePlayer || actor.LastAnimationFrame < 0 ? count - 1 : actor.LastAnimationFrame;
            int frame = unchecked((short)(actor.AnimationFrame16 + unchecked((short)actor.AnimationSpeed)));
            if (frame > last << 4)
            {
                frame = freePlayer ? 0 : unchecked((short)(last << 4));
            }
            updated = updated with { Jump = state, AnimationFrame16 = frame, LastAnimationFrame = last };
            SetActorSlot(actor.Slot, _ => updated);
            if (actor.PlayerControlled)
            {
                UpdateLineContacts(updated, updated, moving: false);
            }
        }
    }
}
