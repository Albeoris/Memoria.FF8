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
    Random,
    MainPartySlot
}

public sealed record FieldHostRead(FieldHostReadKind Kind, int Parameter = 0, int Selector = 0);
public sealed record FieldScriptInstructionSite(int Entity, int ProgramCounter);
public sealed record FieldActorLadderLanding(int Entity, int ProgramCounter, FieldActorLadder Command, bool Literal);
public sealed record FieldActorPlacementLanding(int Entity, int ProgramCounter, FieldActorPlacement Command);
public sealed record FieldActorJumpLanding(int Entity, int ProgramCounter, FieldActorJump Command, bool Literal);
public sealed record FieldScriptAnalysis(bool Complete, string? Reason, string Fingerprint, bool ChangesStory, FieldVariableRead[] Reads, int[] ProgramCounters, int[] DestinationFields)
{
    public FieldHostRead[] HostReads { get; init; } = [];
    public bool MayChangeNavigation { get; init; }
    public bool InputIndependent { get; init; }
    public bool DeterministicTransfer { get; init; }
    public FieldStochasticPresentation? StochasticPresentation { get; init; }
    public string? NativeRandomExecutionDomain { get; init; }
    public int[] ExitProgramCounters { get; init; } = [];
    public int[] UnblockedTriangles { get; init; } = [];
    public FieldActorLadderLanding[] LadderLandings { get; init; } = [];
    public FieldActorJumpLanding[] TerminalJumpLandings { get; init; } = [];
    public FieldActorPlacementLanding[] PlacementLandings { get; init; } = [];
    public FieldActorJumpLanding[] PossibleJumpLandings { get; init; } = [];
    public bool IndependentActorMotionLoop { get; init; }
    public FieldFadeCommand? RepeatingConstantFade { get; init; }
    public FieldScriptInstructionSite[] InstructionSites { get; init; } = [];
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
    private static bool HasTerminalJumpFooter(FieldProgram program, int pc)
    {
        while ((uint)pc < program.Length)
        {
            byte opcode = program.Read(pc);
            if (opcode == 0)
            {
                return true;
            }
            if (opcode is 0x33 or 0x4A && pc + 1 < program.Length && program.Read(pc + 1) == 0)
            {
                pc += 2;
            }
            else if (opcode == 0xA2 && pc + 2 < program.Length && program.Read(pc + 2) != 0)
            {
                pc += 3;
            }
            else if (opcode is 0x10 or 0x11 && pc + (opcode == 0x11 ? 2 : 1) < program.Length)
            {
                int distance = opcode == 0x11 ? program.Read(pc + 1) | program.Read(pc + 2) << 8 : program.Read(pc + 1);
                int next = unchecked((ushort)(pc + 1 + distance));
                if (next <= pc)
                {
                    return false;
                }
                pc = next;
            }
            else
            {
                return false;
            }
        }
        return false;
    }

    private sealed class InstructionInspection
    {
        public List<FieldCommand> Commands { get; } = [];
        public bool StopAtHost { get; init; }
        public Func<FieldActorQuery, FieldHostReply>? ActorReply { get; init; }
    }

    private InstructionInspection? inspection;

    private sealed class NavigationInvariantInspection(NavigationByteDomains domains, IReadOnlySet<FieldNavigationEntryPoint> coveredEntries)
    {
        public bool RestrictPartyRecipientsToCurrentAllocation { get; init; }
        public bool MayChangePartyAllocation { get; set; }
        public NavigationByteDomains Domains { get; } = domains;
        public HashSet<FieldVariableRead> Writes { get; } = [];
        public HashSet<FieldVariableRead> AssumedReads { get; } = [];
        public Dictionary<FieldVariableRead, (int Entity, int ProgramCounter)> WriteOrigins { get; } = [];
        public List<(int Entity, int ProgramCounter, FieldCommand Command)> Commands { get; } = [];
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

    /// <summary>Inspects native execution with an explicit random-owner dependency, without certifying outcomes or presentation equivalence.</summary>
    public FieldScriptAnalysis AnalyzeNativeRandomExecution(int entity, int script, FieldSpatialRandomDomain domain, int instructionBudget = 8192)
    {
        ArgumentNullException.ThrowIfNull(domain);
        if ((uint)entity >= entities.Length || script is < 0 or > 31 || instructionBudget is < 1 or > 65536)
        {
            throw new ArgumentOutOfRangeException(nameof(entity));
        }
        FieldScriptAnalysis analysis = AnalyzeScriptCore(entity, script, program.Entry(entity, script), instructionBudget, null, 0, collectRandomReads: true);
        return analysis.Complete && analysis.HostReads.Any(read => read.Kind == FieldHostReadKind.Random) ? analysis with { NativeRandomExecutionDomain = domain.Fingerprint } : analysis;
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

    public FieldScriptAnalysis AnalyzeScriptUntilFieldTransition(int entity, int programCounter)
    {
        if ((uint)entity >= entities.Length || (uint)programCounter >= program.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(programCounter));
        }
        return AnalyzeScriptCore(entity, 0, programCounter, 8192, null, 0, stopAtFieldTransition: true);
    }

    private FieldScriptAnalysis AnalyzeScriptCore(int entity, int script, int entry, int instructionBudget, IReadOnlySet<int>? exitBoundaries, byte initialOwnership, NavigationInvariantInspection? navigationInvariant = null, StochasticInspection? stochastic = null, bool collectRandomReads = false, bool stopAtFieldTransition = false)
    {
        byte[] persistent = Variables.CopyPersistent();
        byte[] map = Variables.CopyMap();
        HashSet<(int Entity, int Pc, byte Controls, bool PhysicalValid)> visited = [];
        HashSet<FieldVariableRead> reads = [];
        HashSet<FieldHostRead> hostReads = [];
        HashSet<int> destinations = [];
        HashSet<int> unblockedTriangles = [];
        HashSet<FieldActorLadderLanding> ladderLandings = [];
        HashSet<FieldActorJumpLanding> terminalJumpLandings = [];
        HashSet<FieldActorPlacementLanding> placementLandings = [];
        HashSet<FieldActorJumpLanding> possibleJumpLandings = [];
        HashSet<FieldScriptInstructionSite> independentActorInstructions = [];
        bool independentActorMotion = false;
        HashSet<FieldScriptInstructionSite> constantFadeInstructions = [];
        HashSet<FieldFadeCommand> constantFades = [];
        HashSet<int> exits = [];
        bool inputIndependent = true;
        bool deterministicTransfer = true;
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
                inputIndependent &= opcode is not (0x30 or 0x31 or 0x32);
                deterministicTransfer &= owner == entity && (opcode is 0x00 or 0x10 or 0x11 or >= 0x14 and <= 0x19 or 0x24 or 0x25 or 0x33 or 0x4A or 0x60 or 0x6B or 0x6C);
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
                StochasticNode? stochasticNode = stochastic?.Node(pc, opcode);
                void Enqueue(int offset)
                {
                    stochasticNode?.Successors.Add(unchecked((ushort)offset));
                    pending.Enqueue((owner, unchecked((ushort)offset), controls, physicalValid));
                }
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
                    if (stochastic is not null)
                    {
                        throw new NotSupportedException("Stochastic presentation does not admit script requests.");
                    }
                    int target = Byte(1);
                    if (opcode >= 4)
                    {
                        if (target >= 3)
                        {
                            throw new InvalidDataException("Party request targets an invalid script-party slot.");
                        }
                        reads.Add(new(FieldBank.Persistent1Byte, (ushort)(9 + target), false));
                        if (navigationInvariant is not null && (!navigationInvariant.RestrictPartyRecipientsToCurrentAllocation || navigationInvariant.MayChangePartyAllocation || !navigationInvariant.Domains.IsSingleton(new(FieldBank.Persistent1Byte, (ushort)(9 + target), false))))
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
                    independentActorInstructions.Add(new(owner, pc));
                    constantFadeInstructions.Add(new(owner, pc));
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
                    stochasticNode?.Reads.UnionWith([leftRead, rightRead]);
                    if (navigationInvariant is not null && navigationInvariant.Domains.Compare(leftRead, rightRead, Byte(word ? 6 : 4), opcode >= 0x18) is bool matched)
                    {
                        if (leftRead.Bank != FieldBank.Immediate)
                        {
                            navigationInvariant.AssumedReads.Add(leftRead);
                        }
                        if (rightRead.Bank != FieldBank.Immediate)
                        {
                            navigationInvariant.AssumedReads.Add(rightRead);
                        }
                        Enqueue(matched ? pc + branch + (wide ? 2 : 1) : pc + branch + (wide ? unchecked((short)Word(branch)) : Byte(branch)));
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
                    if (navigationInvariant is not null && Variables.SpatialSourceBytes(read).Any(source => source.Bank == FieldBank.Persistent1Byte && source.Operand is >= 9 and <= 11 && !navigationInvariant.Domains.IsSingleton(source)))
                    {
                        throw new NotSupportedException("A producer requires a mutable native script-party query selector.");
                    }
                };
                variables.WriteObserved = write =>
                {
                    instructionWrites.Add(write);
                    if (navigationInvariant is not null)
                    {
                        foreach (FieldVariableRead source in Variables.SpatialSourceBytes(write))
                        {
                            navigationInvariant.Writes.Add(source);
                            navigationInvariant.WriteOrigins.TryAdd(source, (owner, pc));
                        }
                    }
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
                bool selfActorOnly = owner == entity && instructionWrites.Count == 0 && instructionReads.All(read => read.Bank == FieldBank.Immediate) && detached.inspection.Commands.All(command => command is FieldActorMove { TargetEntity: null } or FieldActorPlacement or FieldAnimation or FieldActorTurn or FieldActorOffset or FieldActorProperty { Property: not (FieldActorPropertyKind.PlayerCharacter or FieldActorPropertyKind.PartyCharacter) });
                if (selfActorOnly && (detached.inspection.Commands.Count != 0 || opcode is 0x24 or 0x5F))
                {
                    independentActorInstructions.Add(new(owner, pc));
                    independentActorMotion |= detached.inspection.Commands.Any(command => command is FieldActorMove or FieldActorPlacement);
                }
                bool literalState = owner == entity && instructionWrites.Count == 0 && instructionReads.All(read => read.Bank == FieldBank.Immediate);
                if (literalState && detached.inspection.Commands is [FieldFadeCommand { Type: 8, Speed: 255, Adjustment: 0, NewFade: false, Wait: false } constantFade])
                {
                    constantFades.Add(constantFade);
                    constantFadeInstructions.Add(new(owner, pc));
                }
                else if (literalState && detached.inspection.Commands.Count == 0 && opcode is 0x24 or 0x5F)
                {
                    constantFadeInstructions.Add(new(owner, pc));
                }
                if (stochasticNode is not null)
                {
                    stochasticNode.Reads.UnionWith(instructionReads);
                    stochasticNode.Writes.UnionWith(instructionWrites);
                    stochasticNode.Commands.AddRange(detached.inspection.Commands);
                }
                if (navigationInvariant is not null)
                {
                    bool known = detached.inspection.Commands.Count == 0 && instructionReads.All(navigationInvariant.Domains.IsSingleton);
                    variables.ReadObserved = null;
                    foreach (FieldVariableRead write in instructionWrites.SelectMany(Variables.SpatialSourceBytes))
                    {
                        navigationInvariant.Domains.Join(write, known ? variables.ReadByte(write.Bank, (byte)write.Operand) : null);
                    }
                }
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
                    inputIndependent &= command is FieldTransitionCommand or FieldPartyProjection;
                    if (navigationInvariant is not null && command is FieldActorBind or FieldPartyCharacter or FieldPartyJoin or FieldPartySplit or FieldActorProperty { Property: FieldActorPropertyKind.PlayerCharacter })
                    {
                        navigationInvariant.MayChangePartyAllocation = true;
                    }
                    navigationInvariant?.Commands.Add((owner, pc, command));
                    if (navigationInvariant is not null && !navigationInvariant.CoversAllocation(owner, command))
                    {
                        throw new NotSupportedException($"A producer can change the external script-trigger allocation through {command.GetType().Name}.");
                    }
                    if (exitBoundaries is not null && command is FieldControlCommand control && control.Property is FieldControlKind.PlayerLocked or FieldControlKind.MenuDisabled)
                    {
                        byte domain = control.Property == FieldControlKind.PlayerLocked ? (byte)1 : (byte)2;
                        controls = control.Value != 0 ? (byte)(controls | domain) : (byte)(controls & ~domain);
                    }
                    mayChangeNavigation |= command is FieldActorLadder or FieldActorJump or FieldActorPlacement or FieldActorMove or FieldTriangleCommand;
                    if (command is FieldActorLadder ladder)
                    {
                        ladderLandings.Add(new(owner, pc, ladder, opcode == 0xC2 && Byte(1) == 0 && Byte(2) == 0));
                    }
                    if (command is FieldActorPlacement placement && opcode == 0xA5 && Byte(1) == 0 && Byte(2) == 0)
                    {
                        placementLandings.Add(new(owner, pc, placement));
                    }
                    if (command is FieldActorJump jump)
                    {
                        FieldActorJumpLanding landing = new(owner, pc, jump, opcode == 0xC0 && Byte(1) == 0 && Byte(2) == 0);
                        possibleJumpLandings.Add(landing);
                        if (HasTerminalJumpFooter(program, pc + 11))
                        {
                            terminalJumpLandings.Add(landing);
                        }
                    }
                    if (command is FieldTriangleCommand { Blocked: false } triangle)
                    {
                        unblockedTriangles.Add(triangle.Triangle);
                    }
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
                        FieldPartyCommand { Operation: FieldPartyOperation.ReadSlot } party => new(FieldHostReadKind.MainPartySlot, party.Member0),
                        FieldRandomCommand => new(FieldHostReadKind.Random),
                        _ => null
                    };
                    if (read is not null)
                    {
                        if (navigationInvariant is not null && instructionReads.Any(source => !navigationInvariant.Domains.IsSingleton(source)))
                        {
                            throw new NotSupportedException("A producer requires a mutable native host-query selector.");
                        }
                        hostReads.Add(read);
                        if (read.Kind == FieldHostReadKind.Random && navigationInvariant is null && stochastic is null && !collectRandomReads)
                        {
                            throw new NotSupportedException("A random-result dependency requires an explicit stochastic event coverage contract.");
                        }
                    }
                }
                if (stopAtFieldTransition && detached.inspection.Commands.Any(command => command is FieldTransitionCommand))
                {
                    continue;
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
        bool independentLoop = reason is null && independentActorMotion && visited.All(site => site.Entity == entity && independentActorInstructions.Contains(new(site.Entity, site.Pc)));
        FieldFadeCommand? repeatingFade = reason is null && constantFades.Count == 1 && visited.All(site => site.Entity == entity && constantFadeInstructions.Contains(new(site.Entity, site.Pc))) ? constantFades.Single() : null;
        return new(reason is null, reason, fingerprint, changesStory, dependencies, visited.Select(value => value.Pc).Distinct().Order().ToArray(), destinations.Order().ToArray()) { HostReads = hostReads.OrderBy(read => read.Kind).ThenBy(read => read.Parameter).ThenBy(read => read.Selector).ToArray(), MayChangeNavigation = mayChangeNavigation, InputIndependent = reason is null && inputIndependent, DeterministicTransfer = reason is null && deterministicTransfer && destinations.Count == 1, ExitProgramCounters = exits.Order().ToArray(), UnblockedTriangles = unblockedTriangles.Order().ToArray(), LadderLandings = ladderLandings.ToArray(), TerminalJumpLandings = terminalJumpLandings.ToArray(), PossibleJumpLandings = possibleJumpLandings.ToArray(), PlacementLandings = placementLandings.ToArray(), IndependentActorMotionLoop = independentLoop, RepeatingConstantFade = repeatingFade, InstructionSites = visited.Select(value => new FieldScriptInstructionSite(value.Entity, value.Pc)).Distinct().ToArray() };
    }
}
