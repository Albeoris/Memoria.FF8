using System.Text.Json;
using Memoria.FF7.Events;

internal static class NativeScriptAnalysisTests
{
    public static void Run()
    {
        // Both sides of IFUB lead to authored instructions; REQ reaches an aliased child script.
        ushort[] first = new ushort[32];
        ushort[] second = new ushort[32];
        first[1] = 1;
        second[1] = 14;
        second[2] = 14;
        byte[] code = [0, 0x14, 0x10, 7, 1, 0, 7, 0x01, 1, 0x22, 0x10, 2, 0, 0, 0x81, 0x20, 0, 42, 0, 0];
        FieldMachine machine = new(new(code, [first, second]));
        machine.Initialize();
        string before = JsonSerializer.Serialize(machine.Capture());
        FieldScriptAnalysis analysis = machine.AnalyzeScript(0, 1);
        Check(analysis.Complete && analysis.ChangesStory, "Both conditional arms and requested child story writes must be analyzed.");
        Check(analysis.Reads.Contains(new(FieldBank.Persistent0Byte, 7, false)) && analysis.ProgramCounters.Contains(14), "The predicate dependency or aliased child body was lost.");
        Check(before == JsonSerializer.Serialize(machine.Capture()), "Read-only analysis changed the live VM.");
        machine.Variables.WriteByte(FieldBank.Persistent0Byte, 8, 9);
        Check(analysis.Fingerprint == machine.AnalyzeScript(0, 1).Fingerprint, "An unrelated variable changed this event's dependency signature.");
        machine.Variables.WriteByte(FieldBank.Persistent0Byte, 7, 9);
        Check(analysis.Fingerprint != machine.AnalyzeScript(0, 1).Fingerprint, "A predicate variable failed to change its event signature.");
        Check(machine.ReadScriptAnalysisFingerprint(0, 1, analysis.Reads) == machine.AnalyzeScript(0, 1).Fingerprint, "Refreshing cached dependency reads differs from native reanalysis.");
        string allocation = machine.ReadScriptAnalysisAllocationFingerprint();
        machine.Variables.WriteByte(FieldBank.Persistent1Byte, 9, 2);
        Check(allocation != machine.ReadScriptAnalysisAllocationFingerprint(), "A changed party request owner failed to invalidate cached native analysis.");

        ushort[] entry = new ushort[32];
        entry[1] = 1;
        FieldMachine cycle = new(new([0, 0x12, 0], [entry]));
        Check(cycle.AnalyzeScript(0, 1) is { Complete: true, ProgramCounters.Length: 1 }, "A bounded backward cycle was not deduplicated.");
        FieldMachine wide = new(new([0, 0x17, 0x20, 3, 0, 4, 0, 0, 2, 0, 0], [entry]));
        FieldScriptAnalysis wideResult = wide.AnalyzeScript(0, 1);
        Check(wideResult.Complete && wideResult.Reads.Contains(new(FieldBank.Persistent0Word, 3, true)) && wideResult.ProgramCounters.Contains(10), "Word operands or the signed wide branch distance were decoded incorrectly.");
        FieldMachine unknown = new(new([0, 0xFF], [entry]));
        Check(!unknown.AnalyzeScript(0, 1).Complete, "An unsupported native instruction was silently classified as complete.");
        FieldMachine unsupportedComparison = new(new([0, 0x14, 0, 0, 0, 11, 1, 0], [entry]));
        Check(!unsupportedComparison.AnalyzeScript(0, 1).Complete, "An unsupported native comparison selector was silently classified as complete.");
        Check(!machine.AnalyzeScript(0, 1, instructionBudget: 1).Complete, "An exhausted analysis budget was reported as complete.");
        RejectHost host = new();
        FieldMachine dialogue = new(new([0, 0x40, 0, 0, 0], [entry]), host: host);
        Check(dialogue.AnalyzeScript(0, 1).Complete && host.Calls == 0, "Read-only decoding dispatched a native dialogue host command.");
        FieldMachine gil = new(new([0, 0x3B, 0x55, 0, 2, 0], [entry]), host: host);
        Check(gil.AnalyzeScript(0, 1).HostReads.SequenceEqual(new[] { new FieldHostRead(FieldHostReadKind.Gil) }) && host.Calls == 0, "A gil query lost its external dependency or dispatched a host.");
        FieldMachine item = new(new([0, 0x5A, 0x05, 23, 0, 3, 0], [entry]), host: host);
        Check(item.AnalyzeScript(0, 1).HostReads.SequenceEqual(new[] { new FieldHostRead(FieldHostReadKind.ItemQuantity, 23) }), "An item query lost its specific native item dependency.");
        FieldMachine available = new(new([0, 0xCC, 3, 1, 0], [entry]), host: host);
        FieldScriptAnalysis availability = available.AnalyzeScript(0, 1);
        Check(availability.Complete && availability.HostReads.SequenceEqual(new[] { new FieldHostRead(FieldHostReadKind.PartyAvailable, 3) }), "An available-party branch lost its host bit dependency or bounded successors.");
        FieldMachine random = new(new([0, 0x99, 0x50, 0, 0], [entry]), host: host);
        Check(!random.AnalyzeScript(0, 1).Complete && host.Calls == 0, "A random-result query silently used a neutral result as authoritative availability.");
        FieldMachine counter = new(new([0, 0x95, 0x01, 7, 0], [entry]));
        string counterSignature = counter.AnalyzeScript(0, 1).Fingerprint;
        counter.Variables.WriteByte(FieldBank.Persistent0Byte, 7, 5);
        Check(counterSignature == counter.AnalyzeScript(0, 1).Fingerprint, "A hostless own-store counter reopened an event from its internal update value.");
        FieldMachine guardedCounter = new(new([0, 0x95, 0x01, 7, 0x14, 0x10, 7, 0, 0, 1, 0], [entry]));
        string guardedSignature = guardedCounter.AnalyzeScript(0, 1).Fingerprint;
        guardedCounter.Variables.WriteByte(FieldBank.Persistent0Byte, 7, 5);
        Check(guardedSignature != guardedCounter.AnalyzeScript(0, 1).Fingerprint, "Own-store exclusion erased a separate IF predicate reading the same native bytes.");
        FieldMachine compound = new(new([0, 0x85, 0x13, 7, 8, 0], [entry]));
        FieldScriptAnalysis compoundReads = compound.AnalyzeScript(0, 1);
        Check(!compoundReads.Reads.Contains(new(FieldBank.Persistent0Byte, 7, false)) && compoundReads.Reads.Contains(new(FieldBank.Persistent1Byte, 8, false)), "Own-store exclusion removed another operand's dependency.");
        FieldMachine aliasStore = new(new([0, 0x81, 0x12, 7, 7, 0, 0], [entry]));
        Check(aliasStore.AnalyzeScript(0, 1).Reads.Contains(new(FieldBank.Persistent0Word, 7, true)), "A wider aliased source was suppressed although the own-store writes only one byte.");
    }

    private sealed class RejectHost : IFieldHost
    {
        public int Calls { get; private set; }

        public FieldHostReply Execute(FieldHostRequest request)
        {
            Calls++;
            throw new InvalidOperationException("Analysis must never dispatch the authoritative host.");
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
