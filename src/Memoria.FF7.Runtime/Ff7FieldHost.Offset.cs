using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7FieldHost
{
    private void Offset(int entity, FieldActorOffset command)
    {
        if (command.Mode is > 2 || command.Mode != 0 && command.Steps <= 0)
        {
            throw new NotSupportedException("FFVII actor offsets require a supported interpolation and positive duration.");
        }
        SetActor(entity, actor => command.Mode == 0 ? actor with { OffsetX = command.X, OffsetY = command.Y, OffsetZ = command.Z, Offset = null } : actor with { Offset = new(actor.OffsetX, actor.OffsetY, actor.OffsetZ, command.X, command.Y, command.Z, command.Mode, command.Steps) });
    }

    private void AdvanceOffsets()
    {
        foreach (Ff7Actor actor in Process.Actors.Where(actor => actor.Offset is { Completed: false }))
        {
            Ff7OffsetProcess offset = actor.Offset!;
            Ff7NativeMovement movement = Movement ?? throw new NotSupportedException("FFVII actor offsets require native interpolation tables.");
            bool completed = offset.Step == offset.Steps;
            SetActorSlot(actor.Slot, current => current with
            {
                OffsetX = unchecked((short)movement.Interpolate(offset.StartX, offset.TargetX, offset.Steps, offset.Step, offset.Mode == 2)),
                OffsetY = unchecked((short)movement.Interpolate(offset.StartY, offset.TargetY, offset.Steps, offset.Step, offset.Mode == 2)),
                OffsetZ = unchecked((short)movement.Interpolate(offset.StartZ, offset.TargetZ, offset.Steps, offset.Step, offset.Mode == 2)),
                Offset = offset with { Step = completed ? offset.Step : offset.Step + 1, Completed = completed }
            });
        }
    }
}
