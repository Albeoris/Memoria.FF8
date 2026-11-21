namespace Memoria.FF7.Events;

public sealed partial class FieldMachine
{
    /// <summary>Pairs a current button loop with its literal message producer and a choice-only input prefix.</summary>
    public bool ProveCurrentPromptChoiceInput(FieldScriptInputDemand demand, FieldHostRequest prompt, int instructionBudget = 64)
    {
        try
        {
            if (!Initialized || demand.ProgramIdentity != program.Identity || !demand.AwaitingInput || demand.Mask == 0 || demand.Priority != 7 || (uint)demand.Entity >= entities.Length || prompt.Priority is < 0 or >= 7 || prompt.Command is not FieldDialogueCommand { FirstChoice: null, LastChoice: null } || !pendingRequests.Values.Contains(prompt) || (uint)prompt.Entity >= entities.Length)
            {
                return false;
            }
            Entity source = entities[demand.Entity];
            Entity owner = entities[prompt.Entity];
            int producer = source.Pc - 3;
            if (source.Priority != demand.Priority || owner.Priority != prompt.Priority || owner.Pc != prompt.ProgramCounter || producer < 0 || program.Read(producer) != 0x01 || program.Read(producer + 1) != prompt.Entity || program.Read(producer + 2) >> 5 != prompt.Priority || !ReadCurrentInputDemands(instructionBudget).Contains(demand))
            {
                return false;
            }
            FieldMachine initialization = Restore(program, Capture() with { CycleCursor = null }, new InspectionHost());
            initialization.inspection = new();
            initialization.entities[demand.Entity].Pc = program.Entry(demand.Entity, 0);
            int initializationSteps = 0;
            while (program.Read(initialization.entities[demand.Entity].Pc) != 0)
            {
                int pc = initialization.entities[demand.Entity].Pc;
                byte opcode = program.Read(pc);
                if (++initializationSteps > instructionBudget || opcode is >= 0x01 and <= 0x19 or >= 0x30 and <= 0x32)
                {
                    return false;
                }
                initialization.Execute(demand.Entity);
                if (initialization.entities[demand.Entity].Pc <= pc)
                {
                    return false;
                }
            }
            int ambientEntry = initialization.entities[demand.Entity].Pc + 1;
            if (!AnalyzeScriptAtProgramCounter(demand.Entity, ambientEntry, instructionBudget).InstructionSites.Contains(new(demand.Entity, producer)))
            {
                return false;
            }
            FieldMachine detached = Restore(program, Capture() with { CycleCursor = null }, new InspectionHost());
            detached.inspection = new();
            int messageEntry = program.Entry(prompt.Entity, program.Read(producer + 2) & 31);
            detached.entities[prompt.Entity].Pc = messageEntry;
            HashSet<int> visited = [];
            while (detached.entities[prompt.Entity].Pc != prompt.ProgramCounter)
            {
                int pc = detached.entities[prompt.Entity].Pc;
                if (visited.Count >= instructionBudget || !visited.Add(pc) || program.Read(pc) is not (0x50 or 0x51 or 0x52 or 0x53 or 0x55))
                {
                    return false;
                }
                detached.Execute(prompt.Entity);
            }
            detached.inspection.Commands.Clear();
            detached.Execute(prompt.Entity);
            if (detached.inspection.Commands.Count != 1 || detached.inspection.Commands[0] != prompt.Command)
            {
                return false;
            }
            int matched = demand.ProgramCounter + 4;
            if (program.Read(matched) != 0x03)
            {
                return false;
            }
            int worker = program.Read(matched + 1);
            int packed = program.Read(matched + 2);
            int priority = packed >> 5;
            if ((uint)worker >= entities.Length || priority >= 7 || entities[worker].Priority != 7 || entities[worker].Sync[priority] != FieldSyncState.None || entities[worker].Owners[priority] != -1 || entities[worker].Saved[priority] != 0)
            {
                return false;
            }
            detached = Restore(program, Capture() with { CycleCursor = null }, new InspectionHost());
            detached.inspection = new();
            detached.entities[worker].Pc = program.Entry(worker, packed & 31);
            detached.entities[worker].Priority = packed >> 5;
            visited.Clear();
            List<int> initializedResults = [];
            while (visited.Count < instructionBudget)
            {
                int pc = detached.entities[worker].Pc;
                if (!visited.Add(pc))
                {
                    return false;
                }
                byte opcode = program.Read(pc);
                if (opcode == 0x48)
                {
                    FieldBank bank = (FieldBank)(program.Read(pc + 1) & 15);
                    int result = program.Read(pc + 6);
                    detached.Execute(worker);
                    return bank == FieldBank.MapByte && initializedResults.All(operand => operand == result) && detached.inspection.Commands.LastOrDefault() is FieldDialogueCommand { FirstChoice: not null, LastChoice: not null } && detached.inspection.Commands.All(command => command is FieldWindowCommand or FieldDialogueCommand);
                }
                if (opcode == 0x80)
                {
                    if (program.Read(pc + 1) != ((int)FieldBank.MapByte << 4) || program.Read(pc + 3) != 0)
                    {
                        return false;
                    }
                    initializedResults.Add(program.Read(pc + 2));
                }
                else if (opcode is not (0x10 or 0x11 or 0x12 or 0x13 or >= 0x14 and <= 0x19 or 0x50 or 0x51 or 0x52 or 0x53 or 0x55))
                {
                    return false;
                }
                detached.Execute(worker);
            }
        }
        catch (Exception error) when (error is FieldExecutionException or InvalidDataException or NotSupportedException or ArgumentException or IndexOutOfRangeException)
        {
        }
        return false;
    }
}
