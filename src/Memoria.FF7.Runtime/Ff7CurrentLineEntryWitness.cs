using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

internal sealed class Ff7CurrentLineEntryWitness : IDisposable
{
    private readonly FieldMachine machine;
    private readonly int entity;
    private readonly int script;
    private readonly int entry;
    private bool invalid;
    private bool wrote;
    private int instructions;
    private int admissions;
    private int returns;

    internal Ff7CurrentLineEntryWitness(FieldMachine machine, int entity, int script, int entry)
    {
        this.machine = machine;
        this.entity = entity;
        this.script = script;
        this.entry = entry;
        machine.InstructionVariableAccessed += ObserveAccess;
        machine.InstructionExecuted += ObserveInstruction;
        machine.ScriptReturned += ObserveReturn;
        machine.ScriptDispatched += ObserveDispatch;
    }

    internal bool Complete => !invalid && instructions > 0 && admissions == 1 && returns == 1;

    internal void ObserveAdmission(FieldExternalScriptAdmission admission)
    {
        if (admission is { Accepted: true, Started: true, Priority: 1 } && admission.Entity == entity)
        {
            admissions++;
            invalid |= admission.Script != script;
        }
    }

    internal void ObserveHostOperation(FieldHostRequest request) => invalid |= request.Entity == entity && request.Priority == 1;

    private void ObserveAccess(FieldVariableRead access, bool write) => wrote |= write;

    private void ObserveInstruction(int owner, int priority, int pc, byte opcode)
    {
        if (owner == entity && priority == 1)
        {
            invalid |= instructions == 0 && pc != entry || wrote || opcode is not (0 or 0x10 or 0x11 or >= 0x14 and <= 0x19 or >= 0x30 and <= 0x32);
            instructions++;
            invalid |= instructions > 8;
        }
        wrote = false;
    }

    private void ObserveReturn(FieldScriptReturn returned) => returns += returned.Entity == entity && returned.Priority == 1 ? 1 : 0;

    private void ObserveDispatch(FieldScriptDispatch dispatch) => invalid |= dispatch.SourceEntity == entity && dispatch.SourcePriority == 1;

    public void Dispose()
    {
        machine.InstructionVariableAccessed -= ObserveAccess;
        machine.InstructionExecuted -= ObserveInstruction;
        machine.ScriptReturned -= ObserveReturn;
        machine.ScriptDispatched -= ObserveDispatch;
    }
}
