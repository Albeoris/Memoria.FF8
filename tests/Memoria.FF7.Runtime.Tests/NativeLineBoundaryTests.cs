using System.Diagnostics;
using System.Text.Json;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeLineBoundaryTests
{
    internal static void RunOriginal()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_LINE_BOUNDARY_SAVE") ?? throw new InvalidOperationException("Select a native LINE boundary fixture.");
        string game = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR") ?? throw new InvalidOperationException("Select original resources.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, false);
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(game), CombinedSessionCodec.Read(input).Ff7!);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("The native LINE continuation reached an unsupported choice boundary."));
        var old = policy.State.Active ?? throw new InvalidOperationException("The fixture lacks an ordinary native LINE owner.");
        var visit = policy.State.Coverage.Visits.Single(visit => visit.Visit.Key == old.VisitId);
        var requested = visit.Events.Single(item => item.Event.Id == old.EventId);
        string history = JsonSerializer.Serialize(new { requested.Completed, requested.Routes, requested.Conditions!.ConsumedEntries, requested.Conditions.ConsumedExits });
        string? actualId = null;
        string? actualCondition = null;
        HashSet<long> initialMessages = session.Host.Process.Dialogues.Select(dialogue => dialogue.RequestId).ToHashSet();
        HashSet<long> newMessages = [];
        session.InteractionScriptRequested += (entity, script, accepted) =>
        {
            if (entity == old.Entity && script != old.Script && accepted && policy.State.Active is { Accepted: true } actual && actual.Script == script)
            {
                if (!new Ff7AutoPlayScriptedInput(session).IsEntryInputSatisfied(entity, script))
                {
                    throw new InvalidOperationException("A sibling alias adopted an unsatisfied native entry input.");
                }
                actualId = actual.EventId;
                actualCondition = actual.ConditionFingerprint;
            }
        };
        bool Released() => !session.Host.Process.PlayerLocked && session.Host.Process.Dialogues.All(dialogue => dialogue.Completed) && policy.State.Active?.Entity != old.Entity;
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, advanceTick: () => session.AdvanceTick(advanceClock: false), chooseDialogueSelection: policy.ChooseDialogueSelection, readPlanningProgress: () => policy.WorkProgress, readFrontierProgress: () => policy.FrontierProgress);
        controller.Start();
        Stopwatch elapsed = Stopwatch.StartNew();
        TimeSpan previous = TimeSpan.Zero;
        for (int poll = 0; poll < 2048 && controller.Running && !Released() && controller.Steps < 192 && elapsed.Elapsed < TimeSpan.FromSeconds(10); poll++)
        {
            TimeSpan now = elapsed.Elapsed;
            session.AdvanceClock(now - previous);
            previous = now;
            controller.Advance(1);
            foreach (var dialogue in session.Host.Process.Dialogues.Where(dialogue => !initialMessages.Contains(dialogue.RequestId)))
            {
                newMessages.Add(dialogue.RequestId);
            }
        }
        if (controller.Error is not null || !Released() || newMessages.Count > 1)
        {
            throw new InvalidOperationException($"The bounded LINE alias continuation did not complete one actual sibling: steps={controller.Steps}, actual={actualId}, condition={actualCondition}, messages={newMessages.Count}, status={policy.Status}, error={controller.Error}.");
        }
        var observed = new Ff7AutoPlayDiscovery(session).Observe();
        var current = observed.Events.Single(item => item.Id == old.EventId);
        var nativeEvent = requested.Event with { ConditionSignature = current.ConditionFingerprint, EffectFingerprint = current.EffectFingerprint };
        var witness = visit.Events.FirstOrDefault(item => item.Event.Id != old.EventId && item.Completed && item.Event.EffectFingerprint == current.EffectFingerprint && item.Event.ConditionSignature is string condition && item.Conditions?.HasExecutedEntry(condition) == true);
        var actualTarget = observed.Events.FirstOrDefault(item => item.Id == witness?.Event.Id);
        if (witness is null || actualTarget?.EntryInputContract is null || actualTarget.ConditionFingerprint != witness.Event.ConditionSignature)
        {
            throw new InvalidOperationException("A historical sibling lacked its authoritative current entry-button condition and real completion.");
        }
        if (!Ff7AutoPlayPolicy.HasConsumedEffectAlias(policy.State, old.VisitId, nativeEvent) || history != JsonSerializer.Serialize(new { requested.Completed, requested.Routes, requested.Conditions!.ConsumedEntries, requested.Conditions.ConsumedExits }) || !bytes.AsSpan().SequenceEqual(File.ReadAllBytes(path)))
        {
            throw new InvalidOperationException("Real sibling completion did not yield honest duplicate availability, or changed the requested owner's history/source.");
        }
        Console.WriteLine($"PASS original native entry-button sibling actual admission/return; callbacks={controller.Steps}; actual={witness.Event.Id}; condition={witness.Event.ConditionSignature}; newlyAdmitted={actualId}; newMessages={newMessages.Count}; elapsed={elapsed.Elapsed.TotalSeconds:F3}s; requested owner/source unchanged");
    }
}
