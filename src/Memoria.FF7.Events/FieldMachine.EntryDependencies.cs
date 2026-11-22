namespace Memoria.FF7.Events;

public sealed record FieldScriptEntryDependencies(bool Complete, string? Reason, FieldVariableRead[] Reads, FieldHostRead[] HostReads);

public sealed partial class FieldMachine
{
    /// <summary>Reads a finite, side-effect-free entry guard ending at an actual literal bit mutation. Unsupported prefixes retain their full execution fingerprint.</summary>
    public string? ReadEntryGuardFingerprint(int entity, int script, int instructionBudget = 128)
    {
        if ((uint)entity >= entities.Length || script is < 0 or > 31 || instructionBudget is < 1 or > 1024)
        {
            throw new ArgumentOutOfRangeException(nameof(entity));
        }
        Queue<int> pending = new();
        HashSet<int> visited = [];
        HashSet<FieldVariableRead> reads = [];
        bool material = false;
        pending.Enqueue(program.Entry(entity, script));
        try
        {
            while (pending.TryDequeue(out int pc))
            {
                if (!visited.Add(pc))
                {
                    continue;
                }
                if (visited.Count > instructionBudget)
                {
                    return null;
                }
                byte opcode = program.Read(pc);
                byte Byte(int offset) => program.Read(pc + offset);
                ushort Word(int offset) => (ushort)(Byte(offset) | Byte(offset + 1) << 8);
                if (opcode == 0)
                {
                    continue;
                }
                if (opcode is 0x10 or 0x11)
                {
                    int next = pc + 1 + (opcode == 0x11 ? Word(1) : Byte(1));
                    if (next <= pc)
                    {
                        return null;
                    }
                    pending.Enqueue(next);
                    continue;
                }
                if (opcode is >= 0x14 and <= 0x19)
                {
                    bool word = opcode >= 0x16;
                    int branch = word ? 7 : 5;
                    bool wide = (opcode & 1) != 0;
                    reads.Add(new((FieldBank)(Byte(1) >> 4), word ? Word(2) : Byte(2), word));
                    reads.Add(new((FieldBank)(Byte(1) & 15), word ? Word(4) : Byte(3), word));
                    int next = pc + branch + (wide ? unchecked((short)Word(branch)) : Byte(branch));
                    if (next <= pc)
                    {
                        return null;
                    }
                    pending.Enqueue(pc + branch + (wide ? 2 : 1));
                    pending.Enqueue(next);
                    continue;
                }
                if (opcode is 0x82 or 0x83 && (Byte(1) & 15) == 0 && Byte(3) < 8)
                {
                    FieldBank bank = (FieldBank)(Byte(1) >> 4);
                    if (bank is not (FieldBank.MapByte or FieldBank.Persistent0Byte or FieldBank.Persistent1Byte))
                    {
                        return null;
                    }
                    FieldVariableRead read = new(bank, Byte(2), false);
                    reads.Add(read);
                    int value = Variables.ReadByte(bank, Byte(2));
                    bool set = (value & (1 << Byte(3))) != 0;
                    if (set == (opcode == 0x82))
                    {
                        return null;
                    }
                    material = true;
                    continue;
                }
                return null;
            }
            return material ? ReadScriptAnalysisFingerprint(entity, script, reads.OrderBy(read => read.Bank).ThenBy(read => read.Operand).ThenBy(read => read.Word).ToArray()) : null;
        }
        catch (Exception error) when (error is InvalidDataException or NotSupportedException or ArgumentException or IndexOutOfRangeException)
        {
            return null;
        }
    }

    private sealed record EntryContinuation(int Entity, int ProgramCounter);
    private sealed record EntryLocation(int Entity, int ProgramCounter, EntryContinuation[] Returns);
    private sealed record EntryDefinitions(HashSet<FieldVariableRead> Variables, HashSet<int> Positions);

    /// <summary>Separates incoming conditions from values produced on every path before their use, including synchronous child outputs.</summary>
    public FieldScriptEntryDependencies AnalyzeEntryDependencies(int entity, int script, int instructionBudget = 8192)
    {
        return AnalyzeEntryDependencies(entity, script, AnalyzeScript(entity, script, instructionBudget), instructionBudget);
    }

    public FieldScriptEntryDependencies AnalyzeEntryDependencies(int entity, int script, FieldScriptAnalysis full, int instructionBudget = 8192)
    {
        ArgumentNullException.ThrowIfNull(full);
        if ((uint)entity >= entities.Length || script is < 0 or > 31 || instructionBudget is < 1 or > 65536)
        {
            throw new ArgumentOutOfRangeException(nameof(entity));
        }
        return AnalyzeEntryDependenciesCore(entity, script, program.Entry(entity, script), full, instructionBudget);
    }

    private FieldScriptEntryDependencies AnalyzeEntryDependenciesCore(int entity, int script, int entry, FieldScriptAnalysis full, int instructionBudget)
    {
        ArgumentNullException.ThrowIfNull(full);
        if ((uint)entity >= entities.Length || script is < 0 or > 31 || instructionBudget is < 1 or > 65536)
        {
            throw new ArgumentOutOfRangeException(nameof(entity));
        }
        if (!full.Complete)
        {
            return new(false, full.Reason, full.Reads, full.HostReads);
        }
        Queue<(EntryLocation Location, EntryDefinitions Definitions)> pending = new();
        Dictionary<string, EntryDefinitions> incoming = new(StringComparer.Ordinal);
        HashSet<FieldVariableRead> external = [];
        HashSet<FieldHostRead> externalHosts = [];
        pending.Enqueue((new(entity, entry, []), new([], [])));
        int instructions = 0;
        try
        {
            while (pending.TryDequeue(out var work))
            {
                if (++instructions > instructionBudget)
                {
                    throw new NotSupportedException("The bounded entry-dependency analysis exhausted its instruction budget.");
                }
                EntryLocation location = work.Location;
                EntryDefinitions defined = new(new(work.Definitions.Variables), new(work.Definitions.Positions));
                string key = $"{location.Entity}:{location.ProgramCounter}:{string.Join(';', location.Returns.Select(frame => $"{frame.Entity}:{frame.ProgramCounter}"))}";
                if (incoming.TryGetValue(key, out EntryDefinitions? previous))
                {
                    defined.Variables.IntersectWith(previous.Variables);
                    defined.Positions.IntersectWith(previous.Positions);
                    if (defined.Variables.SetEquals(previous.Variables) && defined.Positions.SetEquals(previous.Positions))
                    {
                        continue;
                    }
                }
                incoming[key] = new(new(defined.Variables), new(defined.Positions));
                int pc = location.ProgramCounter;
                byte opcode = program.Read(pc);
                byte Byte(int offset) => program.Read(pc + offset);
                ushort Word(int offset) => (ushort)(Byte(offset) | Byte(offset + 1) << 8);
                void Enqueue(int owner, int next, EntryContinuation[] returns, EntryDefinitions facts)
                {
                    pending.Enqueue((new(owner, next, returns), new(new(facts.Variables), new(facts.Positions))));
                }
                void Continue(int next)
                {
                    Enqueue(location.Entity, next, location.Returns, defined);
                }
                void Read(FieldVariableRead read)
                {
                    if (Variables.SpatialSourceBytes(read).Any(source => !defined.Variables.Contains(source)))
                    {
                        external.Add(read);
                    }
                }
                if (opcode == 0)
                {
                    if (location.Returns.Length > 0)
                    {
                        EntryContinuation frame = location.Returns[^1];
                        Enqueue(frame.Entity, frame.ProgramCounter, location.Returns[..^1], defined);
                    }
                    continue;
                }
                if (opcode is >= 1 and <= 6)
                {
                    int target = Byte(1);
                    if (opcode >= 4)
                    {
                        if (target >= 3)
                        {
                            throw new InvalidDataException("Entry analysis encountered an invalid party request slot.");
                        }
                        Read(new(FieldBank.Persistent1Byte, (ushort)(9 + target), false));
                        int character = ReadParty()[target];
                        target = character == 255 ? -1 : Array.FindIndex(entities, candidate => candidate.PartyCharacter == character);
                    }
                    if (target is < 0 or 255)
                    {
                        Continue(pc + 3);
                        continue;
                    }
                    bool synchronous = opcode is 3 or 6;
                    if (synchronous && location.Returns.Length >= 16)
                    {
                        throw new NotSupportedException("Entry-dependency analysis exceeded its bounded synchronous request depth.");
                    }
                    EntryContinuation[] returns = synchronous ? [.. location.Returns, new(location.Entity, pc + 3)] : [];
                    Enqueue(target, program.Entry(target, Byte(2) & 31), returns, defined);
                    if (!synchronous)
                    {
                        Continue(pc + 3);
                    }
                    continue;
                }
                if (opcode is >= 0x10 and <= 0x13)
                {
                    int distance = (opcode & 1) != 0 ? Word(1) : Byte(1);
                    Continue(opcode < 0x12 ? pc + 1 + distance : pc - distance);
                    continue;
                }
                if (opcode is >= 0x14 and <= 0x19)
                {
                    bool word = opcode >= 0x16;
                    int branch = word ? 7 : 5;
                    bool wide = (opcode & 1) != 0;
                    Read(new((FieldBank)(Byte(1) >> 4), word ? Word(2) : Byte(2), word));
                    Read(new((FieldBank)(Byte(1) & 15), word ? Word(4) : Byte(3), word));
                    Continue(pc + branch + (wide ? 2 : 1));
                    Continue(pc + branch + (wide ? unchecked((short)Word(branch)) : Byte(branch)));
                    continue;
                }
                if (opcode is >= 0x30 and <= 0x32)
                {
                    Continue(pc + 4);
                    Continue(pc + 3 + Byte(3));
                    continue;
                }
                if (opcode is 0xCB or 0xCC)
                {
                    if (opcode == 0xCB)
                    {
                        for (ushort slot = 9; slot < 12; slot++)
                        {
                            Read(new(FieldBank.Persistent1Byte, slot, false));
                        }
                    }
                    else
                    {
                        externalHosts.Add(new(FieldHostReadKind.PartyAvailable, Byte(1)));
                    }
                    Continue(pc + 3);
                    Continue(pc + 2 + Byte(2));
                    continue;
                }
                FieldVariables variables = new(Variables.CopyPersistent(), Variables.CopyMap());
                HashSet<FieldVariableRead> reads = [];
                HashSet<FieldVariableRead> writes = [];
                variables.ReadObserved = read => reads.Add(read);
                variables.WriteObserved = write => writes.Add(write);
                List<FieldActorQuery> actorQueries = [];
                FieldMachine detached = new(program, variables, host: new InspectionHost())
                {
                    inspection = new()
                    {
                        ActorReply = query =>
                        {
                            actorQueries.Add(query);
                            return new(true, Position: new(0, 0, 0, 0));
                        }
                    },
                    Input = Input
                };
                for (int index = 0; index < entities.Length; index++)
                {
                    detached.entities[index].ModelBound = entities[index].ModelBound;
                    detached.entities[index].PartyCharacter = entities[index].PartyCharacter;
                }
                detached.entities[location.Entity].Pc = pc;
                detached.entities[location.Entity].Priority = 1;
                detached.Execute(location.Entity);
                foreach (FieldVariableRead read in reads)
                {
                    Read(read);
                }
                foreach (FieldCommand command in detached.inspection.Commands.Concat<FieldCommand>(actorQueries))
                {
                    if (command is FieldActorQuery query)
                    {
                        if (query.Query is not (FieldActorQueryKind.Position or FieldActorQueryKind.Coordinates or FieldActorQueryKind.Triangle) || !defined.Positions.Contains(query.Entity))
                        {
                            externalHosts.Add(new(FieldHostReadKind.Actor, query.Entity, (int)query.Query));
                        }
                    }
                    // These instructions hold their native request until movement completes; an asynchronous REQ never propagates their outputs back to its caller.
                    if (command is FieldActorMove { TargetEntity: null } or FieldActorLadder or FieldActorJump or FieldActorPlacement { Z: not null, Triangle: not null })
                    {
                        defined.Positions.Add(location.Entity);
                    }
                }
                foreach (FieldVariableRead write in writes)
                {
                    defined.Variables.UnionWith(variables.SpatialSourceBytes(write));
                }
                int next = detached.entities[location.Entity].Pc;
                if (next == pc)
                {
                    if (opcode != 0x24)
                    {
                        throw new NotSupportedException("An entry instruction did not expose a finite decoding successor.");
                    }
                    next = pc + 3;
                }
                Continue(next);
            }
        }
        catch (Exception error) when (error is FieldExecutionException or InvalidDataException or NotSupportedException or ArgumentException or IndexOutOfRangeException)
        {
            return new(false, error.Message, full.Reads, full.HostReads);
        }
        return new(true, null, full.Reads.Where(external.Contains).ToArray(), full.HostReads.Where(read => read.Kind != FieldHostReadKind.Actor || externalHosts.Contains(read)).ToArray());
    }
}
