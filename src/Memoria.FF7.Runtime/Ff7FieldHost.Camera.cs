using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7FieldHost
{
    private FieldHostReply Camera(FieldCameraCommand command)
    {
        if (command.Operation == FieldCameraOperation.Wait)
        {
            bool completed = Process.CameraScroll?.Completed ?? true;
            if (completed)
            {
                Process = Process with { CameraScroll = null };
            }
            return new(completed);
        }
        if (command.Operation == FieldCameraOperation.Coordinates && command.Mode is 5 or 6)
        {
            if (command.Steps < 0)
            {
                throw new NotSupportedException("FFVII negative camera scroll durations are unsupported.");
            }
            Process = Process with { CameraEntity = -1, CameraScroll = new(command.Mode, Process.CameraX, Process.CameraY, command.X, command.Y, command.Steps) };
            return new(true);
        }
        if (command.Steps != 0)
        {
            throw new NotSupportedException("FFVII entity camera interpolation requires its native projected target contract.");
        }
        Process = command.Operation switch
        {
            FieldCameraOperation.Coordinates => Process with { CameraX = command.X, CameraY = command.Y, CameraEntity = -1, CameraScroll = null },
            FieldCameraOperation.Entity => Process with { CameraEntity = command.Entity, CameraScroll = null },
            FieldCameraOperation.Player or FieldCameraOperation.ResetToPlayer => Process with { CameraEntity = Process.Actors.SingleOrDefault(actor => actor.PlayerControlled)?.Entity ?? -1, CameraScroll = null },
            FieldCameraOperation.Lock => Process with { CameraLocked = command.Mode != 0 },
            _ => throw new NotSupportedException("Unsupported FFVII camera operation.")
        };
        return new(true);
    }

    private void AdvanceCamera()
    {
        if (Process.CameraScroll is not { Completed: false } scroll)
        {
            return;
        }
        if (scroll.Steps == 0)
        {
            Process = Process with { CameraScroll = scroll with { Completed = true } };
            return;
        }
        Ff7NativeMovement movement = Movement ?? throw new NotSupportedException("FFVII camera scrolling requires native interpolation tables.");
        bool completed = scroll.Step == scroll.Steps;
        Process = Process with
        {
            CameraX = unchecked((short)movement.Interpolate(scroll.StartX, scroll.TargetX, scroll.Steps, scroll.Step, scroll.Mode == 6)),
            CameraY = unchecked((short)movement.Interpolate(scroll.StartY, scroll.TargetY, scroll.Steps, scroll.Step, scroll.Mode == 6)),
            CameraScroll = scroll with { Step = completed ? scroll.Step : scroll.Step + 1, Completed = completed }
        };
    }
}
