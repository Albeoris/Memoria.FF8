using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;

internal static class WorldScriptAnalysisTests
{
    public static void Run()
    {
        BranchesAndTypedGuards();
        RequestsReuseNativeExecution();
        HostDependenciesRemainIncomplete();
        ReloadAndAmbientClassification();
        BoundsAndSourcePreservation();
        Console.WriteLine("PASS read-only native world mesh CFG, selectors, typed guards, request closure and explicit incomplete dependencies");
    }

    private static void BranchesAndTypedGuards()
    {
        ushort[] code = [0, 0x100, 0x114, 64, 0x201, 12, 0x110, 2, 0x110, 1, 0x318, 0x203, 0x110, 3, 0x110, 0, 0x318, 0x203];
        byte[] persistent = new byte[1280];
        RejectHost host = new();
        WorldMachine machine = Machine(code, persistent, host);
        string before = JsonSerializer.Serialize(machine.Capture());
        byte[] beforePersistent = persistent.ToArray();
        WorldScriptAnalysis analysis = machine.AnalyzeMeshDispatch(0, 0, 3);
        Check(analysis.Complete && analysis.MayEnterField && !analysis.MayReloadWorld, "A canonical bank branch must expose both potential native field-entry arms.");
        Check(analysis.Transitions.Select(transition => transition.Selector).Order().SequenceEqual(new ushort?[] { 0x201, 0x300 }), "Native high/low selector operands or a branch arm were lost.");
        Check(analysis.Reads.SequenceEqual(new[] { new WorldVariableRead(0x114, 64) }), "The persistent bit dependency lost its native typed address.");
        string fingerprint = machine.ReadPredicateFingerprint(analysis.Reads);
        Check(before == JsonSerializer.Serialize(machine.Capture()) && persistent.SequenceEqual(beforePersistent) && host.Calls == 0, "World analysis changed live execution, canonical bytes or native host state.");
        persistent[9] = 1;
        Check(machine.ReadPredicateFingerprint(analysis.Reads) == fingerprint, "An unrelated persistent byte invalidated a typed world predicate.");
        persistent[8] = 1;
        Check(machine.ReadPredicateFingerprint(analysis.Reads) != fingerprint, "The observed persistent bit failed to change its predicate fingerprint.");
        persistent[10] = 0x80;
        persistent[11] = 0xFF;
        WorldMachine signed = Machine([0, 0x110, 0, 0x11C, 10, 0x318, 0x203], persistent, host);
        WorldScriptAnalysis word = signed.AnalyzeMeshDispatch(0, 0, 3);
        Check(word.Complete && word.Transitions.Single().Selector == 0xFF80 && word.Reads.Single() == new WorldVariableRead(0x11C, 10), "A signed native word was not preserved in the field selector contract.");
        WorldProgram program = Program(code);
        Check(program.ReadInstruction(2) is { Opcode: 0x114, Operand: 64, WordLength: 2, NextProgramCounter: 4 } && program.ReadInstruction(10).WordLength == 1, "Shared native instruction widths differ between operands and host commands.");
    }

    private static void RequestsReuseNativeExecution()
    {
        ushort[] code = [0, 0x100, 0x110, 2, 0x300, 0x110, 2, 0x207, 0x203, 0x100, 0x110, 1, 0x110, 0, 0x318, 0x203];
        PcWorldEvents events = new([new(0x8000, 1), new(0x4203, 9)], code);
        RejectHost rejected = new();
        WorldMachine source = new(new(events), new byte[1280], rejected);
        string before = JsonSerializer.Serialize(source.Capture());
        WorldScriptAnalysis analysis = source.AnalyzeMeshDispatch(0, 0, 3);
        Check(analysis.Complete && analysis.Transitions.Single().Selector == 0x100 && analysis.ProgramCounters.Contains(14), "A native requested model-script closure lost its field-entry packet.");
        Check(before == JsonSerializer.Serialize(source.Capture()) && rejected.Calls == 0, "Request inspection allocated a live model or dispatched a native host command.");
        RecordingHost native = new();
        WorldMachine oracle = new(new(events), new byte[1280], native);
        oracle.StartMesh(0, 0, 0);
        oracle.Tick();
        WorldHostRequest actual = native.Requests.Single(request => request.Command.Operation == WorldOperation.FieldTransition);
        Check(analysis.Transitions.Single().ProgramCounter == actual.ProgramCounter && analysis.Transitions.Single().Selector == (ushort)(actual.Command.Arguments[0] << 8 | actual.Command.Arguments[1]), "World inspection disagrees with the existing native scheduler and host operand decoder.");
    }

    private static void HostDependenciesRemainIncomplete()
    {
        RejectHost host = new();
        WorldMachine machine = Machine([0, 0x110, 1, 0x113, 16, 0x318, 0x203], new byte[1280], host);
        string before = JsonSerializer.Serialize(machine.Capture());
        WorldScriptAnalysis analysis = machine.AnalyzeMeshDispatch(0, 0, 3);
        Check(!analysis.Complete && analysis.Reason is not null && analysis.MayEnterField && analysis.Transitions.Single().Selector is null, "An unresolved stochastic host dependency must not publish a fabricated field selector.");
        Check(analysis.HostReads.Contains(new(WorldHostDependencyKind.Special, -1, 16)) && host.Calls == 0 && before == JsonSerializer.Serialize(machine.Capture()), "Inspecting a native random selector advanced the host, RNG or VM.");
        WorldMachine distance = Machine([0, 0x110, 2, 0x18, 0x201, 7, 0x203, 0x110, 1, 0x110, 0, 0x318, 0x203], new byte[1280], host);
        WorldScriptAnalysis zone = distance.AnalyzeMeshDispatch(0, 0, 3);
        Check(!zone.Complete && zone.HostReads.Any(read => read.Kind == WorldHostDependencyKind.ZoneDistance) && host.Calls == 0, "A zone-distance dependency must remain explicit instead of changing native selected-light state.");
    }

    private static void ReloadAndAmbientClassification()
    {
        RejectHost host = new();
        WorldMachine reload = Machine([0, 0x110, 0x301, 0x33D, 0x203], new byte[1280], host);
        WorldScriptAnalysis result = reload.AnalyzeMeshDispatch(0, 0, 3);
        Check(result.Complete && result.MayReloadWorld && !result.MayEnterField && result.Transitions.Single().Selector == 0x301, "A native world reload must not become a field-entry frontier.");
        WorldMachine ambient = Machine([0, 0x110, 43, 0x31D, 0x203], new byte[1280], host);
        Check(ambient.AnalyzeMeshDispatch(0, 0, 3) is { Complete: true, MayEnterField: false, MayReloadWorld: false }, "An audio-only mesh callback must not become a field-entry frontier.");
        Check(ambient.AnalyzeMeshDispatch(0, 0, 2).Entry == 0 && ambient.AnalyzeMeshDispatch(1, 0, 3).Entry == 0, "Non-dispatch terrain flags and absent native mesh entries must not fabricate callbacks.");
    }

    private static void BoundsAndSourcePreservation()
    {
        RejectHost host = new();
        WorldMachine bad = Machine([0, 0x110, 0, 0x201, 500], new byte[1280], host);
        string before = JsonSerializer.Serialize(bad.Capture());
        Check(!bad.AnalyzeMeshDispatch(0, 0, 3).Complete && before == JsonSerializer.Serialize(bad.Capture()), "An invalid native branch target must be incomplete without faulting the live VM.");
        WorldMachine unsupported = Machine([0, 0x356], new byte[1280], host);
        Check(!unsupported.AnalyzeMeshDispatch(0, 0, 3).Complete, "An unsupported native instruction must remain explicit.");
        WorldMachine bounded = Machine([0, 0x100, 0x110, 43, 0x31D, 0x203], new byte[1280], host);
        Check(bounded.AnalyzeMeshDispatch(0, 0, 3, instructionBudget: 1) is { Complete: false, Reason: not null }, "World CFG inspection must enforce its instruction budget.");
    }

    private static WorldMachine Machine(ushort[] code, byte[] persistent, IWorldHost host)
    {
        return new(Program(code), persistent, host);
    }

    private static WorldProgram Program(ushort[] code)
    {
        return new(new([new(0x8000, 1)], code));
    }

    private sealed class RejectHost : IWorldHost
    {
        public int Calls { get; private set; }

        public WorldHostReply Execute(WorldHostRequest request)
        {
            Calls++;
            throw new InvalidOperationException("Read-only world analysis must not dispatch its native host.");
        }

        public int ReadSpecial(int selector, int activeModel)
        {
            Calls++;
            throw new InvalidOperationException("Read-only world analysis must not sample its native host.");
        }
    }

    private sealed class RecordingHost : IWorldHost
    {
        public List<WorldHostRequest> Requests { get; } = [];

        public WorldHostReply Execute(WorldHostRequest request)
        {
            Requests.Add(request);
            return new(true);
        }

        public int ReadSpecial(int selector, int activeModel) => throw new NotSupportedException();
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
