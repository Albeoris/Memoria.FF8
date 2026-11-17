using System.Text.Json;
using System.Numerics;
using Memoria.AutoPlay;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class AutoPlayNavigationActionModeTests
{
    internal static void Run()
    {
        ushort[] entries = new ushort[32];
        entries[4] = 1;
        FieldProgram gate = new([0, 0x30, 0x40, 2, 4, 0x99, 0x50, 0, 0], [entries]);
        FieldMachine machine = new(gate);
        machine.Initialize();
        foreach (FieldInputState previous in new[] { new FieldInputState(), new(Held: 0x40, RawHeld: 0x40), new(Held: 0x20, RawHeld: 0x40), new(Held: 0x40, RawHeld: 0x20) })
        {
            foreach (Ff7AutoplayInput action in new[] { new Ff7AutoplayInput(1, -1, Run: true), new(1, -1, Run: false), new(Held: 0x40, Run: false), new(1, 0, Held: 0x20, Run: false) })
            {
                FieldMachine native = new(gate);
                native.SetInput(previous.Held, previous.Pressed, previous.RawHeld, previous.RawPressed);
                native.SetInput(action.Held, (ushort)(action.Held & ~previous.Held));
                native.SetMovementInput(action.Horizontal, action.Vertical, action.Run);
                FieldInputState actual = native.Input with { Horizontal = 0, Vertical = 0, Run = false };
                Check(Ff7AutoplayDriver.ProjectNavigationInput(action, previous) == actual, "Navigation mode projection must match the native mapped/raw held, pressed and released streams, including movement's run-bit ownership.");
            }
        }
        string before = JsonSerializer.Serialize(machine.Capture());
        FieldInputState run = Ff7AutoplayDriver.ProjectNavigationInput(new(Horizontal: 1, Run: true), new());
        FieldInputState walk = Ff7AutoplayDriver.ProjectNavigationInput(new(Horizontal: 1, Run: false), new());
        Check(!machine.CanSkipScriptEntryForInput(0, 4, run) && machine.CanSkipScriptEntryForInput(0, 4, walk), "Run may enter the native effect while walking must require an actual pure-RET skip proof.");
        FieldMachine unsafeFalse = new(new([0, 0x30, 0x40, 2, 1, 0x99, 0x50, 0, 0], [entries]));
        unsafeFalse.Initialize();
        Check(!unsafeFalse.CanSkipScriptEntryForInput(0, 4, walk), "A false input predicate with another host effect must remain protected in walking mode.");
        FieldProgram releasedGate = new([0, 0x32, 0x40, 2, 4, 0x99, 0x50, 0, 0], [entries]);
        FieldMachine released = new(releasedGate);
        released.Initialize();
        FieldInputState release = Ff7AutoplayDriver.ProjectNavigationInput(new(Horizontal: 1, Run: false), new(Held: 0x40, RawHeld: 0x40));
        Check(!released.CanSkipScriptEntryForInput(0, 4, release) && released.CanSkipScriptEntryForInput(0, 4, walk), "Walking's actual run-release edge must not bypass a native KEYOFF effect.");
        Ff7Line touch = new(0, 0, 0, 0, 0, 1, 0, 0) { Touch = true };
        Check(Ff7AutoplayDriver.HasNativeLineRequest(Ff7AutoPlayTargetKind.LineTouch, touch) && !Ff7AutoplayDriver.HasNativeLineRequest(Ff7AutoPlayTargetKind.LineCross, touch), "An already-touching native owner must remain a dispatch boundary before another physical movement.");
        Ff7AutoplayInput ready = Ff7AutoPlayPolicy.ApplyReadyEntryInput(new(1, -1, Run: false), new(Held: 0x40));
        FieldInputState readyNative = Ff7AutoplayDriver.ProjectNavigationInput(ready, new());
        Check(ready.Horizontal == 1 && ready.Vertical == -1 && ready.Run && readyNative.Held == 0x40 && !machine.CanSkipScriptEntryForInput(0, 4, readyNative), "A selected ready run-button gate must retain its transient movement admission with a coherent actual run mode rather than letting walking clear its required bit.");
        string previousCapability = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("physical-geometry|automatic-triangle-boundaries:1:|approach-admission:2|native-navigation-modes:run-walk:5")));
        Check(Ff7AutoplayDriver.ReadSpatialBoundaryFingerprint("physical-geometry", new HashSet<int>()) != previousCapability, "Random-producer write-set support retained obsolete physical absence receipts.");
        CheckProgressRankedModes();
        CheckOwnedPortals();
        CheckSelectedPushAdmission();
        Check(before == JsonSerializer.Serialize(machine.Capture()), "Mode and skip planning must preserve authoritative VM, input and variables.");
        OriginalPlannedWalk();
        OriginalReadOnlyNavigationEntry();
        OriginalRandomProducerNavigationEntry();
        OriginalAlternatingModeEscape();
    }

    private static void CheckProgressRankedModes()
    {
        Ff7Actor source = new() { Slot = 0, Entity = 0, Model = 0, Triangle = 0 };
        Vector3 waypoint = new(10, 0, 0);
        Ff7Actor away = source with { Y4096 = -16 * 4096 };
        Ff7Actor closer = source with { X4096 = 4 * 4096 };
        bool runAdvances = Ff7AutoplayDriver.HasPhysicalRouteProgress(source, away, waypoint, _ => 0);
        bool walkAdvances = Ff7AutoplayDriver.HasPhysicalRouteProgress(source, closer, waypoint, _ => 0);
        Ff7AutoplayInput run = new(0, 1, Run: true);
        Ff7AutoplayInput walk = new(1, -1, Run: false);
        Check(!runAdvances && walkAdvances && Ff7AutoplayDriver.ShouldTryWalkingAction(run, true, escapePlanned: false) && Ff7AutoplayDriver.PreferAdvancingNavigationInput(run, walk, walkAdvances) == walk, "A line-blocked running detour must yield to the same planner's admitted advancing walk.");
        Check(!Ff7AutoplayDriver.ShouldTryWalkingAction(run, false, escapePlanned: false) && Ff7AutoplayDriver.ShouldTryWalkingAction(run, true, escapePlanned: false), "An unselected line rejection must compare walking even if one running detour appears to advance locally.");
        bool otherDetourAdvances = Ff7AutoplayDriver.HasPhysicalRouteProgress(source, source with { Y4096 = -4 * 4096 }, waypoint, _ => 0);
        Check(Ff7AutoplayDriver.PreferAdvancingNavigationInput(run, walk, otherDetourAdvances) == run, "When neither admitted mode advances, an existing running detour must remain available.");
        bool boundedEscapeAdvances = Ff7AutoplayDriver.HasPhysicalRouteProgress(source, source with { X4096 = 8 * 4096 }, waypoint, _ => 0);
        Check(boundedEscapeAdvances && !Ff7AutoplayDriver.ShouldTryWalkingAction(run, true, escapePlanned: true), "A proved bounded escape must retain its plan even when its first physical prefix retreats.");
        Check(Ff7AutoplayDriver.PreferAdvancingNavigationInput(run, walk with { Held = 0x40 }, false).Held == 0x40, "The selected owner's explicit entry input must retain admission priority over geometric detour ranking.");
    }

    private static void CheckOwnedPortals()
    {
        PcWalkmeshTriangle[] mesh = [new(0, new(0, 0, 0), new(10, 0, 0), new(0, 10, 0), -1, 1, -1), new(1, new(10, 0, 0), new(10, 10, 0), new(0, 10, 0), -1, -1, 0)];
        Ff7Line owner = new(0, 2, 0, 10, 0, 10, 0, 0);
        var owned = Ff7AutoplayDriver.ReadOwnedLinePortals(mesh, [owner]);
        Check(Ff7AutoplayDriver.ReadApproachRoutes(mesh, [0], 0, 1).Distance[0] == 1 && Ff7AutoplayDriver.ReadApproachRoutes(mesh, [0], 0, 1, ownedPortals: owned).Previous[0] == -1, "An unselected native owner covering the shared portal must prevent an ordinary route through that boundary.");
        Check(Ff7AutoplayDriver.ReadApproachRoutes(mesh, [0], 1, 0, ownedPortals: owned).Previous[1] == -1, "A fully owned portal must remain protected in both traversal directions.");
        Check(Ff7AutoplayDriver.ReadOwnedLinePortals(mesh, [owner with { Z1 = 100, Z2 = 100 }]).Count == 0, "A line on another native floor must not own the portal.");
        Check(Ff7AutoplayDriver.ReadOwnedLinePortals(mesh, [owner with { X2 = 5, Y2 = 5 }]).Count == 0, "A partial line overlap must retain the existing native physical-search admission.");
        Check(Ff7AutoplayDriver.ReadApproachRoutes(mesh, [0], 0, 1, ownedPortals: Ff7AutoplayDriver.ReadOwnedLinePortals(mesh, [])).Distance[0] == 1, "Selected or proved-skipped owners must leave their ordinary portal available.");
        Check(!Ff7AutoplayDriver.ShouldTryWalkingAction(new(1, 0, Held: 0x40), true, false) && !Ff7AutoplayDriver.ShouldTryWalkingAction(new(Talk: true), true, false), "Comparing movement modes must preserve explicit owner buttons and talk admission.");
    }

    private static void CheckSelectedPushAdmission()
    {
        Ff7Actor player = new() { Slot = 0, Entity = 0, Model = 0, PlayerControlled = true, CollisionRadius = 34 };
        Ff7FieldHost host = new(NativeDialogueTests.Field(), (_, _) => 1, new Ff7FieldProcess { Actors = [player], Lines = [new(0, 0, -10, 0, 0, 10, 0, 0)] });
        host.UpdateLineContacts(player, player with { X4096 = 4096 }, moving: true);
        Check(host.Process.Lines.Single() is { Push: true, Touch: true }, "The authored physical contact must generate the real native push and touch requests.");
        ushort[] entries = new ushort[32];
        entries[2] = 1;
        FieldMachine machine = new(new([0, 0x31, 0x40, 2, 5, 0x80, 0x50, 0, 1, 0], [entries]));
        machine.Initialize();
        Ff7AutoplayInput prepared = Ff7AutoPlayPolicy.ApplyReadyEntryInput(new(1, 0, Run: false), new(Held: 0x40));
        machine.SetInput(prepared.Held, prepared.Held);
        machine.SetMovementInput(prepared.Horizontal, prepared.Vertical, prepared.Run);
        List<(int Entity, int Script, bool Accepted)> requests = [];
        foreach (var request in Ff7Session.ReadNativeLineScriptRequests(() => host))
        {
            requests.Add((request.Entity, request.Script, machine.TryRequestScript(request.Entity, request.Script)));
        }
        Check(requests.SequenceEqual(new[] { (0, 2, true), (0, 5, false), (0, 4, false) }), "The production line dispatcher must retain push-before-enter-before-touch ordering and native already-busy request rejection.");
        machine.AdvanceTick();
        Check(machine.Variables.ReadByte(FieldBank.MapByte, 0) == 1, "The selected native push request must actually execute its KEYON-gated body after preparing a coherent moving run input.");
    }

    private static void OriginalPlannedWalk()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_NAVIGATION_MODE_SAVE");
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("SKIP original navigation action modes: configure original resources and MEMORIA_FF7_NAVIGATION_MODE_SAVE.");
            return;
        }
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(root), CombinedSessionCodec.Read(input).Ff7!);
        string native = JsonSerializer.Serialize(session.Capture());
        Ff7AutoPlayObservation observation = new Ff7AutoPlayDiscovery(session).Observe();
        Ff7AutoPlayTarget target = observation.Events.Single(candidate => candidate.Kind == Ff7AutoPlayTargetKind.Gateway);
        Ff7AutoPlayTarget[] guardedLines = observation.Events.Where(candidate => candidate.Kind == Ff7AutoPlayTargetKind.LineTouch && candidate.MayChangeNavigation).ToArray();
        Check(guardedLines.Length > 0, "The original fixture must retain its unselected native line boundaries.");
        Ff7AutoplayDriver driver = new(session);
        driver.ConfigureSpatialBoundaries(observation.Events, target);
        List<bool> modes = [];
        bool? runProgress = null;
        bool? walkProgress = null;
        Ff7AutoplayInput planned = driver.ChooseTargetInput(target, observation.Events, run =>
        {
            modes.Add(run);
            Ff7AutoplayInput action = driver.GatewayAt(target.GatewayIndex!.Value, run);
            if (run)
            {
                runProgress = driver.PlannedActionAdvances;
            }
            else
            {
                walkProgress = driver.PlannedActionAdvances;
            }
            return action;
        });
        Check(modes.SequenceEqual(new[] { true, false }) && !planned.Run && (planned.Horizontal != 0 || planned.Vertical != 0), "The same native gateway planner must retain the valid walking move after the running action is blocked by its unselected native owner.");
        Check(runProgress == false && walkProgress == true, "The original admitted walk must advance under the existing native route/waypoint proof while the rejected running action does not.");
        FieldInputState walking = Ff7AutoplayDriver.ProjectNavigationInput(planned, session.Machine.Input);
        FieldInputState running = Ff7AutoplayDriver.ProjectNavigationInput(planned with { Run = true }, session.Machine.Input);
        Check(guardedLines.All(candidate => session.Machine.CanSkipScriptEntryForInput(candidate.Entity, candidate.Script, walking)) && guardedLines.Any(candidate => !session.Machine.CanSkipScriptEntryForInput(candidate.Entity, candidate.Script, running)), "The original walk must prove native RET while the equivalent run still reaches its native effect boundary.");
        Check(native == JsonSerializer.Serialize(session.Capture()) && bytes.SequenceEqual(File.ReadAllBytes(path)), "Original action-mode planning changed native pose, VM, RNG or fixture bytes.");
        Console.WriteLine($"PASS original gateway {target.Id}, blocked run then admitted walk {planned.Horizontal},{planned.Vertical}; exact native skip proof and unchanged source.");
    }

    private static void OriginalReadOnlyNavigationEntry()
    {
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_READONLY_NAVIGATION_SAVE");
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(root))
        {
            Console.WriteLine("SKIP original read-only navigation entry: configure original resources and MEMORIA_FF7_READONLY_NAVIGATION_SAVE.");
            return;
        }
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream source = new(bytes, false);
        Ff7SessionSave saved = CombinedSessionCodec.Read(source).Ff7 ?? throw new InvalidDataException("The configured fixture has no native session.");
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(root), saved);
        Check(saved.FieldName == "cos_btm", "The configured fixture must contain the original no-effect line boundary.");
        string before = JsonSerializer.Serialize(session.Capture());
        FieldInputState input = Ff7AutoplayDriver.ProjectNavigationInput(new(Horizontal: 1), session.Machine.Input);
        Check(!session.Machine.CanSkipScriptEntryForInput(16, 5, input), "The original input-only proof must remain conservative about variable predicates.");
        System.Diagnostics.Stopwatch proofTime = System.Diagnostics.Stopwatch.StartNew();
        FieldNavigationEntryProof proof = session.ReadNavigationEntryProof(16, 5, input);
        proofTime.Stop();
        Check(proof.Complete && proof.HasNoEffect, $"The original conditional return requires a stable producer proof: {proof.Reason}");
        Check(proof.Reads.Length > 0, "The original predicate proof must retain its typed read dependencies.");
        Ff7AutoPlayObservation observation = new Ff7AutoPlayDiscovery(session).Observe();
        Ff7AutoPlayTarget gateway = observation.Events.Single(target => target.Id == "gateway:5");
        Ff7AutoplayDriver driver = new(session);
        driver.ConfigureSpatialBoundaries(observation.Events, gateway);
        Ff7AutoplayInput action = driver.ChooseTargetInput(gateway, observation.Events, run => driver.GatewayAt(gateway.GatewayIndex!.Value, run));
        Check(action.Horizontal != 0 || action.Vertical != 0, "The proved no-effect owner must admit the native gateway approach without clearing its line or skipping its script.");
        Check(JsonSerializer.Serialize(session.Capture()) == before && File.ReadAllBytes(path).SequenceEqual(bytes), "Read-only native entry and route planning must preserve the fixture, VM, RNG and progress.");
        Console.WriteLine($"PASS original conditional-return producer proof ({proofTime.ElapsedMilliseconds}ms) and admitted gateway approach; native source unchanged.");
    }

    private static void OriginalRandomProducerNavigationEntry()
    {
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_RANDOM_PRODUCER_NAVIGATION_SAVE");
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(root))
        {
            Console.WriteLine("SKIP original random-producer navigation entry: configure original resources and MEMORIA_FF7_RANDOM_PRODUCER_NAVIGATION_SAVE.");
            return;
        }
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(root), CombinedSessionCodec.Read(input).Ff7!);
        string before = JsonSerializer.Serialize(session.Capture());
        FieldInputState projected = Ff7AutoplayDriver.ProjectNavigationInput(new(Horizontal: 1), session.Machine.Input);
        FieldNavigationEntryProof proof = session.ReadNavigationEntryProof(13, 4, projected);
        Check(proof.Complete && proof.HasNoEffect, $"An unrelated random producer obstructed the original conditional-return proof: {proof.Reason}");
        Ff7AutoPlayObservation observation = new Ff7AutoPlayDiscovery(session).Observe();
        Ff7AutoPlayTarget target = observation.Events.Single(candidate => candidate.Id == "line:1:entity:11:script:2");
        Ff7AutoplayDriver driver = new(session);
        driver.ConfigureSpatialBoundaries(observation.Events, target);
        Ff7AutoplayInput action = driver.ChooseTargetInput(target, observation.Events, run => driver.ApproachLine(target.Entity, target.Kind, run));
        Check(action.Horizontal != 0 || action.Vertical != 0, "The original no-effect contact must admit the native selected LINE approach.");
        Ff7AutoPlayState state = session.AutoPlayState!.Clone();
        AutoPlayPlanner<Ff7AutoPlayDeferral> planner = Ff7AutoPlayPolicy.CreatePlanner(state);
        Dictionary<string, string> facts = new(StringComparer.Ordinal);
        foreach (var entry in state.NavigationGuards.Where(entry => entry.Value.VisitId == observation.VisitId && entry.Value.NavigationKey == observation.NavigationKey))
        {
            Ff7AutoPlayTarget guarded = observation.Events.Single(candidate => candidate.Id == entry.Value.EventId);
            string physical = driver.ReadPhysicalAvailabilityFingerprint(guarded, entry.Value.BlockingEntities);
            facts[entry.Key] = Ff7AutoPlayPolicy.ReadMovementFailureFingerprint(physical, entry.Value.MovementFailure, session.Host.Process.Actors, entry.Value.BlockingEntities);
        }
        var savedCoverage = state.Coverage.Visits.Single(visit => visit.Visit.Key == observation.VisitId).Events.Single(item => item.Event.Id == target.Id);
        Check(savedCoverage.Unavailable && savedCoverage.UnavailableAttempts.All(attempt => !facts.TryGetValue(attempt.GuardKey, out string? current) || current != attempt.GuardFingerprint), "The new proof capability did not distinguish the original saved negative.");
        AutoPlayNavigationVisit currentNavigation = state.Coverage.NavigationVisits.Single(visit => visit.Key == observation.NavigationKey);
        Ff7Actor currentPlayer = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
        foreach (Ff7AutoPlayTarget gatewayTarget in observation.Events.Where(candidate => candidate.Kind == Ff7AutoPlayTargetKind.Gateway))
        {
            bool reachable = Ff7AutoplayDriver.HasGatewayTopologyPath(session.Field.Walkmesh, session.Host.Process.BlockedTriangles, currentPlayer, session.Field.Triggers.Gateways[gatewayTarget.GatewayIndex!.Value], out int contact);
            AutoPlayEvent nativeGateway = currentNavigation.Events.Single(candidate => candidate.Id == gatewayTarget.Id);
            Ff7AutoPlayPolicy.RecordGatewayTopology(state, currentNavigation, nativeGateway, currentPlayer.Triangle, contact, reachable);
        }
        planner.RefreshUnavailable(observation.VisitId, facts);
        Check(!savedCoverage.Unavailable, "Shared guard refresh retained the obsolete random-producer refusal.");
        AutoPlayPlanStep? selected = planner.PlanNavigation(observation.NavigationKey, needsRefresh: _ => false, preferredGoal: state.NavigationGoal, readAvailability: (navigation, observed) => navigation.Key == observation.NavigationKey ? Ff7AutoPlayPolicy.ReadComponentAvailability(state, navigation, observed, facts) : Ff7AutoPlayPolicy.ReadRetainedNavigationAvailability(state, navigation, observed));
        Check(selected?.Event.Id == target.Id, $"The shared planner did not retry the restored native connector: {selected?.Event.Id}");
        Check(JsonSerializer.Serialize(session.Capture()) == before && File.ReadAllBytes(path).SequenceEqual(bytes), "Random-producer proof or native approach planning changed the source, VM, RNG or progress.");
        Console.WriteLine("PASS original random-producer invariant proof and configured native LINE approach; source unchanged.");
    }

    private static void OriginalAlternatingModeEscape()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_NAVIGATION_CYCLE_SAVE");
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("SKIP original alternating-mode escape: configure original resources and MEMORIA_FF7_NAVIGATION_CYCLE_SAVE.");
            return;
        }
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(root), CombinedSessionCodec.Read(input).Ff7!);
        Check(session.World is null, "The alternating-mode fixture must start before the native World exit.");
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("The bounded movement fixture unexpectedly requested a choice."));
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, chooseDialogueSelection: policy.ChooseDialogueSelection);
        controller.Start();
        for (int step = 0; step < 256 && controller.Running && session.World is null && session.Battle is null; step++)
        {
            controller.Advance(1);
        }
        Check(controller.Error is null && session.World is not null, $"Alternating native modes exhausted the frontier instead of retaining the bounded physical escape: {controller.Error}");
        Check(bytes.SequenceEqual(File.ReadAllBytes(path)), "The bounded native escape changed its external standalone source.");
        Console.WriteLine($"PASS original alternating run/walk history and native World exit after {controller.Steps} normal callbacks; source unchanged.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
