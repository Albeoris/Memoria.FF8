using System.Numerics;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class AutoPlayWorldNavigationTests
{
    public static void Run()
    {
        Ff7WorldTerrainReference goalFace = new(0, 0, 1);
        Ff7AutoPlayWorldFrontierFace face = new(goalFace, new(295, 0, 55), new(305, 0, 55), new(300, 0, 65), 0, 0);
        Ff7AutoPlayWorldScriptObservation target = new("authored", 3, [face], new(true, null, 0, 0, [], [], [], []), "predicates", "geometry", [], true, null);
        Ff7AutoPlayWorldMovementState source = new(0, 0, new(0, 60, 0, 60), new(), new(), 0, 0, null, 0);
        HashSet<string> unrelatedBoundaries = [];
        Ff7AutoPlayWorldMovementPreview Preview(Ff7AutoplayInput input, Ff7AutoPlayWorldMovementState state)
        {
            int x = state.Player.X / 60 + input.Horizontal;
            int z = state.Player.Z / 60 + input.Vertical;
            if (x is < 0 or > 6 || z is < 0 or > 5 || x == 3 && z <= 3)
            {
                return new(true, null, state);
            }
            bool goal = x == 5 && z == 1;
            bool unrelated = x == 2 && z == 1;
            int rawScript = goal ? 3 : unrelated ? 4 : 0;
            Ff7AutoPlayWorldMovementState next = state with { Player = state.Player with { X = x * 60, Z = z * 60 }, Input = state.Input with { TerrainScript = rawScript, GroundTriangle = goal ? goalFace : new(0, 0, 0) } };
            Ff7AutoPlayWorldTerrainBoundary? boundary = rawScript >= 3 && state.Input.TerrainScript != rawScript ? new(0, 0, rawScript - 3, false) : null;
            if (unrelated)
            {
                unrelatedBoundaries.Add(Ff7AutoplayDriver.WorldMovementKey(next));
            }
            return new(true, null, next, state.Player.X != next.Player.X || state.Player.Z != next.Player.Z, boundary);
        }
        Ff7AutoPlayWorldRoute route = Ff7AutoplayDriver.SearchWorldRoute(source, target, Preview, [], 512);
        Check(route.Status == Ff7AutoPlayWorldNavigationStatus.Step && route.Steps.Length > 4 && route.Steps[^1].Boundary is { Script: 0 }, "Bounded native-state search failed to take a non-greedy detour around the authored wall.");
        Ff7AutoPlayWorldMovementState detour = source;
        bool temporaryRetreat = false;
        foreach (var step in route.Steps)
        {
            var next = Preview(step.Input, detour).State!;
            temporaryRetreat |= Ff7AutoplayDriver.WorldFaceDistance(next.Player, face) > Ff7AutoplayDriver.WorldFaceDistance(detour.Player, face);
            detour = next;
        }
        Check(temporaryRetreat, "The proved complete route no longer exercises its necessary temporary retreat.");
        Check(unrelatedBoundaries.Count > 0 && route.Steps.All(step => !unrelatedBoundaries.Contains(step.DestinationKey)), "The search forecast through an unrelated native script instead of stopping at its boundary.");
        Ff7AutoPlayWorldRoute limited = Ff7AutoplayDriver.SearchWorldRoute(source, target, Preview, [], 1);
        Check(limited.Status == Ff7AutoPlayWorldNavigationStatus.SearchLimited && limited.Expanded == 1, "Budget exhaustion was misclassified as physical unavailability or a proved complete route.");
        Ff7AutoPlayWorldMovementState occupied = source with { Player = source.Player with { X = 300 }, Input = source.Input with { TerrainScript = 3, GroundTriangle = goalFace } };
        Ff7AutoPlayWorldRoute rearm = Ff7AutoplayDriver.SearchWorldRoute(occupied, target, Preview, [new(240, 0, 60)], 128);
        Check(rearm.Status == Ff7AutoPlayWorldNavigationStatus.Rearming && rearm.Steps.Length > 0 && rearm.Steps.All(step => step.Boundary is null), "A same-number terrain script was retriggered without a safe native departure first.");
        Ff7AutoPlayWorldRoute unsupported = Ff7AutoplayDriver.SearchWorldRoute(source, target, (_, _) => new(false, "unsupported authored native mechanism"), [], 8);
        Check(unsupported.Status == Ff7AutoPlayWorldNavigationStatus.Unsupported && unsupported.Steps.Length == 0, "An unsupported movement contract authorized physical input.");
        Ff7AutoPlayWorldFrontierFace seam = new(new(35, 0, 0), new(0x48000 - 20, 0, 0), new(0x48000 - 10, 0, 0), new(0x48000 - 10, 0, 20), 0, 0);
        Check(Math.Abs(Ff7AutoplayDriver.WorldFaceDistance(new(0, 10, 0, 0), seam) - 20) < 0.001, "World target distance failed to respect the original wrapped surface seam.");
        Ff7AutoPlayWorldMovementState turned = source with { Player = source.Player with { Direction = 100, TargetDirection = 200 } };
        Check(Ff7AutoplayDriver.WorldMovementKey(source) != Ff7AutoplayDriver.WorldMovementKey(turned), "Native route revalidation lost its orientation state.");
        Check(Ff7AutoplayDriver.WorldPositionKey(source) == Ff7AutoplayDriver.WorldPositionKey(turned), "A changing smooth orientation hid an exact repeated physical position from cycle exploration.");
        Check(Ff7AutoplayDriver.WorldPositionKey(source) != Ff7AutoplayDriver.WorldPositionKey(source with { Player = source.Player with { X = source.Player.X + 1 } }), "Different physical positions shared a cycle preference key.");
        HashSet<Ff7AutoplayInput> attempted = [new(-1, -1, Run: true), new(0, -1, Run: true), new(1, -1, Run: true), new(-1, 0, Run: true), new(1, 0, Run: true), new(-1, 1, Run: true), new(1, 1, Run: true), new(Run: false)];
        Ff7AutoPlayWorldRoute exploring = Ff7AutoplayDriver.SearchWorldRoute(turned, target, Preview, [], 1, attempted);
        Check(exploring.Status == Ff7AutoPlayWorldNavigationStatus.SearchLimited && exploring.Steps.Length == 0, "A bounded search emitted an unproved away prefix or misclassified its budget as absence.");
        CheckPhysicalCycle();
        CheckTerrainGuidance();
        CheckForeignMatchingScript();
        OriginalGuideCycle();
        OriginalUnproductiveGuide();
        OriginalWorldPreview();
        OriginalDisconnectedGuide();
    }

    private static void OriginalGuideCycle()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_WORLD_GUIDE_CYCLE_SAVE");
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("SKIP original guide cycle: configure original resources and MEMORIA_FF7_WORLD_GUIDE_CYCLE_SAVE.");
            return;
        }
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream stream = new(bytes, writable: false);
        Ff7SessionSave saved = CombinedSessionCodec.Read(stream).Ff7!;
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(root), saved);
        Ff7WorldRuntime world = session.World!;
        string[] id = saved.AutoPlay!.Active!.EventId.Split(':');
        var target = world.ReadAutoPlayFrontierCatalog(int.Parse(id[1]), int.Parse(id[2])).Scripts.Single(script => script.Script == int.Parse(id[4]));
        Ff7AutoplayDriver driver = new(session);
        HashSet<string> positions = [];
        for (int step = 0; step < 32; step++)
        {
            var result = ChooseCompletedWorldInput(driver, target);
            Check(result.HasInput, "The original guide-cycle fixture lost native input admission.");
            driver.Apply(result.Input);
            session.AdvanceTick();
            positions.Add(Ff7AutoplayDriver.WorldPositionKey(world.CaptureAutoPlayMovementState()));
        }
        Check(positions.Count > 20, "Changing intermediate guide faces erased repeated-input memory and trapped original movement in a physical cycle.");
        Check(File.ReadAllBytes(path).SequenceEqual(bytes), "Guide-cycle continuation changed the original standalone source.");
    }

    private static void CheckForeignMatchingScript()
    {
        Ff7WorldTerrainReference destination = new(1, 0, 1);
        Ff7AutoPlayWorldFrontierFace face = new(destination, new(175, 0, -5), new(185, 0, -5), new(180, 0, 5), 0, 0);
        Ff7AutoPlayWorldScriptObservation target = new("matching-script", 3, [face], new(true, null, 0, 0, [], [], [], []), "conditions", "geometry", [], true, null);
        Ff7AutoPlayWorldMovementState source = new(0, 0, new(0, 0, 0, 0), new() { TerrainScript = 3, GroundTriangle = new(0, 0, 0) }, new(), 0, 0, null, 0);
        Ff7AutoPlayWorldMovementPreview Preview(Ff7AutoplayInput input, Ff7AutoPlayWorldMovementState state)
        {
            int x = state.Player.X + input.Horizontal * 60;
            if (input.Vertical != 0 || x is < 0 or > 240)
            {
                return new(true, null, state);
            }
            bool goal = x == 180;
            int script = x == 240 ? 0 : 3;
            Ff7AutoPlayWorldMovementState next = state with { Player = state.Player with { X = x }, Input = state.Input with { TerrainScript = script, GroundTriangle = goal ? destination : new(0, 0, 0) } };
            Ff7AutoPlayWorldTerrainBoundary? boundary = goal && state.Input.TerrainScript != script ? new(1, 0, 0, false) : null;
            return new(true, null, next, x != state.Player.X, boundary);
        }
        var approach = Ff7AutoplayDriver.SearchWorldRoute(source, target, Preview, [], 64);
        Check(approach.Status == Ff7AutoPlayWorldNavigationStatus.Step && approach.Steps.Length == 3 && approach.Steps.All(step => step.Input.Horizontal == 1 && step.Boundary is null), "A foreign matching script caused rearming instead of native staging at the actual target face.");
        foreach (var step in approach.Steps)
        {
            source = Preview(step.Input, source).State!;
        }
        var departure = Ff7AutoplayDriver.SearchWorldRoute(source, target, Preview, [new(240, 0, 0)], 64);
        Check(departure.Status == Ff7AutoPlayWorldNavigationStatus.Rearming && departure.Steps.Length == 1 && departure.Steps[0].Input.Horizontal == 1, "Staging at the target did not require a safe native departure.");
        source = Preview(departure.Steps[0].Input, source).State!;
        var arrival = Ff7AutoplayDriver.SearchWorldRoute(source, target, Preview, [], 64);
        Check(arrival.Status == Ff7AutoPlayWorldNavigationStatus.Step && arrival.Steps.Length == 1 && arrival.Steps[0].Boundary is { ChunkX: 1, Script: 0 }, "A staged matching-script route failed to trigger the actual target boundary after native rearming.");
    }

    private static void CheckPhysicalCycle()
    {
        Ff7AutoPlayWorldFrontierFace face = new(new(0, 0, 1), new(120, 0, -5), new(125, 0, 0), new(120, 0, 5), 0, 0);
        Ff7AutoPlayWorldScriptObservation target = new("cycle", 3, [face], new(true, null, 0, 0, [], [], [], []), "conditions", "geometry", [], true, null);
        Ff7AutoPlayWorldMovementState source = new(0, 0, new(0, 0, 0, 0), new(), new(), 0, 0, null, 0);
        Ff7AutoPlayWorldMovementPreview Preview(Ff7AutoplayInput input, Ff7AutoPlayWorldMovementState state)
        {
            Ff7WorldEntity player = state.Player with { Direction = state.Player.Direction + 1 };
            if (player.X == 0 && input.Horizontal == 1 && input.Vertical == 0)
            {
                player = player with { X = 60 };
            }
            else if (player.X == 60 && input.Horizontal == -1 && input.Vertical == 0)
            {
                player = player with { X = 0 };
            }
            else if (player.X == 0 && input.Horizontal == 0 && input.Vertical == 1)
            {
                player = player with { Z = 60 };
            }
            return new(true, null, state with { Player = player }, player.X != state.Player.X || player.Z != state.Player.Z);
        }
        Ff7AutoPlayWorldRoute advancing = Ff7AutoplayDriver.SearchWorldRoute(source, target, Preview, [], 1);
        Check(advancing.Steps.Length == 1 && advancing.Steps[0].Input.Horizontal == 1, "An advancing bounded prefix was discarded.");
        source = Preview(advancing.Steps[0].Input, source).State!;
        Ff7AutoPlayWorldRoute stalled = Ff7AutoplayDriver.SearchWorldRoute(source, target, Preview, [], 1);
        Check(stalled.Status == Ff7AutoPlayWorldNavigationStatus.SearchLimited && stalled.Steps.Length == 0, "A nonadvancing physical retreat was emitted as a useful bounded prefix or misclassified as absence.");
        Ff7AutoPlayWorldRoute exhausted = Ff7AutoplayDriver.SearchWorldRoute(source, target, Preview, [], 32);
        Check(exhausted.Status == Ff7AutoPlayWorldNavigationStatus.Blocked && exhausted.Steps.Length == 0, "A fully exhausted finite native-state graph retained an out-and-back route.");
    }

    private static void OriginalUnproductiveGuide()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_WORLD_ROUTE_STALL_SAVE");
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("SKIP original unproductive guide: configure original resources and MEMORIA_FF7_WORLD_ROUTE_STALL_SAVE.");
            return;
        }
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream stream = new(bytes, writable: false);
        Ff7SessionSave saved = CombinedSessionCodec.Read(stream).Ff7!;
        using (Ff7Session policySession = Ff7Session.Restore(Ff7Installation.Discover(root), saved))
        using (Ff7AutoPlayPolicy policy = new(policySession, (_, _) => throw new InvalidOperationException("The planning regression unexpectedly requested a choice checkpoint.")))
        {
            policy.State.FrontierProgressKey = null;
            policy.State.FrontierIdleSteps = 0;
            bool pending = false;
            Ff7AutoPlayController livePolicy = new(policySession, pendingDriver =>
            {
                Ff7AutoplayInput input = policy.ChooseInput(pendingDriver);
                pending = pendingDriver.PlanningPending;
                return input;
            });
            livePolicy.Start();
            for (int callback = 0; callback < 64 && !pending; callback++)
            {
                Ff7SessionSave nativeBefore = policySession.Capture();
                long stepsBefore = livePolicy.Steps;
                livePolicy.Advance(1);
                if (pending)
                {
                    Ff7SessionSave nativeAfter = policySession.Capture();
                    Check(livePolicy.Running && livePolicy.Error is null && livePolicy.Steps == stepsBefore, "The actual policy rejected or executed a pending planning slice.");
                    Check(JsonSerializer.Serialize(nativeBefore.World) == JsonSerializer.Serialize(nativeAfter.World) && JsonSerializer.Serialize(nativeBefore.Progress) == JsonSerializer.Serialize(nativeAfter.Progress) && JsonSerializer.Serialize(nativeBefore.Vm) == JsonSerializer.Serialize(nativeAfter.Vm), "Policy planning advanced native World execution, progress or script state.");
                }
            }
            Check(pending, "The original policy fixture did not exercise pending physical planning.");
            livePolicy.SaveAndStop();
        }
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(root), saved);
        Ff7WorldRuntime world = session.World!;
        string[] id = saved.AutoPlay!.Active!.EventId.Split(':');
        var target = world.ReadAutoPlayFrontierCatalog(int.Parse(id[1]), int.Parse(id[2])).Scripts.Single(script => script.Script == int.Parse(id[4]));
        string before = JsonSerializer.Serialize(session.Capture());
        HashSet<(Ff7WorldTerrainReference From, Ff7WorldTerrainReference To)> trials = [];
        Ff7AutoPlayWorldGuide guide = world.ReadAutoPlayNavigationGuide(target);
        while (guide.Faces.Length > 1)
        {
            Check(trials.Add((guide.Faces[0].Source, guide.Faces[1].Source)), "A rejected advisory transition was selected again.");
            guide = world.ReadAutoPlayNavigationGuide(target, unproductiveEdges: trials);
        }
        Check(guide.Faces.Length == 0 && guide.DeferredComponent is null, "Exhausted advisory trials incorrectly proved native physical unavailability.");
        Ff7AutoPlayController controller = new(session, pendingDriver => pendingDriver.ChooseWorldInput(target).Input);
        controller.Start();
        controller.Advance(1);
        Check(controller.Steps == 0 && JsonSerializer.Serialize(session.Capture()) == before, "A pending bounded planning slice advanced native execution or applied stale input.");
        controller.SaveAndStop();
        Ff7AutoplayDriver driver = new(session);
        Ff7AutoPlayWorldMovementState initial = world.CaptureAutoPlayMovementState();
        var first = ChooseCompletedWorldInput(driver, target);
        Check(JsonSerializer.Serialize(session.Capture()) == before, "Advisory route repair changed native state before applying input.");
        HashSet<Ff7WorldTerrainReference> crossed = [];
        for (int step = 0; step < 96 && session.Battle is null; step++)
        {
            var result = step == 0 ? first : ChooseCompletedWorldInput(driver, target);
            Check(result.HasInput, "The original unproductive guide did not yield an advancing alternative.");
            driver.Apply(result.Input);
            session.AdvanceTick();
            if (world.CaptureAutoPlayMovementState().Input.GroundTriangle is { } ground)
            {
                crossed.Add(ground);
            }
        }
        var player = world.CaptureAutoPlayMovementState().Player;
        Check(crossed.Count >= 4 && Vector2.Distance(new(initial.Player.X, initial.Player.Z), new(player.X, player.Z)) > 2048, "The original bounded continuation still circles its initial face instead of following a native-admitted alternative.");
        Check(File.ReadAllBytes(path).SequenceEqual(bytes), "Advisory repair changed the standalone source bytes.");
    }

    private static Ff7AutoPlayWorldNavigationResult ChooseCompletedWorldInput(Ff7AutoplayDriver driver, Ff7AutoPlayWorldScriptObservation target)
    {
        for (int slice = 0; slice < 64; slice++)
        {
            var result = driver.ChooseWorldInput(target);
            Check(result.Expanded <= 512, "Advisory retries exceeded the physical planning-slice budget.");
            if (!driver.PlanningPending)
            {
                return result;
            }
            Check(!result.HasInput && result.Status == Ff7AutoPlayWorldNavigationStatus.SearchLimited, "Pending planning work emitted physical input or reported unavailability.");
        }
        throw new InvalidOperationException("The original local advisory repair failed to finish its bounded planning slices.");
    }

    private static void CheckTerrainGuidance()
    {
        Ff7AutoPlayWorldGuideFace Face(int id, int x, int z) => new(new(0, 0, id), new(x, 0, z), new(x + 30, 0, z), new(x, 0, z + 30), 0);
        Ff7AutoPlayWorldGuideFace[] faces = [Face(0, 0, 0), Face(1, 0, 300), Face(2, 300, 300), Face(3, 300, 0), Face(4, 0, -300)];
        int[][] topology = [[1, 4], [0, 2], [1, 3], [2], [0]];
        var route = Ff7WorldRuntime.PlanAutoPlayTerrainGuide(faces[0].Source, new HashSet<Ff7WorldTerrainReference> { faces[3].Source }, reference => faces[reference.Triangle], reference => topology[reference.Triangle].Select(index => faces[index].Source), 16);
        Check(route.Faces.Select(face => face.Source.Triangle).SequenceEqual([0, 1, 2, 3]), "Native terrain-component advice did not take the connected detour around a gap or entered a dead-end component.");
        var limited = Ff7WorldRuntime.PlanAutoPlayTerrainGuide(faces[0].Source, new HashSet<Ff7WorldTerrainReference> { faces[3].Source }, reference => faces[reference.Triangle], reference => topology[reference.Triangle].Select(index => faces[index].Source), 1);
        Check(limited.Limited && limited.Faces.Length == 0, "A bounded topology search claimed physical absence or invented an unsearched route.");
        Check(limited.DeferredComponent is null, "An incomplete topology search invented a closed component deferral.");
        var disconnected = Ff7WorldRuntime.PlanAutoPlayTerrainGuide(faces[0].Source, new HashSet<Ff7WorldTerrainReference> { new(0, 0, 99) }, reference => faces[reference.Triangle], reference => topology[reference.Triangle].Select(index => faces[index].Source), 16);
        Check(!disconnected.Limited && disconnected.DeferredComponent is { Length: 5 } && disconnected.DeferredComponent.Select(reference => reference.Triangle).Order().SequenceEqual([0, 1, 2, 3, 4]), "An exhausted advisory search lost its exact closed component or included unvisited faces.");
        Ff7AutoPlayWorldFrontierFace targetFace = new(faces[3].Source, faces[3].A, faces[3].B, faces[3].C, 0, 0);
        Ff7AutoPlayWorldScriptObservation target = new("guided", 3, [targetFace], new(true, null, 0, 0, [], [], [], []), "conditions", "geometry", [], true, null);
        Ff7AutoPlayWorldMovementState source = new(0, 0, new(0, 0, 0, 0), new(), new(), 0, 0, null, 0);
        int calls = 0;
        Ff7AutoPlayWorldMovementPreview Preview(Ff7AutoplayInput input, Ff7AutoPlayWorldMovementState state)
        {
            calls++;
            return input.Vertical == 1 && input.Horizontal == 0 ? new(true, null, state with { Player = state.Player with { Z = 60 } }, true) : new(true, null, state);
        }
        var step = Ff7AutoplayDriver.SearchWorldRoute(source, target, Preview, [], 1, waypoint: faces[1].Center);
        Check(step.Status == Ff7AutoPlayWorldNavigationStatus.SearchLimited && step.Steps.Length == 1 && step.Steps[0].Input.Vertical == 1 && calls == 9, "Coarse advice failed to guide an away detour through native input admission or falsely completed its remote script.");
        var refused = Ff7AutoplayDriver.SearchWorldRoute(source, target, (_, _) => new(false, "native footprint rejects guide"), [], 2, waypoint: faces[1].Center);
        Check(refused.Steps.Length == 0 && refused.Status == Ff7AutoPlayWorldNavigationStatus.Unsupported, "A coarse topology edge authorized movement without its complete native footprint preview.");
    }

    private static void OriginalDisconnectedGuide()
    {
        string? configuration = Environment.GetEnvironmentVariable("MEMORIA_FF7_WORLD_DISCONNECTED_TARGET");
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_WORLD_TRANSPORT_SAVE");
        if (string.IsNullOrWhiteSpace(configuration) || string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("SKIP original disconnected guide: configure a native fixture and target chunk X,Z,script,component count.");
            return;
        }
        int[] expected = configuration.Split(',').Select(value => int.Parse(value, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        Check(expected.Length == 4, "A disconnected fixture requires target chunk X,Z,script and expected component count.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream stream = new(bytes, writable: false);
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(root), CombinedSessionCodec.Read(stream).Ff7!);
        Ff7WorldRuntime world = session.World ?? throw new InvalidDataException("The disconnected fixture requires a native world.");
        string before = JsonSerializer.Serialize(session.Capture());
        string context = world.ReadAutoPlayNavigationContext();
        Ff7AutoPlayWorldScriptObservation target = world.ReadAutoPlayFrontierCatalog(expected[0], expected[1]).Scripts.Single(entry => entry.Script == expected[2]);
        Ff7AutoPlayWorldNavigationResult result = new Ff7AutoplayDriver(session).ChooseWorldInput(target);
        Check(result.Status == Ff7AutoPlayWorldNavigationStatus.Blocked && !result.HasInput && result.DeferredComponent is { } component && component.Length == expected[3] && component.Contains(world.Process.Input!.GroundTriangle!), "An exhausted native guide lost its bounded component-scoped deferral.");
        Check(context == world.ReadAutoPlayNavigationContext() && before == JsonSerializer.Serialize(session.Capture()), "Reading an exhausted native component changed world execution or admission context.");
        Check(bytes.AsSpan().SequenceEqual(File.ReadAllBytes(path)), "The disconnected guide changed its original standalone fixture.");
    }

    private static void OriginalWorldPreview()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_WORLD_TRANSPORT_SAVE");
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("SKIP original world navigation preview: configure original resources and MEMORIA_FF7_WORLD_TRANSPORT_SAVE.");
            return;
        }
        byte[] source = File.ReadAllBytes(path);
        using MemoryStream stream = new(source, writable: false);
        Ff7SessionSave saved = CombinedSessionCodec.Read(stream).Ff7 ?? throw new InvalidDataException("The native world fixture has no FFVII session.");
        Ff7Installation installation = Ff7Installation.Discover(root);
        using Ff7Session observed = Ff7Session.Restore(installation, saved);
        using Ff7Session untouched = Ff7Session.Restore(installation, saved);
        Ff7WorldRuntime world = observed.World ?? throw new InvalidDataException("The fixture requires its original world continuation.");
        string before = JsonSerializer.Serialize(observed.Capture());
        if (observed.AutoPlayState?.Active is { Kind: Ff7AutoPlayTargetKind.WorldMesh } active)
        {
            string[] identity = active.EventId.Split(':');
            Check(identity.Length == 5 && identity[0] == "mesh" && identity[3] == "script", "The configured native guide fixture has an invalid active mesh identity.");
            int chunkX = int.Parse(identity[1], System.Globalization.CultureInfo.InvariantCulture);
            int chunkZ = int.Parse(identity[2], System.Globalization.CultureInfo.InvariantCulture);
            int script = int.Parse(identity[4], System.Globalization.CultureInfo.InvariantCulture);
            Ff7AutoPlayWorldScriptObservation target = world.ReadAutoPlayFrontierCatalog(chunkX, chunkZ).Scripts.Single(entry => entry.Script == script);
            Ff7AutoPlayWorldGuide guide = world.ReadAutoPlayNavigationGuide(target);
            Check(guide.Faces.Length > 0 && guide.Faces[^1].Script == script && !guide.Limited, $"The configured native guide fixture lost its terrain-transition route: {guide.Reason}");
            Check(before == JsonSerializer.Serialize(observed.Capture()), "Native terrain-transition advice changed the source world execution state.");
        }
        Ff7AutoPlayWorldMovementPreview preview = world.PreviewAutoPlayInput(new(Horizontal: 1));
        Check(preview is { Supported: true, State: not null, RequiresVmPhase: true }, $"The configured native foot-input fixture is not supported: {preview.Reason}");
        Check(before == JsonSerializer.Serialize(observed.Capture()), "A native world preview changed saved progression, scripts, models, input/collision caches or RNG.");
        foreach (Ff7Session session in new[] { observed, untouched })
        {
            session.SetInput(0, 0);
            session.MovePlayer(0, 0, true);
            session.AdvanceTick(advanceClock: false);
        }
        Check(JsonSerializer.Serialize(observed.Capture()) == JsonSerializer.Serialize(untouched.Capture()), "Native preview cache/RNG restoration changed the next original neutral callback.");
        Check(source.AsSpan().SequenceEqual(File.ReadAllBytes(path)), "World navigation preview modified its original standalone fixture.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
