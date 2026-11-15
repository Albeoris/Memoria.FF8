using System.Text.Json;
using Memoria.FF7.Events;

internal static class FieldInitializationTests
{
    public static void Run()
    {
        FieldProgram program = new([0, 0x33, 1, 0x24, 2, 0, 0], [new ushort[32]]);
        Host host = new();
        FieldMachine machine = new(program, host: host);
        Check(!machine.HasCompletedInitialCycle, "An uninitialized field already completed its initial normal cycle.");
        machine.Initialize();
        Check(machine.Initialized && !machine.HasCompletedInitialCycle && machine.Capture().Entities[0].ProgramCounter == 1 && host.Calls == 0, "Initializer completion executed or falsely completed the pending native main continuation.");
        FieldVmSnapshot initial = machine.Capture();
        Host coldHost = new();
        FieldMachine cold = FieldMachine.Restore(program, initial, coldHost);
        Check(!cold.HasCompletedInitialCycle && JsonSerializer.Serialize(initial) == JsonSerializer.Serialize(cold.Capture()), "Restoring an initialized field lost its pending first normal cycle or changed saved execution.");
        machine.AdvanceTick();
        cold.AdvanceTick();
        Check(machine.HasCompletedInitialCycle && cold.HasCompletedInitialCycle && host.Locked && coldHost.Locked && host.Calls == 1 && coldHost.Calls == 1, "The first normal native cycle did not expose its main-owned player control lock.");
        Check(machine.Capture().Entities[0].WaitCounter == 2 && JsonSerializer.Serialize(machine.Capture()) == JsonSerializer.Serialize(cold.Capture()), "The readiness phase changed the existing native WAIT or cold continuation contract.");
        FieldMachine ambient = new(new FieldProgram([0, 0x24, 1, 0, 0], [new ushort[32]]));
        ambient.Initialize();
        ambient.AdvanceTick();
        Check(ambient.HasCompletedInitialCycle && ambient.Capture().Entities[0].WaitCounter == 1, "A resident ambient wait was mistaken for an incomplete first normal cycle.");
    }

    private sealed class Host : IFieldHost
    {
        public bool Locked { get; private set; }
        public int Calls { get; private set; }

        public FieldHostReply Execute(FieldHostRequest request)
        {
            Calls++;
            if (request.Command is not FieldControlCommand { Property: FieldControlKind.PlayerLocked } control)
            {
                throw new InvalidOperationException("The authored initializer unexpectedly dispatched another native host command.");
            }
            Locked = control.Value != 0;
            return new(true);
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
