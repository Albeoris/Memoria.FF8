using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Memoria.FF7.Events;

public sealed record FieldSpatialSample(int Id, FieldActorPosition Position);
public sealed record FieldSpatialInputGuard(FieldScriptInputDemand Demand, bool Matched);
public sealed record FieldSpatialEffectOwner(int Entity, int Priority, int ProgramCounter, FieldCommand Command, int[] ExitProgramCounters);
public sealed record FieldSpatialDemand(int Entity, int ProgramCounter, int TargetEntity, int TargetScript, int SampleId, int[] DestinationFields, bool ChangesStory, string Fingerprint, FieldVariableRead[] Reads, FieldHostRead[] HostReads)
{
    public bool MayChangeNavigation { get; init; }
    public int RequestMode { get; init; }
    public int RequestPriority { get; init; }
    public int SourcePriority { get; init; } = 7;
    public bool TriangleOnly { get; init; }
    public bool Stochastic { get; init; }
    public FieldSpatialInputGuard[] InputGuards { get; init; } = [];
    public FieldSpatialEffectOwner? DirectEffectOwner { get; init; }
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
    /// <summary>Identifies native execution facts that change current-cycle physical-query provenance, without using ticks or presentation counters.</summary>
    public string ReadSpatialExecutionFingerprint()
    {
        byte[] facts = JsonSerializer.SerializeToUtf8Bytes(new
        {
            program.Identity,
            CycleCursor,
            Owners = entities.Select(entity => new { entity.Pc, entity.Priority, WaitState = entity.Wait == 0 ? 0 : entity.Wait == 1 ? 1 : 2 }).ToArray()
        });
        return Convert.ToHexString(SHA256.HashData(facts));
    }

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
    private sealed record SpatialInspectionAlternative(byte[] Phases, FieldSpatialInputGuard[] InputGuards);

    private sealed class SpatialInspectionQueryException : Exception
    {
    }

    private sealed class SpatialInputInspectionLimitException() : NotSupportedException("The bounded native spatial input witness search exhausted its constraint budget.")
    {
    }

    private sealed class SpatialEffectInspectionException(string message) : NotSupportedException(message)
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
        FieldVmSnapshot captured = Capture();
        FieldCycleCursor? sourceCursor = captured.CycleCursor;
        FieldVmSnapshot source = captured with { CycleCursor = null };
        List<FieldSpatialDemand> demands = [];
        HashSet<FieldVariableRead> observationReads = [];
        try
        {
            for (int entity = 0; entity < entities.Length; entity++)
            {
                if (source.Entities[entity].Priority != 7 || sourceCursor is not null && entity < sourceCursor.Entity)
                {
                    continue;
                }
                Queue<SpatialInspectionAlternative> probes = new();
                probes.Enqueue(new([], []));
                bool hasSpatialQuery = false;
                int projected = 0;
                while (probes.TryDequeue(out SpatialInspectionAlternative? probe))
                {
                    if (++projected > 4096)
                    {
                        throw new NotSupportedException("The bounded native spatial input-path inspection exhausted its alternative budget.");
                    }
                    if (Project(entity, null, probe.Phases, probes, probe.InputGuards))
                    {
                        hasSpatialQuery = true;
                        break;
                    }
                }
                if (!hasSpatialQuery)
                {
                    continue;
                }
                foreach (FieldSpatialSample sample in samples)
                {
                    Queue<SpatialInspectionAlternative> alternatives = new();
                    alternatives.Enqueue(new(randomDomain?.Phases ?? [0], []));
                    projected = 0;
                    while (alternatives.TryDequeue(out SpatialInspectionAlternative? alternative))
                    {
                        if (++projected > 4096)
                        {
                            throw new NotSupportedException("The bounded native spatial input-path inspection exhausted its alternative budget.");
                        }
                        Project(entity, sample, alternative.Phases, alternatives, alternative.InputGuards);
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

        bool Project(int entity, FieldSpatialSample? sample, byte[] phases, Queue<SpatialInspectionAlternative>? alternatives, FieldSpatialInputGuard[] selectedGuards)
        {
            FieldMachine detached = Restore(program, source, new InspectionHost());
            List<FieldSpatialInputGuard> inputGuards = [];
            HashSet<FieldVariableRead> dependencies = [];
            HashSet<FieldVariableRead> written = [];
            HashSet<FieldVariableRead> instructionReads = [];
            HashSet<FieldVariableRead> instructionWrites = [];
            bool sampled = false;
            bool ownQuerySampled = false;
            bool prefixSlice = false;
            bool physicalSampleValid = true;
            int projectionOwner = entity;
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
            HashSet<int> physicalPredicates = [];
            HashSet<FieldControlKind> acquiredControls = [];
            detached.inspection = new()
            {
                StopAtHost = true,
                ActorReply = query =>
                {
                    if (query.Entity != controlledEntity || query.Query is not (FieldActorQueryKind.Position or FieldActorQueryKind.Coordinates or FieldActorQueryKind.Triangle))
                    {
                        if (prefixSlice)
                        {
                            instructionTaint |= SpatialTaint.Unknown;
                            return new(true, Position: new(0, 0, 0, 0));
                        }
                        throw new InputInspectionBoundaryException();
                    }
                    if (!physicalSampleValid)
                    {
                        instructionTaint |= SpatialTaint.Unknown;
                        return new(true, Position: new(0, 0, 0, 0));
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
                    ownQuerySampled |= projectionOwner == entity;
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
            try
            {
                ProjectEarlierOwners();
            }
            catch (SpatialInspectionQueryException)
            {
                observationReads.UnionWith(dependencies);
                return true;
            }
            projectionOwner = entity;
            prefixSlice = false;
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
                        if (sampled && (ownQuerySampled || IsPhysicalPredicate()))
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
                    if (opcode is >= 0x30 and <= 0x32)
                    {
                        FieldScriptInputDemand demand = detached.ReadInputDemand(entity, pc, opcode);
                        FieldSpatialInputGuard? selected = selectedGuards.SingleOrDefault(guard => guard.Demand.Entity == entity && guard.Demand.ProgramCounter == pc);
                        if (selected is null)
                        {
                            FieldSpatialInputGuard matched = new(demand, true);
                            FieldSpatialInputGuard unmatched = new(demand, false);
                            bool canMatch = HasSpatialInputWitness([.. inputGuards, matched]);
                            bool canMiss = HasSpatialInputWitness([.. inputGuards, unmatched]);
                            if (canMatch && canMiss)
                            {
                                alternatives!.Enqueue(new(phases, [.. inputGuards, unmatched]));
                            }
                            selected = canMatch ? matched : canMiss ? unmatched : null;
                        }
                        if (selected is null || !HasSpatialInputWitness([.. inputGuards, selected]))
                        {
                            break;
                        }
                        inputGuards.Add(selected);
                        owner.Pc = unchecked((ushort)(selected.Matched ? pc + 4 : pc + 3 + program.Read(pc + 3)));
                    }
                    else if (opcode is >= 0x14 and <= 0x19)
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
                                alternatives.Enqueue(new(unmatched.ToArray(), inputGuards.ToArray()));
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
                        if ((instructionTaint & (SpatialTaint.Coordinates | SpatialTaint.Triangle)) != 0 && (instructionTaint & SpatialTaint.Unknown) == 0)
                        {
                            physicalPredicates.Add(pc);
                        }
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
                        foreach (FieldControlCommand control in detached.inspection.Commands.OfType<FieldControlCommand>().Where(control => control.Property is FieldControlKind.PlayerLocked or FieldControlKind.MenuDisabled))
                        {
                            if (control.Value != 0)
                            {
                                acquiredControls.Add(control.Property);
                            }
                            else
                            {
                                acquiredControls.Remove(control.Property);
                            }
                        }
                        detached.inspection = prior;
                    }
                    else
                    {
                        if (sampled && IsPhysicalPredicate() && opcode is 0x40 or 0x48 or 0x6D)
                        {
                            AddDirectEffect(pc);
                            break;
                        }
                        if (sampled && IsPhysicalPredicate() && opcode is 0x50 or 0x51 or 0x52 or 0x53 or 0xF1)
                        {
                            InstructionInspection prior = detached.inspection;
                            detached.inspection = new() { ActorReply = prior.ActorReply };
                            detached.Execute(entity);
                            detached.inspection = prior;
                            dependencies.UnionWith(instructionReads.Where(read => !written.Contains(read)));
                            written.UnionWith(instructionWrites);
                            continue;
                        }
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
                catch (SpatialInputInspectionLimitException)
                {
                    throw;
                }
                catch (SpatialEffectInspectionException)
                {
                    throw;
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

            bool IsPhysicalPredicate()
            {
                return (predicateTaint & (SpatialTaint.Coordinates | SpatialTaint.Triangle)) != 0 && (predicateTaint & SpatialTaint.Unknown) == 0;
            }

            void InvalidateAliases()
            {
                foreach (FieldVariableRead address in taints.Keys.ToArray())
                {
                    taints[address] = new(SpatialTaint.Unknown, 0, 1);
                }
            }

            void ProjectEarlierOwners()
            {
                prefixSlice = true;
                for (int owner = sourceCursor?.Entity ?? 0; owner < entity; owner++)
                {
                    projectionOwner = owner;
                    int completed = sourceCursor?.Entity == owner ? sourceCursor.HandlersCompleted : 0;
                    for (int handler = completed; handler < 8; handler++)
                    {
                        int pc = detached.entities[owner].Pc;
                        byte opcode = program.Read(pc);
                        instructionReads.Clear();
                        instructionWrites.Clear();
                        instructionTaint = SpatialTaint.None;
                        queryKind = null;
                        queryWrites = 0;
                        try
                        {
                            if (opcode is >= 0x30 and <= 0x32)
                            {
                                FieldScriptInputDemand demand = detached.ReadInputDemand(owner, pc, opcode);
                                bool matched = AreSpatialInputGuardsSatisfied([new(demand, true)], detached.Input);
                                inputGuards.Add(new(demand, matched));
                            }
                            if (opcode is >= 0x14 and <= 0x19)
                            {
                                _ = detached.ReadConditional(owner, opcode);
                                if ((instructionTaint & SpatialTaint.Unknown) != 0)
                                {
                                    InvalidateAliases();
                                    break;
                                }
                            }
                            bool supported = IsSpatialInspectionOpcode(opcode) || opcode is 0 or 0x24 or 0x5F or >= 0x30 and <= 0x32;
                            InstructionInspection prior = detached.inspection!;
                            if (!supported)
                            {
                                detached.inspection = new() { ActorReply = prior.ActorReply };
                            }
                            bool yielded = detached.Execute(owner);
                            FieldCommand[] commands = detached.inspection!.Commands.ToArray();
                            detached.inspection = prior;
                            dependencies.UnionWith(instructionReads.Where(read => !written.Contains(read)));
                            written.UnionWith(instructionWrites);
                            if (!supported)
                            {
                                if (owner == controlledEntity && commands.Any(command => command is FieldActorPlacement or FieldActorMove or FieldActorJump or FieldActorLadder) || commands.Any(command => command is FieldPartyJoin or FieldPartySplit))
                                {
                                    physicalSampleValid = false;
                                }
                                InvalidateAliases();
                                break;
                            }
                            if (yielded)
                            {
                                break;
                            }
                        }
                        catch (SpatialInspectionQueryException)
                        {
                            throw;
                        }
                        catch (Exception error) when (error is InputInspectionBoundaryException or FieldExecutionException or InvalidDataException or NotSupportedException or ArgumentException or IndexOutOfRangeException)
                        {
                            InvalidateAliases();
                            break;
                        }
                    }
                }
            }

            void AddDirectEffect(int pc)
            {
                FieldScriptAnalysis body = detached.AnalyzeScriptAtProgramCounter(entity, pc, instructionBudget: 8192, exitBoundaries: physicalPredicates, acquiredControls: acquiredControls);
                if (!body.Complete)
                {
                    throw new SpatialEffectInspectionException(body.Reason ?? "The native autonomous effect body cannot be inspected completely.");
                }
                if (!body.MayChangeNavigation)
                {
                    return;
                }
                if (body.ExitProgramCounters.Length == 0)
                {
                    throw new SpatialEffectInspectionException("The native autonomous navigation effect has no proved body exit.");
                }
                InstructionInspection prior = detached.inspection!;
                detached.inspection = new() { ActorReply = prior.ActorReply };
                detached.Execute(entity);
                FieldCommand command = detached.inspection.Commands.Single();
                detached.inspection = prior;
                foreach (FieldVariableRead read in body.Reads)
                {
                    dependencies.UnionWith(detached.Variables.SpatialSourceBytes(read).Where(value => !written.Contains(value)));
                }
                FieldSpatialEffectOwner effect = new(entity, source.Entities[entity].Priority, pc, command, body.ExitProgramCounters);
                RecordDemand(pc, entity, -1, body.DestinationFields, body.ChangesStory, body.HostReads, true, directEffect: effect);
            }

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

            void RecordDemand(int pc, int target, int script, int[] destinations, bool changesStory, FieldHostRead[] hostReads, bool mayChangeNavigation = false, int requestMode = 0, int requestPriority = 0, FieldSpatialEffectOwner? directEffect = null)
            {
                dependencies.UnionWith(instructionReads.Where(read => !written.Contains(read)));
                FieldVariableRead[] reads = dependencies.OrderBy(read => read.Bank).ThenBy(read => read.Operand).ToArray();
                string facts = $"{ReadScriptAnalysisAllocationFingerprint()}|{entity}:{pc}:{target}:{script}|{ReadScriptAnalysisFingerprint(target, Math.Max(0, script), reads)}|{(stochasticPredicate ? randomDomain?.Fingerprint : null)}";
                if (inputGuards.Count != 0)
                {
                    facts += "|input:" + string.Join(';', inputGuards.Select(guard => $"{guard.Demand.Entity}:{guard.Demand.Priority}:{guard.Demand.ProgramCounter}:{guard.Demand.Kind}:{guard.Demand.Mask}:{guard.Demand.Mapped}:{guard.Matched}").Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
                }
                if (directEffect is not null)
                {
                    facts += $"|effect:{directEffect.Entity}:{directEffect.Priority}:{directEffect.ProgramCounter}:{JsonSerializer.Serialize(directEffect.Command)}:{string.Join(',', directEffect.ExitProgramCounters)}";
                }
                string fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(facts)));
                demands.Add(new(entity, pc, target, script, sample!.Id, destinations, changesStory, fingerprint, reads, hostReads) { TriangleOnly = predicateTaint == SpatialTaint.Triangle, Stochastic = stochasticPredicate, MayChangeNavigation = mayChangeNavigation, RequestMode = requestMode, RequestPriority = requestPriority, SourcePriority = source.Entities[entity].Priority, InputGuards = inputGuards.ToArray(), DirectEffectOwner = directEffect });
            }
        }
    }

    /// <summary>Evaluates native ANY-bit input predicates and their selected branch outcomes against an exact input state.</summary>
    public static bool AreSpatialInputGuardsSatisfied(IReadOnlyList<FieldSpatialInputGuard> guards, FieldInputState input)
    {
        return guards.All(guard =>
        {
            FieldScriptInputDemand demand = guard.Demand;
            ushort keys = demand.Kind switch
            {
                FieldScriptInputKind.Held => demand.Mapped ? input.Held : input.RawHeld,
                FieldScriptInputKind.Pressed => demand.Mapped ? input.Pressed : input.RawPressed,
                FieldScriptInputKind.Released => demand.Mapped ? input.Released : input.RawReleased,
                _ => throw new NotSupportedException("The native spatial input guard has an unsupported input selector.")
            };
            return ((keys & demand.Mask) != 0) == guard.Matched;
        });
    }

    /// <summary>Finds coherent previous/current native button states without advancing input. Optional shared buttons constrain mapped and raw streams to the same physical buttons.</summary>
    public static bool TryFindSpatialInputWitness(IReadOnlyList<FieldSpatialInputGuard> guards, out FieldInputState input, bool sharedPhysicalButtons = false, FieldInputState? previousInput = null)
    {
        byte[] states = Enumerable.Repeat((byte)15, sharedPhysicalButtons ? 16 : 32).ToArray();
        int Index(FieldScriptInputDemand demand, int bit) => bit + (sharedPhysicalButtons || demand.Mapped ? 0 : 16);
        static int Matches(FieldScriptInputKind kind) => kind switch
        {
            FieldScriptInputKind.Held => 10,
            FieldScriptInputKind.Pressed => 2,
            FieldScriptInputKind.Released => 4,
            _ => throw new NotSupportedException("The native spatial input guard has an unsupported input selector.")
        };
        if (previousInput is not null)
        {
            for (int bit = 0; bit < 16; bit++)
            {
                int mask = 1 << bit;
                states[bit] &= (byte)((previousInput.Held & mask) != 0 ? 12 : 3);
                int rawIndex = sharedPhysicalButtons ? bit : bit + 16;
                states[rawIndex] &= (byte)((previousInput.RawHeld & mask) != 0 ? 12 : 3);
            }
        }
        foreach (FieldSpatialInputGuard guard in guards.Where(guard => !guard.Matched))
        {
            int matches = Matches(guard.Demand.Kind);
            for (int bit = 0; bit < 16; bit++)
            {
                if ((guard.Demand.Mask & (1 << bit)) != 0)
                {
                    states[Index(guard.Demand, bit)] &= (byte)(15 & ~matches);
                }
            }
        }
        input = new();
        if (states.Any(value => value == 0))
        {
            return false;
        }
        FieldSpatialInputGuard[] positive = guards.Where(guard => guard.Matched).ToArray();
        int expanded = 0;
        if (!Solve(states))
        {
            return false;
        }
        ushort held = 0;
        ushort previous = 0;
        ushort rawHeld = 0;
        ushort rawPrevious = 0;
        for (int index = 0; index < states.Length; index++)
        {
            int state = 0;
            while ((states[index] & (1 << state)) == 0)
            {
                state++;
            }
            int bit = 1 << (index & 15);
            if (index < 16)
            {
                held |= (ushort)((state & 1) != 0 ? bit : 0);
                previous |= (ushort)((state & 2) != 0 ? bit : 0);
            }
            else
            {
                rawHeld |= (ushort)((state & 1) != 0 ? bit : 0);
                rawPrevious |= (ushort)((state & 2) != 0 ? bit : 0);
            }
        }
        if (sharedPhysicalButtons)
        {
            rawHeld = held;
            rawPrevious = previous;
        }
        input = new(held, (ushort)(held & ~previous), (ushort)(previous & ~held), rawHeld, (ushort)(rawHeld & ~rawPrevious), (ushort)(rawPrevious & ~rawHeld), PreviousHeld: previous, PreviousRawHeld: rawPrevious);
        return true;

        bool Solve(byte[] candidate)
        {
            if (++expanded > 4096)
            {
                throw new SpatialInputInspectionLimitException();
            }
            foreach (FieldSpatialInputGuard guard in positive)
            {
                int matches = Matches(guard.Demand.Kind);
                bool guaranteed = false;
                for (int bit = 0; bit < 16; bit++)
                {
                    if ((guard.Demand.Mask & (1 << bit)) != 0 && (candidate[Index(guard.Demand, bit)] & ~matches) == 0)
                    {
                        guaranteed = true;
                        break;
                    }
                }
                if (guaranteed)
                {
                    continue;
                }
                for (int bit = 0; bit < 16; bit++)
                {
                    if ((guard.Demand.Mask & (1 << bit)) == 0)
                    {
                        continue;
                    }
                    int index = Index(guard.Demand, bit);
                    int allowed = candidate[index] & matches;
                    for (int state = 0; state < 4; state++)
                    {
                        if ((allowed & (1 << state)) == 0)
                        {
                            continue;
                        }
                        byte[] branch = candidate.ToArray();
                        branch[index] = (byte)(1 << state);
                        if (Solve(branch))
                        {
                            branch.CopyTo(candidate, 0);
                            return true;
                        }
                    }
                }
                return false;
            }
            return true;
        }
    }

    private static bool HasSpatialInputWitness(IReadOnlyList<FieldSpatialInputGuard> guards)
    {
        return TryFindSpatialInputWitness(guards, out _);
    }

    private static bool IsSpatialInspectionOpcode(byte opcode)
    {
        return opcode is >= 0x10 and <= 0x19 or 0x75 or 0x76 or 0x77 or 0x78 or >= 0x80 and <= 0x89 or >= 0x8D and <= 0x98 or 0xB8 or 0xB9 or 0xC1;
    }
}
