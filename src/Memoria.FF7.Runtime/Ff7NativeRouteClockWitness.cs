using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

internal sealed class Ff7NativeRouteClockWitness : IDisposable
{
    private readonly FieldMachine machine;
    internal bool Independent { get; private set; } = true;

    internal Ff7NativeRouteClockWitness(FieldMachine machine)
    {
        this.machine = machine;
        machine.InstructionVariableAccessed += Observe;
    }

    private void Observe(FieldVariableRead access, bool write)
    {
        Independent &= !Ff7AutoplayDriver.IsNativeClockAccess(access);
    }

    public void Dispose()
    {
        machine.InstructionVariableAccessed -= Observe;
    }
}
