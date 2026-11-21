namespace Memoria.FF7.Events;

public enum FieldScriptInputKind
{
    Held,
    Pressed,
    Released
}

public enum FieldScriptInputContinuation
{
    Unknown,
    ReturnsToPollingLoop,
    LeavesPollingLoop
}

public sealed record FieldScriptInputDemand(string Identity, string ProgramIdentity, int Entity, int Priority, int ProgramCounter, FieldScriptInputKind Kind, ushort Mask, bool Mapped, bool AwaitingInput)
{
    public bool PresentationPolling { get; init; }
    public int DelayTicks { get; init; }
    public FieldScriptInputContinuation MatchedContinuation { get; init; }
}

public sealed partial class FieldMachine
{
    private sealed class InputInspectionBoundaryException : Exception
    {
    }

    /// <summary>Proves that projected input makes every variable-dependent path return using only predicates and non-yielding jumps.</summary>
    public bool CanSkipScriptEntryForInput(int entity, int script, FieldInputState projectedInput, int instructionBudget = 64)
    {
        ArgumentNullException.ThrowIfNull(projectedInput);
        if ((uint)entity >= entities.Length || script is < 0 or > 31 || instructionBudget is < 1 or > 1024)
        {
            throw new ArgumentOutOfRangeException(nameof(entity));
        }
        if (!Initialized)
        {
            return false;
        }
        FieldMachine detached = Restore(program, Capture() with { CycleCursor = null }, new InspectionHost());
        detached.inspection = new() { StopAtHost = true };
        detached.entities[entity].Pc = program.Entry(entity, script);
        detached.entities[entity].Priority = 1;
        detached.entities[entity].Wait = 0;
        detached.Input = projectedInput;
        Dictionary<int, bool?> visited = [];
        int steps = 0;
        bool Returns(int pc)
        {
            if (visited.TryGetValue(pc, out bool? proved))
            {
                return proved == true;
            }
            if (steps++ >= instructionBudget)
            {
                return false;
            }
            visited.Add(pc, null);
            try
            {
                byte opcode = program.Read(pc);
                if (opcode == 0)
                {
                    visited[pc] = true;
                    return true;
                }
                if (opcode is not (>= 0x10 and <= 0x1B or 0x30 or 0x31 or 0x32))
                {
                    return false;
                }
                detached.entities[entity].Pc = pc;
                if (detached.Execute(entity))
                {
                    return false;
                }
                bool result;
                if (opcode is >= 0x14 and <= 0x1B)
                {
                    // Mutable physical aliases are deliberately unknown: both native successors must return.
                    int branch = opcode >= 0x16 ? 7 : 5;
                    bool wide = (opcode & 1) != 0;
                    int distance = wide ? unchecked((short)(program.Read(pc + branch) | program.Read(pc + branch + 1) << 8)) : program.Read(pc + branch);
                    result = Returns(unchecked((ushort)(pc + branch + (wide ? 2 : 1)))) && Returns(unchecked((ushort)(pc + branch + distance)));
                }
                else
                {
                    result = Returns(detached.entities[entity].Pc);
                }
                visited[pc] = result;
                return result;
            }
            catch (Exception error) when (error is InputInspectionBoundaryException or FieldExecutionException or InvalidDataException or NotSupportedException or ArgumentException or IndexOutOfRangeException)
            {
                return false;
            }
        }
        return Returns(program.Entry(entity, script));
    }

    /// <summary>Reads the first input gate whose matched current continuation is not a predicate-only return, without executing a live interaction.</summary>
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
                    FieldScriptInputDemand demand = detached.ReadInputDemand(entity, owner.Pc, opcode);
                    if (!detached.EntryInputReturnsWithoutEffect(entity, demand, instructionBudget - step))
                    {
                        return demand;
                    }
                    owner.Pc = unchecked((ushort)(owner.Pc + 3 + program.Read(owner.Pc + 3)));
                    continue;
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

    private bool EntryInputReturnsWithoutEffect(int entity, FieldScriptInputDemand demand, int instructionBudget)
    {
        FieldMachine detached = Restore(program, Capture() with { CycleCursor = null }, new InspectionHost());
        detached.inspection = new() { StopAtHost = true };
        ushort held = demand.Kind == FieldScriptInputKind.Released ? (ushort)0 : demand.Mask;
        ushort pressed = demand.Kind == FieldScriptInputKind.Pressed ? demand.Mask : (ushort)0;
        ushort released = demand.Kind == FieldScriptInputKind.Released ? demand.Mask : (ushort)0;
        detached.Input = new(held, pressed, released, held, pressed, released);
        detached.entities[entity].Pc = demand.ProgramCounter + 4;
        HashSet<int> visited = [];
        for (int step = 0; step < instructionBudget; step++)
        {
            int pc = detached.entities[entity].Pc;
            if (!visited.Add(pc))
            {
                return false;
            }
            try
            {
                byte opcode = program.Read(pc);
                if (opcode == 0)
                {
                    return true;
                }
                if (opcode is not (>= 0x10 and <= 0x1B or 0x30 or 0x31 or 0x32) || detached.Execute(entity))
                {
                    return false;
                }
            }
            catch (Exception error) when (error is InputInspectionBoundaryException or FieldExecutionException or InvalidDataException or NotSupportedException or ArgumentException or IndexOutOfRangeException)
            {
                return false;
            }
        }
        return false;
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
            bool presentationPrefix = false;
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
                    if (detached.SkipInputPresentationNoOp(entity, opcode))
                    {
                        presentationPrefix = true;
                        continue;
                    }
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
                        bool loopPresentation = false;
                        bool awaiting = demand.Mask != 0 && unmatched != unchecked((ushort)(pc + 4)) && LoopsBackToInput(detached, entity, unmatched, pc, instructionBudget, out loopPresentation);
                        demands.Add(demand with { AwaitingInput = awaiting, DelayTicks = delayTicks, PresentationPolling = presentationPrefix || loopPresentation });
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
        return demands.Select(demand => demand.AwaitingInput ? demand with { MatchedContinuation = ReadMatchedInputContinuation(source, demand, demands.Where(other => other.Entity == demand.Entity && other.Priority == demand.Priority && other.AwaitingInput).Select(other => other.ProgramCounter).ToHashSet(), instructionBudget) } : demand).ToArray();
    }

    private FieldScriptInputContinuation ReadMatchedInputContinuation(FieldVmSnapshot source, FieldScriptInputDemand demand, IReadOnlySet<int> pollingGates, int instructionBudget)
    {
        FieldMachine detached = Restore(program, source, new InspectionHost());
        detached.inspection = new() { StopAtHost = true };
        Entity owner = detached.entities[demand.Entity];
        owner.Pc = unchecked((ushort)(demand.ProgramCounter + 4));
        owner.Wait = 0;
        HashSet<int> visited = [];
        for (int step = 0; step < instructionBudget; step++)
        {
            int pc = owner.Pc;
            if (pollingGates.Contains(pc))
            {
                return FieldScriptInputContinuation.ReturnsToPollingLoop;
            }
            if (!visited.Add(pc))
            {
                return FieldScriptInputContinuation.Unknown;
            }
            try
            {
                byte opcode = program.Read(pc);
                if (opcode == 0)
                {
                    return FieldScriptInputContinuation.LeavesPollingLoop;
                }
                if (opcode is >= 0x30 and <= 0x32)
                {
                    return FieldScriptInputContinuation.Unknown;
                }
                if (SkipInputPollingDelay(owner, opcode, out _))
                {
                    continue;
                }
                if (IsInputInspectionBoundary(opcode))
                {
                    return FieldScriptInputContinuation.Unknown;
                }
                detached.Execute(demand.Entity);
            }
            catch (Exception error) when (error is InputInspectionBoundaryException or FieldExecutionException or InvalidDataException or NotSupportedException or ArgumentException or IndexOutOfRangeException)
            {
                return FieldScriptInputContinuation.Unknown;
            }
        }
        return FieldScriptInputContinuation.Unknown;
    }

    private FieldScriptInputDemand ReadInputDemand(int entity, int pc, byte opcode)
    {
        ushort mask = (ushort)(program.Read(pc + 1) | program.Read(pc + 2) << 8);
        bool mapped = (program.Read(pc + 2) & 2) != 0;
        int priority = entities[entity].Priority;
        string identity = $"{program.Identity}:{entity}:{priority}:{pc:X4}";
        return new(identity, program.Identity, entity, priority, pc, (FieldScriptInputKind)(opcode - 0x30), mask, mapped, false);
    }

    private bool LoopsBackToInput(FieldMachine source, int entity, int unmatched, int target, int instructionBudget, out bool presentation)
    {
        presentation = false;
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
                if (detached.SkipInputPresentationNoOp(entity, opcode))
                {
                    presentation = true;
                    continue;
                }
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

    private bool SkipInputPresentationNoOp(int entity, byte opcode)
    {
        int pc = entities[entity].Pc;
        if (opcode != 0xF2 || program.Read(pc + 1) != 0 || program.Read(pc + 2) != 0 || program.Read(pc + 3) != 0 || program.Read(pc + 4) is not (0xC8 or 0xC9 or 0xCA))
        {
            return false;
        }
        InstructionInspection? previous = inspection;
        inspection = new();
        try
        {
            return !Execute(entity) && inspection.Commands is [FieldAudioCommand { Operation: 0xC8 or 0xC9 or 0xCA }];
        }
        finally
        {
            inspection = previous;
        }
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
