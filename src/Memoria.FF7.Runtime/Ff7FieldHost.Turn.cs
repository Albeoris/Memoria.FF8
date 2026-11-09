using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7FieldHost
{
    private FieldHostReply FaceActor(int entity, FieldActorFace command)
    {
        Ff7Actor actor = GetActor(entity);
        if (actor.TurnMode == 3 || actor.TurnStep != 0 && actor.TurnMode == 2 && actor.TurnSteps == command.Steps)
        {
            return Turn(entity, new FieldActorTurn(0, command.DirectionPolicy, command.Steps, 2));
        }
        Ff7Actor target = GetActor(command.TargetEntity);
        int sourceX = actor.X4096 >> 12;
        int sourceY = actor.Y4096 >> 12;
        int targetX = target.X4096 >> 12;
        int targetY = target.Y4096 >> 12;
        if (sourceX == targetX && sourceY == targetY)
        {
            sourceX++;
        }
        Ff7NativeMovement movement = Movement ?? throw new NotSupportedException("FFVII actor facing requires native direction tables.");
        int direction = movement.Direction(targetX - sourceX, targetY - sourceY);
        return Turn(entity, new FieldActorTurn((short)direction, command.DirectionPolicy, command.Steps, 2));
    }

    private FieldHostReply Turn(int entity, FieldActorTurn command)
    {
        Ff7Actor actor = GetActor(entity);
        if (actor.TurnMode == 3)
        {
            SetActor(entity, current => current with { TurnMode = 0, TurnStep = 0, TurnSteps = 0 });
            return new(true);
        }
        if (command.Mode is not (1 or 2) || command.Steps == 0)
        {
            throw new NotSupportedException("FFVII actor turns require a supported interpolation mode and positive step count.");
        }
        if (actor.TurnStep != 0 && actor.TurnMode == command.Mode && actor.TurnSteps == command.Steps && (!command.SignedTarget || actor.TurnEnd == command.Direction))
        {
            return new(false);
        }
        int target = command.Direction;
        if (!command.SignedTarget)
        {
            switch (command.DirectionPolicy)
            {
                case 0:
                    if (target < actor.Direction)
                    {
                        target += 256;
                    }
                    break;
                case 1:
                    if (actor.Direction < target)
                    {
                        target -= 256;
                    }
                    break;
                case 2:
                    if (Math.Abs(target - actor.Direction) >= 129)
                    {
                        target += target > actor.Direction ? -256 : 256;
                    }
                    break;
            }
        }
        SetActor(entity, current => current with { TurnStart = current.Direction, TurnEnd = target, TurnMode = command.Mode, TurnSteps = command.Steps });
        return new(false);
    }

    private void AdvanceTurns()
    {
        foreach (Ff7Actor actor in Process.Actors.Where(actor => actor.TurnMode is 1 or 2))
        {
            Ff7NativeMovement movement = Movement ?? throw new NotSupportedException("FFVII actor turns require native interpolation tables.");
            int direction = movement.Interpolate(actor.TurnStart, actor.TurnEnd, actor.TurnSteps, actor.TurnStep, actor.TurnMode == 2) & 255;
            bool completed = actor.TurnStep == actor.TurnSteps;
            SetActorSlot(actor.Slot, current => current with { Direction = direction, TurnMode = completed ? 3 : current.TurnMode, TurnStep = completed ? current.TurnStep : current.TurnStep + 1 });
        }
    }
}
