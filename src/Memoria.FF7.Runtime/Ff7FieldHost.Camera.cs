using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7FieldHost
{
    private FieldHostReply Camera(FieldCameraCommand command)
    {
        if (command.Operation == FieldCameraOperation.ViewOffset)
        {
            Process = command.Mode == 0
                ? Process with { CameraFocusOffset = command.X, CameraViewOffset = null }
                : Process with { CameraViewOffset = new(checked((byte)command.Mode), checked((byte)command.Steps), Process.CameraFocusOffset!.Value, command.X) };
            return new(true);
        }
        if (command.Operation == FieldCameraOperation.Wait)
        {
            bool completed = (Process.CameraScroll?.Completed ?? true) && (Process.CameraTracking?.Completed ?? true);
            if (completed)
            {
                Process = Process with { CameraScroll = null };
            }
            return new(completed);
        }
        if (command.Operation is FieldCameraOperation.Entity or FieldCameraOperation.Player or FieldCameraOperation.ResetToPlayer)
        {
            if (command.Operation == FieldCameraOperation.ResetToPlayer || command.Mode == 0)
            {
                Process = Process with { CameraFollowPlayer = true, CameraEntity = -1, CameraScroll = null, CameraTracking = null, CameraX = 0, CameraY = 0 };
                return new(true);
            }
            if (command.Mode is < 1 or > 3 || command.Steps < 0)
            {
                throw new NotSupportedException("FFVII camera following requires a native continuous, linear or eased target mode and nonnegative duration.");
            }
            Ff7Actor target = command.Operation == FieldCameraOperation.Entity ? GetActor(command.Entity) : Process.Actors.SingleOrDefault(actor => actor.PlayerControlled) ?? throw new NotSupportedException("FFVII camera following requires its assigned player slot.");
            _ = CameraProjection ?? throw new NotSupportedException("FFVII entity camera following requires native field projection resources.");
            Process = Process with { CameraFollowPlayer = false, CameraEntity = target.Entity, CameraScroll = null, CameraTracking = new(command.Mode, target.Slot, command.Steps, Process.CameraX, Process.CameraY) };
            return new(true);
        }
        if (command.Operation == FieldCameraOperation.Coordinates && command.Mode is 5 or 6)
        {
            if (command.Steps < 0)
            {
                throw new NotSupportedException("FFVII negative camera scroll durations are unsupported.");
            }
            Process = Process with { CameraFollowPlayer = false, CameraTracking = null, CameraEntity = -1, CameraScroll = new(command.Mode, Process.CameraX, Process.CameraY, command.X, command.Y, command.Steps) };
            return new(true);
        }
        Process = command.Operation switch
        {
            FieldCameraOperation.Coordinates => Process with { CameraFollowPlayer = false, CameraTracking = null, CameraX = command.X, CameraY = command.Y, CameraEntity = -1, CameraScroll = null },
            FieldCameraOperation.Lock => Process with { CameraLocked = command.Mode != 0 },
            _ => throw new NotSupportedException("Unsupported FFVII camera operation.")
        };
        return new(true);
    }

    private void AdvanceCamera()
    {
        if (Process.CameraTracking is { Completed: false } tracking)
        {
            Ff7NativeCamera projection = CameraProjection ?? throw new NotSupportedException("FFVII camera following requires native field projection resources.");
            Ff7Actor actor = Process.Actors.Single(item => item.Slot == tracking.Slot);
            (short x, short y) = projection.Follow(actor, Process.CameraFocusOffset!.Value);
            if (tracking.Mode == 1)
            {
                Process = Process with { CameraX = x, CameraY = y };
                return;
            }
            if (tracking.Steps == 0)
            {
                Process = Process with { CameraTracking = tracking with { Completed = true } };
                return;
            }
            Ff7NativeMovement interpolation = Movement ?? throw new NotSupportedException("FFVII camera following requires native interpolation tables.");
            bool finished = tracking.Step == tracking.Steps;
            Process = Process with
            {
                CameraX = unchecked((short)interpolation.Interpolate(tracking.StartX, x, tracking.Steps, tracking.Step, tracking.Mode == 3)),
                CameraY = unchecked((short)interpolation.Interpolate(tracking.StartY, y, tracking.Steps, tracking.Step, tracking.Mode == 3)),
                CameraTracking = tracking with { Step = finished ? tracking.Step : tracking.Step + 1, Completed = finished }
            };
            return;
        }
        if (Process.CameraScroll is not { Completed: false } scroll)
        {
            if (Process.CameraFollowPlayer != false && CameraProjection is not null && Process.Actors.SingleOrDefault(actor => actor.PlayerControlled) is Ff7Actor player)
            {
                (short x, short y) = CameraProjection.Follow(player, Process.CameraFocusOffset!.Value);
                Process = Process with { CameraX = x, CameraY = y };
            }
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

    private void AdvanceViewOffset()
    {
        if (Process.CameraFollowPlayer == false || Process.CameraViewOffset is not { Mode: 1 or 2 } offset)
        {
            return;
        }
        Ff7NativeMovement movement = Movement ?? throw new NotSupportedException("FFVII view offset interpolation requires native interpolation tables.");
        short value = unchecked((byte)movement.Interpolate(offset.Start, offset.Target, offset.Steps, offset.Step, offset.Mode == 2));
        bool completed = offset.Step == offset.Steps;
        Process = Process with
        {
            CameraFocusOffset = value,
            CameraViewOffset = offset with { Mode = completed ? (byte)3 : offset.Mode, Step = completed ? offset.Step : unchecked((byte)(offset.Step + 1)) }
        };
    }
}
