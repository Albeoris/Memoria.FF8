using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7Session
{
    internal bool HasCurrentNoEffectTouchCallback(int entity, int script, FieldInputState input, bool talk)
    {
        if (talk || (input.Pressed & 0x100) != 0 || script != 4 || World is not null || Battle is not null || HasPendingTick || Machine.CycleCursor is not null || !Machine.HasCompletedInitialCycle || !Ff7AutoplayDriver.HasNativeRouteClockContract(this) || Host.Process.PlayerLocked || Host.Process.MenuDisabled || Host.Process.GlobalCommand != Ff7GlobalCommand.None || Host.Process.Join is not null || Host.Process.Transition is not null || !Host.Process.Lines.Any(line => line.Entity == entity && line.Enabled == 1 && line.Touch) || (uint)entity >= Field.Scripts.Entities.Count || Machine.Capture().Entities[entity].Priority == 1)
        {
            return false;
        }
        string source = Ff7AutoplayDriver.ReadNativeRouteFingerprint(this);
        using Ff7Session branch = ForkAutoPlayNavigation();
        using Ff7NativeRouteClockWitness clock = new(branch.Machine);
        int entry = Field.Scripts.Entities[entity].ScriptOffsets[script];
        using Ff7CurrentLineEntryWitness witness = new(branch.Machine, entity, script, entry);
        branch.NativeInteractionScriptRequested += witness.ObserveAdmission;
        branch.Host.NativeOperationExecuted += (request, _) => witness.ObserveHostOperation(request);
        branch.Machine.SetInput(input.Held, input.Pressed, input.RawHeld, input.RawPressed);
        // Native field scripts consume the button streams before the physical movement tail.
        branch.MovePlayer(0, 0, false);
        branch.AdvanceNavigationTick();
        return witness.Complete && clock.Independent && !branch.HasPendingTick && branch.FieldGeneration == FieldGeneration && source == Ff7AutoplayDriver.ReadNativeRouteFingerprint(this);
    }
}
