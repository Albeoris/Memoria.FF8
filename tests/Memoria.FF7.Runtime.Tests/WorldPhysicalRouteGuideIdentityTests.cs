using System.Numerics;
using System.Text.Json;
using Memoria.FF7.Runtime;

internal static class WorldPhysicalRouteGuideIdentityTests
{
    internal static void Run()
    {
        CheckConstructiveDetour();
        CheckNativeStepValidation();
        Console.WriteLine("Validated world vehicle suffixes retain immutable guide intent across changing coarse waypoints.");
    }

    private static Ff7AutoPlayWorldScriptObservation Target => new("mesh:goal:script:7", 7, [], new(true, null, 0, 0, [], [], [], []), "predicate", "geometry", [], true, null);

    private static Ff7AutoPlayWorldMovementState Source => new(0, 1, new(5, 0, 0, 0), new() { GroundTriangle = new(0, 0, 77) }, new(), 0, 0, null, 0);

    private static Ff7AutoPlayWorldModeGuide Guide()
    {
        Ff7AutoPlayWorldGuideFace Face(int triangle) => new(new(0, 0, triangle), Vector3.Zero, Vector3.UnitX, Vector3.UnitZ, 0);
        Ff7AutoPlayWorldGuide transport = new([Face(77), Face(78), Face(92)], 3, false, null);
        return new(transport, 0, new(0, 0, 92), new(0, 0, 93), new([], 1, false, null), [new(0, 0, 93)], 3, false, null);
    }

    private static void CheckConstructiveDetour()
    {
        Ff7AutoPlayWorldModeGuide guide = Guide();
        Ff7AutoPlayWorldScriptObservation target = Target;
        Ff7AutoPlayWorldMovementState source = Source;
        string before = JsonSerializer.Serialize(new { source, target, guide });
        string identity = Ff7AutoplayDriver.ReadWorldVehicleApproachIdentity(source, target, guide);
        Ff7AutoPlayWorldMovementState[] states =
        [
            source,
            source with { Player = source.Player with { X = 1 }, Input = source.Input with { GroundTriangle = new(0, 0, 78) } },
            source with { Player = source.Player with { Z = 1 }, Input = source.Input with { GroundTriangle = new(0, 0, 77) } },
            source with { Player = source.Player with { X = 2, Z = 1 }, Input = source.Input with { GroundTriangle = new(0, 0, 92) } }
        ];
        Ff7AutoplayInput[] inputs = [new(1, 0), new(-1, 1), new(1, 0)];
        for (int index = 0; index < inputs.Length; index++)
        {
            var current = states[index];
            var next = states[index + 1];
            Check(Ff7AutoplayDriver.ReadWorldVehicleApproachIdentity(current, target, guide) == identity, "A coarse waypoint or constructive backstep discarded the ultimate guide intent.");
            Ff7AutoPlayWorldRouteStep retained = new(Ff7AutoplayDriver.WorldMovementKey(current), inputs[index], Ff7AutoplayDriver.WorldMovementKey(next), null);
            Check(Ff7AutoplayDriver.IsValidatedWorldVehicleRouteStep(retained, Ff7AutoplayDriver.WorldMovementKey(current), new(true, null, next, true)), "A previously proved native suffix was lost while crossing a coarse guide face.");
            Check(!Ff7AutoplayDriver.IsValidatedWorldVehicleRouteStep(retained, Ff7AutoplayDriver.WorldMovementKey(current), new(true, null, current)), "The same nonzero input without its predicted native progress validated a route step.");
        }
        Check(Ff7AutoplayDriver.ReadWorldVehicleApproachIdentity(source with { WorldId = 1 }, target, guide) != identity, "A changed world retained vehicle intent.");
        Check(Ff7AutoplayDriver.ReadWorldVehicleApproachIdentity(source with { WorldMapState = 2 }, target, guide) != identity, "A changed world map state retained vehicle intent.");
        Check(Ff7AutoplayDriver.ReadWorldVehicleApproachIdentity(source with { Player = source.Player with { Model = 0 } }, target, guide) != identity, "A changed controlled model retained vehicle intent.");
        Check(Ff7AutoplayDriver.ReadWorldVehicleApproachIdentity(source, target with { Id = "other" }, guide) != identity, "Another ultimate target retained the old route intent.");
        Check(Ff7AutoplayDriver.ReadWorldVehicleApproachIdentity(source, target with { PredicateFingerprint = "changed" }, guide) != identity, "Changed native predicates retained vehicle intent.");
        Check(Ff7AutoplayDriver.ReadWorldVehicleApproachIdentity(source, target with { GeometryFingerprint = "changed" }, guide) != identity, "Changed target geometry retained vehicle intent.");
        Check(Ff7AutoplayDriver.ReadWorldVehicleApproachIdentity(source, target, guide with { RiderModel = 1 }) != identity, "A changed rider retained vehicle intent.");
        Check(Ff7AutoplayDriver.ReadWorldVehicleApproachIdentity(source, target, guide with { LandingSource = new(0, 0, 94) }) != identity, "Another landing target retained vehicle intent.");
        Check(before == JsonSerializer.Serialize(new { source, target, guide }), "Reading route identity altered source, target or guide.");
    }

    private static void CheckNativeStepValidation()
    {
        Ff7AutoPlayWorldMovementState source = Source;
        Ff7AutoPlayWorldMovementState next = source with { Player = source.Player with { X = 1 } };
        Ff7AutoPlayWorldTerrainBoundary boundary = new(0, 0, 0, false, true);
        Ff7AutoPlayWorldModelBoundary model = new(2, 1, true);
        Ff7AutoPlayWorldRouteStep step = new(Ff7AutoplayDriver.WorldMovementKey(source), new(1, 0), Ff7AutoplayDriver.WorldMovementKey(next), boundary, [model]);
        Ff7AutoPlayWorldMovementPreview preview = new(true, null, next, true, boundary, [model]);
        Check(Ff7AutoplayDriver.IsValidatedWorldVehicleRouteStep(step, step.SourceKey, preview), "An exact safe native route step failed validation.");
        Check(!Ff7AutoplayDriver.IsValidatedWorldVehicleRouteStep(step, "other-source", preview), "A route suffix accepted another native source.");
        foreach (Ff7AutoPlayWorldMovementPreview invalid in new[]
        {
            preview with { Supported = false },
            preview with { State = null },
            preview with { State = next with { Input = next.Input with { PreviousControlHeld = 1 } } },
            preview with { TerrainBoundary = null },
            preview with { TerrainBoundary = boundary with { Script = 1 } },
            preview with { ModelBoundaries = [] },
            preview with { ModelBoundaries = [model with { Event = 2 }] }
        })
        {
            Check(!Ff7AutoplayDriver.IsValidatedWorldVehicleRouteStep(step, step.SourceKey, invalid), "A changed native result or boundary validated an obsolete route suffix.");
        }
        var effectfulTerrain = boundary with { Inert = false };
        Check(!Ff7AutoplayDriver.IsValidatedWorldVehicleRouteStep(step with { Boundary = effectfulTerrain }, step.SourceKey, preview with { TerrainBoundary = effectfulTerrain }), "An effectful unselected native terrain boundary was traversed.");
        var effectfulModel = model with { Inert = false };
        Check(!Ff7AutoplayDriver.IsValidatedWorldVehicleRouteStep(step with { ModelBoundaries = [effectfulModel] }, step.SourceKey, preview with { ModelBoundaries = [effectfulModel] }), "An effectful unselected model boundary was traversed.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
