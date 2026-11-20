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

    internal static void RunOriginal()
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
        FieldInfo planField = typeof(Ff7AutoPlayPolicy).GetField("talkContactPlan", BindingFlags.Instance | BindingFlags.NonPublic)!;
        string? lastTrace = null;
        int traceRows = 0;
        object? failedPlan = null;
        bool rotated = false;
        object? ReadPoint(object? plan) => plan?.GetType().GetProperty("Point")?.GetValue(plan);
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
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, chooseDialogueSelection: policy.ChooseDialogueSelection, readPlanningProgress: () => policy.WorkProgress, readFrontierProgress: () => policy.FrontierProgress);
        controller.Start();
        Stopwatch elapsed = Stopwatch.StartNew();
        for (int slice = 0; slice < 10000 && controller.Running && admitted is null && controller.Steps < 600 && elapsed.Elapsed < TimeSpan.FromSeconds(20); slice++)
        {
            object? beforePlan = planField.GetValue(policy);
            controller.Advance(1);
            if (policy.Status == "Trying another native talk contact after a movement cycle" && beforePlan is not null)
            {
                failedPlan = beforePlan;
                if (Environment.GetEnvironmentVariable("MEMORIA_FF7_TALK_INGRESS_DIAGNOSTIC") == "1")
                {
                    ReadIngressDiagnostic(session, policy, old.EventId!, (Vector3)ReadPoint(beforePlan)!, (int)beforePlan.GetType().GetProperty("Triangle")!.GetValue(beforePlan)!);
                    Check(bytes.AsSpan().SequenceEqual(File.ReadAllBytes(path)), "The ingress diagnostic changed its original source.");
                    return;
                }
            }
            object? nextPlan = planField.GetValue(policy);
            string trace = $"map={session.MapId};active={policy.State.Active?.EventId};goal={policy.State.NavigationGoal?.EventId};point={ReadPoint(nextPlan)};triangle={nextPlan?.GetType().GetProperty("Triangle")?.GetValue(nextPlan)};status={policy.Status}";
            if (trace != lastTrace && traceRows < 24)
            {
                Console.WriteLine($"TRACE callbacks={controller.Steps};{trace}");
                traceRows++;
                lastTrace = trace;
            }
            if (failedPlan is not null && nextPlan is not null && !Equals(ReadPoint(failedPlan), ReadPoint(nextPlan)))
            {
                rotated = true;
            }
            if (session.Battle is not null || session.Host.HasUnfinishedBattle || session.MapId != initialMap && session.MapId != targetMap)
            {
                break;
            }
        }
        Check(controller.Error is null && admitted is not null && !consumed.Contains(admitted), $"The ordinary driver failed to admit a fresh contact condition: admitted={admitted}, callbacks={controller.Steps}, error={controller.Error}, status={policy.Status}.");
        Check(coverage.Conditions.HasExecutedEntry(old.Conditions!) == oldConsumed, "An aspirational historical condition acquired synthetic entry credit.");
        Check(bytes.AsSpan().SequenceEqual(File.ReadAllBytes(path)), "The native contact test changed its original source.");
        Console.WriteLine($"PASS original condition-aware native talk admission; callbacks={controller.Steps}; rotated-real-cycle={rotated}; actualCF={admitted}; triangle={session.Host.Process.Actors.Single(actor => actor.PlayerControlled).Triangle}; elapsed={elapsed.Elapsed.TotalSeconds:F3}s; old goal credit/source unchanged");
    }

    private static void ReadIngressDiagnostic(Ff7Session session, Ff7AutoPlayPolicy policy, string eventId, Vector3 failedPoint, int destination)
    {
        Ff7AutoPlayObservation observation = new Ff7AutoPlayDiscovery(session).Observe();
        Ff7AutoPlayTarget target = observation.Events.Single(item => item.Id == eventId);
        Ff7Actor player = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
        Ff7AutoplayDriver driver = new(session);
        driver.ConfigureSpatialBoundaries(observation.Events, target);
        var coverage = policy.State.Coverage.Visits.Single(visit => visit.Visit.Key == observation.VisitId).Events.Single(item => item.Event.Id == eventId);
        var domain = policy.State.TalkConditionDomains[observation.NavigationKey + "|" + eventId];
        var contacts = driver.DiscoverTalkContacts(session.Host.GetActor(target.Entity), player).Contacts;
        Console.WriteLine($"INGRESS player={player.X4096},{player.Y4096},{player.Z4096}/tri{player.Triangle}/direction{player.Direction}; freshContactGroups=" + string.Join(';', contacts.Where(contact => domain.Conditions.TryGetValue(contact.Triangle, out string? condition) && !coverage.Conditions!.HasExecutedEntry(condition)).GroupBy(contact => contact.Triangle).Select(group => $"{group.Key}:{group.Count()}")));
        var (previous, distance, _) = Ff7AutoplayDriver.ReadAdvisedApproachRoutes(session.Field.Walkmesh, session.Host.Process.BlockedTriangles, player.Triangle, destination, driver.CaptureSpatialBoundaryTriangles().ToHashSet(), null, new HashSet<(int, int)>(), new HashSet<(int, int)>());
        var step = Ff7AutoplayDriver.ReadPortalApproachStep(session.Field.Walkmesh, player.Triangle, destination, previous, new(player.X4096 / 4096f, player.Y4096 / 4096f, player.Z4096 / 4096f), failedPoint, player.CollisionRadius);
        Console.WriteLine($"PORTAL finalApproach={step.FinalApproach};waypoint={step.Waypoint};destination={destination};sourceDistance={distance[player.Triangle]};destinationDistance={distance[destination]}");
        MethodInfo allows = typeof(Ff7AutoplayDriver).GetMethod("AllowsPlannedMove", BindingFlags.NonPublic | BindingFlags.Instance)!;
        foreach (bool run in new[] { true, false })
        {
            driver.ChooseTargetInput(target, observation.Events, _ =>
            {
                Ff7AutoplayInput selectedInput = driver.Approach(failedPoint, destination, run);
                Console.WriteLine($"APPROACH run={run};input={selectedInput};pending={driver.PlanningPending}");
                Ff7Actor controlled = player with { MoveSpeed = checked((ushort)session.GetPlayerMovementSpeed(run)) };
                for (int direction = 0; direction < 256; direction += 32)
                {
                    byte heading = (byte)((direction + session.Field.Triggers.ControlDirection) & 255);
                    Ff7NativeMoveResult preview = session.Host.Movement!.StepDirection(controlled, heading, session.Host.Process.Actors, session.Host.Process.BlockedTriangles, playerLocked: false, lineBlocked: session.Host.Process.LineBlocked);
                    bool admitted = (bool)allows.Invoke(driver, [controlled, preview])!;
                    Console.WriteLine($"PREVIEW run={run};heading={heading};moved={preview.Moved};tri={preview.Actor.Triangle};pose={preview.Actor.X4096},{preview.Actor.Y4096},{preview.Actor.Z4096};talk={session.PreviewTalkTarget(preview.Actor)?.Entity};driverAllows={admitted}");
                }
                if (run)
                {
                    Ff7NativeMoveResult ingress = session.Host.Movement!.StepDirection(controlled, 64, session.Host.Process.Actors, session.Host.Process.BlockedTriangles, false, session.Host.Process.LineBlocked);
                    Ff7Actor successor = ingress.Actor;
                    var finalStep = Ff7AutoplayDriver.ReadPortalApproachStep(session.Field.Walkmesh, successor.Triangle, destination, previous, new(successor.X4096 / 4096f, successor.Y4096 / 4096f, successor.Z4096 / 4096f), failedPoint, successor.CollisionRadius);
                    List<(int Heading, Ff7Actor Actor, int Remaining, float Distance)> legal = [];
                    for (int direction = 0; direction < 256; direction += 32)
                    {
                        Ff7NativeMoveResult next = session.Host.Movement.StepDirection(successor, (byte)direction, session.Host.Process.Actors, session.Host.Process.BlockedTriangles, false, session.Host.Process.LineBlocked);
                        if (next.Moved && (bool)allows.Invoke(driver, [successor, next])!)
                        {
                            legal.Add((direction, next.Actor, distance[next.Actor.Triangle], Vector2.DistanceSquared(new(next.Actor.X4096 / 4096f, next.Actor.Y4096 / 4096f), new(finalStep.Waypoint.X, finalStep.Waypoint.Y))));
                        }
                    }
                    var oldRanking = legal.OrderBy(item => finalStep.FinalApproach ? 0 : item.Remaining).ThenBy(item => item.Distance).First();
                    var requiredRanking = legal.OrderBy(item => item.Remaining).ThenBy(item => item.Distance).First();
                    Console.WriteLine($"SUCCESSOR sourceTri={successor.Triangle};finalApproach={finalStep.FinalApproach};oldHeading={oldRanking.Heading}/tri{oldRanking.Actor.Triangle}/distance{oldRanking.Distance};requiredHeading={requiredRanking.Heading}/tri{requiredRanking.Actor.Triangle}/distance{requiredRanking.Distance}");
                }
                return default;
            }, requiredRun: run);
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
