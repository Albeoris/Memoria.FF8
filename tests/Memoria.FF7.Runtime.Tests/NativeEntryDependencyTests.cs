using System.Text.Json;
using Memoria.AutoPlay;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeEntryDependencyTests
{
    private static readonly byte[] BytePredicate = [0x14, 0x50, 8, 1, 0, 1, 0];
    private static readonly byte[] PositionQuery = [0xC1, 0x66, 0x66, 1, 2, 4, 6, 8];
    private static readonly byte[] TrianglePredicate = [0x16, 0x60, 8, 0, 0, 0, 0, 1, 0];
    private static readonly byte[] Move = [0xA8, 0, 10, 0, 20, 0];

    public static void Run()
    {
        CheckVariableProvenance();
        CheckSpatialProvenance();
        CheckPlannerProgression();
        CheckIncompleteCacheRecovery();
        CheckOriginal();
    }

    private static void CheckVariableProvenance()
    {
        FieldMachine constant = Create([0x80, 0x50, 8, 1, .. BytePredicate]);
        Check(constant.AnalyzeScript(0, 1).Reads.Contains(new(FieldBank.MapByte, 8, false)), "Entry provenance changed the conservative full effect reads.");
        Check(!Analyze(constant).Reads.Contains(new(FieldBank.MapByte, 8, false)), "A guaranteed local store became an incoming predicate.");
        FieldMachine derived = Create([0x80, 0x51, 8, 7, .. BytePredicate]);
        FieldScriptEntryDependencies derivedEntry = Analyze(derived);
        Check(derivedEntry.Reads.Contains(new(FieldBank.Persistent0Byte, 7, false)) && !derivedEntry.Reads.Contains(new(FieldBank.MapByte, 8, false)), "A derived store erased its incoming operand or retained its produced destination.");
        FieldMachine bypass = Create([0x14, 0x10, 6, 1, 0, 5, 0x80, 0x50, 8, 1, .. BytePredicate]);
        Check(Analyze(bypass).Reads.Contains(new(FieldBank.MapByte, 8, false)), "A branch bypassing a write erased a genuine incoming predicate.");
        FieldMachine both = Create([0x14, 0x10, 6, 1, 0, 7, 0x80, 0x50, 8, 1, 0x10, 5, 0x80, 0x50, 8, 2, .. BytePredicate]);
        Check(!Analyze(both).Reads.Contains(new(FieldBank.MapByte, 8, false)), "Intersecting two guaranteed branch stores lost their produced value.");
        FieldMachine width = Create([0x80, 0x50, 8, 1, .. TrianglePredicate]);
        Check(Analyze(width).Reads.Contains(new(FieldBank.MapWord, 8, true)), "A one-byte store erased the unwritten half of a word predicate.");
        Check(Analyze(Create([0x12, 0])).Complete, "A bounded loop failed to converge in entry provenance.");
        Check(!constant.AnalyzeEntryDependencies(0, 1, instructionBudget: 1).Complete, "An exhausted entry-provenance budget was reported as complete.");
        Check(!Create([0xFF]).AnalyzeEntryDependencies(0, 1).Complete, "An unknown native effect produced a complete empty entry signature.");
    }

    private static void CheckSpatialProvenance()
    {
        FieldMachine incoming = Create([.. PositionQuery, .. TrianglePredicate]);
        Check(Analyze(incoming).HostReads.Contains(new(FieldHostReadKind.Actor, 1, (int)FieldActorQueryKind.Position)), "A true incoming actor-position predicate was suppressed.");
        FieldMachine synchronous = Create([3, 1, 0x41, .. PositionQuery, .. TrianglePredicate], [.. Move, 0]);
        FieldScriptEntryDependencies entry = Analyze(synchronous);
        Check(!entry.HostReads.Any(read => read.Kind == FieldHostReadKind.Actor) && !entry.Reads.Contains(new(FieldBank.MapWord, 8, true)), "Guaranteed synchronous movement/query outputs reopened an incoming condition.");
        FieldScriptAnalysis full = synchronous.AnalyzeScript(0, 1);
        Check(full.HostReads.Any(read => read.Kind == FieldHostReadKind.Actor) && full.Reads.Contains(new(FieldBank.MapWord, 8, true)), "Narrow entry provenance erased full native effects or spatial reads.");
        FieldMachine asynchronous = Create([1, 1, 0x41, .. PositionQuery, .. TrianglePredicate], [.. Move, 0]);
        Check(Analyze(asynchronous).HostReads.Any(read => read.Kind == FieldHostReadKind.Actor), "An asynchronous child falsely guaranteed completion before its caller's position query.");
        FieldMachine bypass = Create([0x14, 0x10, 6, 1, 0, 4, 3, 1, 0x41, .. PositionQuery, .. TrianglePredicate], [.. Move, 0]);
        Check(Analyze(bypass).HostReads.Any(read => read.Kind == FieldHostReadKind.Actor), "A path bypassing synchronous movement lost its incoming spatial condition.");
        FieldMachine beforeMove = Create([.. PositionQuery, .. TrianglePredicate[..^1], 3, 1, 0x41, 0], [.. Move, 0]);
        Check(Analyze(beforeMove).HostReads.Any(read => read.Kind == FieldHostReadKind.Actor), "A later movement erased an earlier incoming actor-position read.");
        string before = JsonSerializer.Serialize(synchronous.Capture());
        string projected = JsonSerializer.Serialize(Analyze(synchronous));
        Check(before == JsonSerializer.Serialize(synchronous.Capture()) && projected == JsonSerializer.Serialize(Analyze(synchronous)), "Entry provenance changed native state or was nondeterministic.");
    }

    private static FieldScriptEntryDependencies Analyze(FieldMachine machine)
    {
        FieldScriptEntryDependencies result = machine.AnalyzeEntryDependencies(0, 1);
        Check(result.Complete, $"Synthetic entry dependency analysis was incomplete: {result.Reason}");
        return result;
    }

    private static void CheckPlannerProgression()
    {
        FieldMachine machine = Create([3, 1, 0x41, .. PositionQuery, .. TrianglePredicate], [.. Move, 0]);
        FieldScriptEntryDependencies entry = Analyze(machine);
        string signature = machine.ReadScriptAnalysisFingerprint(0, 1, entry.Reads);
        Ff7AutoPlayState state = new();
        AutoPlayPlanner<Ff7AutoPlayDeferral> planner = Ff7AutoPlayPolicy.CreatePlanner(state);
        AutoPlayVisit visit = new("native-field", "native-program");
        AutoPlayEvent doorway = new("optional-door", AutoPlayEventKind.Interaction, Order: 1, ConditionSignature: signature);
        AutoPlayEvent frontier = new("unconsumed-exit", AutoPlayEventKind.Interaction, Order: 2, ConditionSignature: "external-story-facts");
        planner.Observe(visit, [doorway, frontier]);
        planner.ObserveNavigation(visit.Key, "component", [doorway, frontier]);
        planner.Completed(visit.Key, doorway.Id, entrySignature: signature, entrySignatureAuthoritative: true);
        machine.Variables.WriteWord(FieldBank.MapWord, 8, 17);
        string afterMovement = machine.ReadScriptAnalysisFingerprint(0, 1, entry.Reads);
        Check(signature == afterMovement, "A script-produced triangle reopened a consumed entry signature.");
        planner.Observe(visit, [doorway with { ConditionSignature = afterMovement }, frontier]);
        planner.ObserveNavigation(visit.Key, "component", [doorway with { ConditionSignature = afterMovement }, frontier]);
        Check(planner.PlanNavigation("component")?.Event.Id == frontier.Id, "Normal shared planning replayed a consumed staged doorway before an unconsumed exit.");
    }

    private static FieldMachine Create(byte[] main, byte[]? child = null)
    {
        child ??= [0];
        ushort[] sourceEntries = new ushort[32];
        sourceEntries[1] = 1;
        ushort[] childEntries = new ushort[32];
        childEntries[1] = checked((ushort)(main.Length + 2));
        FieldProgram program = new([0, .. main, 0, .. child], [sourceEntries, childEntries]);
        FieldMachine machine = new(program);
        machine.Initialize();
        machine.Variables.WriteByte(FieldBank.Persistent1Byte, 9, 0);
        machine.Variables.WriteByte(FieldBank.Persistent1Byte, 10, 255);
        machine.Variables.WriteByte(FieldBank.Persistent1Byte, 11, 255);
        FieldVmSnapshot snapshot = machine.Capture();
        snapshot.Entities[1] = snapshot.Entities[1] with { ModelBound = true, PartyCharacter = 0 };
        return FieldMachine.Restore(program, snapshot);
    }

    private static void CheckIncompleteCacheRecovery()
    {
        FieldMachine machine = Create([0x8D, 0x15, 7, 8, 0]);
        Ff7AutoPlayDiscovery.EntryDependencyCache cache = new();
        string before = JsonSerializer.Serialize(machine.Capture());
        FieldScriptAnalysis invalid = machine.AnalyzeScript(0, 1);
        Check(!invalid.Complete && !cache.Read(machine, 0, 1, invalid).Complete, "A zero remainder divisor was accepted by the entry cache.");
        Check(before == JsonSerializer.Serialize(machine.Capture()), "An unsupported entry-cache observation changed native state.");
        machine.Variables.WriteByte(FieldBank.MapByte, 8, 2);
        FieldScriptAnalysis valid = machine.AnalyzeScript(0, 1);
        Check(valid.Complete && valid.HostReads.SequenceEqual(invalid.HostReads), "The native cache regression did not exercise equal host layouts across decode recovery.");
        FieldScriptEntryDependencies recovered = cache.Read(machine, 0, 1, valid);
        Check(recovered.Complete && recovered.Reads.Contains(new(FieldBank.MapByte, 8, false)), "A prior incomplete result hid a newly supported entry with the same host layout.");
        Check(ReferenceEquals(recovered, cache.Read(machine, 0, 1, valid)), "Complete entry structure was decoded again despite unchanged allocation and host layout.");
        machine.Variables.WriteByte(FieldBank.MapByte, 8, 0);
        FieldScriptAnalysis failedAgain = machine.AnalyzeScript(0, 1);
        Check(!cache.Read(machine, 0, 1, failedAgain).Complete, "A cached complete entry structure masked a current native decode failure.");
        machine.Variables.WriteByte(FieldBank.MapByte, 8, 3);
        FieldScriptEntryDependencies recoveredAgain = cache.Read(machine, 0, 1, machine.AnalyzeScript(0, 1));
        Check(recoveredAgain.Complete && !ReferenceEquals(recovered, recoveredAgain), "A failed full observation retained an earlier complete entry structure.");
    }

    private static void CheckOriginal()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_ENTRY_DEPENDENCY_SAVE");
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("SKIP original entry provenance: configure resources and an external standalone doorway fixture.");
            return;
        }
        byte[] source = File.ReadAllBytes(path);
        using MemoryStream stream = new(source, writable: false);
        CombinedSessionSave envelope = CombinedSessionCodec.Read(stream);
        Ff7Installation installation = Ff7Installation.Discover(root);
        using Ff7Session session = Ff7Session.Restore(installation, envelope.Ff7!);
        string before = JsonSerializer.Serialize(session.Capture());
        int entity = session.Host.Process.Lines.First().Entity;
        FieldScriptAnalysis full = session.Machine.AnalyzeScript(entity, 4);
        FieldScriptEntryDependencies entry = session.Machine.AnalyzeEntryDependencies(entity, 4, full);
        Check(full.Complete && entry.Complete && full.HostReads.Any(read => read.Kind == FieldHostReadKind.Actor) && !entry.HostReads.Any(read => read.Kind == FieldHostReadKind.Actor), "The original staged doorway did not distinguish produced position from incoming facts.");
        Check(!entry.Reads.Any(read => read.Bank is FieldBank.MapByte or FieldBank.MapWord), "The original staged doorway retained overwritten spatial scratch as an incoming condition.");
        Check(entry.Reads.Any(read => read.Bank == FieldBank.Persistent1Byte), "Entry provenance erased the original native party owner dependency.");
        _ = new Ff7AutoPlayDiscovery(session).Observe();
        Check(before == JsonSerializer.Serialize(session.Capture()), "Read-only entry discovery changed script PCs, saved planner facts, progress or RNG.");
        using MemoryStream serialized = new();
        CombinedSessionCodec.Write(serialized, envelope with { Ff7 = session.Capture() });
        serialized.Position = 0;
        using Ff7Session cold = Ff7Session.Restore(installation, CombinedSessionCodec.Read(serialized).Ff7!);
        Check(JsonSerializer.Serialize(entry) == JsonSerializer.Serialize(cold.Machine.AnalyzeEntryDependencies(entity, 4)), "Cold restoration changed the original entry provenance.");
        Check(source.AsSpan().SequenceEqual(File.ReadAllBytes(path)), "Entry projection changed the original standalone source.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
