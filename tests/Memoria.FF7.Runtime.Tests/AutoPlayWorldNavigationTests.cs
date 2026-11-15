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
        OriginalWorldPreview();
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
