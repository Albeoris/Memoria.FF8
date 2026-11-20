using System.Diagnostics;
using System.Text.Json;
using Memoria.AutoPlay;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class WorldSourceChoiceTests
{
    public static void Run()
    {
        const string visit = "ff7:10:program";
        Ff7AutoPlayState state = new();
        AutoPlayPlanner<Ff7AutoPlayDeferral> planner = Ff7AutoPlayPolicy.CreatePlanner(state);
        planner.Observe(new(visit, "field"), [new("actor", AutoPlayEventKind.Interaction, ConditionSignature: "source")]);
        planner.BeginEvent(visit, "actor");
        planner.Completed(visit, "actor", entrySignature: "source", entrySignatureAuthoritative: true);
        Ff7AutoPlayActiveEvent active = new(visit, "actor", "source", 4, 0, 1, Ff7AutoPlayTargetKind.ActorTalk, null, 641, "before", Accepted: true, Returned: true, NavigationKey: "source-component", NavigationProbeKey: "unrecorded-probe");
        state.Active = active;
        Ff7AutoPlayActiveEvent before = active with { Returned = false };
        Ff7AutoPlayChoiceState parent = new("parent", before, OwnerIdentity: new('1', 64));
        Ff7AutoPlayChoiceState child = new("child", before, parent, active with { CompletionRecorded = true }, new('2', 64));
        state.Choice = child;
        state.Coverage.Choices.Add(new() { Key = "child", FirstOption = 1, OptionCount = 2, ActiveOption = 2, CompletedOptions = [1], BeforeCheckpoint = "child-before", ContinuationCheckpoint = "child-primary" });
        state.Coverage.Choices.Add(new() { Key = "parent", FirstOption = 2, OptionCount = 2, ActiveOption = 2, BeforeCheckpoint = "parent-before" });
        WorldEntitySnapshot idle = new(-1, 0, 0, 0, false, 0, []);
        WorldSnapshot execution = new(1, "world", 0, 1, false, new byte[64], [], idle, []);
        Ff7WorldProcess world = new(0, 0, 7, execution, [], 0, true, 0, 0, null, [], -1, new(), 0, 1);
        FieldEntitySnapshot returned = new(2, 7, 1, new int[8], new FieldSyncState[8], Enumerable.Repeat(-1, 8).ToArray());
        FieldEntitySnapshot transfer = returned with { ProgramCounter = 10, Priority = 6, WaitCounter = 0 };
        FieldTransitionCommand transition = new(7, 0, 0, 0, 0);
        FieldHostRequest request = new(1, 1, 6, 10, transition);
        FieldVmSnapshot vm = new(1, FieldMachine.SemanticProfile, "program", 5, true, new byte[1280], new byte[256], [returned, transfer], PendingRequests: [request]);
        Ff7FieldProcess process = new() { GlobalCommand = Ff7GlobalCommand.Transition, GlobalRequestId = 1, Transition = transition, Receipts = [new(request, new(true))] };
        bool Can(FieldVmSnapshot? native = null, Ff7FieldProcess? facts = null, Func<string, bool>? exists = null) => Ff7AutoPlayPolicy.CanDetachReturnedWorldChoiceSource(state, 10, 4, world, native ?? vm, facts ?? process, exists ?? (_ => true));
        string original = JsonSerializer.Serialize(new { state, vm, process, world });
        Check(Can(), "A returned source choice with an authenticated completed handoff was pinned.");
        state.Active = active with { CompletionRecorded = true };
        Ff7AutoPlayState completed = state.Clone();
        completed.Active = null;
        Check(Can() && Ff7AutoPlayPolicy.TryDetachReturnedWorldChoiceSource(state, 10, 4, world, vm, process, _ => true) && JsonSerializer.Serialize(state) == JsonSerializer.Serialize(completed), "Already recorded live source completion prevented an otherwise identical detach-only handoff.");
        state.Active = active;
        foreach (Ff7AutoPlayActiveEvent wrong in new[] { active with { Accepted = false }, active with { Returned = false }, active with { Entity = 1 }, active with { ConditionFingerprint = "other" }, active with { Generation = 5 } })
        {
            state.Active = wrong;
            Check(!Can(), "Wrong source ownership authenticated live-task detachment.");
        }
        state.Active = active;
        Check(!Can(vm with { ProgramIdentity = "other" }) && !Can(vm with { Entities = [returned with { Priority = 1 }, transfer] }), "A wrong program or unfinished parent authenticated detachment.");
        Check(!Can(facts: process with { Receipts = [] }) && !Can(facts: process with { Receipts = [new(request, new(false))] }) && !Can(vm with { PendingRequests = [request with { ProgramCounter = 11 }] }), "A missing, incomplete or mismatched native handoff authenticated detachment.");
        Check(!Can(exists: path => path != "child-primary"), "A missing native continuation dependency was ignored.");
        state.Choice = child with { BeforeActive = before with { Script = 2 } };
        Check(!Can(), "A different choice root authenticated detachment.");
        state.Choice = child with { ContinuationActive = active };
        Check(!Can(), "An unrecorded primary continuation authenticated detachment.");
        state.Choice = child;
        var branch = state.Coverage.Choices.Single(choice => choice.Key == "child");
        branch.ActiveOption = 1;
        Check(!Can(), "An already completed option was treated as current unaccounted execution.");
        branch.ActiveOption = 2;
        Check(original == JsonSerializer.Serialize(new { state, vm, process, world }), "Handoff qualification changed native facts or choice history.");
        Ff7AutoPlayState expected = state.Clone();
        expected.Active = null;
        Check(Ff7AutoPlayPolicy.TryDetachReturnedWorldChoiceSource(state, 10, 4, world, vm, process, _ => true) && JsonSerializer.Serialize(state) == JsonSerializer.Serialize(expected), "Detachment changed something besides the stale live planning task.");
        Check(!Ff7AutoPlayPolicy.TryDetachReturnedWorldChoiceSource(state, 10, 4, world, vm, process, _ => true), "The same live task detached twice.");
        RunOriginal();
        Console.WriteLine("PASS returned field choice handoff preserves native ownership and nested outcomes");
    }

    private static void RunOriginal()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_WORLD_SOURCE_CHOICE_SAVE");
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("SKIP external returned field choice handoff.");
            return;
        }
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream stream = new(bytes, writable: false);
        Ff7SessionSave saved = CombinedSessionCodec.Read(stream).Ff7!;
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(root), saved);
        Ff7AutoPlayChoiceState choice = saved.AutoPlay!.Choice!;
        string[] dependencies = saved.AutoPlay.Coverage.Choices.Where(item => item.Key == choice.Key || item.Key == choice.Parent!.Key).SelectMany(item => new[] { item.BeforeCheckpoint, item.ContinuationCheckpoint }).OfType<string>().Distinct(StringComparer.Ordinal).ToArray();
        Dictionary<string, string> hashes = dependencies.ToDictionary(file => file, file => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(file))), StringComparer.Ordinal);
        Ff7AutoPlayState detached = saved.AutoPlay.Clone();
        Ff7AutoPlayState expected = detached.Clone();
        expected.Active = null;
        string native = JsonSerializer.Serialize(new { Vm = session.Machine.Capture(), Process = session.Host.Capture(), World = session.Capture().World, session.Progress });
        Check(Ff7AutoPlayPolicy.TryDetachReturnedWorldChoiceSource(detached, session.MapId, session.FieldGeneration, session.World!.Process, session.Machine.Capture(), session.Host.Capture(), File.Exists), "The current returned source was not authenticated.");
        Check(JsonSerializer.Serialize(detached) == JsonSerializer.Serialize(expected) && native == JsonSerializer.Serialize(new { Vm = session.Machine.Capture(), Process = session.Host.Capture(), World = session.Capture().World, session.Progress }), "Detachment changed choice history, native state or progress.");
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("The existing primary continuation must not be overwritten."));
        policy.State.FrontierIdleSteps = 0;
        policy.State.FrontierProgressKey = null;
        string eventHistory = JsonSerializer.Serialize(policy.State.Coverage.Visits.Single(visit => visit.Visit.Key == saved.AutoPlay.Active!.VisitId).Events.Single(item => item.Event.Id == saved.AutoPlay.Active!.EventId));
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, chooseDialogueSelection: policy.ChooseDialogueSelection, advanceTick: () => session.AdvanceTick(advanceClock: false));
        controller.Start();
        Stopwatch elapsed = Stopwatch.StartNew();
        int polls = 0;
        while (controller.Running && policy.State.PendingRestore is null && polls++ < 16 && elapsed.Elapsed < TimeSpan.FromSeconds(3))
        {
            controller.Advance(1);
        }
        var child = policy.State.Coverage.Choices.Single(item => item.Key == choice.Key);
        var parent = policy.State.Coverage.Choices.Single(item => item.Key == choice.Parent!.Key);
        Console.WriteLine($"External source choice: {elapsed.Elapsed.TotalSeconds:F3}s, {polls} polls, {controller.Steps} callbacks, restore {policy.State.PendingRestore is not null}, child [{string.Join(',', child.CompletedOptions)}], parent [{string.Join(',', parent.CompletedOptions)}], error {controller.Error}");
        Check(controller.Error is null && policy.State.PendingRestore?.Path == saved.AutoPlay.Coverage.Choices.Single(item => item.Key == choice.Key).ContinuationCheckpoint && child.CompletedOptions.Order().SequenceEqual(new[] { 1, 2 }) && parent.CompletedOptions.Count == 0, "The current actual option did not finish through the existing workflow, or parent outcomes were invented.");
        Check(eventHistory == JsonSerializer.Serialize(policy.State.Coverage.Visits.Single(visit => visit.Visit.Key == saved.AutoPlay.Active!.VisitId).Events.Single(item => item.Event.Id == saved.AutoPlay.Active!.EventId)), "Source completion or World route credit was fabricated.");
        Check(bytes.AsSpan().SequenceEqual(File.ReadAllBytes(path)) && hashes.All(item => item.Value == Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(item.Key)))) , "The source or original choice dependencies changed.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
