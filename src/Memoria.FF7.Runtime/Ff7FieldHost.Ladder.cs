using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7FieldHost
{
    private FieldHostReply Ladder(long requestId, int entity, FieldActorLadder command)
    {
        Ff7Actor actor = GetActor(entity);
        if (actor.Ladder is Ff7LadderProcess active)
        {
            if (active.RequestId != requestId)
            {
                throw new NotSupportedException("FFVII ladder preemption requires its native request ownership contract.");
            }
            if (!active.Completed)
            {
                return new(false);
            }
            SetActor(entity, current => current with { Ladder = null });
            return new(true);
        }
        if (actor.Jump is not null)
        {
            throw new NotSupportedException("FFVII ladder preemption of a jump requires its native request ownership contract.");
        }
        if (command.Mode > 3 || command.AnimationDivisor == 0 || command.Triangle < 0 || command.Triangle >= field.Walkmesh.Count)
        {
            throw new NotSupportedException("FFVII ladder requires a supported axis/reversal, positive animation divisor and native destination triangle.");
        }
        _ = animationLength(actor.Model, command.Animation);
        Ff7LadderProcess state = new(requestId, command.X << 12, command.Y << 12, command.Z << 12, command.Triangle, command.Mode);
        SetActor(entity, current => current with { Ladder = state, Animation = command.Animation, RequestedAnimation = null, AnimationSpeed = unchecked((short)current.BaseAnimationSpeed) / command.AnimationDivisor, Direction = command.Direction });
        return new(false);
    }

    private void AdvanceLadders()
    {
        foreach (Ff7Actor actor in Process.Actors.Where(actor => actor.Ladder is not null))
        {
            Ff7LadderProcess state = actor.Ladder!;
            int last = animationLength(actor.Model, actor.Animation) - 1;
            if (last < 0)
            {
                throw new InvalidDataException("FFVII ladder animation has no native frames.");
            }
            if (!state.Initialized)
            {
                int dx = (state.TargetX - actor.X4096) / 4096;
                int dy = (state.TargetY - actor.Y4096) / 4096;
                int dz = (state.TargetZ - actor.Z4096) / 4096;
                int squared = unchecked(dx * dx + dy * dy + dz * dz);
                if (squared < 0)
                {
                    throw new NotSupportedException("FFVII ladder distance exceeds the native signed square-root range.");
                }
                int steps = unchecked((short)((int)Math.Sqrt(squared) / 4));
                if (steps <= 0)
                {
                    throw new NotSupportedException("FFVII zero-duration ladder reaches a native divide-by-zero contract.");
                }
                if (actor.PlayerControlled)
                {
                    UpdateLineContacts(actor, actor, moving: false);
                }
                state = state with { StartX = actor.X4096, StartY = actor.Y4096, StartZ = actor.Z4096, Steps = steps, Initialized = true };
                SetActorSlot(actor.Slot, current => current with { Ladder = state, AnimationFrame16 = 0, LastAnimationFrame = last });
                continue;
            }
            int frame = actor.AnimationFrame16;
            int triangle = actor.Triangle;
            if (actor.PlayerControlled && !Process.PlayerLocked)
            {
                ushort keys = ReadInputHeld?.Invoke() ?? throw new NotSupportedException("FFVII player ladder requires native held-key input.");
                int backward = state.Mode < 2 ? 0x1000 : 0x8000;
                int forward = state.Mode < 2 ? 0x4000 : 0x2000;
                if ((state.Mode & 1) != 0)
                {
                    (backward, forward) = (forward, backward);
                }
                if ((keys & backward) != 0)
                {
                    StepLadder(-1);
                }
                if ((keys & forward) != 0)
                {
                    StepLadder(1);
                }
            }
            else
            {
                StepLadder(1);
            }
            Ff7NativeMovement movement = Movement ?? throw new NotSupportedException("FFVII ladder requires native signed interpolation.");
            Ff7Actor updated = actor with
            {
                Ladder = state,
                X4096 = movement.Interpolate(state.StartX, state.TargetX, state.Steps, state.Step, false),
                Y4096 = movement.Interpolate(state.StartY, state.TargetY, state.Steps, state.Step, false),
                Z4096 = movement.Interpolate(state.StartZ, state.TargetZ, state.Steps, state.Step, false),
                Triangle = triangle,
                AnimationFrame16 = frame
            };
            SetActorSlot(actor.Slot, _ => updated);

            void StepLadder(int direction)
            {
                if (direction < 0 && state.Step == 0)
                {
                    state = state with { Completed = true };
                }
                else if (direction > 0 && state.Step == state.Steps)
                {
                    triangle = state.TargetTriangle;
                    state = state with { Completed = true };
                }
                else
                {
                    state = state with { Step = state.Step + direction };
                    frame = unchecked((short)(frame + direction * unchecked((short)actor.AnimationSpeed)));
                    if (direction < 0 && frame < 0)
                    {
                        frame = unchecked((short)(last << 4));
                    }
                    else if (direction > 0 && frame > last << 4)
                    {
                        frame = 0;
                    }
                }
            }
        }
    }
}
