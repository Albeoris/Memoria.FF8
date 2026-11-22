namespace Memoria.FF7.Events;

public sealed partial class FieldMachine
{
    /// <summary>Proves that the current button worker takes the projected input to one matching window close and its return.</summary>
    public bool CanCloseDialogueFromCurrentInput(FieldScriptInputDemand demand, int window, FieldInputState input, int instructionBudget = 64)
    {
        ArgumentNullException.ThrowIfNull(demand);
        ArgumentNullException.ThrowIfNull(input);
        if (instructionBudget is < 1 or > 1024)
        {
            throw new ArgumentOutOfRangeException(nameof(instructionBudget));
        }
        if (!Initialized || CycleCursor is not null || demand.ProgramIdentity != program.Identity || !demand.AwaitingInput || demand.Mask == 0 || (uint)demand.Entity >= entities.Length || entities[demand.Entity].Priority != demand.Priority)
        {
            return false;
        }
        try
        {
            FieldVmSnapshot source = Capture();
            FieldMachine detached = Restore(program, source, new InspectionHost());
            detached.Input = input;
            detached.inspection = new();
            Entity owner = detached.entities[demand.Entity];
            HashSet<int> visited = [];
            bool matched = false;
            bool closed = false;
            for (int step = 0; step < instructionBudget; step++)
            {
                int pc = owner.Pc;
                if (!visited.Add(pc))
                {
                    return false;
                }
                byte opcode = program.Read(pc);
                if (opcode == 0)
                {
                    return matched && closed && source.PersistentVariables.AsSpan().SequenceEqual(detached.Variables.CopyPersistent()) && source.MapVariables.AsSpan().SequenceEqual(detached.Variables.CopyMap());
                }
                if (detached.SkipInputPollingDelay(owner, opcode, out _))
                {
                    continue;
                }
                if (opcode is not (0x10 or 0x11 or >= 0x30 and <= 0x32 or 0x2E or 0x52 or 0x54))
                {
                    return false;
                }
                if (pc == demand.ProgramCounter)
                {
                    if (opcode is not (>= 0x30 and <= 0x32))
                    {
                        return false;
                    }
                    FieldScriptInputDemand actual = detached.ReadInputDemand(demand.Entity, pc, opcode);
                    ushort keys = actual.Kind switch
                    {
                        FieldScriptInputKind.Held => actual.Mapped ? input.Held : input.RawHeld,
                        FieldScriptInputKind.Pressed => actual.Mapped ? input.Pressed : input.RawPressed,
                        _ => actual.Mapped ? input.Released : input.RawReleased
                    };
                    if (actual.Kind != demand.Kind || actual.Mask != demand.Mask || actual.Mapped != demand.Mapped || (keys & actual.Mask) == 0)
                    {
                        return false;
                    }
                    matched = true;
                }
                detached.inspection.Commands.Clear();
                detached.Execute(demand.Entity);
                foreach (FieldCommand command in detached.inspection.Commands)
                {
                    if (!matched || command is not FieldWindowCommand control || control.Window != window)
                    {
                        return false;
                    }
                    if (control.Operation is FieldWindowOperation.Close or FieldWindowOperation.CloseAndWait && !closed)
                    {
                        closed = true;
                    }
                    else if (!closed || control.Operation != FieldWindowOperation.Style || control.Style != 0 || control.Flags != 0)
                    {
                        return false;
                    }
                }
            }
        }
        catch (Exception error) when (error is FieldExecutionException or InvalidDataException or NotSupportedException or ArgumentException or IndexOutOfRangeException)
        {
        }
        return false;
    }
}
