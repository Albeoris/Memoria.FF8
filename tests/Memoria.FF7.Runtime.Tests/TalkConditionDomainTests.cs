using System.Numerics;
using System.Diagnostics;
using System.Reflection;
using Memoria.FF7.Resources;
using System.Text.Json;
using Memoria.AutoPlay;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Runtime;

internal static class TalkConditionDomainTests
{
    internal static void Run()
    {
        Check(Ff7AutoPlayPolicy.ReadTalkContactPlanKind(false, 0) == Ff7TalkContactPlanKind.Unsupported && Ff7AutoPlayPolicy.ReadTalkContactPlanKind(false, 1) == Ff7TalkContactPlanKind.Unsupported, "An unsupported condition acquired an AnyFresh contact job.");
        Check(Ff7AutoPlayPolicy.ReadTalkContactPlanKind(true, 1) == Ff7TalkContactPlanKind.FreshWitness && Ff7AutoPlayPolicy.ReadTalkContactPlanKind(true, 0) == Ff7TalkContactPlanKind.UnknownNoFreshWitness, "A supported domain without fresh standing witnesses lost its unknown state.");
        Ff7AutoPlayActiveEvent owner = new("visit", "actor", "source", 1, 15, 1, Ff7AutoPlayTargetKind.ActorTalk, null, 641, "before");
        Check(Ff7AutoPlayPolicy.OwnsUnknownTalkContactSearch(owner, owner) && !Ff7AutoPlayPolicy.OwnsUnknownTalkContactSearch(owner, null), "Unknown contact ownership was lost or invented without a real prepared job.");
        Check(!Ff7AutoPlayPolicy.OwnsUnknownTalkContactSearch(owner with { EventId = "other" }, owner) && !Ff7AutoPlayPolicy.OwnsUnknownTalkContactSearch(owner with { Accepted = true }, owner), "An unknown physical goal suppressed a different or accepted native owner.");
        PcWalkmeshTriangle outside = new(0, new(-498, 418, 0), new(-442, 453, 0), new(-448, 386, 0), -1, -1, -1);
        Check(!Ff7AutoPlayPolicy.MayContainTalkContact(outside, -320, 564, 0, 164), "A rectangular-corner false contact survived the native distance bound.");
        Check(Ff7AutoPlayPolicy.MayContainTalkContact(outside, -320, 564, 0, 166), "The conservative domain discarded an interior circle intersection.");
        FieldScriptAnalysis guarded = new(true, null, "program", false, [], [], []) { InstructionSites = [new(1, 1)] };
        foreach (byte input in new byte[] { 0x30, 0x31, 0x32 })
        {
            Check(Ff7AutoPlayDiscovery.HasTalkContactInputReads(guarded, [0, input, 0]), "A later native keyboard guard acquired triangle-only closure.");
        }
        Check(!Ff7AutoPlayDiscovery.HasTalkContactInputReads(guarded, [0, 0x40, 0]), "An ordinary dialogue was misclassified as a native keyboard guard.");
        FieldHostRead[] triangle = [new(FieldHostReadKind.Actor, 8, (int)FieldActorQueryKind.Triangle)];
        Check(Ff7AutoPlayPolicy.SupportsTalkContactProjection(triangle, 8), "A finite controlled-player triangle dependency was not supported.");
        Check(!Ff7AutoPlayPolicy.SupportsTalkContactProjection([new(FieldHostReadKind.Actor, 8, (int)FieldActorQueryKind.Direction)], 8), "A direction-dependent reward was treated as triangle-only.");
        Check(!Ff7AutoPlayPolicy.SupportsTalkContactProjection(triangle, 9) && !Ff7AutoPlayPolicy.SupportsTalkContactProjection([], 8), "An unknown/different native role acquired a contact domain.");
        AutoPlayConditionCoverage coverage = new() { ConsumedEntries = ["actual-a"] };
        Ff7AutoPlayTalkConditionDomain domain = new("spawn", "guard", "context", [], triangle, new() { [1] = "actual-a", [2] = "actual-b" }, Complete: true);
        Check(!Ff7AutoPlayPolicy.HasConsumedTalkContactDomain(domain, "spawn", "guard", "context", false, coverage), "One sampled consumed contact suppressed an unconsumed contact condition.");
        coverage.ConsumedEntries.Add("actual-b");
        Check(!Ff7AutoPlayPolicy.HasConsumedTalkContactDomain(domain with { Complete = false }, "spawn", "guard", "context", false, coverage), "A standing-only domain suppressed unproved rejected-move triangle labels.");
        string before = JsonSerializer.Serialize(coverage);
        Check(Ff7AutoPlayPolicy.HasConsumedTalkContactDomain(domain, "spawn", "guard", "context", false, coverage) && before == JsonSerializer.Serialize(coverage) && !coverage.HasExecutedEntry("spawn"), "Derived availability fabricated spawn-condition execution.");
        Check(!Ff7AutoPlayPolicy.HasConsumedTalkContactDomain(domain, "spawn", "changed", "context", false, coverage) && !Ff7AutoPlayPolicy.HasConsumedTalkContactDomain(domain, "spawn", "guard", "changed", false, coverage) && !Ff7AutoPlayPolicy.HasConsumedTalkContactDomain(domain, "spawn", "guard", "context", true, coverage), "An obsolete context retained derived availability.");
        Check(!Ff7AutoPlayPolicy.HasConsumedTalkContactDomain(null, "spawn", "guard", "context", false, coverage), "Absent old-save metadata invented closure.");
        Console.WriteLine("PASS conservative native talk triangle domain, physical dependency scope and no-credit conditional closure");
    }

    internal static void RunOriginal(bool searchOwnership = false)
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_TALK_CONDITION_SAVE") ?? throw new InvalidOperationException("Select a native talk-condition fixture.");
        string game = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR") ?? throw new InvalidOperationException("Select original resources.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        CombinedSessionSave saved = CombinedSessionCodec.Read(input);
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(game), saved.Ff7!);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("The native admission test reached an unhandled choice checkpoint."));
        int initialMap = session.MapId;
        var old = policy.State.NavigationGoal ?? throw new InvalidOperationException("The fixture lacks its actual planned interaction.");
        string targetVisit = policy.State.Coverage.NavigationVisits.Single(visit => visit.Key == old.NavigationKey).VisitKey;
        int targetMap = int.Parse(targetVisit.Split(':')[1], System.Globalization.CultureInfo.InvariantCulture);
        var coverage = policy.State.Coverage.Visits.Single(visit => visit.Visit.Key == targetVisit).Events.Single(item => item.Event.Id == old.EventId);
        FieldInfo liveDriver = typeof(Ff7AutoPlayController).GetField("driver", BindingFlags.NonPublic | BindingFlags.Instance)!;
        HashSet<string> consumed = new(coverage.Conditions!.ConsumedEntries, StringComparer.Ordinal);
        bool oldConsumed = coverage.Conditions.HasExecutedEntry(old.Conditions!);
        string? admitted = null;
        session.InteractionScriptRequested += (entity, script, accepted) =>
        {
            if (accepted && policy.State.Active is { } active && active.Entity == entity && active.Script == script && active.EventId == old.EventId)
            {
                admitted = active.ConditionFingerprint;
            }
        };
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, advanceTick: () => session.AdvanceTick(advanceClock: false), chooseDialogueSelection: policy.ChooseDialogueSelection, readPlanningProgress: () => policy.WorkProgress, readFrontierProgress: () => policy.FrontierProgress);
        controller.Start();
        Stopwatch elapsed = Stopwatch.StartNew();
        TimeSpan previousClock = TimeSpan.Zero;
        for (int slice = 0; slice < (searchOwnership ? 128 : 10000) && controller.Running && admitted is null && controller.Steps < 600 && elapsed.Elapsed < TimeSpan.FromSeconds(20); slice++)
        {
            TimeSpan currentClock = elapsed.Elapsed;
            session.AdvanceClock(currentClock - previousClock);
            previousClock = currentClock;
            controller.Advance(1);
            if (session.Battle is not null || session.Host.HasUnfinishedBattle || session.MapId != initialMap && session.MapId != targetMap)
            {
                break;
            }
        }
        Ff7AutoplayDriver finalDriver = (Ff7AutoplayDriver)liveDriver.GetValue(controller)!;
        object? finalMode = typeof(Ff7AutoplayDriver).GetField("navigationRunMode", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(finalDriver);
        var parkedModes = (System.Collections.IDictionary)typeof(Ff7AutoplayDriver).GetField("parkedNavigationModes", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(finalDriver)!;
        string modeWork = string.Join(';', parkedModes.Keys.Cast<bool>().Order().Select(run =>
        {
            object state = parkedModes[run]!;
            var cursor = (Ff7AutoplayDriver.PhysicalSearchCursor?)state.GetType().GetProperty("ObstacleSearch")!.GetValue(state);
            return $"{(run ? "run" : "walk")}:{cursor?.Expanded}";
        }));
        if (searchOwnership)
        {
            bool activeGraph = (bool)typeof(Ff7AutoPlayPolicy).GetField("nativePlanningForActive", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(policy)!;
            Check(controller.Error is null && finalDriver.PhysicalPlanningWork > 0 && !activeGraph, $"Unknown contact work did not retain a real physical search: physical={finalDriver.PhysicalPlanningWork}, activeGraph={activeGraph}, error={controller.Error}, status={policy.Status}.");
            Check(admitted is null || !consumed.Contains(admitted), "A consumed standing heuristic became a repeated native admission.");
            Check(coverage.Conditions.HasExecutedEntry(old.Conditions!) == oldConsumed && bytes.AsSpan().SequenceEqual(File.ReadAllBytes(path)), "Unknown contact search changed historical credit or its source.");
            Console.WriteLine($"PASS original unknown-contact real physical ownership; callbacks={controller.Steps}; physical={finalDriver.PhysicalPlanningWork}; pending={finalDriver.PlanningPending}; activeGraph={activeGraph}; freshAdmission={admitted}; parked={modeWork}; source/old credit unchanged");
            return;
        }
        Check(controller.Error is null && admitted is not null && !consumed.Contains(admitted), $"The bounded original contact check ended before fresh admission: admitted={admitted}, callbacks={controller.Steps}, pending={finalDriver.PlanningPending}, physicalWork={finalDriver.PhysicalPlanningWork}, modeRun={finalMode}, parked={modeWork}, error={controller.Error}, status={policy.Status}.");
        Check(coverage.Conditions.HasExecutedEntry(old.Conditions!) == oldConsumed, "An aspirational historical condition acquired synthetic entry credit.");
        Check(bytes.AsSpan().SequenceEqual(File.ReadAllBytes(path)), "The native contact test changed its original source.");
        Console.WriteLine($"PASS original condition-aware native talk admission; callbacks={controller.Steps}; actualCF={admitted}; triangle={session.Host.Process.Actors.Single(actor => actor.PlayerControlled).Triangle}; elapsed={elapsed.Elapsed.TotalSeconds:F3}s; App elapsed clock bridge active; old goal credit/source unchanged");
    }

    internal static void RunSiblingOriginal()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_TALK_CONDITION_SAVE") ?? throw new InvalidOperationException("Select a native contact fixture.");
        string game = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR") ?? throw new InvalidOperationException("Select original resources.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(game), CombinedSessionCodec.Read(input).Ff7!);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("The sibling continuation reached a choice boundary."));
        var old = policy.State.Active ?? throw new InvalidOperationException("The fixture lacks an ordinary contact owner.");
        var oldCoverage = policy.State.Coverage.Visits.Single(visit => visit.Visit.Key == old.VisitId).Events.Single(item => item.Event.Id == old.EventId);
        string history = JsonSerializer.Serialize(new { oldCoverage.Completed, oldCoverage.Routes, oldCoverage.Conditions!.ConsumedEntries, oldCoverage.Conditions.ConsumedExits });
        int sourceMap = session.MapId;
        string? admission = null;
        session.NativeInteractionScriptRequested += request =>
        {
            if (request.Accepted && request.Started)
            {
                admission = $"{request.Entity}/{request.Script}";
                Check(request.Entity != old.Entity || request.Script != old.Script, "An unknown contact search replayed its consumed interaction.");
            }
        };
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, advanceTick: () => session.AdvanceTick(advanceClock: false), chooseDialogueSelection: policy.ChooseDialogueSelection, readPlanningProgress: () => policy.WorkProgress, readFrontierProgress: () => policy.FrontierProgress);
        controller.Start();
        Stopwatch elapsed = Stopwatch.StartNew();
        TimeSpan previous = TimeSpan.Zero;
        for (int poll = 0; poll < 2048 && controller.Running && session.MapId == sourceMap && session.Battle is null && controller.Steps < 128 && elapsed.Elapsed < TimeSpan.FromSeconds(10); poll++)
        {
            TimeSpan now = elapsed.Elapsed;
            session.AdvanceClock(now - previous);
            previous = now;
            controller.Advance(1);
        }
        Check(controller.Error is null && session.MapId != sourceMap, $"Unknown-contact sibling scheduling reached no field handoff: map={session.MapId}, steps={controller.Steps}, owner={policy.State.Active?.EventId}, goal={policy.State.NavigationGoal}, admission={admission}, status={policy.Status}, error={controller.Error}.");
        Check(history == JsonSerializer.Serialize(new { oldCoverage.Completed, oldCoverage.Routes, oldCoverage.Conditions!.ConsumedEntries, oldCoverage.Conditions.ConsumedExits }), "Sibling travel credited the unknown contact owner.");
        Check(bytes.AsSpan().SequenceEqual(File.ReadAllBytes(path)), "Sibling continuation changed its original source.");
        Console.WriteLine($"PASS original unknown-contact sibling native field handoff {sourceMap}->{session.MapId}; callbacks={controller.Steps}; admission={admission}; owner={policy.State.Active?.EventId}; goal={policy.State.NavigationGoal}; elapsed={elapsed.Elapsed.TotalSeconds:F3}s; source/contact history unchanged");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
