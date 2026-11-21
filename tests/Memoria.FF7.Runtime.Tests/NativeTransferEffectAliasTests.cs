using System.Numerics;
using System.Text.Json;
using Memoria.AutoPlay;
using Memoria.FF7.Events;
using Memoria.FF7.Runtime;

internal static class NativeTransferEffectAliasTests
{
    internal static void Run()
    {
        byte[] transfer = [0x60, 2, 2, 0, 0, 0, 0, 0, 0, 0, 0];
        byte[] prefix = [0x33, 1, 0x4A, 1, 0x6B, 0, 0, 0, 0, 0, 8, 2, 0, 0x6C];
        FieldScriptAnalysis analysis = Analyze([.. prefix, .. transfer]);
        Check(analysis.Complete && analysis.DeterministicTransfer && !analysis.InputIndependent, "A literal control/fade/transfer body did not acquire its distinct deterministic-transfer certificate.");
        FieldScriptEntryDependencies dependencies = new(true, null, [], []);
        string? Key(FieldScriptAnalysis facts, string program = "program", int entry = 2, int priority = 1, string guard = "guard", bool input = false) => Ff7AutoPlayDiscovery.ReadEffectFingerprint(program, 1, entry, priority, guard, "host", facts, dependencies, true, input);
        string key = Key(analysis) ?? throw new InvalidOperationException("Missing proved transfer effect identity.");
        Check(Key(analysis, program: "other") != key && Key(analysis, entry: 3) != key && Key(analysis, guard: "changed") != key, "A different program, entry or current guard inherited transfer identity.");
        Check(Key(analysis, priority: 2) is null && Key(analysis, input: true) is null && Key(analysis with { Complete = false }) is null, "An unproved priority, input or incomplete transfer acquired an alias.");
        foreach (byte[] disallowed in new byte[][]
        {
            [0x30, 0x20, 0, 1, 0],
            [1, 0, 0xC0],
            [0x99, 5, 9],
            [0x80, 0x50, 10, 1],
            [0x81, 0x10, 0, 0x82, 3],
            [0xA1, 0],
            [0x49, 0, 0, 0]
        })
        {
            FieldScriptAnalysis rejected = Analyze([.. disallowed, .. prefix, .. transfer]);
            Check(!rejected.DeterministicTransfer && Key(rejected) is null, "Input, request, RNG, variable/story write, binding or menu work inherited the deterministic-transfer certificate.");
        }
        FieldScriptAnalysis looping = Analyze([.. prefix, 0x12, 0, .. transfer]);
        Check(!looping.DeterministicTransfer && Key(looping) is null, "A backward polling cycle inherited deterministic transfer equivalence.");
        Ff7AutoPlayTarget talk = new("talk", 1, 1, Ff7AutoPlayTargetKind.LineTalk, "talk-condition", false, Vector3.Zero) { EffectFingerprint = key };
        Ff7AutoPlayTarget push = talk with { Id = "push", Script = 2, Kind = Ff7AutoPlayTargetKind.LinePush, ConditionFingerprint = "push-condition" };
        Ff7AutoPlayActiveEvent planned = new("source", talk.Id, talk.ConditionFingerprint, 1, 1, 1, talk.Kind, null, 1, "before");
        FieldExternalScriptAdmission admission = new(1, 2, 1, true, true);
        Ff7AutoPlayActiveEvent adopted = Ff7AutoPlayPolicy.AdoptEffectAlias(planned, talk, push, admission, 2, 2) ?? throw new InvalidOperationException("A real started native transfer alias was not adopted.");
        Check(adopted.EventId == push.Id && adopted.Script == 2 && !adopted.Accepted && !adopted.Returned && !adopted.CompletionRecorded && !adopted.TransitionOwned, "Effect identity manufactured native admission, return or route ownership.");
        Check(Ff7AutoPlayPolicy.AdoptEffectAlias(planned, talk, push, admission, 2, 3) is null && Ff7AutoPlayPolicy.AdoptEffectAlias(planned, talk, push, admission with { Priority = 2 }, 2, 2) is null && Ff7AutoPlayPolicy.AdoptEffectAlias(planned, talk, push, admission with { Started = false }, 2, 2) is null, "A different entry, priority or unstarted alias replaced the plan.");
        Check(Ff7AutoPlayPolicy.AdoptEffectAlias(planned, talk, push with { EffectFingerprint = Key(analysis, program: "other") }, admission, 2, 2) is null && Ff7AutoPlayPolicy.AdoptEffectAlias(planned, talk, push with { EffectFingerprint = Key(analysis, guard: "changed") }, admission, 2, 2) is null, "A different program or current guard received transfer adoption.");
        Ff7AutoPlayState state = new();
        var planner = Ff7AutoPlayPolicy.CreatePlanner(state);
        AutoPlayEvent first = new(talk.Id, AutoPlayEventKind.Interaction, ConditionSignature: talk.ConditionFingerprint) { EffectFingerprint = key };
        AutoPlayEvent second = new(push.Id, AutoPlayEventKind.Interaction, ConditionSignature: push.ConditionFingerprint) { EffectFingerprint = key };
        planner.Observe(new("source", "program"), [first, second]);
        Check(!Ff7AutoPlayPolicy.HasConsumedEffectAlias(state, "source", first), "Offered equivalent entries received unexecuted completion credit.");
        planner.Completed("source", second.Id, "destination", observedStoryChanged: false, entrySignature: push.ConditionFingerprint, entrySignatureAuthoritative: true, targetId: second.Id);
        string history = JsonSerializer.Serialize(state.Coverage);
        Check(Ff7AutoPlayPolicy.HasConsumedEffectAlias(state, "source", first), "A genuinely completed matching transfer did not suppress duplicate effect novelty.");
        var untouched = state.Coverage.Visits.Single().Events.Single(item => item.Event.Id == first.Id);
        Check(!untouched.Completed && untouched.Routes.Count == 0 && untouched.Conditions!.ConsumedEntries.Count == 0 && history == JsonSerializer.Serialize(state.Coverage), "Alias lookup forged source completion or route history for the unadmitted variant.");
        Console.WriteLine("Deterministic transfer aliases retain exact entry guards and real native completion boundaries.");
    }

    private static FieldScriptAnalysis Analyze(byte[] body)
    {
        ushort[][] entries = [new ushort[32], new ushort[32]];
        entries[1][1] = entries[1][2] = 2;
        FieldMachine machine = new(new FieldProgram([0, 0, .. body], entries));
        machine.Initialize();
        string before = JsonSerializer.Serialize(machine.Capture());
        FieldScriptAnalysis analysis = machine.AnalyzeScript(1, 1);
        Check(before == JsonSerializer.Serialize(machine.Capture()), "Transfer analysis changed native source state.");
        return analysis;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
