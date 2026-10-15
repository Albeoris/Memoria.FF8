namespace Memoria.FF8.Runtime;

public sealed partial class ReplayJournal
{
    public IReadOnlyList<ReplayCompletedVisit> CompletedVisits => visits.Select(visit => visit with { EntryFieldLoad = visit.EntryFieldLoad is null ? null : Snapshot(visit.EntryFieldLoad), Intents = visit.Intents.Select(Snapshot).ToArray() }).ToArray();

    public IReadOnlyList<ReplayJournalEntry> ActiveEntries
    {
        get
        {
            List<ReplayJournalEntry> active = [];
            for (Node? node = tail; node is not null && !ReferenceEquals(node, completedVisits); node = node.Previous)
            {
                active.Add(Snapshot(node.Entry));
            }
            active.Reverse();
            return active;
        }
    }

    /// <summary>Projects an immutable v1 capsule without modifying its literal inputs or claiming missing admission metadata.</summary>
    public ReplayJournal GetSemanticProjection()
    {
        if (Version == CurrentVersion)
        {
            return this;
        }
        ReplayJournal projected = StartNewGame(SessionId);
        foreach (ReplayJournalEntry entry in Entries)
        {
            if (projected.tail is not null && (entry.Kind == ReplayEntryKind.FieldLoad || entry.Scene == SceneKind.WorldMap && projected.tail.Entry.Scene != SceneKind.WorldMap))
            {
                projected = projected.CompleteVisit(entry, true);
            }
            projected = projected.AppendCore(entry);
        }
        return IsComplete ? projected : projected.MarkUnavailable(UnavailableReason!);
    }

    private static IEnumerable<ReplayJournalEntry> VisitEntries(ReplayCompletedVisit visit)
    {
        if (visit.EntryFieldLoad is not null)
        {
            yield return visit.EntryFieldLoad;
        }
        foreach (ReplayJournalEntry entry in visit.Intents)
        {
            yield return entry;
        }
    }

    private ReplayJournalEntry[] ProjectVisit(ReplayJournalEntry[] entries, bool inferred)
    {
        List<ReplayJournalEntry> intents = [];
        const int traversalReleaseNeeded = -1;
        int representedHeld = 0;
        bool foundHeld = false;
        for (int visitIndex = visits.Length - 1; visitIndex >= 0 && !foundHeld; visitIndex--)
        {
            foreach (ReplayJournalEntry previous in visits[visitIndex].Intents.Reverse())
            {
                if (previous.Kind == ReplayEntryKind.InputSample || previous.Kind == ReplayEntryKind.Action && previous.Code == 8)
                {
                    representedHeld = previous.Value;
                    foundHeld = true;
                    break;
                }
                if (previous.Kind == ReplayEntryKind.NativeTraversal)
                {
                    representedHeld = traversalReleaseNeeded;
                    foundHeld = true;
                    break;
                }
            }
        }
        ReplayJournalEntry? allocation = entries.FirstOrDefault(entry => entry.Kind == ReplayEntryKind.FieldLoad);
        for (int inputIndex = 0; inputIndex < entries.Length; inputIndex++)
        {
            ReplayJournalEntry entry = entries[inputIndex];
            bool cadence = entry.Kind is ReplayEntryKind.FieldLoad or ReplayEntryKind.WorldAllocation or ReplayEntryKind.InputPulse or ReplayEntryKind.Step or ReplayEntryKind.Update || entry.Kind is ReplayEntryKind.Action or ReplayEntryKind.WorldAction && entry.Code is 4 or 5;
            bool representedInteraction = entry.Kind == ReplayEntryKind.Action && entry.Code is 0 or 3 && entry.SemanticAdmissions.Any(admission => admission.Kind == ReplaySemanticTriggerKind.ActorInteraction);
            if (!cadence && !representedInteraction && RetainExplicitInput(entry, entries, inputIndex, inferred))
            {
                if (entry.Kind != ReplayEntryKind.InputSample || RetainInputSample(entry, ref representedHeld))
                {
                    intents.Add(WithoutPhysicalDiagnostics(entry));
                    if (entry.Kind == ReplayEntryKind.Action && entry.Code == 8)
                    {
                        representedHeld = entry.Value;
                    }
                    else if (entry.Kind == ReplayEntryKind.NativeTraversal)
                    {
                        representedHeld = traversalReleaseNeeded;
                    }
                }
            }
            List<(int Sequence, int Order, ReplayJournalEntry Intent)> outcomes = [];
            foreach (ReplayWorldEffectCause cause in entry.WorldEffects)
            {
                outcomes.Add((outcomes.Count, outcomes.Count, new ReplayJournalEntry(ReplayEntryKind.PersistentEffect, entry.Scene, entry.FieldId, entry.Frame) { Code = (int)ReplayPersistentEffectKind.WorldCallbackEffects, EffectArguments = cause.ToArguments() }));
            }
            bool consumedInputAdmission = false;
            foreach (ReplaySemanticTrigger admission in entry.SemanticAdmissions)
            {
                if (!consumedInputAdmission && entry.SemanticTrigger is { } input && input with { Sequence = -1, ExecutableHash = input.ExecutableHash ?? admission.ExecutableHash } == admission with { Sequence = -1 })
                {
                    consumedInputAdmission = true;
                    continue;
                }
                outcomes.Add((admission.Sequence, outcomes.Count, new ReplayJournalEntry(ReplayEntryKind.SemanticTrigger, entry.Scene, entry.FieldId, entry.Frame) { SemanticTrigger = admission, NativeSourceHash = admission.ProgramHash }));
            }
            foreach (ReplayReceipt receipt in entry.Receipts)
            {
                if (receipt.Kind == ReplayReceiptKind.PersistentEffect)
                {
                    if (entry.Kind == ReplayEntryKind.PersistentEffect && (receipt.Code == entry.Code || receipt.Code is (int)ReplayPersistentEffectKind.SalaryProgress or (int)ReplayPersistentEffectKind.SalaryPayment && entry.Code is (int)ReplayPersistentEffectKind.SalaryProgress or (int)ReplayPersistentEffectKind.SalaryPayment) && entry.EffectArguments is not null && receipt.Arguments is not null && entry.EffectArguments.SequenceEqual(receipt.Arguments))
                    {
                        continue;
                    }
                    outcomes.Add((receipt.Sequence, outcomes.Count, new ReplayJournalEntry(ReplayEntryKind.PersistentEffect, entry.Scene, receipt.FieldId, receipt.Frame) { Code = receipt.Code, Frames = receipt.Arguments is { Length: >= 2 } arguments && arguments[1] is > 0 and <= 30000 ? arguments[1] : throw new InvalidDataException("Persistent effect receipt lacks its explicit causal callback count."), EffectArguments = receipt.Arguments is null ? null : (int[])receipt.Arguments.Clone(), Receipts = [receipt] }));
                    continue;
                }
                if (receipt.Kind is not (ReplayReceiptKind.InputSample or ReplayReceiptKind.Command or ReplayReceiptKind.NativeTraversal))
                {
                    continue;
                }
                string? hash = allocation?.NativeSourceHash;
                if (hash is null || receipt.FieldId != allocation!.FieldId)
                {
                    continue;
                }
                ReplayScriptGoal goal = new(receipt.FieldId, hash, allocation?.Text, receipt.ObjectId, receipt.ScriptId, receipt.Operation, receipt.Code, inferred);
                if (receipt.Kind == ReplayReceiptKind.NativeTraversal)
                {
                    if (receipt.Arguments is not { Length: 3 } traversalArguments || traversalArguments[0] is not (3 or 4) || traversalArguments[1] is not (-1 or 1) || traversalArguments[2] is < 0 or > ushort.MaxValue)
                    {
                        throw new InvalidDataException("Native traversal completion lost its actual mode/direction provenance.");
                    }
                    ReplayNativeTraversal traversal = new(1, goal, (byte)traversalArguments[0], traversalArguments[1] == 1, inferred);
                    if (entry.NativeTraversal is { } inputTraversal && inputTraversal.Mode == traversal.Mode && inputTraversal.Forward == traversal.Forward && inputTraversal.Producer == traversal.Producer)
                    {
                        continue;
                    }
                    outcomes.Add((receipt.Sequence, outcomes.Count, new ReplayJournalEntry(ReplayEntryKind.NativeTraversal, entry.Scene, receipt.FieldId, receipt.Frame) { NativeTraversal = traversal }));
                    continue;
                }
                outcomes.Add((receipt.Sequence, outcomes.Count, new ReplayJournalEntry(receipt.Kind == ReplayReceiptKind.InputSample ? ReplayEntryKind.InputSample : ReplayEntryKind.ScriptGoal, entry.Scene, receipt.FieldId, receipt.Frame) { ScriptGoal = goal, Value = receipt.Kind == ReplayReceiptKind.InputSample ? receipt.Value : 0, MappedPulse = receipt.Kind == ReplayReceiptKind.InputSample ? ReadMappedPulseCause(receipt) : (ushort)0 }));
            }
            foreach (var outcome in outcomes.OrderBy(value => value.Sequence < 0 ? int.MaxValue : value.Sequence).ThenBy(value => value.Order))
            {
                if (outcome.Intent.Kind == ReplayEntryKind.InputSample && !RetainInputSample(outcome.Intent, ref representedHeld))
                {
                    continue;
                }
                if (outcome.Intent.Kind == ReplayEntryKind.NativeTraversal)
                {
                    representedHeld = traversalReleaseNeeded;
                }
                intents.Add(outcome.Intent);
            }
        }
        List<ReplayJournalEntry> compact = [];
        foreach (ReplayJournalEntry intent in intents)
        {
            ReplayJournalEntry? previous = compact.LastOrDefault();
            if (intent.Kind == ReplayEntryKind.PersistentEffect && previous?.Kind == ReplayEntryKind.PersistentEffect && intent.Code == previous.Code && intent.FieldId == previous.FieldId && intent.Scene == previous.Scene && intent.EffectArguments is not null && previous.EffectArguments is not null && intent.EffectArguments.SequenceEqual(previous.EffectArguments) && previous.Frames <= 30000 - intent.Frames)
            {
                compact[^1] = previous with { Frames = previous.Frames + intent.Frames };
            }
            else
            {
                compact.Add(intent);
            }
        }
        return compact.ToArray();
    }

    private static bool RetainInputSample(ReplayJournalEntry entry, ref int representedHeld)
    {
        bool retain = entry.Value != 0 || entry.MappedPulse != 0 || representedHeld != 0;
        representedHeld = entry.Value;
        return retain;
    }

    private static ushort ReadMappedPulseCause(ReplayReceipt receipt)
    {
        if (receipt.Arguments is not { Length: >= 4 } arguments)
        {
            throw new InvalidDataException("Native input sample lost its physical/mapped cause audit.");
        }
        if (arguments.Length < 5)
        {
            if ((arguments[0] & ~receipt.Value) != 0)
            {
                throw new NotSupportedException("This native input sample predates the retained logical pulse cause; recreate its authentic source history.");
            }
            return 0;
        }
        int pulse = arguments[4];
        if ((pulse & ~0x50) != 0)
        {
            throw new InvalidDataException("Native input sample contains an unrepresented logical pulse cause.");
        }
        return (ushort)pulse;
    }

    private static bool RetainExplicitInput(ReplayJournalEntry entry, ReplayJournalEntry[] visit, int index, bool inferred)
    {
        if (entry.Kind is not (ReplayEntryKind.Action or ReplayEntryKind.WorldAction))
        {
            return true;
        }
        if (!entry.Accepted)
        {
            return false;
        }
        if (entry.Code == 8)
        {
            //Version1 lacks traversal-completion receipts. Its explicit held-input
            //intent is retained under the visit's inferred provenance, without cadence.
            return inferred;
        }
        if (entry.Code != 0 || entry.MessageId is not null || entry.Trigger is 2 or 8 or 9 || entry.Receipts.Any(receipt => receipt.Kind is ReplayReceiptKind.Command or ReplayReceiptKind.Dialogue or ReplayReceiptKind.PersistentEffect))
        {
            return true;
        }
        for (int nextIndex = index; nextIndex < visit.Length; nextIndex++)
        {
            ReplayJournalEntry next = visit[nextIndex];
            if (next.Receipts.Any(receipt => receipt.Kind == ReplayReceiptKind.InputSample && receipt.Arguments is { Length: >= 4 } args && (args[0] & ~receipt.Value) != 0 && (args[2] & 0x40) != 0))
            {
                return true;
            }
            if (next.Advanced != 0 || next.Repeat != 1)
            {
                break;
            }
        }
        return false;
    }

    private static ReplayJournalEntry WithoutPhysicalDiagnostics(ReplayJournalEntry entry)
    {
        return entry with { Horizontal = 0, Vertical = 0, CameraHorizontal = 0, Frames = entry.Kind == ReplayEntryKind.PersistentEffect ? entry.Frames : 1, Advanced = 0, Repeat = 1, StartPose = null, EndPose = null, Navigation = [], WorldNavigation = [], CommandCount = 0, CommandDigest = 0, SemanticAdmissions = [], WorldEffects = [], Receipts = entry.Receipts.Where(receipt => receipt.Kind is not (ReplayReceiptKind.Branch or ReplayReceiptKind.InputSample or ReplayReceiptKind.NativeTraversal) && (receipt.Kind != ReplayReceiptKind.PersistentEffect || entry.Kind == ReplayEntryKind.PersistentEffect)).ToArray() };
    }

    internal static ReplayJournal Restore(int version, string sessionId, ReplayCompletedVisit[] visits, ReplayJournalEntry[] active, bool complete, string? reason)
    {
        ReplayJournal journal = new(sessionId, null, 0, 2, true, null, version: version);
        foreach (ReplayCompletedVisit visit in visits)
        {
            if (visit is null || visit.Ordinal != journal.visits.Length || visit.Intents is null || visit.Exit is null || !Enum.IsDefined(visit.Scene) || !Enum.IsDefined(visit.Exit.Scene) || visit.FieldId is < 0 or > ushort.MaxValue || visit.Exit.FieldId is < 0 or > ushort.MaxValue || visit.EntryFieldLoad is not null && (visit.EntryFieldLoad.Kind is not (ReplayEntryKind.FieldLoad or ReplayEntryKind.WorldAllocation) || visit.EntryFieldLoad.FieldId != visit.FieldId || visit.EntryFieldLoad.Scene != visit.Scene))
            {
                throw new InvalidDataException("Invalid completed semantic visit boundary.");
            }
            visit.Exit.Trigger?.Validate();
            foreach (ReplayJournalEntry entry in VisitEntries(visit))
            {
                entry.Validate();
                journal = journal.AppendCore(entry);
            }
            journal = new ReplayJournal(sessionId, journal.tail, journal.EntryCount, journal.EncodedByteCount, true, null, journal.tail, [.. journal.visits, visit], version);
        }
        foreach (ReplayJournalEntry entry in active)
        {
            entry.Validate();
            if (version == 1)
            {
                int bytes = ReplayJournalEntryStorage.Encode(entry).Length + (journal.EntryCount == 0 ? 0 : 1);
                journal = new ReplayJournal(sessionId, new Node(Snapshot(entry), journal.tail), journal.EntryCount + 1, checked(journal.EncodedByteCount + bytes), true, null, version: 1);
            }
            else
            {
                journal = journal.AppendCore(entry);
            }
        }
        return complete ? journal : journal.MarkUnavailable(reason ?? throw new InvalidDataException("Missing causal journal unavailable reason."));
    }
}
