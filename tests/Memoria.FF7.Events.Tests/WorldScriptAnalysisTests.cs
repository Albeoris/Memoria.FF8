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
        BlockingAcknowledgementsDoNotReadAValue();
        ObservableWorldEffectsExcludeTemporaryBookkeeping();
        CurrentEntryPreservesConcreteGuards();
        ReloadAndAmbientClassification();
        PureFieldTransferProof();
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
        Check(analysis.Complete && analysis.MayEnterField && !analysis.MayReloadWorld && analysis.IsPureFieldTransfer, "A canonical read-only bank branch must expose both pure potential native field-entry arms.");
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
        Check(!analysis.IsPureFieldTransfer, "A requested transfer with native model allocation was falsely proved side-effect free.");
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

    private static void BlockingAcknowledgementsDoNotReadAValue()
    {
        foreach (WorldOperation operation in new[] { WorldOperation.CloseWindow, WorldOperation.WaitWindow, WorldOperation.WaitChocoboPath, WorldOperation.WaitChocoboRuns })
        {
            ushort[] code = [0, (ushort)operation, 0x110, 1, 0x110, 0, 0x318, 0x203];
            RejectHost rejected = new();
            WorldMachine source = Machine(code, new byte[1280], rejected);
            string before = JsonSerializer.Serialize(source.Capture());
            WorldScriptAnalysis analysis = source.AnalyzeMeshDispatch(0, 0, 3);
            Check(analysis.Complete && analysis.MayEnterField && !analysis.IsPureFieldTransfer && analysis.HostReads.Length == 0, $"The scheduling acknowledgement {operation} was treated as an unobserved value or a pure transfer.");
            Check(before == JsonSerializer.Serialize(source.Capture()) && rejected.Calls == 0, "Inspecting a scheduling acknowledgement advanced the native host or source VM.");
            AcknowledgementHost host = new(operation);
            WorldMachine native = Machine(code, new byte[1280], host);
            native.StartMesh(0, 0, 0);
            native.Tick();
            native.Tick();
            Check(native.Capture().System.ProgramCounter == 1 && !host.Transferred, "The native blocking host operation advanced before acknowledgement.");
            host.Ready = true;
            native.Tick();
            Check(host.Transferred, "The native blocking operation did not continue to its field handoff after acknowledgement.");
        }
        WorldScriptAnalysis mutation = Machine([0, 0x110, 0, 0x340, 0x203], new byte[1280], new RejectHost()).AnalyzeMeshDispatch(0, 0, 3);
        Check(!mutation.Complete && mutation.HostReads.Any(read => read.Kind == WorldHostDependencyKind.OperationResult), "A host-mutating path command was silently treated as a scheduling acknowledgement.");
    }

    private sealed class AcknowledgementHost(WorldOperation operation) : IWorldHost
    {
        public bool Ready { get; set; }
        public bool Transferred { get; private set; }

        public WorldHostReply Execute(WorldHostRequest request)
        {
            if (request.Command.Operation == operation)
            {
                return new(Ready);
            }
            Check(request.Command.Operation == WorldOperation.FieldTransition, "The authored native wait unexpectedly dispatched another command.");
            Transferred = true;
            return new(true);
        }

        public int ReadSpecial(int selector, int activeModel) => throw new InvalidOperationException("A scheduling acknowledgement read an external value.");
    }

    private static void ObservableWorldEffectsExcludeTemporaryBookkeeping()
    {
        foreach (ushort reference in new ushort[] { 0x114, 0x118, 0x11C })
        {
            WorldScriptAnalysis persistent = Machine([0, reference, 0, 0x110, 1, 0xE0, 0x203], new byte[1280], new RejectHost()).AnalyzeMeshDispatch(0, 0, 3);
            Check(persistent.Complete && persistent.ChangesPersistentState && persistent.MayInteract && !persistent.MayEnterField && persistent.Writes.SequenceEqual(new[] { new WorldVariableRead(reference, 0) }), "A typed native persistent write disappeared from a standalone world interaction.");
        }
        WorldScriptAnalysis local = Machine([0, 0x119, 0, 0x110, 1, 0xE0, 0x203], new byte[1280], new RejectHost()).AnalyzeMeshDispatch(0, 0, 3);
        Check(local.Complete && !local.ChangesPersistentState && !local.MayInteract && !local.IsInert, "Local scratch assignment fabricated a world progress frontier or an inert execution proof.");
        WorldScriptAnalysis text = Machine([0, 0x110, 0, 0x325, 0x32E, 0x203], new byte[1280], new RejectHost()).AnalyzeMeshDispatch(0, 0, 3);
        Check(text.Complete && text.HasDialogue && text.MayInteract && !text.MayEnterField, "Native world dialogue without a field transfer was lost.");
        WorldScriptAnalysis sound = Machine([0, 0x110, 43, 0x31D, 0x203], new byte[1280], new RejectHost()).AnalyzeMeshDispatch(0, 0, 3);
        Check(sound.Complete && !sound.MayInteract, "Ambient sound fabricated a persistent or dialogue frontier.");
    }

    private static void CurrentEntryPreservesConcreteGuards()
    {
        ushort[] code = [0, 0x114, 64, 0x201, 6, 0x203, 0x110, 1, 0x110, 0, 0x318, 0x203];
        foreach (byte value in new byte[] { 0, 1 })
        {
            byte[] persistent = new byte[1280];
            persistent[8] = value;
            RejectHost rejected = new();
            WorldMachine source = Machine(code, persistent, rejected);
            string before = JsonSerializer.Serialize(source.Capture());
            WorldScriptAnalysis potential = source.AnalyzeMeshDispatch(0, 0, 3);
            WorldScriptAnalysis entry = source.AnalyzeMeshDispatch(0, 0, 3, currentEntryOnly: true);
            RecordingHost host = new();
            WorldMachine native = Machine(code, persistent.ToArray(), host);
            native.StartMesh(0, 0, 0);
            native.Tick();
            bool transferred = host.Requests.Any(request => request.Command.Operation == WorldOperation.FieldTransition);
            Check(potential.Complete && potential.MayEnterField && entry.Complete && entry.MayEnterField == transferred && entry.IsInert != transferred, "Current-entry inspection diverged from native branch execution or removed a potential discovery arm.");
            Check(before == JsonSerializer.Serialize(source.Capture()) && persistent[8] == value && rejected.Calls == 0, "Current-entry inspection changed source execution or sampled its host.");
        }
        WorldScriptAnalysis unknown = Machine([0, 0x11B, 16, 0x201, 6, 0x203, 0x203], new byte[1280], new RejectHost()).AnalyzeMeshDispatch(0, 0, 3, currentEntryOnly: true);
        Check(!unknown.Complete && !unknown.IsInert, "An unknown stochastic guard fabricated an inert native entry.");
        WorldScriptAnalysis polling = Machine([0, 0x200, 1], new byte[1280], new RejectHost()).AnalyzeMeshDispatch(0, 0, 3, currentEntryOnly: true);
        Check(polling.Complete && !polling.IsInert, "A side-effect-free infinite native poll was mistaken for a terminating inert callback.");
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

    private static void PureFieldTransferProof()
    {
        RejectHost host = new();
        byte[] persistent = new byte[1280];
        WorldMachine pure = Machine([0, 0x100, 0x110, 2, 0x110, 1, 0x318, 0x203], persistent, host);
        Check(pure.AnalyzeMeshDispatch(0, 0, 3) is { Complete: true, IsPureFieldTransfer: true }, "A complete literal field handoff was not proved pure.");
        WorldMachine assignment = Machine([0, 0x118, 8, 0x110, 1, 0xE0, 0x100, 0x110, 2, 0x110, 1, 0x318, 0x203], persistent, host);
        string before = JsonSerializer.Serialize(assignment.Capture());
        byte[] bytes = persistent.ToArray();
        Check(assignment.AnalyzeMeshDispatch(0, 0, 3) is { Complete: true, MayEnterField: true, IsPureFieldTransfer: false }, "A native variable assignment before handoff was classified as a pure transfer.");
        WorldMachine audio = Machine([0, 0x110, 43, 0x31D, 0x100, 0x110, 2, 0x110, 1, 0x318, 0x203], persistent, host);
        Check(audio.AnalyzeMeshDispatch(0, 0, 3) is { Complete: true, MayEnterField: true, IsPureFieldTransfer: false }, "Another native host operation before handoff was classified as a pure transfer.");
        WorldMachine unresolved = Machine([0, 0x110, 2, 0x113, 16, 0x318, 0x203], persistent, host);
        Check(unresolved.AnalyzeMeshDispatch(0, 0, 3) is { Complete: false, IsPureFieldTransfer: false }, "An unresolved host dependency was treated as a pure transfer proof.");
        Check(!pure.AnalyzeMeshDispatch(0, 0, 3, instructionBudget: 1).IsPureFieldTransfer, "A truncated CFG inspection fabricated a pure transfer proof.");
        Check(!pure.AnalyzeMeshDispatch(1, 0, 3).IsPureFieldTransfer, "An absent native handler fabricated a pure transfer proof.");
        Check(before == JsonSerializer.Serialize(assignment.Capture()) && bytes.SequenceEqual(persistent) && host.Calls == 0, "Pure-transfer proof inspection changed live execution, variables or host state.");
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
