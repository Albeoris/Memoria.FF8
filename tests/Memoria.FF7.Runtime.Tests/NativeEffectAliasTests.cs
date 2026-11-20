using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using Memoria.AutoPlay;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeEffectAliasTests
{
    internal static void Run()
    {
        FieldScriptAnalysis analysis = Analyze([0x60, 2, 2, 0, 0, 0, 0, 0, 0, 0, 0]);
        Check(analysis.Complete && analysis.InputIndependent, "A deterministic native transition was not eligible for input-independent effect identity.");
        FieldScriptEntryDependencies dependencies = new(true, null, [], []);
        string? Key(FieldScriptAnalysis facts, string program = "program", int entry = 2, int priority = 1, string guard = "guard", bool input = false) => Ff7AutoPlayDiscovery.ReadEffectFingerprint(program, 1, entry, priority, guard, "host", facts, dependencies, true, input);
        string key = Key(analysis) ?? throw new InvalidOperationException("Missing effect identity.");
        Check(Key(analysis, program: "other") != key && Key(analysis, entry: 3) != key && Key(analysis, guard: "changed") != key, "A different program, entry or canonical condition reused an effect identity.");
        Check(Key(analysis, priority: 2) is null && Key(analysis, input: true) is null && Key(analysis with { Complete = false }) is null && Key(analysis with { HostReads = [new(FieldHostReadKind.Random)] }) is null, "An unproved priority, input, stochastic or unsupported host dependency created an effect alias.");
        foreach (byte[] code in new byte[][] { [0x30, 0x20, 0, 2, 0, 0], [0x40, 0, 1, 0], [0x48, 5, 0, 1, 0, 1, 10, 0], [0x49, 0, 14, 0, 0], [0x70, 0, 1, 0, 0], [0x99, 0x01, 10, 0] })
        {
            FieldScriptAnalysis dependent = Analyze(code);
            Check(Key(dependent) is null, "A button, dialogue, choice, menu, battle or random host effect was incorrectly input-independent.");
        }
        Ff7AutoPlayTarget talk = new("talk", 1, 1, Ff7AutoPlayTargetKind.LineTalk, "talk-condition", false, Vector3.Zero) { EffectFingerprint = key };
        Ff7AutoPlayTarget push = talk with { Id = "push", Script = 2, Kind = Ff7AutoPlayTargetKind.LinePush, ConditionFingerprint = "push-condition" };
        Ff7AutoPlayActiveEvent planned = new("source", talk.Id, talk.ConditionFingerprint, 1, 1, 1, talk.Kind, null, 641, "before");
        FieldExternalScriptAdmission admission = new(1, 2, 1, true, true);
        Ff7AutoPlayActiveEvent actual = Ff7AutoPlayPolicy.AdoptEffectAlias(planned, talk, push, admission, 2, 2) ?? throw new InvalidOperationException("A genuine alias owner could not replace its unaccepted duplicate plan.");
        Check(actual.EventId == push.Id && actual.Script == 2 && !actual.Accepted && !actual.CompletionRecorded && !actual.TransitionOwned, "Adopting an actual owner manufactured acceptance or completion before the normal admission path.");
        Check(Ff7AutoPlayPolicy.AdoptEffectAlias(planned with { Accepted = true }, talk, push, admission, 2, 2) is null && Ff7AutoPlayPolicy.AdoptEffectAlias(planned, talk, push, admission with { Priority = 2 }, 2, 2) is null && Ff7AutoPlayPolicy.AdoptEffectAlias(planned, talk, push, admission with { Started = false }, 2, 2) is null, "An accepted, wrong-priority or not-started owner was replaced by an alias.");
        Check(Ff7AutoPlayPolicy.AdoptEffectAlias(planned, talk, push, admission, 2, 3) is null && Ff7AutoPlayPolicy.AdoptEffectAlias(planned, talk with { ConditionFingerprint = "stale" }, push, admission, 2, 2) is null && Ff7AutoPlayPolicy.AdoptEffectAlias(planned, talk, push with { EffectFingerprint = Key(analysis, guard: "changed") }, admission, 2, 2) is null, "A different native entry or stale canonical/current condition was adopted.");
        Ff7AutoPlayState state = new();
        AutoPlayPlanner<Ff7AutoPlayDeferral> planner = new(state.Coverage);
        AutoPlayEvent first = new(talk.Id, AutoPlayEventKind.Interaction, ConditionSignature: talk.ConditionFingerprint) { EffectFingerprint = key };
        AutoPlayEvent second = new(push.Id, AutoPlayEventKind.Interaction, ConditionSignature: push.ConditionFingerprint) { EffectFingerprint = key };
        planner.Observe(new("source", "program"), [first, second]);
        Check(!Ff7AutoPlayPolicy.HasConsumedEffectAlias(state, "source", first), "An unexecuted duplicate suppressed a real frontier.");
        planner.BeginEvent("source", second.Id);
        planner.Completed("source", second.Id, observedStoryChanged: false, entrySignature: second.ConditionSignature, entrySignatureAuthoritative: true);
        Check(Ff7AutoPlayPolicy.HasConsumedEffectAlias(state, "source", first), "A real consumed same-condition effect did not exclude its duplicate frontier.");
        Check(!state.Coverage.Visits.Single().Events.Single(item => item.Event.Id == first.Id).Completed, "Effect alias accounting gave the unadmitted duplicate fake completion credit.");
        Check(!Ff7AutoPlayPolicy.HasConsumedEffectAlias(state, "source", first with { EffectFingerprint = "different" }), "A different canonical guard inherited duplicate suppression.");
        AutoPlayEvent legacy = JsonSerializer.Deserialize<AutoPlayEvent>("{\"Id\":\"legacy\",\"Kind\":0,\"ConditionSignature\":\"unchanged\"}")!;
        Check(legacy.EffectFingerprint is null && legacy.ConditionSignature == "unchanged" && !Ff7AutoPlayPolicy.HasConsumedEffectAlias(state, "source", legacy), "Old metadata-null observations changed condition identity or gained alias suppression.");
        Console.WriteLine("PASS native effect alias identity, input/host exclusions, real-owner adoption, current consumption and old metadata compatibility");
    }

    private static FieldScriptAnalysis Analyze(byte[] body)
    {
        ushort[][] entries = [new ushort[32], new ushort[32]];
        entries[1][1] = entries[1][2] = 2;
        FieldMachine machine = new(new FieldProgram([0, 0, .. body], entries));
        machine.Initialize();
        return machine.AnalyzeScript(1, 1);
    }

    internal static void RunOriginal()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_EFFECT_ALIAS_SAVE") ?? throw new InvalidOperationException("Select an external native alias fixture.");
        string game = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR") ?? throw new InvalidOperationException("Original resources are required.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(game), CombinedSessionCodec.Read(input).Ff7!);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("Effect alias continuation requested an optional checkpoint."));
        int sourceField = session.MapId;
        Ff7AutoPlayObservation source = new Ff7AutoPlayDiscovery(session).Observe();
        Ff7AutoPlayTarget talk = source.Events.Single(target => target.Kind == Ff7AutoPlayTargetKind.LineTalk && target.Id == policy.State.NavigationGoal?.EventId);
        Ff7AutoPlayTarget push = source.Events.Single(target => target.Entity == talk.Entity && target.Script == 2);
        Check(talk.EffectFingerprint is not null && talk.EffectFingerprint == push.EffectFingerprint, "The original entry aliases did not have a complete input-independent canonical guard proof.");
        bool admittedActual = false;
        string? admittedCondition = null;
        session.InteractionScriptRequested += (entity, script, accepted) =>
        {
            if (accepted && entity == push.Entity && script == push.Script)
            {
                Check(policy.State.Active is { Accepted: true } actual && actual.EventId == push.Id && actual.Script == push.Script, "The real Push owner was not recorded by the normal native admission path.");
                admittedActual = true;
                admittedCondition = policy.State.Active!.ConditionFingerprint;
            }
        };
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, chooseDialogueSelection: policy.ChooseDialogueSelection, readPlanningProgress: () => policy.WorkProgress);
        controller.Start();
        Stopwatch timer = Stopwatch.StartNew();
        bool progressed = false;
        while (controller.Running && timer.Elapsed < TimeSpan.FromSeconds(30))
        {
            controller.Advance(1);
            AutoPlayEventCoverage<Ff7AutoPlayDeferral> actualEffect = policy.State.Coverage.Visits.Single(visit => visit.Visit.Key == source.VisitId).Events.Single(item => item.Event.Id == push.Id);
            AutoPlayEventRoute? actualRoute = actualEffect.Routes.LastOrDefault(route => route.ConditionSignature == admittedCondition);
            int destination = actualRoute is not null && int.TryParse(actualRoute.DestinationVisitKey.Split(':')[1], out int field) ? field : sourceField;
            if (admittedActual && actualEffect.Completed && actualRoute is not null && (session.World is not null || session.MapId != sourceField && session.MapId != destination))
            {
                progressed = true;
                break;
            }
            if (admittedActual && actualEffect.Completed && policy.State.Active is { Accepted: true } next && next.EventId != talk.Id && next.EventId != push.Id && next.Kind != Ff7AutoPlayTargetKind.Gateway)
            {
                progressed = true;
                break;
            }
        }
        AutoPlayVisitCoverage<Ff7AutoPlayDeferral> visit = policy.State.Coverage.Visits.Single(visit => visit.Visit.Key == source.VisitId);
        Check(controller.Error is null && progressed && visit.Events.Single(item => item.Event.Id == push.Id).Completed, $"Original alias accounting did not escape its repeated field route: {controller.Error}; status={policy.Status}; steps={controller.Steps}.");
        Check(!visit.Events.Single(item => item.Event.Id == talk.Id).Completed && !visit.Events.Single(item => item.Event.Id == talk.Id).Conditions!.HasExecutedEntry(talk.ConditionFingerprint), "The unadmitted Talk alias received fake completion credit.");
        Check(bytes.AsSpan().SequenceEqual(File.ReadAllBytes(path)), "Alias continuation changed its authoritative source.");
        Console.WriteLine($"PASS original actual-owner effect accounting escapes repeated route without Talk credit; steps={controller.Steps}; elapsed={timer.Elapsed.TotalSeconds:F3}s; source unchanged");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
