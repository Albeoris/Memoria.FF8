namespace Memoria.FF7.Events;

public sealed record FieldNavigationEntryPoint(int Entity, int Script);
public sealed record FieldNavigationEntryProof(bool Complete, bool HasNoEffect, string? Reason, FieldVariableRead[] Reads);

public sealed partial class FieldMachine
{
    /// <summary>Proves a read-only entry returns within one native handler quantum. The caller must supply every possible external variable publication and competing external script entry before dispatch; this method includes all captured owners, saved continuations and native child requests.</summary>
    public FieldNavigationEntryProof ProveNavigationEntryHasNoEffect(int entity, int script, FieldInputState projectedInput, IReadOnlyList<FieldVariableRead> externalWrites, IReadOnlyList<FieldNavigationEntryPoint> possibleExternalEntries, int instructionBudget = 8192)
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
            HashSet<int> visited = [];
            bool returned = false;
            for (int handler = 0; handler < 8; handler++)
            {
                int pc = detached.entities[entity].Pc;
                if (!visited.Add(pc))
                {
                    return Reject("The entry does not expose a finite read-only return path.");
                }
                byte opcode = program.Read(pc);
                if (opcode == 0)
                {
                    returned = true;
                    break;
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
                    return Reject("The entry reaches an effect or an unsupported read-only instruction.");
                }
                if (detached.Execute(entity))
                {
                    return Reject("The entry yields before its read-only return.");
                }
            }
            if (!returned)
            {
                return Reject("The entry return exceeds one native handler quantum.");
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
            foreach (FieldNavigationEntryPoint entry in possibleExternalEntries)
            {
                if ((uint)entry.Entity >= entities.Length || entry.Script is < 0 or > 31)
                {
                    return Reject("An external script entry is outside the native allocation.");
                }
                roots.Add((entry.Entity, program.Entry(entry.Entity, entry.Script)));
            }
            // Candidate bytes are hypothetical snapshot constants, not trusted bank properties.
            // Removing every reachable write and repeating the full closure finds an inductive fixed point.
            FieldBank[] byteBanks = [FieldBank.Persistent0Byte, FieldBank.Persistent1Byte, FieldBank.Persistent2Byte, FieldBank.Persistent3Byte, FieldBank.Persistent4Byte, FieldBank.MapByte];
            HashSet<FieldVariableRead> candidates = byteBanks.SelectMany(bank => Enumerable.Range(0, 256).Select(address => new FieldVariableRead(bank, (ushort)address, false))).ToHashSet();
            candidates.ExceptWith(externalWrites.SelectMany(Variables.SpatialSourceBytes));
            int remaining = instructionBudget;
            while (true)
            {
                NavigationInvariantInspection invariant = new(candidates, possibleExternalEntries.ToHashSet());
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
                int count = candidates.Count;
                candidates.ExceptWith(invariant.Writes);
                if (candidates.Count == count)
                {
                    reads.UnionWith(invariant.AssumedReads);
                    break;
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
