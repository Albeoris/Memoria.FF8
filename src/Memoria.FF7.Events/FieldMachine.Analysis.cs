using System.Security.Cryptography;
using System.Text;

namespace Memoria.FF7.Events;

public sealed record FieldVariableRead(FieldBank Bank, ushort Operand, bool Word);
public enum FieldHostReadKind
{
    Gil,
    ItemQuantity,
    PartyAvailable,
    Actor,
    PreviousMap,
    BattleResult,
    MateriaCapacity,
    MovieFrame,
    MusicPlaying,
    Random
}

public sealed record FieldHostRead(FieldHostReadKind Kind, int Parameter = 0, int Selector = 0);
public sealed record FieldScriptAnalysis(bool Complete, string? Reason, string Fingerprint, bool ChangesStory, FieldVariableRead[] Reads, int[] ProgramCounters, int[] DestinationFields)
{
    public FieldHostRead[] HostReads { get; init; } = [];
    public bool MayChangeNavigation { get; init; }
    public int[] ExitProgramCounters { get; init; } = [];
}

public sealed partial class FieldVariables
{
    internal Action<FieldVariableRead>? ReadObserved { get; set; }
    internal Action<FieldVariableRead>? WriteObserved { get; set; }

    internal bool RepresentsSameStorage(FieldVariableRead read, FieldVariableRead write)
    {
        if (read.Bank == FieldBank.Immediate || write.Bank == FieldBank.Immediate)
        {
            return false;
        }
        var source = Locate(read.Bank, (byte)read.Operand);
        var target = Locate(write.Bank, (byte)write.Operand);
        int sourceWidth = read.Word && source.Word ? 2 : 1;
        int targetWidth = write.Word && target.Word ? 2 : 1;
        return ReferenceEquals(source.Storage, target.Storage) && source.Index == target.Index && sourceWidth == targetWidth;
    }
}

public sealed partial class FieldMachine
{
    private sealed class InstructionInspection
    {
        public List<FieldCommand> Commands { get; } = [];
        public bool StopAtHost { get; init; }
        public Func<FieldActorQuery, FieldHostReply>? ActorReply { get; init; }
    }

    private InstructionInspection? inspection;

    private sealed class NavigationInvariantInspection(IReadOnlySet<FieldVariableRead> immutableBytes, IReadOnlySet<FieldNavigationEntryPoint> coveredEntries)
    {
        public IReadOnlySet<FieldVariableRead> ImmutableBytes { get; } = immutableBytes;
        public HashSet<FieldVariableRead> Writes { get; } = [];
        public int Instructions { get; set; }

        public bool CoversAllocation(int owner, FieldCommand command)
        {
            return command switch
            {
                FieldActorBind or FieldPartyCharacter => coveredEntries.Contains(new(owner, 2)),
                FieldActorProperty { Property: FieldActorPropertyKind.PlayerCharacter } control => coveredEntries.Contains(new(control.Value, 2)),
                FieldLineCommand { Operation: FieldLineOperation.Define or FieldLineOperation.Replace } => Enumerable.Range(2, 5).All(script => coveredEntries.Contains(new(owner, script))),
                FieldPartyJoin or FieldPartySplit => false,
                _ => true
            };
        }
    }

    private sealed class InspectionHost : IFieldHost
    {
        public FieldHostReply Execute(FieldHostRequest request) => throw new InvalidOperationException("Read-only instruction inspection must suppress host dispatch.");
    }

    /// <summary>Identifies the allocation facts that can change native operand decoding or child request ownership.</summary>
    public string ReadScriptAnalysisAllocationFingerprint()
    {
        return $"{program.Identity}|{string.Join(';', entities.Select(entity => $"{entity.ModelBound}:{entity.PartyCharacter}"))}|{string.Join(',', ReadParty())}";
    }

    public string ReadScriptAnalysisFingerprint(int entity, int script, IReadOnlyList<FieldVariableRead> reads)
    {
        StringBuilder signature = new($"{program.Identity}:{entity}:{script}");
        foreach (FieldVariableRead read in reads)
        {
            int value = read.Word ? Variables.ReadWord(read.Bank, read.Operand) : Variables.ReadByte(read.Bank, (byte)read.Operand);
            signature.Append($"|{(int)read.Bank}:{read.Operand}:{read.Word}:{value}");
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(signature.ToString())));
    }

    /// <summary>Walks both branch arms using detached single-instruction decoding. No live VM, host or RNG is advanced.</summary>
    public FieldScriptAnalysis AnalyzeScript(int entity, int script, int instructionBudget = 8192)
    {
        if ((uint)entity >= entities.Length || script is < 0 or > 31 || instructionBudget is < 1 or > 65536)
        {
            throw new ArgumentOutOfRangeException(nameof(entity));
        }
        return AnalyzeScriptCore(entity, script, program.Entry(entity, script), instructionBudget, null, 0);
    }

    /// <summary>Inspects a native autonomous body from its observed entry, ending at represented source reentry boundaries or RET.</summary>
    public FieldScriptAnalysis AnalyzeScriptAtProgramCounter(int entity, int programCounter, int instructionBudget = 8192, IReadOnlySet<int>? exitBoundaries = null, IReadOnlySet<FieldControlKind>? acquiredControls = null)
    {
        if ((uint)entity >= entities.Length || (uint)programCounter >= program.Length || instructionBudget is < 1 or > 65536)
        {
            throw new ArgumentOutOfRangeException(nameof(entity));
        }
        byte ownership = 0;
        foreach (FieldControlKind control in acquiredControls ?? new HashSet<FieldControlKind>())
        {
            ownership |= control switch
            {
                FieldControlKind.PlayerLocked => (byte)1,
                FieldControlKind.MenuDisabled => (byte)2,
                _ => throw new NotSupportedException("The autonomous body has an unsupported owned control domain.")
            };
        }
        FieldScriptAnalysis result = AnalyzeScriptCore(entity, 0, programCounter, instructionBudget, exitBoundaries, ownership);
        string fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{result.Fingerprint}:pc:{programCounter}")));
        return result with { Fingerprint = fingerprint };
    }

    private FieldScriptAnalysis AnalyzeScriptCore(int entity, int script, int entry, int instructionBudget, IReadOnlySet<int>? exitBoundaries, byte initialOwnership, NavigationInvariantInspection? navigationInvariant = null)
    {
        byte[] persistent = Variables.CopyPersistent();
        byte[] map = Variables.CopyMap();
        HashSet<(int Entity, int Pc, byte Controls, bool PhysicalValid)> visited = [];
        HashSet<FieldVariableRead> reads = [];
        HashSet<FieldHostRead> hostReads = [];
        HashSet<int> destinations = [];
        HashSet<int> exits = [];
        Dictionary<int, bool> physicalExits = [];
        HashSet<string> physicalPredicates = [];
        HashSet<FieldVariableRead> physicalBytes = [];
        string PredicateIdentity(int pc)
        {
            byte opcode = program.Read(pc);
            if (opcode is < 0x14 or > 0x19)
            {
                return string.Empty;
            }
            int width = opcode >= 0x16 ? 7 : 5;
            byte[] operands = Enumerable.Range(0, width).Select(offset => program.Read(pc + offset)).ToArray();
            operands[0] &= 0xFE;
            return Convert.ToHexString(operands);
        }
        foreach (int predicate in exitBoundaries ?? new HashSet<int>())
        {
            string identity = PredicateIdentity(predicate);
            if (identity.Length == 0)
            {
                throw new ArgumentException("A physical body boundary requires a proved native conditional.", nameof(exitBoundaries));
            }
            physicalPredicates.Add(identity);
            FieldMachine owner = new(program, new FieldVariables(persistent, map));
            owner.entities[entity].Pc = predicate;
            FieldConditional condition = owner.ReadConditional(entity, program.Read(predicate));
            physicalBytes.UnionWith(Variables.SpatialSourceBytes(condition.LeftSource));
            physicalBytes.UnionWith(Variables.SpatialSourceBytes(condition.RightSource));
        }
        Queue<(int Entity, int Pc, byte Controls, bool PhysicalValid)> pending = new();
        pending.Enqueue((entity, entry, initialOwnership, true));
        bool changesStory = false;
        bool mayChangeNavigation = false;
        string? reason = null;
        while (pending.TryDequeue(out var location))
        {
            if (visited.Contains(location))
            {
                continue;
            }
            if (visited.Count >= instructionBudget)
            {
                reason = "The bounded field control-flow analysis exhausted its instruction budget.";
                break;
            }
            visited.Add(location);
            if (navigationInvariant is not null)
            {
                navigationInvariant.Instructions++;
            }
            try
            {
                int pc = location.Pc;
                int owner = location.Entity;
                byte controls = location.Controls;
                bool physicalValid = location.PhysicalValid;
                byte opcode = program.Read(pc);
                if (owner == entity && physicalPredicates.Contains(PredicateIdentity(pc)))
                {
                    bool returned = controls == 0 && physicalValid;
                    physicalExits[pc] = physicalExits.GetValueOrDefault(pc, true) && returned;
                    if (returned)
                    {
                        continue;
                    }
                }
                byte Byte(int offset) => program.Read(pc + offset);
                ushort Word(int offset) => (ushort)(Byte(offset) | Byte(offset + 1) << 8);
                void Enqueue(int offset) => pending.Enqueue((owner, unchecked((ushort)offset), controls, physicalValid));
                if (opcode == 0)
                {
                    if (owner == entity)
                    {
                        exits.Add(pc);
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
                            throw new InvalidDataException("Party request targets an invalid script-party slot.");
                        }
                        reads.Add(new(FieldBank.Persistent1Byte, (ushort)(9 + target), false));
                        if (navigationInvariant is not null)
                        {
                            // A producer can change either script-party storage or model registration.
                            // Include every recipient rather than assuming the captured allocation persists.
                            for (int recipient = 0; recipient < entities.Length; recipient++)
                            {
                                pending.Enqueue((recipient, program.Entry(recipient, Byte(2) & 31), controls, physicalValid));
                            }
                            Enqueue(pc + 3);
                            continue;
                        }
                        int character = persistent[256 + 9 + target];
                        target = character == 255 ? -1 : Array.FindIndex(entities, value => value.PartyCharacter == character);
                    }
                    if (target != 255 && target >= 0)
                    {
                        if (target >= entities.Length)
                        {
                            throw new InvalidDataException("Script request targets an absent entity.");
                        }
                        pending.Enqueue((target, program.Entry(target, Byte(2) & 31), controls, physicalValid));
                    }
                    Enqueue(pc + 3);
                    continue;
                }
                if (opcode is >= 0x10 and <= 0x13)
                {
                    int distance = (opcode & 1) != 0 ? Word(1) : Byte(1);
                    Enqueue(opcode < 0x12 ? pc + 1 + distance : pc - distance);
                    continue;
                }
                if (opcode is >= 0x14 and <= 0x19)
                {
                    bool word = opcode >= 0x16;
                    int branch = word ? 7 : 5;
                    bool wide = (opcode & 1) != 0;
                    FieldBank left = (FieldBank)(Byte(1) >> 4);
                    FieldBank right = (FieldBank)(Byte(1) & 15);
                    if (Byte(word ? 6 : 4) > 10)
                    {
                        throw new NotSupportedException("The field conditional comparison is outside the proved comparison subset.");
                    }
                    reads.Add(new(left, word ? Word(2) : Byte(2), word));
                    reads.Add(new(right, word ? Word(4) : Byte(3), word));
                    FieldVariableRead leftRead = new(left, word ? Word(2) : Byte(2), word);
                    FieldVariableRead rightRead = new(right, word ? Word(4) : Byte(3), word);
                    if (navigationInvariant is not null && Variables.SpatialSourceBytes(leftRead).Concat(Variables.SpatialSourceBytes(rightRead)).All(navigationInvariant.ImmutableBytes.Contains))
                    {
                        FieldMachine predicate = new(program, new FieldVariables(persistent, map));
                        predicate.entities[owner].Pc = pc;
                        FieldConditional condition = predicate.ReadConditional(owner, opcode);
                        Enqueue(predicate.Compare(condition.Left, condition.Right, condition.Operation) ? condition.MatchedPc : condition.UnmatchedPc);
                        continue;
                    }
                    Enqueue(pc + branch + (wide ? 2 : 1));
                    Enqueue(pc + branch + (wide ? unchecked((short)Word(branch)) : Byte(branch)));
                    continue;
                }
                if (opcode is >= 0x30 and <= 0x32)
                {
                    Enqueue(pc + 4);
                    Enqueue(pc + 3 + Byte(3));
                    continue;
                }
                if (opcode is 0xCB or 0xCC)
                {
                    if (opcode == 0xCB)
                    {
                        for (ushort slot = 9; slot < 12; slot++)
                        {
                            reads.Add(new(FieldBank.Persistent1Byte, slot, false));
                        }
                    }
                    else
                    {
                        hostReads.Add(new(FieldHostReadKind.PartyAvailable, Byte(1)));
                    }
                    Enqueue(pc + 3);
                    Enqueue(pc + 2 + Byte(2));
                    continue;
                }
                FieldVariables variables = new(persistent, map);
                HashSet<FieldVariableRead> instructionReads = [];
                HashSet<FieldVariableRead> instructionWrites = [];
                variables.ReadObserved = read =>
                {
                    instructionReads.Add(read);
                    if (navigationInvariant is not null && Variables.SpatialSourceBytes(read).Any(source => source.Bank == FieldBank.Persistent1Byte && source.Operand is >= 9 and <= 11 && !navigationInvariant.ImmutableBytes.Contains(source)))
                    {
                        throw new NotSupportedException("A producer requires a mutable native script-party query selector.");
                    }
                };
                variables.WriteObserved = write =>
                {
                    instructionWrites.Add(write);
                    navigationInvariant?.Writes.UnionWith(Variables.SpatialSourceBytes(write));
                    if (variables.SpatialSourceBytes(write).Any(physicalBytes.Contains))
                    {
                        physicalValid = false;
                    }
                    if ((write.Bank is FieldBank.Persistent0Byte or FieldBank.Persistent0Word) && (byte)write.Operand <= 1)
                    {
                        changesStory = true;
                    }
                };
                FieldMachine detached = new(program, variables, host: new InspectionHost()) { inspection = new() };
                for (int index = 0; index < entities.Length; index++)
                {
                    detached.entities[index].ModelBound = navigationInvariant is not null || entities[index].ModelBound;
                    detached.entities[index].PartyCharacter = entities[index].PartyCharacter;
                }
                detached.entities[owner].Pc = pc;
                detached.entities[owner].Priority = 1;
                detached.Input = Input;
                detached.Execute(owner);
                // A straight-line own-store consumes its old value only to update itself.
                // Predicates and child requests were handled above; reads from other instructions remain independent.
                bool ownStore = detached.inspection.Commands.Count == 0 && instructionWrites.Count == 1;
                FieldVariableRead? ownWrite = ownStore ? instructionWrites.Single() : null;
                foreach (FieldVariableRead read in instructionReads)
                {
                    if (ownWrite is null || !variables.RepresentsSameStorage(read, ownWrite))
                    {
                        reads.Add(read);
                    }
                }
                foreach (FieldTransitionCommand transition in detached.inspection.Commands.OfType<FieldTransitionCommand>())
                {
                    destinations.Add(transition.Map);
                }
                foreach (FieldCommand command in detached.inspection.Commands)
                {
                    if (navigationInvariant is not null && !navigationInvariant.CoversAllocation(owner, command))
                    {
                        throw new NotSupportedException("A producer can change the external script-trigger allocation.");
                    }
                    if (exitBoundaries is not null && command is FieldControlCommand control && control.Property is FieldControlKind.PlayerLocked or FieldControlKind.MenuDisabled)
                    {
                        byte domain = control.Property == FieldControlKind.PlayerLocked ? (byte)1 : (byte)2;
                        controls = control.Value != 0 ? (byte)(controls | domain) : (byte)(controls & ~domain);
                    }
                    mayChangeNavigation |= command is FieldActorLadder or FieldActorJump or FieldActorPlacement or FieldActorMove or FieldTriangleCommand;
                    FieldHostRead? read = command switch
                    {
                        FieldGilQuery => new(FieldHostReadKind.Gil),
                        FieldItemQuantityQuery item => new(FieldHostReadKind.ItemQuantity, item.ItemId),
                        FieldActorQuery actor => new(FieldHostReadKind.Actor, actor.Entity, (int)actor.Query),
                        FieldControlCommand { Property: FieldControlKind.PreviousMap } => new(FieldHostReadKind.PreviousMap),
                        FieldControlCommand { Property: FieldControlKind.BattleResult } => new(FieldHostReadKind.BattleResult),
                        FieldMateriaCommand => new(FieldHostReadKind.MateriaCapacity),
                        FieldMovieCommand { Operation: FieldMovieOperation.ReadFrame } => new(FieldHostReadKind.MovieFrame),
                        FieldMusicPlayingQuery => new(FieldHostReadKind.MusicPlaying),
                        FieldRandomCommand => new(FieldHostReadKind.Random),
                        _ => null
                    };
                    if (read is not null)
                    {
                        hostReads.Add(read);
                        if (read.Kind == FieldHostReadKind.Random)
                        {
                            throw new NotSupportedException("A random-result dependency requires an explicit stochastic event coverage contract.");
                        }
                    }
                }
                int next = detached.entities[owner].Pc;
                if (next == pc)
                {
                    if (opcode == 0x24)
                    {
                        next = pc + 3;
                    }
                    else
                    {
                        throw new NotSupportedException("The native instruction did not expose a finite decoding successor.");
                    }
                }
                Enqueue(next);
            }
            catch (Exception error) when (error is FieldExecutionException or InvalidDataException or NotSupportedException or ArgumentException or IndexOutOfRangeException)
            {
                reason = $"{location.Entity}:{location.Pc:X4}: {error.Message}";
                break;
            }
        }
        FieldVariableRead[] dependencies = reads.Where(read => read.Bank != FieldBank.Immediate).OrderBy(read => read.Bank).ThenBy(read => read.Operand).ThenBy(read => read.Word).ToArray();
        exits.UnionWith(physicalExits.Where(exit => exit.Value).Select(exit => exit.Key));
        string fingerprint;
        try
        {
            fingerprint = ReadScriptAnalysisFingerprint(entity, script, dependencies);
        }
        catch (Exception error) when (error is InvalidDataException or NotSupportedException or ArgumentException)
        {
            reason ??= error.Message;
            fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{program.Identity}:{entity}:{script}:unsupported")));
        }
        return new(reason is null, reason, fingerprint, changesStory, dependencies, visited.Select(value => value.Pc).Distinct().Order().ToArray(), destinations.Order().ToArray()) { HostReads = hostReads.OrderBy(read => read.Kind).ThenBy(read => read.Parameter).ThenBy(read => read.Selector).ToArray(), MayChangeNavigation = mayChangeNavigation, ExitProgramCounters = exits.Order().ToArray() };
    }
}
