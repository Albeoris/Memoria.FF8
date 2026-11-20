using System.Numerics;
using System.Reflection;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class ActorContactRegionTests
{
    internal static void Run()
    {
        Ff7Actor player = new() { Entity = 1, Slot = 0, Model = 0, PlayerControlled = true, Direction = 128 };
        Ff7NativeMoveResult push = new(player, false, false, [6]);
        var input = Ff7AutoplayDriver.SelectActorPushInput(6, false, 128, 0, _ => push, _ => true);
        Check(input is { Talk: false, Run: false } && (input.Value.Horizontal != 0 || input.Value.Vertical != 0), "A legal zero-motion collision failed to emit its actual movement input, or became Talk.");
        Check(Ff7AutoplayDriver.SelectActorPushInput(6, true, 128, 0, _ => push, _ => false) is null, "Native collision bypassed a protected admission guard.");
        Check(Ff7AutoplayDriver.SelectActorPushInput(7, true, 128, 0, _ => push, _ => true) is null, "An unrelated actor collision became selected contact.");
        int probes = 0;
        input = Ff7AutoplayDriver.SelectActorPushInput(6, true, 128, 32, heading =>
        {
            probes++;
            return heading == 224 ? push : new(player, true, false, []);
        }, _ => true);
        Check(input is { Run: true, Talk: false } && probes > 1, "Contact discovery used only the actor-facing axis instead of all native control axes.");
        string Context(IEnumerable<int> labels, string geometry = "stable") => Ff7AutoplayDriver.ReadActorContactCatalogContext(1, geometry, labels, false, 0, 4096, 1024);
        Check(Context([0, 1]) != Context([0, 2]) && Context([0, 1]) == Context([1, 0]) && Context([0, 1]) != Context([0, 1], "changed"), "Directed reachable sets sharing an anchor, or changed native geometry, reused a stale contact catalog.");
        bool disposed = false;
        IEnumerable<(Vector3 Point, int Triangle)> Candidates()
        {
            try
            {
                yield return (default, -1);
                yield return (new(2, 3, 4), 5);
                yield return (new(6, 7, 8), 9);
            }
            finally
            {
                disposed = true;
            }
        }
        using var catalog = new Ff7AutoplayDriver.ActorContactCatalog("source", Candidates().GetEnumerator());
        Check(catalog.Resume(new(1)) == 1 && !catalog.Completed && catalog.Contacts.Count == 0, "A cooperative negative candidate became completion or fake contact.");
        Check(catalog.Resume(new(1)) == 1 && !catalog.Completed && catalog.Contacts.Count == 1, "Catalog continuation restarted or lost its first real witness.");
        Check(catalog.Resume(new(8)) == 2 && catalog.Completed && catalog.Contacts.Count == 2 && disposed, "Finite contact work did not finish/dispose exactly once.");
        Ff7AutoplayDriver modes = new(null!);
        int runAttempts = 0;
        int walkQuanta = 0;
        var failure = new Ff7AutoPlayNavigationUnavailableException(6, "finite represented run exhaustion");
        Ff7AutoplayInput Choose(bool run)
        {
            if (run)
            {
                runAttempts++;
                throw failure;
            }
            walkQuanta++;
            modes.DeferPlanning();
            return default;
        }
        for (int poll = 0; poll < 4; poll++)
        {
            modes.ChooseNavigationModes("source", "goal", new(), null, Choose, _ => true);
        }
        Check(runAttempts == 1 && walkQuanta == 4, "A finished run graph restarted while retained walking work was pending.");
        modes.ChooseNavigationModes("changed-source", "goal", new(), null, Choose, _ => true);
        modes.ChooseNavigationModes("changed-source", "changed-goal", new(), null, Choose, _ => true);
        Check(runAttempts == 3, "Real native source or intent change failed to reopen a terminal mode.");
        typeof(Ff7AutoplayDriver).GetMethod("UpdateNavigationModeGoal", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(modes, ["changed-physical-goal"]);
        modes.ChooseNavigationModes("changed-source", "changed-goal", new(), null, Choose, _ => true);
        Check(runAttempts == 4, "Changing the actual physical goal failed to release terminal mode work.");
        Ff7AutoplayInput readyWalk = modes.ChooseNavigationModes("changed-source", "changed-goal", new(), null, run => run ? throw failure : new(1, 0, Run: false), _ => true);
        Check(readyWalk is { Horizontal: 1, Run: false } && !modes.PlanningPending, "A ready surviving mode was not adopted.");
        modes.ChooseNavigationModes("changed-source", "changed-goal", new(), null, Choose, _ => true);
        Check(runAttempts == 5, "Ready mode adoption retained stale terminal failures.");
        Ff7AutoplayDriver both = new(null!);
        var walkFailure = new Ff7AutoPlayNavigationUnavailableException(6, "finite represented walk exhaustion") { PhysicalFailure = new(2, "walk-contract", "exact-source", 9) };
        try
        {
            both.ChooseNavigationModes("source", "goal", new(), null, run => throw (run ? failure : walkFailure), _ => true);
            throw new InvalidOperationException("Both terminal modes became fake Pending.");
        }
        catch (Ff7AutoPlayNavigationUnavailableException actual)
        {
            Check(ReferenceEquals(actual, walkFailure) && actual.PhysicalFailure == walkFailure.PhysicalFailure && !both.PlanningPending, "Both-mode exhaustion lost the actual failure receipt or became Pending.");
        }
        Console.WriteLine("PASS native ActorContact zero-motion Push, no Talk requirement, protected guard, exact owner and finite axes");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    internal static void RunOriginal()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_ACTOR_CONTACT_SAVE") ?? throw new InvalidOperationException("Select a native actor-contact fixture.");
        string game = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR") ?? throw new InvalidOperationException("Select original resources.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, false);
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(game), CombinedSessionCodec.Read(input).Ff7!);
        int entity = session.AutoPlayState!.Active!.Entity;
        long generation = session.FieldGeneration;
        bool admitted = false;
        bool returned = false;
        session.NativeInteractionScriptRequested += receipt => admitted |= session.FieldGeneration == generation && receipt.Entity == entity && receipt.Script == 2 && receipt.Accepted && receipt.Started;
        session.Machine.ScriptReturned += receipt => returned |= admitted && session.FieldGeneration == generation && receipt.Entity == entity && receipt.Priority == 1;
        Ff7AutoPlayObservation observed = new Ff7AutoPlayDiscovery(session).Observe();
        Ff7AutoPlayTarget selected = observed.Events.Single(target => target.Kind == Ff7AutoPlayTargetKind.ActorContact && target.Entity == entity);
        Ff7AutoplayDriver? retainedDriver = null;
        Ff7AutoPlayController controller = new(session, driver =>
        {
            retainedDriver = driver;
            driver.ConfigureSpatialBoundaries(observed.Events, selected);
            return driver.ChooseTargetInput(selected, observed.Events, run => driver.Contact(entity, run));
        }, advanceTick: () => session.AdvanceTick(advanceClock: false));
        controller.Start();
        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        TimeSpan previous = TimeSpan.Zero;
        int polls = 0;
        long memoryBefore = GC.GetTotalMemory(false);
        TimeSpan nextSignal = TimeSpan.FromSeconds(30);
        while (controller.Running && !returned && session.FieldGeneration == generation && elapsed.Elapsed < TimeSpan.FromSeconds(120) && GC.GetTotalMemory(false) < memoryBefore + 3L * 1024 * 1024 * 1024)
        {
            TimeSpan now = elapsed.Elapsed;
            session.AdvanceClock(now - previous);
            previous = now;
            controller.Advance(1);
            polls++;
            if (elapsed.Elapsed >= nextSignal)
            {
                Console.WriteLine($"CONTACT work elapsed={elapsed.Elapsed.TotalSeconds:F3}s callbacks={controller.Steps} {controller.PlanningProgress} managed={GC.GetTotalMemory(false)}");
                nextSignal += TimeSpan.FromSeconds(30);
                if (retainedDriver is not null)
                {
                    var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                    var modes = (System.Collections.IDictionary)typeof(Ff7AutoplayDriver).GetField("parkedNavigationModes", flags)!.GetValue(retainedDriver)!;
                    var failures = (System.Collections.IDictionary)typeof(Ff7AutoplayDriver).GetField("failedNavigationModes", flags)!.GetValue(retainedDriver)!;
                    foreach (System.Collections.DictionaryEntry mode in modes)
                    {
                        var cursor = (Ff7AutoplayDriver.PhysicalSearchCursor?)mode.Value!.GetType().GetProperty("ObstacleSearch")!.GetValue(mode.Value);
                        if (cursor is not null)
                        {
                            var costs = (System.Collections.IDictionary)cursor.GetType().GetField("costs", flags)!.GetValue(cursor)!;
                            var queue = cursor.GetType().GetField("pending", flags)!.GetValue(cursor)!;
                            int frontier = (int)queue.GetType().GetProperty("Count")!.GetValue(queue)!;
                            Console.WriteLine($"CURSOR run={mode.Key} identity={System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(cursor)} expandedAllGuides={cursor.Expanded} rootVisited={costs.Count} rootFrontier={frontier} retainedFailures={failures.Count}");
                        }
                    }
                }
            }
        }
        Ff7Actor player = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
        Console.WriteLine($"CONTACT original: admitted={admitted}, returnedP1={returned}, callbacks={controller.Steps}, polls={polls}, tick={session.Machine.Tick}, triangle={player.Triangle}, planning={controller.PlanningProgress}, managedBytes={GC.GetTotalMemory(false)}, error={controller.Error}, elapsed={elapsed.Elapsed.TotalSeconds:F3}s; source unchanged={bytes.AsSpan().SequenceEqual(File.ReadAllBytes(path))}");
        if (controller.Failure is Ff7AutoPlayNavigationUnavailableException unavailable)
        {
            Console.WriteLine($"CONTACT terminal receipt={System.Text.Json.JsonSerializer.Serialize(unavailable.PhysicalFailure)}");
        }
        var terminalFailures = retainedDriver is null ? null : (System.Collections.IDictionary)typeof(Ff7AutoplayDriver).GetField("failedNavigationModes", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(retainedDriver)!;
        bool finiteBothModes = terminalFailures is { Count: 2 } && terminalFailures.Values.Cast<Ff7AutoPlayNavigationUnavailableException>().All(failure => failure.PhysicalFailure is { Revision: 2, Expanded: > 0 }) && !controller.PlanningProgress.Pending;
        Check((admitted && returned && controller.Error is null || finiteBothModes && !admitted && !returned) && bytes.AsSpan().SequenceEqual(File.ReadAllBytes(path)), "Original ActorContact neither admitted and returned nor exhausted both represented native mode graphs.");
    }

    internal static void RunPolicyOriginal()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_ACTOR_CONTACT_SAVE") ?? throw new InvalidOperationException("Select a native actor-contact fixture.");
        string game = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR") ?? throw new InvalidOperationException("Select original resources.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, false);
        Ff7SessionSave save = CombinedSessionCodec.Read(input).Ff7!;
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(game), save);
        string id = save.AutoPlay!.Active!.EventId;
        int entity = save.AutoPlay.Active.Entity;
        long generation = session.FieldGeneration;
        bool contactAdmitted = false;
        Memoria.FF7.Events.FieldHostRequest? departure = null;
        session.NativeInteractionScriptRequested += receipt => contactAdmitted |= session.FieldGeneration == generation && receipt.Entity == entity && receipt.Script == 2 && receipt.Accepted && receipt.Started;
        session.NativeFieldDeparting += _ => departure = session.Host.Capture().Receipts.SingleOrDefault(receipt => receipt.Request.Id == session.Host.Process.GlobalRequestId)?.Request;
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("Unexpected choice boundary."));
        int Count() => policy.State.Coverage.Visits.SelectMany(visit => visit.Events).Where(item => item.Event.Id == id).Sum(item => item.Conditions?.ConsumedEntries.Count ?? 0);
        int before = Count();
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, advanceTick: () => session.AdvanceTick(advanceClock: false), chooseDialogueSelection: policy.ChooseDialogueSelection, readPlanningProgress: () => policy.WorkProgress, readFrontierProgress: () => policy.FrontierProgress);
        controller.Start();
        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        TimeSpan previous = TimeSpan.Zero;
        while (controller.Running && session.FieldGeneration == generation && elapsed.Elapsed < TimeSpan.FromSeconds(10))
        {
            TimeSpan now = elapsed.Elapsed;
            session.AdvanceClock(now - previous);
            previous = now;
            controller.Advance(1);
        }
        Check(controller.Error is null && session.FieldGeneration != generation && departure?.Command is Memoria.FF7.Events.FieldTransitionCommand transition && transition.Map == session.MapId && !contactAdmitted && Count() == before && bytes.AsSpan().SequenceEqual(File.ReadAllBytes(path)), "Ordinary contact planning did not preserve a real alternate departure and exact native actor credit.");
        Console.WriteLine($"PASS ordinary contact alternative: callbacks={controller.Steps}, destination={session.MapId}, nativeOwner={departure!.Entity}/p{departure.Priority}/pc{departure.ProgramCounter:X4}, contact credit unchanged, elapsed={elapsed.Elapsed.TotalSeconds:F3}s; source unchanged");
    }
}
