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
        Check(exploring.Status == Ff7AutoPlayWorldNavigationStatus.SearchLimited && exploring.Steps.Length == 1 && exploring.Steps[0].Input == new Ff7AutoplayInput(0, 1, Run: true), "A bounded physical cycle preference failed to retain a freshly admitted away detour or misclassified the search limit as absence.");
        CheckPhysicalCycle();
        OriginalWorldPreview();
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
        Dictionary<string, HashSet<Ff7AutoplayInput>> attempted = [];
        for (int callback = 0; callback < 3; callback++)
        {
            string key = Ff7AutoplayDriver.WorldPositionKey(source);
            if (!attempted.TryGetValue(key, out HashSet<Ff7AutoplayInput>? inputs))
            {
                attempted[key] = inputs = [];
            }
            Ff7AutoPlayWorldRoute route = Ff7AutoplayDriver.SearchWorldRoute(source, target, Preview, [], 1, inputs);
            Check(route.Status == Ff7AutoPlayWorldNavigationStatus.SearchLimited && route.Steps.Length == 1, "A native-admitted moving prefix was lost to stationary orientation states or treated as absence.");
            inputs.Add(route.Steps[0].Input);
            source = Preview(route.Steps[0].Input, source).State!;
        }
        Check(source.Player.X == 0 && source.Player.Z == 60 && source.Player.Direction == 3, "Orientation changes hid a two-position cycle instead of exploring its admitted away detour.");
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
