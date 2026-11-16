using System.Security.Cryptography;
using System.Text;

namespace Memoria.FF7.Events;

public sealed record FieldSpatialSample(int Id, FieldActorPosition Position);
public sealed record FieldSpatialDemand(int Entity, int ProgramCounter, int TargetEntity, int TargetScript, int SampleId, int[] DestinationFields, bool ChangesStory, string Fingerprint, FieldVariableRead[] Reads, FieldHostRead[] HostReads)
{
    public bool MayChangeNavigation { get; init; }
    public int RequestMode { get; init; }
    public int RequestPriority { get; init; }
    public int SourcePriority { get; init; } = 7;
    public bool TriangleOnly { get; init; }
    public bool Stochastic { get; init; }
}
public sealed record FieldSpatialObservation(FieldSpatialDemand[] Demands, FieldVariableRead[] Reads, string Fingerprint);

public sealed partial class FieldVariables
{
    internal IEnumerable<FieldVariableRead> SpatialSourceBytes(FieldVariableRead read)
    {
        if (read.Bank == FieldBank.Immediate)
        {
            yield break;
        }
        var (storage, index, word) = Locate(read.Bank, (byte)read.Operand);
        int width = read.Word && word ? 2 : 1;
        for (int offset = 0; offset < width; offset++)
        {
            int address = index + offset;
            FieldBank bank = ReferenceEquals(storage, map) ? FieldBank.MapByte : (address / 256) switch
            {
                0 => FieldBank.Persistent0Byte,
                1 => FieldBank.Persistent1Byte,
                2 => FieldBank.Persistent2Byte,
                3 => FieldBank.Persistent3Byte,
                4 => FieldBank.Persistent4Byte,
                _ => throw new InvalidDataException("Spatial inspection read exceeds variable storage.")
            };
            yield return new(bank, checked((ushort)(ReferenceEquals(storage, map) ? address : address % 256)), false);
        }
    }
}

public sealed partial class FieldMachine
{
    [Flags]
    private enum SpatialTaint
    {
        None = 0,
        Coordinates = 1,
        Triangle = 2,
        Unknown = 4,
        Stochastic = 8
    }

    private readonly record struct SpatialScalarTaint(SpatialTaint Taint, int Scalar, int Width, int? RandomDraw = null);

    private sealed class SpatialInspectionQueryException : Exception
    {
    }

    private sealed class SpatialStochasticPredicateException(int entity, int pc) : Exception($"FFVII autoplay spatial projection requires a stochastic predicate contract for entity {entity}, PC {pc:X4}.")
    {
    }

    /// <summary>Projects current autonomous spatial predicates onto supplied physical samples without dispatching scripts or hosts.</summary>
    public FieldSpatialObservation ReadCurrentSpatialDemands(int controlledEntity, IReadOnlyList<FieldSpatialSample> samples, int instructionBudget = 128, FieldSpatialRandomDomain? randomDomain = null)
    {
        ArgumentNullException.ThrowIfNull(samples);
        if ((uint)controlledEntity >= entities.Length || instructionBudget is < 1 or > 4096 || samples.Select(sample => sample.Id).Distinct().Count() != samples.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(controlledEntity));
        }
        if (!Initialized || !entities[controlledEntity].ModelBound)
        {
            return new([], [], program.Identity);
        }
        FieldVmSnapshot source = Capture() with { CycleCursor = null };
        List<FieldSpatialDemand> demands = [];
        HashSet<FieldVariableRead> observationReads = [];
        try
        {
            for (int entity = 0; entity < entities.Length; entity++)
            {
                if (source.Entities[entity].Priority != 7)
                {
                    continue;
                }
                if (!Project(entity, null, [], null))
                {
                    continue;
                }
                foreach (FieldSpatialSample sample in samples)
                {
                    Queue<byte[]> alternatives = new();
                    alternatives.Enqueue(randomDomain?.Phases ?? [0]);
                    while (alternatives.TryDequeue(out byte[]? phases))
                    {
                        Project(entity, sample, phases, alternatives);
                    }
                }
            }
        }
        catch (SpatialStochasticPredicateException error)
        {
            throw new NotSupportedException(error.Message, error);
        }
        FieldVariableRead[] observed = observationReads.OrderBy(read => read.Bank).ThenBy(read => read.Operand).ToArray();
        string observationFacts = $"{ReadScriptAnalysisAllocationFingerprint()}|{string.Join(';', source.Entities.Select(entity => $"{entity.Priority}:{entity.ProgramCounter}"))}|{ReadScriptAnalysisFingerprint(controlledEntity, 0, observed)}";
        FieldSpatialDemand[] distinct = demands.GroupBy(demand => (demand.Entity, demand.ProgramCounter, demand.TargetEntity, demand.TargetScript, demand.SampleId, demand.Fingerprint)).Select(group => group.First()).ToArray();
        return new(distinct, observed, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(observationFacts))));

        bool Project(int entity, FieldSpatialSample? sample, byte[] phases, Queue<byte[]>? alternatives)
        {
            FieldMachine detached = Restore(program, source, new InspectionHost());
            HashSet<FieldVariableRead> dependencies = [];
            HashSet<FieldVariableRead> written = [];
            HashSet<FieldVariableRead> instructionReads = [];
            HashSet<FieldVariableRead> instructionWrites = [];
            bool sampled = false;
            Dictionary<FieldVariableRead, SpatialScalarTaint> taints = [];
            SpatialTaint instructionTaint = SpatialTaint.None;
            SpatialTaint predicateTaint = SpatialTaint.None;
            FieldActorQueryKind? queryKind = null;
            int queryWrites = 0;
            int scalar = 0;
            int randomDraw = 0;
            int? instructionRandomDraw = null;
            bool stochasticPredicate = false;
            HashSet<int> visited = [];
            detached.inspection = new()
            {
                StopAtHost = true,
                ActorReply = query =>
                {
                    if (query.Entity != controlledEntity || query.Query is not (FieldActorQueryKind.Position or FieldActorQueryKind.Coordinates or FieldActorQueryKind.Triangle))
                    {
                        throw new InputInspectionBoundaryException();
                    }
                    if (sample is null)
                    {
                        throw new SpatialInspectionQueryException();
                    }
                    if (!sampled)
                    {
                        visited.Clear();
                    }
                    sampled = true;
                    queryKind = query.Query;
                    int value = query.Query == FieldActorQueryKind.Triangle ? sample.Position.Triangle : (ushort)sample.Position.X | (sample.Position.Y << 16);
                    return new(true, value, sample.Position);
                }
            };
            detached.Variables.ReadObserved = read =>
            {
                FieldVariableRead[] bytes = detached.Variables.SpatialSourceBytes(read).ToArray();
                instructionReads.UnionWith(bytes);
                SpatialScalarTaint[] values = bytes.Select(value => taints.GetValueOrDefault(value)).ToArray();
                SpatialTaint consumed = values.Aggregate(SpatialTaint.None, (result, value) => result | value.Taint);
                if (consumed != SpatialTaint.None && (values.Any(value => value.Width != bytes.Length) || values.Select(value => value.Scalar).Distinct().Count() != 1))
                {
                    consumed |= SpatialTaint.Unknown;
                }
                instructionTaint |= consumed;
            };
            detached.Variables.WriteObserved = write =>
            {
                FieldVariableRead[] bytes = detached.Variables.SpatialSourceBytes(write).ToArray();
                instructionWrites.UnionWith(bytes);
                SpatialTaint result = instructionTaint;
                if (queryKind is FieldActorQueryKind kind)
                {
                    result = kind == FieldActorQueryKind.Triangle || kind == FieldActorQueryKind.Position && queryWrites == 3 ? SpatialTaint.Triangle : SpatialTaint.Coordinates;
                    if (!write.Word || bytes.Length != 2 || instructionTaint != SpatialTaint.None)
                    {
                        result |= SpatialTaint.Unknown;
                    }
                    queryWrites++;
                }
                SpatialScalarTaint value = new(result, ++scalar, bytes.Length, instructionRandomDraw);
                foreach (FieldVariableRead address in bytes)
                {
                    taints[address] = value;
                }
            };
            for (int step = 0; step < instructionBudget; step++)
            {
                Entity owner = detached.entities[entity];
                int pc = owner.Pc;
                if (!visited.Add(pc))
                {
                    break;
                }
                instructionReads.Clear();
                instructionWrites.Clear();
                instructionTaint = SpatialTaint.None;
                queryKind = null;
                queryWrites = 0;
                instructionRandomDraw = null;
                try
                {
                    byte opcode = program.Read(pc);
                    if (opcode == 0x60)
                    {
                        if (sampled && (predicateTaint & (SpatialTaint.Coordinates | SpatialTaint.Triangle)) != 0 && (predicateTaint & SpatialTaint.Unknown) == 0)
                        {
                            detached.inspection = new() { ActorReply = detached.inspection.ActorReply };
                            detached.Execute(entity);
                            FieldTransitionCommand transition = detached.inspection.Commands.OfType<FieldTransitionCommand>().Single();
                            RecordDemand(pc, entity, -1, [transition.Map], false, []);
                        }
                        break;
                    }
                    if (opcode is >= 1 and <= 6)
                    {
                        if (sampled)
                        {
                            AddDemand(pc, opcode);
                        }
                        break;
                    }
                    if (opcode == 0x24 || opcode == 0x5F)
                    {
                        owner.Pc = unchecked((ushort)(pc + (opcode == 0x24 ? 3 : 1)));
                        owner.Wait = 0;
                        continue;
                    }
                    if (opcode is >= 0x14 and <= 0x19)
                    {
                        FieldConditional condition = detached.ReadConditional(entity, opcode);
                        if ((instructionTaint & SpatialTaint.Stochastic) != 0)
                        {
                            if (!sampled)
                            {
                                break;
                            }
                            if (randomDomain is null || alternatives is null)
                            {
                                throw new SpatialStochasticPredicateException(entity, pc);
                            }
                            int? leftDraw = ReadRandomDraw(condition.LeftSource, pc);
                            int? rightDraw = ReadRandomDraw(condition.RightSource, pc);
                            List<byte> matched = [];
                            List<byte> unmatched = [];
                            foreach (byte phase in phases)
                            {
                                int left = leftDraw is int firstDraw ? randomDomain.Value(phase, firstDraw) : condition.Left;
                                int right = rightDraw is int secondDraw ? randomDomain.Value(phase, secondDraw) : condition.Right;
                                (detached.Compare(left, right, condition.Operation) ? matched : unmatched).Add(phase);
                            }
                            if (matched.Count != 0 && unmatched.Count != 0)
                            {
                                alternatives.Enqueue(unmatched.ToArray());
                            }
                            phases = matched.Count != 0 ? matched.ToArray() : unmatched.ToArray();
                            owner.Pc = matched.Count != 0 ? condition.MatchedPc : condition.UnmatchedPc;
                            stochasticPredicate = true;
                        }
                        else
                        {
                            owner.Pc = detached.Compare(condition.Left, condition.Right, condition.Operation) ? condition.MatchedPc : condition.UnmatchedPc;
                        }
                        predicateTaint |= instructionTaint & ~SpatialTaint.Stochastic;
                    }
                    else if (opcode is 0x99 or 0x33 or 0x4A)
                    {
                        if (opcode == 0x99)
                        {
                            instructionTaint = SpatialTaint.Stochastic;
                            instructionRandomDraw = ++randomDraw;
                        }
                        InstructionInspection prior = detached.inspection;
                        detached.inspection = new() { ActorReply = prior.ActorReply };
                        detached.Execute(entity);
                        detached.inspection = prior;
                    }
                    else
                    {
                        if (!IsSpatialInspectionOpcode(opcode))
                        {
                            break;
                        }
                        detached.Execute(entity);
                    }
                    // Stores own their result; only preceding source reads can define their dependencies.
                    dependencies.UnionWith(instructionReads.Where(read => !written.Contains(read) && !instructionWrites.Contains(read)));
                    written.UnionWith(instructionWrites);
                }
                catch (Exception error) when (error is InputInspectionBoundaryException or FieldExecutionException or InvalidDataException or NotSupportedException or ArgumentException or IndexOutOfRangeException)
                {
                    break;
                }
                catch (SpatialInspectionQueryException)
                {
                    dependencies.UnionWith(instructionReads.Where(read => !written.Contains(read)));
                    observationReads.UnionWith(dependencies);
                    return true;
                }
            }
            observationReads.UnionWith(dependencies);
            return false;

            int? ReadRandomDraw(FieldVariableRead read, int pc)
            {
                FieldVariableRead[] bytes = detached.Variables.SpatialSourceBytes(read).ToArray();
                if (!bytes.Any(value => (taints.GetValueOrDefault(value).Taint & SpatialTaint.Stochastic) != 0))
                {
                    return null;
                }
                if (bytes.Length != 1 || taints.GetValueOrDefault(bytes[0]).RandomDraw is not int draw)
                {
                    throw new SpatialStochasticPredicateException(entity, pc);
                }
                return draw;
            }

            void AddDemand(int pc, byte opcode)
            {
                int target = program.Read(pc + 1);
                if (opcode >= 4)
                {
                    if (target >= 3)
                    {
                        return;
                    }
                    int character = detached.ReadParty()[target];
                    target = character == 255 ? -1 : Array.FindIndex(detached.entities, actor => actor.PartyCharacter == character);
                }
                if ((uint)target >= detached.entities.Length)
                {
                    return;
                }
                int script = program.Read(pc + 2) & 31;
                FieldScriptAnalysis child = detached.AnalyzeScript(target, script);
                if (!child.Complete || child.DestinationFields.Length == 0 && !child.MayChangeNavigation)
                {
                    return;
                }
                dependencies.UnionWith(instructionReads.Where(read => !written.Contains(read)));
                foreach (FieldVariableRead read in child.Reads)
                {
                    FieldVariableRead[] bytes = detached.Variables.SpatialSourceBytes(read).ToArray();
                    if (bytes.Any(value => (taints.GetValueOrDefault(value).Taint & SpatialTaint.Stochastic) != 0))
                    {
                        return;
                    }
                    dependencies.UnionWith(bytes.Where(value => !written.Contains(value)));
                }
                RecordDemand(pc, target, script, child.DestinationFields, child.ChangesStory, child.HostReads, child.MayChangeNavigation, opcode > 3 ? opcode - 3 : opcode, program.Read(pc + 2) >> 5);
            }

            void RecordDemand(int pc, int target, int script, int[] destinations, bool changesStory, FieldHostRead[] hostReads, bool mayChangeNavigation = false, int requestMode = 0, int requestPriority = 0)
            {
                dependencies.UnionWith(instructionReads.Where(read => !written.Contains(read)));
                FieldVariableRead[] reads = dependencies.OrderBy(read => read.Bank).ThenBy(read => read.Operand).ToArray();
                string facts = $"{ReadScriptAnalysisAllocationFingerprint()}|{entity}:{pc}:{target}:{script}|{ReadScriptAnalysisFingerprint(target, Math.Max(0, script), reads)}|{(stochasticPredicate ? randomDomain?.Fingerprint : null)}";
                string fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(facts)));
                demands.Add(new(entity, pc, target, script, sample!.Id, destinations, changesStory, fingerprint, reads, hostReads) { TriangleOnly = predicateTaint == SpatialTaint.Triangle, Stochastic = stochasticPredicate, MayChangeNavigation = mayChangeNavigation, RequestMode = requestMode, RequestPriority = requestPriority, SourcePriority = source.Entities[entity].Priority });
            }
        }
    }

    private static bool IsSpatialInspectionOpcode(byte opcode)
    {
        return opcode is >= 0x10 and <= 0x19 or 0x75 or 0x76 or 0x77 or 0x78 or >= 0x80 and <= 0x89 or >= 0x8D and <= 0x98 or 0xB8 or 0xB9 or 0xC1;
    }
}
