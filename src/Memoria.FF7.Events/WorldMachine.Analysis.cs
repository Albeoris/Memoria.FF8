using System.Security.Cryptography;
using System.Text.Json;

namespace Memoria.FF7.Events;

public sealed record WorldVariableRead(ushort Opcode, int Operand);
public enum WorldHostDependencyKind
{
    Special,
    ModelDistance,
    ZoneDistance,
    StoredPositionDistance,
    ZoneDirection,
    OperationResult
}

public sealed record WorldHostDependency(WorldHostDependencyKind Kind, int Model, int Selector, int? FreeModelIndex = null);
public sealed record WorldHostReadValue(WorldHostDependency Dependency, int? Value);
public sealed record WorldTransitionObservation(int ProgramCounter, WorldOperation Operation, ushort? Selector);
public sealed record WorldScriptAnalysis(bool Complete, string? Reason, int Header, int Entry, WorldVariableRead[] Reads, WorldHostDependency[] HostReads, WorldTransitionObservation[] Transitions, int[] ProgramCounters)
{
    public WorldHostReadValue[] HostValues { get; init; } = [];
    public bool IsPureFieldTransfer { get; init; }
    public bool MayEnterField => Transitions.Any(transition => transition.Operation == WorldOperation.FieldTransition);
    public bool MayReloadWorld => Transitions.Any(transition => transition.Operation == WorldOperation.FieldTransitionSelector);
}

public sealed partial class WorldMachine
{
    private sealed record WorldInspectionRequest(int Model, int Header, int Priority);

    private sealed class WorldInspection
    {
        public HashSet<WorldVariableRead> Reads { get; } = [];
        public HashSet<WorldHostDependency> HostReads { get; } = [];
        public List<WorldHostRequest> Commands { get; } = [];
        public List<WorldInspectionRequest> Requests { get; } = [];
        public Dictionary<WorldHostDependency, int?> HostValues { get; } = [];
        public Func<WorldHostDependency, int?>? ReadHost { get; set; }
        public bool Unresolved { get; set; }
        public string? Reason { get; set; }

        public WorldInspection Fork()
        {
            return new() { Unresolved = Unresolved, Reason = Reason, ReadHost = ReadHost };
        }

        public void BeginInstruction()
        {
            Reads.Clear();
            HostReads.Clear();
            HostValues.Clear();
            Commands.Clear();
            Requests.Clear();
        }

        public int Unknown(WorldHostDependency dependency)
        {
            HostReads.Add(dependency);
            int? projected = ReadHost?.Invoke(dependency);
            HostValues[dependency] = projected;
            if (projected is int value)
            {
                return value;
            }
            Unresolved = true;
            Reason ??= $"Native world host dependency {dependency.Kind}/{dependency.Selector} requires a read-only snapshot projection.";
            return 0;
        }
    }

    private sealed class WorldInspectionHost(WorldInspection observation) : IWorldHost
    {
        public WorldHostReply Execute(WorldHostRequest request)
        {
            observation.Commands.Add(request);
            if (request.Command.Operation is WorldOperation.RunChocoboPath or WorldOperation.WaitWindow or WorldOperation.WaitChocoboPath or WorldOperation.WaitChocoboRuns)
            {
                observation.Unknown(new(WorldHostDependencyKind.OperationResult, request.ActiveModel, (int)request.Command.Operation, request.FreeModelIndex));
            }
            return new(true);
        }

        public int ReadSpecial(int selector, int model) => ReadSpecial(selector, model, null);
        public int ReadSpecial(int selector, int model, int? freeModelIndex) => observation.Unknown(new(WorldHostDependencyKind.Special, model, selector, freeModelIndex));
        public int ReadModelDistance(int model, int target) => ReadModelDistance(model, target, null);
        public int ReadModelDistance(int model, int target, int? freeModelIndex) => observation.Unknown(new(WorldHostDependencyKind.ModelDistance, model, target, freeModelIndex));
        public int ReadZoneDistance(int model, int zone) => ReadZoneDistance(model, zone, null);
        public int ReadZoneDistance(int model, int zone, int? freeModelIndex) => observation.Unknown(new(WorldHostDependencyKind.ZoneDistance, model, zone, freeModelIndex));
        public int ReadStoredPositionDistance(int model, int slot) => ReadStoredPositionDistance(model, slot, null);
        public int ReadStoredPositionDistance(int model, int slot, int? freeModelIndex) => observation.Unknown(new(WorldHostDependencyKind.StoredPositionDistance, model, slot, freeModelIndex));
        public int ReadZoneDirection(int model, int zone) => ReadZoneDirection(model, zone, null);
        public int ReadZoneDirection(int model, int zone, int? freeModelIndex) => observation.Unknown(new(WorldHostDependencyKind.ZoneDirection, model, zone, freeModelIndex));
    }

    private WorldInspection? inspection;

    /// <summary>Inspects potential native mesh-script paths using detached execution of the same single-instruction decoder. No live host, VM, persistent byte or RNG is advanced.</summary>
    public WorldScriptAnalysis AnalyzeMeshDispatch(int chunkX, int chunkZ, int terrainScript, int instructionBudget = 2048, Func<WorldHostDependency, int?>? readHost = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(terrainScript, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(terrainScript, 7);
        ArgumentOutOfRangeException.ThrowIfLessThan(instructionBudget, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(instructionBudget, 65536);
        int header = WorldProgram.MeshDispatchHeader(chunkX, chunkZ, Math.Max(0, terrainScript - 3));
        int entry = terrainScript < 3 ? 0 : program.Find(header);
        if (entry == 0)
        {
            return new(true, null, header, 0, [], [], [], []);
        }
        WorldMachine initial = ForkInspection();
        initial.inspection!.ReadHost = readHost;
        initial.system.Pc = entry;
        initial.system.Priority = 3;
        initial.system.MoveCounter = 0;
        initial.system.Calls.Clear();
        initial.active = initial.executing = initial.system;
        Queue<WorldMachine> pending = new();
        pending.Enqueue(initial);
        HashSet<string> visited = new(StringComparer.Ordinal);
        HashSet<WorldVariableRead> reads = [];
        HashSet<WorldHostDependency> hostReads = [];
        Dictionary<WorldHostDependency, int?> hostValues = [];
        HashSet<WorldTransitionObservation> transitions = [];
        HashSet<int> counters = [];
        bool hasSideEffects = false;
        string? reason = null;
        int instructions = 0;
        while (pending.TryDequeue(out WorldMachine? machine))
        {
            try
            {
                Entity owner = machine.executing;
                if (owner.Pc == 0 || !visited.Add(machine.InspectionIdentity()))
                {
                    continue;
                }
                if (instructions++ >= instructionBudget)
                {
                    reason ??= "The bounded native world control-flow inspection exhausted its instruction budget.";
                    break;
                }
                WorldInstruction instruction = program.ReadInstruction(owner.Pc);
                counters.Add(instruction.Address);
                hasSideEffects |= instruction.Opcode == 0xE0 || instruction.Opcode >= 0x300 && instruction.Opcode != (ushort)WorldOperation.FieldTransition;
                if (instruction.Opcode == 0x203 && owner.Calls.Count == 0)
                {
                    continue;
                }
                WorldInspection observation = machine.inspection!;
                observation.BeginInstruction();
                bool yielded = machine.ExecuteInstruction(owner);
                hasSideEffects |= observation.Commands.Any(command => command.Command.Operation != WorldOperation.FieldTransition);
                reads.UnionWith(observation.Reads);
                hostReads.UnionWith(observation.HostReads);
                foreach (var value in observation.HostValues)
                {
                    hostValues[value.Key] = value.Value;
                }
                reason ??= observation.Reason;
                bool handoff = false;
                foreach (WorldHostRequest command in observation.Commands.Where(command => command.Command.Operation is WorldOperation.FieldTransition or WorldOperation.FieldTransitionSelector))
                {
                    int[] values = command.Command.Arguments;
                    int selector = command.Command.Operation == WorldOperation.FieldTransition ? values[0] << 8 | values[1] : values[0];
                    int minimum = command.Command.Operation == WorldOperation.FieldTransition ? short.MinValue : 0;
                    ushort? represented = observation.Unresolved || selector < minimum || selector > ushort.MaxValue ? null : unchecked((ushort)selector);
                    transitions.Add(new(command.ProgramCounter, command.Command.Operation, represented));
                    if (represented is null)
                    {
                        reason ??= "A potential native world handoff has an unresolved selector dependency.";
                    }
                    handoff = true;
                }
                foreach (WorldInspectionRequest request in observation.Requests)
                {
                    int requestedEntry = program.Find(request.Header);
                    if (requestedEntry == 0)
                    {
                        continue;
                    }
                    WorldMachine requested = machine.ForkInspection();
                    Entity? target = request.Model < 0 ? requested.system : requested.models.SingleOrDefault(entity => entity.Model == request.Model && !entity.Detached);
                    if (target is null)
                    {
                        reason ??= "A native world script request has no represented resident execution owner.";
                        continue;
                    }
                    target.Pc = requestedEntry;
                    target.Priority = (byte)request.Priority;
                    target.MoveCounter = 0;
                    target.Calls.Clear();
                    requested.executing = requested.active = target;
                    pending.Enqueue(requested);
                }
                if (handoff)
                {
                    continue;
                }
                if (instruction.Opcode == 0x201)
                {
                    int alternate = owner.Pc == instruction.Operand ? instruction.NextProgramCounter : instruction.Operand;
                    machine.CheckPc(alternate);
                    WorldMachine branch = machine.ForkInspection();
                    branch.executing.Pc = alternate;
                    pending.Enqueue(branch);
                }
                if (yielded)
                {
                    machine.moved = false;
                }
                pending.Enqueue(machine);
            }
            catch (Exception error) when (error is InvalidDataException or NotSupportedException or ArgumentException or InvalidOperationException or OverflowException)
            {
                reason ??= error.Message;
            }
        }
        return new(reason is null, reason, header, entry, reads.OrderBy(read => read.Opcode).ThenBy(read => read.Operand).ToArray(), hostReads.OrderBy(read => read.Kind).ThenBy(read => read.Model).ThenBy(read => read.Selector).ThenBy(read => read.FreeModelIndex).ToArray(), transitions.OrderBy(transition => transition.ProgramCounter).ThenBy(transition => transition.Operation).ThenBy(transition => transition.Selector).ToArray(), counters.Order().ToArray())
        {
            HostValues = hostValues.OrderBy(value => value.Key.Kind).ThenBy(value => value.Key.Model).ThenBy(value => value.Key.Selector).ThenBy(value => value.Key.FreeModelIndex).Select(value => new WorldHostReadValue(value.Key, value.Value)).ToArray(),
            IsPureFieldTransfer = reason is null && !hasSideEffects && transitions.Any(transition => transition.Operation == WorldOperation.FieldTransition)
        };
    }

    /// <summary>Reads exact typed native bank dependencies without evaluating host or stochastic selectors.</summary>
    public string ReadPredicateFingerprint(IReadOnlyList<WorldVariableRead> reads)
    {
        return PredicateFingerprint(program.Identity, reads, read => ReadValue(new(read.Opcode, read.Operand)));
    }

    /// <summary>Projects only native global bank dependencies from current canonical storage, without restoring a foreign scene or supplying local memory.</summary>
    public static string ReadGlobalPredicateFingerprint(string programIdentity, byte[] persistent, IReadOnlyList<WorldVariableRead> reads)
    {
        if (persistent.Length != 1280)
        {
            throw new ArgumentException("World predicate storage must contain the canonical 1280 persistent bytes.", nameof(persistent));
        }
        if (reads.Any(read => (read.Opcode & 3) != 0))
        {
            throw new ArgumentException("A remote world predicate accepts only global native bank dependencies.", nameof(reads));
        }
        return PredicateFingerprint(programIdentity, reads, read => ReadBankValue(new(read.Opcode, read.Operand), persistent, []));
    }

    private static string PredicateFingerprint(string programIdentity, IReadOnlyList<WorldVariableRead> reads, Func<WorldVariableRead, int> readValue)
    {
        var facts = reads.Distinct().OrderBy(read => read.Opcode).ThenBy(read => read.Operand).Select(read =>
        {
            if (read.Opcode is < 0x114 or > 0x11F || (read.Opcode & 3) == 3)
            {
                throw new ArgumentException("World predicate dependencies must identify native typed bank reads.", nameof(reads));
            }
            return new { Read = read, Value = readValue(read) };
        }).ToArray();
        return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { Identity = programIdentity, Values = facts })));
    }

    private WorldMachine ForkInspection()
    {
        WorldInspection observation = inspection?.Fork() ?? new();
        WorldMachine copy = new(program, persistent.ToArray(), new WorldInspectionHost(observation));
        copy.Restore(Capture());
        copy.inspection = observation;
        copy.active = InspectionEntity(copy, active);
        copy.executing = InspectionEntity(copy, executing);
        copy.moved = moved;
        return copy;
    }

    private Entity InspectionEntity(WorldMachine copy, Entity source)
    {
        if (ReferenceEquals(source, system))
        {
            return copy.system;
        }
        int resident = models.IndexOf(source);
        if (resident >= 0)
        {
            return copy.models[resident];
        }
        int free = Array.IndexOf(freeModels.ToArray(), source);
        if (free >= 0)
        {
            return copy.freeModels.ToArray()[free];
        }
        throw new InvalidDataException("The native world inspection owner is outside its represented model pool.");
    }

    private string InspectionIdentity()
    {
        WorldSnapshot state = Capture() with { Tick = 0, NextRequestId = 1 };
        byte[] facts = JsonSerializer.SerializeToUtf8Bytes(new { State = state, Persistent = persistent, Active = active.Model, Executing = executing.Model, Moved = moved, Unresolved = inspection!.Unresolved });
        return Convert.ToHexString(SHA256.HashData(facts));
    }
}
