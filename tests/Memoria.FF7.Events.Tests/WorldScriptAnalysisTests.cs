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
        PotentialTransitionDependencies();
        BlockingAcknowledgementsDoNotReadAValue();
        ObservableWorldEffectsExcludeTemporaryBookkeeping();
        CurrentEntryPreservesConcreteGuards();
        ReloadAndAmbientClassification();
        PureFieldTransferProof();
        BoundsAndSourcePreservation();
        ModelDispatchOwnershipAndAdmission();
        ControlledModelTransitionReceivers();
        ModelScriptTailOrdering();
        AdmittedScriptDispatchReceipts();
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

    private static void PotentialTransitionDependencies()
    {
        // The native entrance shape consumes a model guard before clearing the stack and constructing a literal handoff.
        ushort[] guarded = [0, 0x113, 8, 0x201, 12, 0x100, 0x110, 8, 0x110, 0, 0x318, 0x203, 0x100, 0x110, 9, 0x110, 0, 0x318, 0x203];
        RejectHost host = new();
        byte[] persistent = new byte[1280];
        WorldMachine machine = Machine(guarded, persistent, host);
        string before = JsonSerializer.Serialize(machine.Capture());
        WorldScriptAnalysis ordinary = machine.AnalyzeMeshDispatch(0, 0, 3);
        WorldScriptAnalysis topology = machine.AnalyzeMeshDispatch(0, 0, 3, isolateTransitionDependencies: true);
        Check(!ordinary.Complete && ordinary.Transitions.All(transition => transition.Selector is null), "Default native admission lost its unresolved host guard.");
        Check(!topology.Complete && !topology.IsPureFieldTransfer && topology.Transitions.Select(transition => transition.Selector).Order().SequenceEqual(new ushort?[] { 0x800, 0x900 }), "Consumed unknown guards hid possible literal handoffs or fabricated admission completeness.");
        Check(topology.HostReads.Contains(new(WorldHostDependencyKind.Special, -1, 8)), "Topology advice discarded its unknown native control dependency.");
        WorldScriptAnalysis projected = machine.AnalyzeMeshDispatch(0, 0, 3, readHost: _ => 0, currentEntryOnly: true);
        Check(projected.Complete && projected.Transitions.Single().Selector == 0x900, "Default current-entry analysis stopped honoring a genuine read-only host projection.");
        foreach (ushort[] code in new ushort[][]
        {
            [0, 0x110, 8, 0x113, 16, 0x318, 0x203],
            [0, 0x110, 8, 0x113, 16, 0x110, 1, 0x40, 0x318, 0x203],
            [0, 0x118, 8, 0x113, 16, 0xE0, 0x100, 0x110, 8, 0x118, 8, 0x318, 0x203],
            [0, 0x110, 8, 0x118, 8, 0x318, 0x203],
            [0, 0x110, 8, 0x119, 8, 0x318, 0x203],
            [0, 0x113, 16, 0x110, 1, 0x40, 0x110, 8, 0x110, 0, 0x318, 0x203]
        })
        {
            WorldMachine unknown = Machine(code, persistent, host);
            WorldScriptAnalysis analysis = unknown.AnalyzeMeshDispatch(0, 0, 3, isolateTransitionDependencies: true);
            Check(!analysis.Complete && analysis.Transitions.Single().Selector is null, "An unknown direct, arithmetic, bank, local or still-live stack operand fabricated a constant topology selector.");
        }
        WorldMachine consumed = Machine([0, 0x113, 8, 0x201, 5, 0x110, 8, 0x110, 0, 0x318, 0x203], persistent, host);
        Check(consumed.AnalyzeMeshDispatch(0, 0, 3, isolateTransitionDependencies: true).Transitions.Single().Selector == 0x800, "A completely consumed control guard contaminated a fresh constant operand stack.");
        WorldMachine cleared = Machine([0, 0x113, 16, 0x110, 1, 0x40, 0x100, 0x110, 8, 0x110, 0, 0x318, 0x203], persistent, host);
        Check(cleared.AnalyzeMeshDispatch(0, 0, 3, isolateTransitionDependencies: true).Transitions.Single().Selector == 0x800, "Native stack clear did not retire a discarded arithmetic dependency.");
        Check(before == JsonSerializer.Serialize(machine.Capture()) && persistent.All(value => value == 0) && host.Calls == 0, "Potential topology analysis changed the live VM, canonical bytes or native host.");
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

    private static void ControlledModelTransitionReceivers()
    {
        ushort[] code = [0, 0x110, 5, 0x330, 0x30C, 0x302, 0x335, 0x203, 0x110, 5, 0x207, 0x203, 0x30C, 0x203];
        PcWorldEvents events = new([new(0x4603, 1), new(0x4604, 8), new(0x4503, 12)], code);
        byte[] persistent = new byte[1280];
        RejectHost host = new();
        WorldMachine machine = new(new(events), persistent, host);
        machine.Restore(machine.Capture() with { Models = [new(6, 0, 0, 0, false, 0, []), new(5, 0, 0, 0, false, 0, [])] });
        string before = JsonSerializer.Serialize(machine.Capture());
        WorldScriptAnalysis selected = machine.AnalyzeModelDispatch(6, 3);
        Check(selected.Complete && selected.Transitions.Single(transition => transition.Operation == WorldOperation.BoardTransport).Model == 5 && selected.Transitions.Single(transition => transition.Operation == WorldOperation.SetPlayer).Model == 5, "Controlled-model transition metadata used the executing callback owner instead of the selected native receiver.");
        Check(selected.Transitions.Single(transition => transition.Operation == WorldOperation.RestoreAttachedRider).Model is null, "Dynamic attached-rider restoration invented a controlled model receiver.");
        WorldScriptAnalysis requested = machine.AnalyzeModelDispatch(6, 4);
        Check(requested.Complete && requested.Transitions.Single().Model == 5 && requested.Transitions.Single().ProgramCounter == 12, "A nested native request lost its child boarding receiver.");
        Check(JsonSerializer.Serialize(new WorldTransitionObservation(7, WorldOperation.FieldTransition, 0x800)) == "{\"ProgramCounter\":7,\"Operation\":792,\"Selector\":2048}", "Absent receiver metadata changed existing field transition serialization.");
        Check(before == JsonSerializer.Serialize(machine.Capture()) && persistent.All(value => value == 0) && host.Calls == 0, "Receiver analysis changed source VM, canonical storage or live host state.");
    }

    private static void ModelDispatchOwnershipAndAdmission()
    {
        ushort[] code = [0, 0x118, 8, 0x110, 1, 0xE0, 0x30C, 0x203, 0x302, 0x203, 0x356, 0x113, 15, 0x201, 16, 0x203, 0x30C, 0x203];
        PcWorldEvents events = new([new(0x4603, 1), new(0x4604, 8), new(0x4605, 8), new(0x4606, 10), new(0x4607, 11)], code);
        byte[] persistent = new byte[1280];
        RejectHost rejected = new();
        WorldMachine source = new(new(events), persistent, rejected);
        WorldSnapshot empty = source.Capture();
        WorldEntitySnapshot resident = new(6, 0, 0, 0, false, 0, []);
        source.Restore(empty with { Models = [resident] });
        string before = JsonSerializer.Serialize(source.Capture());
        WorldScriptAnalysis boarding = source.AnalyzeModelDispatch(6, 3);
        Check(boarding is { Complete: true, Header: 0x4603, Entry: 1, MayChangeControlledModel: true, MayInteract: true, IsInert: false } && boarding.Transitions.Single().Operation == WorldOperation.BoardTransport, "A resident model boarding callback lost its native header or controlled-model effect.");
        Check(boarding.Writes.SequenceEqual(new[] { new WorldVariableRead(0x118, 8) }), "Model analysis lost the typed persistent write.");
        Check(before == JsonSerializer.Serialize(source.Capture()) && persistent.All(value => value == 0) && rejected.Calls == 0, "Model inspection changed the resident VM, persistent bytes or live host.");
        WorldScriptAnalysis select = source.AnalyzeModelDispatch(6, 4);
        Check(select.Complete && select.MayChangeControlledModel && select.MayInteract && !select.ChangesPersistentState && select.Transitions.Single().Operation == WorldOperation.SetPlayer, "A controlled-model-only callback was omitted from interaction discovery.");
        Check(source.AnalyzeModelDispatch(6, 6) is { Complete: false, Reason: not null }, "An unsupported model instruction was represented as a complete callback.");
        Check(source.AnalyzeModelDispatch(6, 3, instructionBudget: 1) is { Complete: false, Reason: not null }, "Model inspection ignored its instruction bound.");
        WorldScriptAnalysis unknown = source.AnalyzeModelDispatch(6, 7);
        Check(!unknown.Complete && unknown.HostReads.Contains(new(WorldHostDependencyKind.Special, 6, 15)), "A model-owned unresolved host dependency lost its owner or fabricated completeness.");
        WorldScriptAnalysis projected = source.AnalyzeModelDispatch(6, 7, currentEntryOnly: true, readHost: dependency => dependency == new WorldHostDependency(WorldHostDependencyKind.Special, 6, 15) ? 6 : null);
        Check(projected.Complete && projected.IsInert && !projected.MayChangeControlledModel, "A concrete model guard ignored its owned read-only host projection.");
        Check(source.AnalyzeModelDispatch(7, 3) is { Complete: true, Entry: 0, IsInert: true }, "A missing resident owner fabricated a model callback.");
        foreach (WorldEntitySnapshot unavailable in new[] { resident with { ScriptDisabled = true }, resident with { Detached = true } })
        {
            source.Restore(empty with { Models = [unavailable] });
            Check(source.AnalyzeModelDispatch(6, 3) is { Complete: true, Entry: 0, IsInert: true }, "A disabled or detached resident admitted a model callback.");
        }
        source.Restore(empty with { Models = [resident with { ProgramCounter = 8, Priority = 1 }] });
        before = JsonSerializer.Serialize(source.Capture());
        Check(source.AnalyzeModelDispatch(6, 3) is { Complete: true, Entry: 0, IsInert: true }, "A busy model admitted a same-priority contact callback.");
        Check(source.AnalyzeModelDispatch(6, 4) is { Complete: true, Entry: 8, MayChangeControlledModel: true }, "A higher-priority model callback was rejected or followed its suspended caller.");
        Check(before == JsonSerializer.Serialize(source.Capture()), "Model preemption analysis changed the live call stack.");
        source.Restore(empty with { Models = [resident with { ProgramCounter = 8, Priority = 1, Calls = [new(8, 0, 1), new(8, 0, 1), new(8, 0, 1), new(8, 0, 1)] }] });
        Check(source.AnalyzeModelDispatch(6, 5) is { Complete: false, Reason: not null }, "An unsupported model call-stack admission was silently accepted.");
        RecordingHost nativeHost = new();
        WorldMachine native = new(new(events), new byte[1280], nativeHost);
        native.Restore(empty with { Models = [resident with { ProgramCounter = 8, Priority = 1 }] });
        native.StartModel(6, 4);
        WorldEntitySnapshot admitted = native.Capture().Models.Single();
        Check(admitted is { ProgramCounter: 8, Priority: 2 } && admitted.Calls.Length == 1, "The model analysis priority assumption differs from native dispatch.");
        native.Restore(empty with { Models = [resident] });
        native.StartModel(6, 3);
        native.Tick();
        Check(nativeHost.Requests.Single(request => request.Command.Operation == WorldOperation.BoardTransport).ActiveModel == 6, "The analyzed boarding callback disagrees with native model ownership.");
    }

    private static void ModelScriptTailOrdering()
    {
        ushort[] code = [0, 0x32E, 0x203, 0x30C, 0x203];
        PcWorldEvents events = new([new(0x4603, 3)], code);
        foreach (int sourcePc in new[] { 0, 1 })
        {
            ModelTailHost host = new();
            WorldMachine machine = new(new(events), new byte[1280], host);
            host.Machine = machine;
            WorldSnapshot empty = machine.Capture();
            machine.Restore(empty with { Models = [new(0, sourcePc, 0, 0, false, 0, []), new(6, 0, 0, 0, false, 0, [])] });
            machine.Tick();
            Check(host.Tails.SequenceEqual(new (int Model, int? FreeIndex)[] { (0, null), (6, null) }), "A native idle or waiting model omitted its per-owner tail, or a system tail was synthesized.");
            Check(host.Operations.SequenceEqual(new[] { "tail:0", "board:6", "tail:6" }), "A contact requested after a source model did not execute on a later native owner in the same tick.");
            Check(sourcePc == 0 || machine.Capture().Models.Single(model => model.Model == 0).ProgramCounter == 1, "The tail hook advanced an unacknowledged native wait.");
        }
        ModelTailHost deletedHost = new() { DispatchContact = false };
        ushort[] deletedCode = [0, 0x31A, 0x110, 6, 0x330, 0x203];
        WorldMachine deleted = new(new(new([], deletedCode)), new byte[1280], deletedHost);
        deletedHost.Machine = deleted;
        WorldSnapshot initial = deleted.Capture();
        deleted.Restore(initial with { Models = [new(0, 1, 0, 0, false, 0, []), new(6, 0, 0, 0, false, 0, [])] });
        deleted.Tick();
        Check(deletedHost.Tails.SequenceEqual(new (int Model, int? FreeIndex)[] { (0, 0) }) && deleted.Capture().Models.Single().Model == 6, "The native model tail used the selected actor instead of its freed execution owner or changed deletion iteration.");
    }

    private sealed class ModelTailHost : IWorldHost
    {
        public WorldMachine Machine { get; set; } = null!;
        public bool DispatchContact { get; init; } = true;
        public List<(int Model, int? FreeIndex)> Tails { get; } = [];
        public List<string> Operations { get; } = [];

        public void ModelScriptCompleted(int model, int? freeModelIndex = null)
        {
            Tails.Add((model, freeModelIndex));
            Operations.Add($"tail:{model}");
            if (DispatchContact && model == 0)
            {
                Machine.StartModel(6, 3);
            }
        }

        public WorldHostReply Execute(WorldHostRequest request)
        {
            if (request.Command.Operation == WorldOperation.BoardTransport)
            {
                Operations.Add($"board:{request.ActiveModel}");
            }
            return new(request.Command.Operation != WorldOperation.WaitWindow);
        }

        public int ReadSpecial(int selector, int activeModel) => throw new NotSupportedException();
    }

    private static void AdmittedScriptDispatchReceipts()
    {
        ushort[] code = [0, 0x110, 0, 0x214, 0x203, 0x110, 3, 0x325, 0x203, 0x203];
        PcWorldEvents events = new([new(0x4604, 1), new(0x4010, 5), new(0x4002, 9)], code);
        RecordingHost host = new();
        WorldMachine machine = new(new(events), new byte[1280], host);
        WorldSnapshot empty = machine.Capture();
        machine.Restore(empty with { Models = [new(0, 0, 0, 0, false, 0, []), new(6, 1, 0, 2, false, 0, [new(4, 0, 0)])] });
        List<WorldScriptDispatchObservation> receipts = [];
        machine.ScriptDispatched += receipts.Add;
        machine.Tick();
        Check(receipts.SequenceEqual(new[] { new WorldScriptDispatchObservation(6, 4, 0, 0x4010, 5, 0, 3, 2, 1) }), "A real opcode214 model request lost its executing source frame, next PC, target header or admitted priority.");
        Check(host.Requests.Count == 0, "A later source owner made an already visited descendant execute during the same tick.");
        machine.StartModel(6, 4);
        machine.StartModel(6, 7);
        Check(receipts.Count == 1, "An equal-priority rejection or absent native header emitted an admitted receipt.");
        WorldSnapshot pending = machine.Capture();
        RecordingHost coldHost = new();
        WorldMachine cold = new(new(events), machine.CopyPersistent(), coldHost);
        cold.Restore(JsonSerializer.Deserialize<WorldSnapshot>(JsonSerializer.Serialize(pending))!);
        machine.Tick();
        cold.Tick();
        Check(host.Requests.Single().Command.Operation == WorldOperation.Text && host.Requests.Single().ActiveModel == 0, "The admitted descendant did not execute on its native owner in the next tick.");
        Check(receipts.Any(receipt => receipt.TargetModel == 0 && receipt.Header == 0x4002 && receipt.Priority == 0), "Native default callback admission did not retain its distinguishable zero priority.");
        Check(JsonSerializer.Serialize(machine.Capture()) == JsonSerializer.Serialize(cold.Capture()) && JsonSerializer.Serialize(host.Requests) == JsonSerializer.Serialize(coldHost.Requests), "Dispatch observation changed legacy cold execution or host operations.");
        int count = receipts.Count;
        machine.AnalyzeModelDispatch(6, 4);
        Check(receipts.Count == count, "Detached model analysis emitted a live native dispatch receipt.");
        machine.StartModel(0, 16);
        WorldScriptDispatchObservation preempt = receipts[^1];
        Check(preempt is { TargetModel: 0, Header: 0x4010, Priority: 3, CallDepth: 1 }, "An admitted preemption receipt lost its suspended caller depth.");
        RecordingHost initialHost = new();
        WorldMachine initial = new(new(new([new(0, 9), new(3, 9), new(4, 9)], code)), new byte[1280], initialHost);
        WorldScriptDispatchObservation? initialization = null;
        initial.ScriptDispatched += receipt => initialization = receipt;
        initial.StartSystem(0);
        Check(initialization is { SourceModel: -1, SourceProgramCounter: 0, TargetModel: -1, Header: 0, Entry: 9 }, "Initial dispatch did not report its ordinary system owner safely.");
        initial.StartSystem(3);
        initial.StartSystem(4);
        Check(initialization is { SourceModel: -1, SourceProgramCounter: 9, SourcePriority: 1, SourceCallDepth: 1, TargetModel: -1, Priority: 2, CallDepth: 2 }, "Same-owner preemption captured the mutated target frame instead of the native caller frame.");
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
