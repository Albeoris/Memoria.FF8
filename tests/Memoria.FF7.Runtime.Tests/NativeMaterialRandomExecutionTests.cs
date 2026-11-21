using System.Text.Json;
using Memoria.AutoPlay;
using Memoria.FF7.Events;
using Memoria.FF7.Runtime;

internal static class NativeMaterialRandomExecutionTests
{
    private sealed class RejectingHost : IFieldHost
    {
        internal int Calls { get; private set; }

        public FieldHostReply Execute(FieldHostRequest request)
        {
            Calls++;
            throw new InvalidOperationException("Analysis must not execute a live request or consume native RNG.");
        }
    }

    internal static void Run()
    {
        byte[] code = [0, 0, 1, 1, 0xC1, 0x99, 5, 9, 0x80, 0x15, 0x83, 9, 0, 0, 0x80, 0x50, 10, 1, 0];
        byte[] originalCode = code.ToArray();
        ushort[][] entries = [new ushort[32], new ushort[32]];
        entries[0][1] = 2;
        entries[1][1] = 14;
        FieldProgram program = new(code, entries);
        RejectingHost host = new();
        FieldMachine machine = new(program, host: host);
        machine.Initialize();
        byte[] table = Enumerable.Range(0, 256).Select(value => (byte)value).ToArray();
        FieldSpatialRandomDomain domain = new(table, 1, 7);
        string source = JsonSerializer.Serialize(machine.Capture());
        FieldScriptAnalysis ordinary = machine.AnalyzeScript(0, 1);
        Check(!ordinary.Complete, "Material random execution silently inherited ordinary deterministic analysis.");
        FieldScriptAnalysis cosmetic = machine.AnalyzeStochasticPresentation(0, 1, domain);
        Check(!cosmetic.Complete && cosmetic.StochasticPresentation is null, "A random-dependent persistent write was certified as cosmetic presentation.");
        byte[] presentationCode = [0, 0, 0x99, 5, 9, 0x14, 0x50, 9, 128, 3, 6, 0x40, 0, 1, 0x10, 4, 0x40, 0, 2, 0];
        ushort[][] presentationEntries = [new ushort[32]];
        presentationEntries[0][1] = 2;
        RejectingHost presentationHost = new();
        FieldMachine presentation = new(new FieldProgram(presentationCode, presentationEntries), host: presentationHost);
        presentation.Initialize();
        string presentationBefore = JsonSerializer.Serialize(presentation.Capture());
        FieldScriptAnalysis retainedCosmetic = presentation.AnalyzeStochasticPresentation(0, 1, domain);
        Check(retainedCosmetic.Complete && retainedCosmetic.StochasticPresentation is not null && retainedCosmetic.NativeRandomExecutionDomain is null && presentationHost.Calls == 0 && presentationBefore == JsonSerializer.Serialize(presentation.Capture()), "A genuine converging random-dialogue presentation lost its existing cosmetic certificate or mutated live state.");
        FieldScriptAnalysis material = machine.AnalyzeNativeRandomExecution(0, 1, domain);
        Check(material.Complete && material.HostReads.Any(read => read.Kind == FieldHostReadKind.Random) && material.NativeRandomExecutionDomain == domain.Fingerprint && material.StochasticPresentation is null, "Explicit material analysis did not retain its native random owner without claiming presentation equivalence.");
        Check(material.InstructionSites.Any(site => site.Entity == 1 && site.ProgramCounter == 14), "Material native analysis lost the requested child closure.");
        byte[] unsupported = code.ToArray();
        unsupported[8] = 0xFF;
        FieldMachine unknown = new(new FieldProgram(unsupported, entries), host: new RejectingHost());
        unknown.Initialize();
        Check(!unknown.AnalyzeNativeRandomExecution(0, 1, domain).Complete, "An unsupported required opcode was hidden by the native random admission path.");
        Check(host.Calls == 0 && source == JsonSerializer.Serialize(machine.Capture()) && code.SequenceEqual(originalCode), "Analysis changed source instructions, native variables, request ownership or live RNG.");

        Ff7Progress progress = new() { InitialKernelData = [], Characters = [], Party = [], Inventory = [], Materia = [], Gil = 10, FieldRandomIndex = 7, FieldRandomStride = 1 };
        Ff7FieldProcess process = new();
        Check(!Ff7AutoPlayDiscovery.ReadHostConditions(material.HostReads, progress, process).Complete, "A missing explicit random execution contract was treated as complete coverage.");
        Ff7AutoPlayHostConditions original = Ff7AutoPlayDiscovery.ReadHostConditions(material.HostReads, progress, process, nativeRandomDomain: domain.Fingerprint);
        FieldSpatialRandomDomain anotherDomain = new(table, 2, 8);
        Ff7Progress advanced = progress with { FieldRandomIndex = 8, FieldRandomStride = 2 };
        Ff7AutoPlayHostConditions changed = Ff7AutoPlayDiscovery.ReadHostConditions(material.HostReads, advanced, process, nativeRandomDomain: anotherDomain.Fingerprint);
        Check(original.Complete && changed.Complete && original.Fingerprint == changed.Fingerprint, "RNG phase or stride alone rearmed an already consumed source condition.");
        Ff7AutoPlayState coverageState = new();
        var planner = Ff7AutoPlayPolicy.CreatePlanner(coverageState);
        planner.Observe(new("visit", "program"), [new("material", AutoPlayEventKind.Interaction, ConditionSignature: original.Fingerprint)]);
        planner.Completed("visit", "material", observedStoryChanged: false, entrySignature: original.Fingerprint, entrySignatureAuthoritative: true);
        string completedHistory = JsonSerializer.Serialize(coverageState.Coverage);
        planner.Observe(new("visit", "program"), [new("material", AutoPlayEventKind.Interaction, ConditionSignature: changed.Fingerprint)]);
        var completed = coverageState.Coverage.Visits.Single().Events.Single();
        Check(completed.Completed && completed.Conditions!.ConsumedEntries.Contains(original.Fingerprint) && completedHistory == JsonSerializer.Serialize(coverageState.Coverage), "RNG-only reobservation rearmed consumed material coverage or fabricated route/progress history.");
        FieldHostRead[] withGil = [.. material.HostReads, new(FieldHostReadKind.Gil)];
        Check(Ff7AutoPlayDiscovery.ReadHostConditions(withGil, progress, process, nativeRandomDomain: domain.Fingerprint).Fingerprint != Ff7AutoPlayDiscovery.ReadHostConditions(withGil, progress with { Gil = 11 }, process, nativeRandomDomain: domain.Fingerprint).Fingerprint, "Stable random coverage masked a genuine material host dependency.");
        Ff7SessionSave save = new() { ResourceProfile = "test", Language = "test", FieldName = "field", FieldGeneration = 1, Progress = progress, Vm = machine.Capture(), Process = process };
        string before = JsonSerializer.Serialize(save);
        string exact = Ff7AutoplayDriver.ReadNativeRouteFingerprint(save);
        Check(exact != Ff7AutoplayDriver.ReadNativeRouteFingerprint(save with { Progress = progress with { FieldRandomIndex = 8 } }) && exact != Ff7AutoplayDriver.ReadNativeRouteFingerprint(save with { Progress = progress with { FieldRandomStride = 2 } }), "An exact native route proof reused a changed RNG owner despite stable source coverage.");
        Check(before == JsonSerializer.Serialize(save), "Reading an exact native planning fingerprint mutated the retained save.");
        Console.WriteLine("Material native random execution preserves real RNG ownership, stable coverage novelty and exact planning-state fences.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
