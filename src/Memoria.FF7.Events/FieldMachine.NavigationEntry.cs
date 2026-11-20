namespace Memoria.FF7.Events;

public sealed record FieldNavigationEntryPoint(int Entity, int Script);
public sealed record FieldNavigationEntryProof(bool Complete, bool HasNoEffect, string? Reason, FieldVariableRead[] Reads)
{
    public FieldTransitionCommand? DirectTransition { get; init; }
    public string? DirectGuardFingerprint { get; init; }
}
public sealed record FieldNavigationControlState(bool PlayerLocked, bool MenuDisabled, int PlayerEntity, int PlayerAnimationStatus, IReadOnlyList<FieldNavigationEntryPoint> PossibleEntries);

public sealed partial class FieldMachine
{
    // Domains expand monotonically from native snapshot bytes; no bank or sampled host result is trusted as constant.
    private sealed class NavigationByteDomains
    {
        private readonly FieldVariables variables;
        private readonly Func<int, int, int, bool> compare;
        private readonly Dictionary<FieldVariableRead, (int Minimum, int Maximum)> bytes = [];
        public int Revision { get; private set; }

        public NavigationByteDomains(FieldVariables variables, IReadOnlyList<FieldVariableRead> externalWrites, Func<int, int, int, bool> compare, IReadOnlySet<FieldVariableRead>? knownBytes = null)
        {
            this.variables = variables;
            this.compare = compare;
            FieldBank[] banks = [FieldBank.Persistent0Byte, FieldBank.Persistent1Byte, FieldBank.Persistent2Byte, FieldBank.Persistent3Byte, FieldBank.Persistent4Byte, FieldBank.MapByte];
            foreach (FieldBank bank in banks)
            {
                for (int address = 0; address < 256; address++)
                {
                    byte value = variables.ReadByte(bank, (byte)address);
                    FieldVariableRead source = new(bank, (ushort)address, false);
                    bytes.Add(source, knownBytes is null || knownBytes.Contains(source) ? (value, value) : (0, 255));
                }
            }
            foreach (FieldVariableRead write in externalWrites.SelectMany(variables.SpatialSourceBytes))
            {
                Join(write, null);
            }
        }

        public bool IsSingleton(FieldVariableRead read) => variables.SpatialSourceBytes(read).All(source => bytes[source].Minimum == bytes[source].Maximum);

        public void Join(FieldVariableRead source, byte? value)
        {
            var current = bytes[source];
            var next = (Minimum: value is byte known ? Math.Min(current.Minimum, known) : 0, Maximum: value is byte knownMaximum ? Math.Max(current.Maximum, knownMaximum) : 255);
            if (next != current)
            {
                bytes[source] = next;
                Revision++;
            }
        }

        private (int Minimum, int Maximum) Range(FieldVariableRead read, bool unsigned)
        {
            FieldVariableRead[] sources = variables.SpatialSourceBytes(read).ToArray();
            if (sources.Length == 0)
            {
                int value = read.Word ? unchecked((short)read.Operand) : (byte)read.Operand;
                if (unsigned)
                {
                    value = (ushort)value;
                }
                return (value, value);
            }
            var low = bytes[sources[0]];
            int minimum = low.Minimum;
            int maximum = low.Maximum;
            if (sources.Length == 2)
            {
                var high = bytes[sources[1]];
                minimum += high.Minimum << 8;
                maximum += high.Maximum << 8;
                if (!unsigned)
                {
                    if (minimum >= 32768)
                    {
                        return (minimum - 65536, maximum - 65536);
                    }
                    if (maximum >= 32768)
                    {
                        return (short.MinValue, short.MaxValue);
                    }
                }
            }
            return (minimum, maximum);
        }

        public bool? Compare(FieldVariableRead leftRead, FieldVariableRead rightRead, int operation, bool unsigned)
        {
            var left = Range(leftRead, unsigned);
            var right = Range(rightRead, unsigned);
            if (left.Minimum == left.Maximum && right.Minimum == right.Maximum)
            {
                return compare(left.Minimum, right.Minimum, operation);
            }
            return operation switch
            {
                0 when left.Maximum < right.Minimum || right.Maximum < left.Minimum => false,
                1 when left.Maximum < right.Minimum || right.Maximum < left.Minimum => true,
                2 when left.Minimum > right.Maximum => true,
                2 when left.Maximum <= right.Minimum => false,
                3 when left.Maximum < right.Minimum => true,
                3 when left.Minimum >= right.Maximum => false,
                4 when left.Minimum >= right.Maximum => true,
                4 when left.Maximum < right.Minimum => false,
                5 when left.Maximum <= right.Minimum => true,
                5 when left.Minimum > right.Maximum => false,
                _ => null
            };
        }
    }

    /// <summary>Proves an entry has no observable effect and returns within one native handler quantum. The caller must supply every possible external variable publication and competing external script entry before dispatch; this method includes all captured owners, saved continuations and native child requests.</summary>
    public FieldNavigationEntryProof ProveNavigationEntryHasNoEffect(int entity, int script, FieldInputState projectedInput, IReadOnlyList<FieldVariableRead> externalWrites, IReadOnlyList<FieldNavigationEntryPoint> possibleExternalEntries, int instructionBudget = 8192, FieldNavigationControlState? controls = null, bool allInputs = false)
    {
        return ProveNavigationEntry(entity, script, projectedInput, externalWrites, possibleExternalEntries, instructionBudget, controls, allInputs, false);
    }

    public FieldNavigationEntryProof ProveNavigationEntryDirectTransition(int entity, int script, FieldInputState projectedInput, IReadOnlyList<FieldVariableRead> externalWrites, IReadOnlyList<FieldNavigationEntryPoint> possibleExternalEntries, int instructionBudget = 8192)
    {
        return ProveNavigationEntry(entity, script, projectedInput, externalWrites, possibleExternalEntries, instructionBudget, null, false, true);
    }

    private FieldNavigationEntryProof ProveNavigationEntry(int entity, int script, FieldInputState projectedInput, IReadOnlyList<FieldVariableRead> externalWrites, IReadOnlyList<FieldNavigationEntryPoint> possibleExternalEntries, int instructionBudget, FieldNavigationControlState? controls, bool allInputs, bool directTransition)
    {
        ArgumentNullException.ThrowIfNull(projectedInput);
        ArgumentNullException.ThrowIfNull(externalWrites);
        ArgumentNullException.ThrowIfNull(possibleExternalEntries);
        if ((uint)entity >= entities.Length || script is < 0 or > 31 || instructionBudget is < 1 or > 65536)
        {
            throw new ArgumentOutOfRangeException(nameof(entity));
        }
        HashSet<FieldVariableRead> reads = [];
        FieldNavigationEntryProof Reject(string reason) => new(false, false, reason, reads.OrderBy(read => read.Bank).ThenBy(read => read.Operand).ThenBy(read => read.Word).ToArray());
        if (!Initialized || CycleCursor is not null)
        {
            return Reject("Navigation entry proof requires an initialized native cycle boundary.");
        }
        try
        {
            FieldMachine detached = Restore(program, Capture(), new InspectionHost());
            detached.inspection = new() { StopAtHost = true };
            detached.entities[entity].Pc = program.Entry(entity, script);
            detached.entities[entity].Priority = 1;
            detached.entities[entity].Wait = 0;
            detached.Input = projectedInput;
            bool publishesControls = false;
            FieldTransitionCommand? transition = null;
            List<object> transitionGuards = [];
            HashSet<FieldVariableRead> queryOutputs = [];
            string? ProveReturn(int pc, int handlers, HashSet<int> ancestors, HashSet<FieldVariableRead> unknown)
            {
                if (handlers >= 8)
                {
                    return "The entry return exceeds one native handler quantum.";
                }
                if (!ancestors.Add(pc))
                {
                    return "The entry does not expose a finite no-effect return path.";
                }
                detached.entities[entity].Pc = pc;
                byte opcode = program.Read(pc);
                if (directTransition)
                {
                    if (opcode == 0x60)
                    {
                        detached.inspection = new();
                        detached.Execute(entity);
                        transition = detached.inspection.Commands.SingleOrDefault() as FieldTransitionCommand;
                        return transition is { Map: > 64 and < 32767 } ? null : "The entry has no supported literal field transition.";
                    }
                    if (opcode is not (0x10 or 0x11 or >= 0x14 and <= 0x19))
                    {
                        return "A direct field transition reaches a return, effect, input or unsupported instruction.";
                    }
                }
                if (opcode == 0)
                {
                    return null;
                }
                if (allInputs && opcode is >= 0x30 and <= 0x32)
                {
                    // Discovery must not confuse today's unpressed button with a terminal entry.
                    _ = program.Read(pc + 1);
                    _ = program.Read(pc + 2);
                    int unmatched = pc + 3 + program.Read(pc + 3);
                    string? matchedFailure = ProveReturn(pc + 4, handlers + 1, new(ancestors), new(unknown));
                    return matchedFailure is not null ? matchedFailure : ProveReturn(unmatched, handlers + 1, new(ancestors), new(unknown));
                }
                if (opcode == 0xCB)
                {
                    // Prove both party-membership outcomes without borrowing mutable party facts.
                    _ = program.Read(pc + 1);
                    int distance = program.Read(pc + 2);
                    detached.Advance(entity, 3);
                    int present = detached.entities[entity].Pc;
                    detached.entities[entity].Pc = pc;
                    detached.Advance(entity, distance + 2);
                    int absent = detached.entities[entity].Pc;
                    string? presentFailure = ProveReturn(present, handlers + 1, new(ancestors), new(unknown));
                    return presentFailure is not null ? presentFailure : ProveReturn(absent, handlers + 1, new(ancestors), new(unknown));
                }
                if (opcode is 0x73 or 0xB7)
                {
                    // Use the native query decoder, but do not sample a physical heading as invariant.
                    HashSet<FieldVariableRead> written = detached.Variables.SpatialSourceBytes(new((FieldBank)(program.Read(pc + 1) & 15), program.Read(pc + 3), false)).ToHashSet();
                    detached.Variables.WriteObserved = write => written.UnionWith(detached.Variables.SpatialSourceBytes(write));
                    detached.inspection = new() { StopAtHost = true, ActorReply = _ => new(true) };
                    bool yielded;
                    try
                    {
                        yielded = detached.Execute(entity);
                    }
                    finally
                    {
                        detached.Variables.WriteObserved = null;
                        detached.inspection = new() { StopAtHost = true };
                    }
                    if (yielded || written.Any(write => write.Bank != FieldBank.MapByte))
                    {
                        return "The query does not publish only field-local scratch bytes synchronously.";
                    }
                    queryOutputs.UnionWith(written);
                    unknown.UnionWith(written);
                    return ProveReturn(detached.entities[entity].Pc, handlers + 1, ancestors, unknown);
                }
                if (opcode is 0x33 or 0x4A)
                {
                    bool value = program.Read(pc + 1) != 0;
                    if (controls is null || (opcode == 0x33 ? value != controls.PlayerLocked || !value && controls.PlayerAnimationStatus != 0 : value != controls.MenuDisabled))
                    {
                        return "The entry changes a captured native control publication.";
                    }
                    publishesControls = true;
                    detached.Advance(entity, 2);
                    return ProveReturn(detached.entities[entity].Pc, handlers + 1, ancestors, unknown);
                }
                if (opcode is >= 0x14 and <= 0x19)
                {
                    FieldConditional condition = detached.ReadConditional(entity, opcode);
                    if (directTransition)
                    {
                        transitionGuards.Add(new { Opcode = opcode, condition.LeftSource, condition.RightSource, condition.Left, condition.Right, condition.Operation });
                    }
                    bool IsUnknown(FieldVariableRead read) => detached.Variables.SpatialSourceBytes(read).Any(source => unknown.Contains(source) || queryOutputs.Contains(source));
                    if (IsUnknown(condition.LeftSource) || IsUnknown(condition.RightSource))
                    {
                        bool word = opcode >= 0x16;
                        int branchOffset = word ? 7 : 5;
                        bool wide = (opcode & 1) != 0;
                        int distance = wide ? unchecked((short)(program.Read(pc + branchOffset) | program.Read(pc + branchOffset + 1) << 8)) : program.Read(pc + branchOffset);
                        int matched = pc + branchOffset + (wide ? 2 : 1);
                        int unmatched = pc + branchOffset + distance;
                        string? matchedFailure = ProveReturn(matched, handlers + 1, new(ancestors), new(unknown));
                        return matchedFailure is not null ? matchedFailure : ProveReturn(unmatched, handlers + 1, new(ancestors), new(unknown));
                    }
                    if (condition.LeftSource.Bank != FieldBank.Immediate)
                    {
                        reads.Add(condition.LeftSource);
                    }
                    if (condition.RightSource.Bank != FieldBank.Immediate)
                    {
                        reads.Add(condition.RightSource);
                    }
                }
                else if (opcode is not (0x10 or 0x11 or 0x30 or 0x31 or 0x32))
                {
                    return "The entry reaches an effect or an unsupported read-only instruction.";
                }
                if (detached.Execute(entity))
                {
                    return "The entry yields before its read-only return.";
                }
                return ProveReturn(detached.entities[entity].Pc, handlers + 1, ancestors, unknown);
            }
            string? returnFailure = ProveReturn(program.Entry(entity, script), 0, [], []);
            if (returnFailure is not null)
            {
                return Reject(returnFailure);
            }
            HashSet<FieldVariableRead> immutable = reads.SelectMany(Variables.SpatialSourceBytes).ToHashSet();
            if (externalWrites.SelectMany(Variables.SpatialSourceBytes).Any(immutable.Contains))
            {
                return Reject("An external publication can overwrite an entry predicate byte.");
            }
            HashSet<(int Entity, int Pc)> roots = [];
            roots.Add((entity, program.Entry(entity, script)));
            for (int owner = 0; owner < entities.Length; owner++)
            {
                roots.Add((owner, entities[owner].Pc));
                foreach (int continuation in entities[owner].Saved.Where(pc => pc != 0))
                {
                    roots.Add((owner, continuation));
                }
                // Native RET skips zero saved PCs only below priority seven.
                if (entities[owner].Priority < 7 && entities[owner].Saved[7] == 0)
                {
                    roots.Add((owner, 0));
                }
            }
            foreach (FieldHostRequest pending in pendingRequests.Values)
            {
                roots.Add((pending.Entity, pending.ProgramCounter));
            }
            HashSet<(int Entity, int Pc)> currentRoots = roots.ToHashSet();
            foreach (FieldNavigationEntryPoint entry in possibleExternalEntries)
            {
                if ((uint)entry.Entity >= entities.Length || entry.Script is < 0 or > 31)
                {
                    return Reject("An external script entry is outside the native allocation.");
                }
                roots.Add((entry.Entity, program.Entry(entry.Entity, entry.Script)));
            }
            NavigationByteDomains domains = new(Variables, externalWrites, Compare);
            int remaining = instructionBudget;
            while (true)
            {
                int revision = domains.Revision;
                NavigationInvariantInspection invariant = new(domains, possibleExternalEntries.ToHashSet());
                foreach (var root in roots)
                {
                    if (remaining < 1)
                    {
                        return Reject("The bounded producer closure exhausted its instruction budget.");
                    }
                    int before = invariant.Instructions;
                    FieldScriptAnalysis producer = AnalyzeScriptCore(root.Entity, 0, root.Pc, remaining, null, 0, invariant);
                    if (!producer.Complete)
                    {
                        return Reject(producer.Reason ?? "A producer has an incomplete native write contract.");
                    }
                    if (queryOutputs.Count != 0)
                    {
                        FieldScriptEntryDependencies consumers = AnalyzeEntryDependenciesCore(root.Entity, 0, root.Pc, producer, remaining);
                        if (!consumers.Complete || consumers.Reads.SelectMany(Variables.SpatialSourceBytes).Any(queryOutputs.Contains))
                        {
                            return Reject("A current, suspended, pending or external native consumer can observe the query scratch publication.");
                        }
                    }
                    if (invariant.Writes.Overlaps(immutable))
                    {
                        FieldVariableRead written = immutable.First(invariant.Writes.Contains);
                        var origin = invariant.WriteOrigins[written];
                        return Reject($"{origin.Entity}:{origin.ProgramCounter:X4}: A native producer can overwrite entry predicate byte {written.Bank}/{written.Operand}.");
                    }
                    remaining -= invariant.Instructions - before;
                }
                if (domains.Revision == revision)
                {
                    reads.UnionWith(invariant.AssumedReads);
                    break;
                }
            }
            if (publishesControls)
            {
                if (entities[entity].Priority <= 1)
                {
                    return Reject("The native control entry cannot preempt its current owner.");
                }
                foreach (FieldNavigationEntryPoint entry in controls!.PossibleEntries)
                {
                    if (!possibleExternalEntries.Contains(entry))
                    {
                        return Reject("A scoped control entry has no external allocation contract.");
                    }
                    currentRoots.Add((entry.Entity, program.Entry(entry.Entity, entry.Script)));
                }
                NavigationInvariantInspection controlInvariant = new(domains, possibleExternalEntries.ToHashSet());
                foreach (var root in currentRoots)
                {
                    int before = controlInvariant.Instructions;
                    if (remaining < 1)
                    {
                        return Reject("The bounded control producer closure exhausted its instruction budget.");
                    }
                    FieldScriptAnalysis producer = AnalyzeScriptCore(root.Entity, 0, root.Pc, remaining, null, 0, controlInvariant);
                    if (!producer.Complete)
                    {
                        return Reject(producer.Reason ?? "A control producer has an incomplete native contract.");
                    }
                    remaining -= controlInvariant.Instructions - before;
                }
                foreach (var publication in controlInvariant.Commands)
                {
                    if (publication.Command is FieldMenuCommand or FieldBattleCommand or FieldMinigameCommand)
                    {
                        return Reject($"Native producer {publication.Entity}/{publication.Command.GetType().Name} has an opaque mode-return publication contract.");
                    }
                    if (publication.Command is FieldControlCommand control && (control.Property == FieldControlKind.PlayerLocked && (control.Value != 0) != controls.PlayerLocked || control.Property == FieldControlKind.MenuDisabled && (control.Value != 0) != controls.MenuDisabled))
                    {
                        return Reject("A native producer can change an idempotent entry control.");
                    }
                    if (publication.Command is FieldLineCommand or FieldActorBind or FieldPartyCharacter or FieldPartyCommand or FieldPartyProjection or FieldPartyJoin or FieldPartySplit or FieldActorProperty or FieldActorMove or FieldActorPlacement or FieldActorLadder or FieldActorJump || publication.Entity == controls.PlayerEntity && publication.Command is FieldAnimation)
                    {
                        return Reject($"Native producer {publication.Entity}/{publication.Command.GetType().Name} can change the scoped control-entry geometry or player animation.");
                    }
                }
            }
            FieldVariableRead[] footprint = reads.OrderBy(read => read.Bank).ThenBy(read => read.Operand).ThenBy(read => read.Word).ToArray();
            string? guardFingerprint = null;
            if (directTransition)
            {
                if (transition is null)
                {
                    return Reject("The entry did not publish a direct field transition.");
                }
                byte[] guard = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new
                {
                    Contract = "native-direct-field-transition:1",
                    program.Identity,
                    Transition = transition,
                    Guards = transitionGuards,
                    Inputs = footprint.Select(read => new { read.Bank, read.Operand, read.Word, Value = read.Word ? Variables.ReadWord(read.Bank, read.Operand) : Variables.ReadByte(read.Bank, unchecked((byte)read.Operand)) }).ToArray()
                });
                guardFingerprint = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(guard));
            }
            return new(true, !directTransition, null, footprint) { DirectTransition = transition, DirectGuardFingerprint = guardFingerprint };
        }
        catch (Exception error) when (error is InputInspectionBoundaryException or FieldExecutionException or InvalidDataException or NotSupportedException or ArgumentException or IndexOutOfRangeException)
        {
            return Reject(error.Message);
        }
    }
}
