namespace Memoria.FF7.Events;

public sealed record FieldNavigationEntryPoint(int Entity, int Script);
public sealed record FieldNavigationEntryProof(bool Complete, bool HasNoEffect, string? Reason, FieldVariableRead[] Reads);
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

    /// <summary>Proves a read-only entry returns within one native handler quantum. The caller must supply every possible external variable publication and competing external script entry before dispatch; this method includes all captured owners, saved continuations and native child requests.</summary>
    public FieldNavigationEntryProof ProveNavigationEntryHasNoEffect(int entity, int script, FieldInputState projectedInput, IReadOnlyList<FieldVariableRead> externalWrites, IReadOnlyList<FieldNavigationEntryPoint> possibleExternalEntries, int instructionBudget = 8192, FieldNavigationControlState? controls = null)
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
            string? ProveReturn(int pc, int handlers, HashSet<int> ancestors)
            {
                if (handlers >= 8)
                {
                    return "The entry return exceeds one native handler quantum.";
                }
                if (!ancestors.Add(pc))
                {
                    return "The entry does not expose a finite read-only return path.";
                }
                detached.entities[entity].Pc = pc;
                byte opcode = program.Read(pc);
                if (opcode == 0)
                {
                    return null;
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
                    string? presentFailure = ProveReturn(present, handlers + 1, new(ancestors));
                    return presentFailure is not null ? presentFailure : ProveReturn(absent, handlers + 1, new(ancestors));
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
                    return ProveReturn(detached.entities[entity].Pc, handlers + 1, ancestors);
                }
                if (opcode is >= 0x14 and <= 0x19)
                {
                    FieldConditional condition = detached.ReadConditional(entity, opcode);
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
                return ProveReturn(detached.entities[entity].Pc, handlers + 1, ancestors);
            }
            string? returnFailure = ProveReturn(program.Entry(entity, script), 0, []);
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
            return new(true, true, null, reads.OrderBy(read => read.Bank).ThenBy(read => read.Operand).ThenBy(read => read.Word).ToArray());
        }
        catch (Exception error) when (error is InputInspectionBoundaryException or FieldExecutionException or InvalidDataException or NotSupportedException or ArgumentException or IndexOutOfRangeException)
        {
            return Reject(error.Message);
        }
    }
}
