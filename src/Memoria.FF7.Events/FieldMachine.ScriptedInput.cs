namespace Memoria.FF7.Events;

public enum FieldScriptInputKind
{
    Held,
    Pressed,
    Released
}

public sealed record FieldScriptInputDemand(string Identity, string ProgramIdentity, int Entity, int Priority, int ProgramCounter, FieldScriptInputKind Kind, ushort Mask, bool Mapped, bool AwaitingInput)
{
    public int DelayTicks { get; init; }
}

public sealed partial class FieldMachine
{
    private sealed class InputInspectionBoundaryException : Exception
    {
    }

    /// <summary>Reads the first input gate on the current variable-dependent entry path, without executing a live interaction.</summary>
    public FieldScriptInputDemand? ReadEntryInputDemand(int entity, int script, int instructionBudget = 64)
    {
        if ((uint)entity >= entities.Length || script is < 0 or > 31 || instructionBudget is < 1 or > 1024)
        {
            throw new ArgumentOutOfRangeException(nameof(entity));
        }
        if (!Initialized)
        {
            return null;
        }
        FieldMachine detached = Restore(program, Capture() with { CycleCursor = null }, new InspectionHost());
        detached.inspection = new() { StopAtHost = true };
        detached.entities[entity].Pc = program.Entry(entity, script);
        detached.entities[entity].Priority = 1;
        detached.entities[entity].Wait = 0;
        HashSet<(int Pc, int Priority)> visited = [];
        for (int step = 0; step < instructionBudget; step++)
        {
            Entity owner = detached.entities[entity];
            if (!visited.Add((owner.Pc, owner.Priority)))
            {
                return null;
            }
            try
            {
                byte opcode = program.Read(owner.Pc);
                if (opcode is >= 0x30 and <= 0x32)
                {
                    return detached.ReadInputDemand(entity, owner.Pc, opcode);
                }
                if (IsInputInspectionBoundary(opcode))
                {
                    return null;
                }
                detached.Execute(entity);
            }
            catch (Exception error) when (error is InputInspectionBoundaryException or FieldExecutionException or InvalidDataException or NotSupportedException or ArgumentException or IndexOutOfRangeException)
            {
                return null;
            }
        }
        return null;
    }

    /// <summary>Finds current native button waits without dispatching hosts, requesting scripts or advancing the live VM.</summary>
    public IReadOnlyList<FieldScriptInputDemand> ReadCurrentInputDemands(int instructionBudget = 64)
    {
        if (instructionBudget is < 1 or > 1024)
        {
            throw new ArgumentOutOfRangeException(nameof(instructionBudget));
        }
        if (!Initialized)
        {
            return [];
        }
        FieldVmSnapshot source = Capture() with { CycleCursor = null };
        List<FieldScriptInputDemand> demands = [];
        for (int entity = 0; entity < entities.Length; entity++)
        {
            FieldMachine detached = Restore(program, source, new InspectionHost());
            detached.inspection = new() { StopAtHost = true };
            HashSet<(int Pc, int Priority)> visited = [];
            int delayTicks = 0;
            bool sawInput = false;
            for (int step = 0; step < instructionBudget; step++)
            {
                Entity owner = detached.entities[entity];
                if (!visited.Add((owner.Pc, owner.Priority)))
                {
                    break;
                }
                try
                {
                    byte opcode = program.Read(owner.Pc);
                    if (SkipInputPollingDelay(owner, opcode, out int delay))
                    {
                        if (!sawInput)
                        {
                            delayTicks += delay;
                        }
                        continue;
                    }
                    if (opcode is >= 0x30 and <= 0x32)
                    {
                        int pc = owner.Pc;
                        FieldScriptInputDemand demand = detached.ReadInputDemand(entity, pc, opcode);
                        int unmatched = unchecked((ushort)(pc + 3 + program.Read(pc + 3)));
                        bool awaiting = demand.Mask != 0 && unmatched != unchecked((ushort)(pc + 4)) && LoopsBackToInput(detached, entity, unmatched, pc, instructionBudget);
                        demands.Add(demand with { AwaitingInput = awaiting, DelayTicks = delayTicks });
                        sawInput = true;
                        if (!awaiting)
                        {
                            break;
                        }
                        // Inspect the same no-button path used to prove the wait, without entering any accepted handler.
                        owner.Pc = unmatched;
                        continue;
                    }
                    if (IsInputInspectionBoundary(opcode))
                    {
                        break;
                    }
                    detached.Execute(entity);
                }
                catch (Exception error) when (error is InputInspectionBoundaryException or FieldExecutionException or InvalidDataException or NotSupportedException or ArgumentException or IndexOutOfRangeException)
                {
                    break;
                }
            }
        }
        return demands;
    }

    private FieldScriptInputDemand ReadInputDemand(int entity, int pc, byte opcode)
    {
        ushort mask = (ushort)(program.Read(pc + 1) | program.Read(pc + 2) << 8);
        bool mapped = (program.Read(pc + 2) & 2) != 0;
        int priority = entities[entity].Priority;
        string identity = $"{program.Identity}:{entity}:{priority}:{pc:X4}";
        return new(identity, program.Identity, entity, priority, pc, (FieldScriptInputKind)(opcode - 0x30), mask, mapped, false);
    }

    private bool LoopsBackToInput(FieldMachine source, int entity, int unmatched, int target, int instructionBudget)
    {
        FieldVmSnapshot original = source.Capture() with { CycleCursor = null };
        FieldMachine detached = Restore(program, original, new InspectionHost());
        detached.inspection = new() { StopAtHost = true };
        detached.entities[entity].Pc = unmatched;
        HashSet<int> visited = [];
        for (int step = 0; step < instructionBudget; step++)
        {
            int pc = detached.entities[entity].Pc;
            if (pc == target)
            {
                return original.PersistentVariables.AsSpan().SequenceEqual(detached.Variables.CopyPersistent()) && original.MapVariables.AsSpan().SequenceEqual(detached.Variables.CopyMap());
            }
            if (!visited.Add(pc))
            {
                return false;
            }
            try
            {
                byte opcode = program.Read(pc);
                if (SkipInputPollingDelay(detached.entities[entity], opcode, out _))
                {
                    continue;
                }
                if (opcode is >= 0x30 and <= 0x32)
                {
                    detached.entities[entity].Pc = unchecked((ushort)(pc + 3 + program.Read(pc + 3)));
                    continue;
                }
                if (IsInputInspectionBoundary(opcode))
                {
                    return false;
                }
                detached.Execute(entity);
            }
            catch (Exception error) when (error is InputInspectionBoundaryException or FieldExecutionException or InvalidDataException or NotSupportedException or ArgumentException or IndexOutOfRangeException)
            {
                return false;
            }
        }
        return false;
    }

    private static bool IsInputInspectionBoundary(byte opcode)
    {
        return opcode is <= 0x06 or 0x24 or 0x5F;
    }

    private bool SkipInputPollingDelay(Entity owner, byte opcode, out int delayTicks)
    {
        delayTicks = 0;
        if (opcode is not (0x24 or 0x5F))
        {
            return false;
        }
        int length = opcode == 0x24 ? 3 : 1;
        if (opcode == 0x24)
        {
            int duration = program.Read(owner.Pc + 1) | program.Read(owner.Pc + 2) << 8;
            delayTicks = owner.Wait == 0 ? Math.Max(1, duration) : owner.Wait - 1;
        }
        else
        {
            delayTicks = 1;
        }
        owner.Pc = unchecked((ushort)(owner.Pc + length));
        owner.Wait = 0;
        return true;
    }
}
