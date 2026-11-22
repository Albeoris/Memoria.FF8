using System.Numerics;
using System.Text.Json;
using Memoria.FF7.Runtime;

internal static class NativeWorldModelPortalTests
{
    internal static void Run()
    {
        Ff7WorldTerrainReference currentGround = new(0, 0, 1);
        Ff7WorldTerrainReference nextGround = new(0, 0, 2);
        Ff7AutoPlayWorldGuideFace current = new(currentGround, new(0, 0, 0), new(120, 0, 0), new(0, 0, 120), 0);
        Ff7AutoPlayWorldGuideFace next = new(nextGround, new(120, 0, 0), new(120, 0, 240), new(0, 0, 120), 0);
        Vector3? portal = Ff7AutoplayDriver.ReadWorldGuidePortal(current, next);
        Check(portal == new Vector3(60, 0, 60) && portal != next.Center, "An adjacent skew face used its center instead of the exact shared edge.");
        Check(Ff7AutoplayDriver.ReadWorldGuidePortal(next, current) == portal, "Reversing adjacent faces changed their physical portal.");
        Vector3 wrap = new(0x48000, 0, 0);
        Check(Ff7AutoplayDriver.ReadWorldGuidePortal(current, next with { A = next.A + wrap, B = next.B + wrap, C = next.C + wrap }) == portal, "World-wrapped adjacent vertices lost their actual shared portal.");
        Check(Ff7AutoplayDriver.ReadWorldGuidePortal(current, next with { A = new(180, 0, 0), C = new(180, 0, 120) }) is null, "Nonadjacent faces acquired a fabricated portal.");
        Ff7AutoPlayWorldMovementState source = new(0, 0, new(0, 0, 0, 0), new() { GroundTriangle = currentGround }, new(), 0, 0, null, 0);
        Ff7AutoPlayWorldModelScriptObservation target = new("model:6:event:4", 6, 4, new(6, 360, 0, 60), new(true, null, 0, 0, [], [], [], []), "predicate", "geometry", true, null);
        string before = JsonSerializer.Serialize(new { source, target, current, next });
        Ff7AutoPlayWorldMovementPreview Preview(Ff7AutoplayInput input, Ff7AutoPlayWorldMovementState state)
        {
            if (input.Horizontal != 0 && input.Vertical != 0)
            {
                return new(true, null, state);
            }
            int x = state.Player.X + input.Horizontal * 60;
            int z = state.Player.Z + input.Vertical * 60;
            if (!((x == 0 && z is 0 or 60) || (z == 60 && x is >= 0 and <= 360)))
            {
                return new(true, null, state);
            }
            Ff7AutoPlayWorldMovementState moved = state with { Player = state.Player with { X = x, Z = z }, Input = state.Input with { Held = input.Held, GroundTriangle = x >= 60 ? nextGround : currentGround } };
            return new(true, null, moved, x != state.Player.X || z != state.Player.Z, ModelBoundaries: x == 360 ? [new(6, 4)] : []);
        }
        Ff7AutoPlayWorldRoute route = Ff7AutoplayDriver.SearchWorldModelRoute(source, target, Preview, 128, waypoint: portal, intermediateGround: nextGround);
        Check(route.Status == Ff7AutoPlayWorldNavigationStatus.Step && route.Steps.Length == 2 && route.Steps.All(step => step.ModelBoundaries is null || step.ModelBoundaries.Length == 0), "A legal intermediate portal could not commit real ground progress without claiming model contact.");
        Ff7AutoPlayWorldMovementState reached = source;
        foreach (Ff7AutoPlayWorldRouteStep step in route.Steps)
        {
            reached = Preview(step.Input, reached).State!;
        }
        Check(reached.Input.GroundTriangle == nextGround && reached.Player.X == 60 && reached.Player.Z == 60 && route.Steps.All(step => (step.Input.Held & 32) != 0), "A portal route changed held-input semantics or stopped before its actual next ground owner.");
        Ff7AutoPlayWorldRoute contact = Ff7AutoplayDriver.SearchWorldModelRoute(source, target, Preview, 128);
        Check(contact.Status == Ff7AutoPlayWorldNavigationStatus.Step && contact.Steps[^1].ModelBoundaries?.Single() == new Ff7AutoPlayWorldModelBoundary(6, 4), "Omitting an intermediate portal weakened final native model admission.");
        Ff7AutoPlayWorldMovementPreview NearOnly(Ff7AutoplayInput input, Ff7AutoPlayWorldMovementState state) => new(true, null, state with { Player = state.Player with { X = 60, Z = 60 } }, true);
        Check(Ff7AutoplayDriver.SearchWorldModelRoute(source, target, NearOnly, 8, waypoint: portal, intermediateGround: nextGround).Status != Ff7AutoPlayWorldNavigationStatus.Step, "A closer pose without the actual next ground owner proved a portal.");
        Check(Ff7AutoplayDriver.SearchWorldModelRoute(source, target, NearOnly, 8, intermediateGround: currentGround).Status != Ff7AutoPlayWorldNavigationStatus.Step, "The unchanged source ground was accepted as intermediate progress.");
        foreach (Func<Ff7AutoplayInput, Ff7AutoPlayWorldMovementState, Ff7AutoPlayWorldMovementPreview> invalid in new Func<Ff7AutoplayInput, Ff7AutoPlayWorldMovementState, Ff7AutoPlayWorldMovementPreview>[]
        {
            (_, state) => new(false, "unsupported", state),
            (_, state) => new(true, null, state with { Input = state.Input with { GroundTriangle = nextGround } }, TerrainBoundary: new(0, 0, 0, false)),
            (_, state) => new(true, null, state with { Input = state.Input with { GroundTriangle = nextGround } }, ModelBoundaries: [new(5, 4)]),
            (_, state) => new(true, null, state with { Input = state.Input with { GroundTriangle = new(0, 0, 3) } })
        })
        {
            Check(Ff7AutoplayDriver.SearchWorldModelRoute(source, target, invalid, 8, intermediateGround: nextGround).Status != Ff7AutoPlayWorldNavigationStatus.Step, "Unsupported, effectful or wrong-ground preview acquired intermediate model-route authority.");
        }
        Check(before == JsonSerializer.Serialize(new { source, target, current, next }), "Portal planning changed source native mode, target or terrain geometry.");
        Console.WriteLine("Model routes commit exact adjacent ground portals while preserving final native model contact.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
