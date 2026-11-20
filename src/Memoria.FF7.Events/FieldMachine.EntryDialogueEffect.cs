namespace Memoria.FF7.Events;

public sealed partial class FieldMachine
{
    public bool ProveEntryInputDialogueEffect(int entity, int script)
    {
        try
        {
            int entry = program.Entry(entity, script);
            byte gate = program.Read(entry);
            if (gate is not (0x30 or 0x31 or 0x32) || program.Read(entry + program.Read(entry + 3) + 3) != 0 || ReadEntryInputDemand(entity, script) is not { Mask: > 0, Priority: 1 })
            {
                return false;
            }
            int pc = entry + 4;
            bool dialogue = false;
            Dictionary<FieldControlKind, int> controls = [];
            while (true)
            {
                byte opcode = program.Read(pc);
                if (opcode == 0)
                {
                    return dialogue && controls.Values.All(value => value == 0);
                }
                if (opcode is not (0x33 or 0x40 or 0x4A or 0x50 or 0x51 or 0x52 or 0x53 or 0x55))
                {
                    return false;
                }
                FieldVariables variables = new(Variables.CopyPersistent(), Variables.CopyMap());
                bool reads = false;
                bool writes = false;
                variables.ReadObserved = _ => reads = true;
                variables.WriteObserved = _ => writes = true;
                FieldMachine detached = new(program, variables, host: new InspectionHost()) { inspection = new() };
                detached.entities[entity].Pc = pc;
                detached.entities[entity].Priority = 1;
                detached.Execute(entity);
                if (reads || writes || detached.inspection.Commands.Count != 1)
                {
                    return false;
                }
                FieldCommand command = detached.inspection.Commands.Single();
                if (command is not (FieldControlCommand { Property: FieldControlKind.PlayerLocked or FieldControlKind.MenuDisabled, Value: 0 or 1 } or FieldDialogueCommand { FirstChoice: null, LastChoice: null } or FieldWindowCommand { Operation: FieldWindowOperation.Size or FieldWindowOperation.Move or FieldWindowOperation.Style or FieldWindowOperation.Reset or FieldWindowOperation.Rows }))
                {
                    return false;
                }
                if (command is FieldControlCommand control)
                {
                    if (!controls.ContainsKey(control.Property) && control.Value != 1)
                    {
                        return false;
                    }
                    controls[control.Property] = control.Value;
                }
                dialogue |= command is FieldDialogueCommand;
                int next = detached.entities[entity].Pc;
                if (next <= pc)
                {
                    return false;
                }
                pc = next;
            }
        }
        catch (Exception exception) when (exception is InvalidDataException or NotSupportedException or ArgumentOutOfRangeException or IndexOutOfRangeException)
        {
            return false;
        }
    }
}
