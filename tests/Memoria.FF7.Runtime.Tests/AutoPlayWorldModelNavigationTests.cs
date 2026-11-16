using System.Text.Json;
using Memoria.FF7.Runtime;

internal static class AutoPlayWorldModelNavigationTests
{
    internal static void Run()
    {
        Ff7AutoPlayWorldMovementState source = new(0, 0, new(0, 60, 0, 60), new(), new(), 0, 0, null, 0);
        Ff7AutoPlayWorldModelScriptObservation target = new("model:6:event:4", 6, 4, new(6, 300, 0, 60), new(true, null, 0, 0, [], [], [], []), "predicate", "geometry", true, null);
        string before = JsonSerializer.Serialize(source);
        List<(int X, int Z)> rejected = [];
        Ff7AutoPlayWorldMovementPreview Preview(Ff7AutoplayInput input, Ff7AutoPlayWorldMovementState state)
        {
            int x = state.Player.X + input.Horizontal * 60;
            int z = state.Player.Z + input.Vertical * 60;
            if (x is < 0 or > 360 || z is < 0 or > 180)
            {
                return new(true, null, state);
            }
            Ff7AutoPlayWorldMovementState next = state with { Player = state.Player with { X = x, Z = z }, Input = state.Input with { Held = input.Held } };
            bool foreign = x == 120 && z == 60;
            bool terrain = x == 180 && z == 60;
            bool goal = x == 300 && z == 60;
            if (foreign || terrain)
            {
                rejected.Add((x, z));
            }
            Ff7AutoPlayWorldModelBoundary[] contacts = foreign ? [new(5, 4)] : goal ? [new(6, (input.Held & 32) != 0 ? 4 : 3)] : [];
            return new(true, null, next, x != state.Player.X || z != state.Player.Z, terrain ? new(0, 0, 0, false) : null, contacts);
        }
        Ff7AutoPlayWorldRoute route = Ff7AutoplayDriver.SearchWorldModelRoute(source, target, Preview, 128);
        Check(route.Status == Ff7AutoPlayWorldNavigationStatus.Step && route.Steps.Length > 0 && route.Steps[^1].ModelBoundaries?.Single() == new Ff7AutoPlayWorldModelBoundary(6, 4), "Model navigation must finish at the exact native model and Confirm event.");
        Check(route.Steps.All(step => (step.Input.Held & 32) != 0), "Confirm model goals must use the native held input.");
        Ff7AutoPlayWorldMovementState cursor = source;
        foreach (Ff7AutoPlayWorldRouteStep step in route.Steps)
        {
            Ff7AutoPlayWorldMovementPreview next = Preview(step.Input, cursor);
            Check(next.TerrainBoundary is null && (next.ModelBoundaries ?? []).All(boundary => boundary.Model == 6), "A model route crossed an effectful terrain or foreign model dispatch.");
            cursor = next.State!;
        }
        Check(rejected.Count > 0 && before == JsonSerializer.Serialize(source), "Bounded model planning must examine barriers without changing its source state.");
        Ff7AutoPlayWorldRoute touch = Ff7AutoplayDriver.SearchWorldModelRoute(source, target with { Event = 3 }, Preview, 128);
        Check(touch.Status == Ff7AutoPlayWorldNavigationStatus.Step && touch.Steps.All(step => (step.Input.Held & 32) == 0) && touch.Steps[^1].ModelBoundaries?.Single() == new Ff7AutoPlayWorldModelBoundary(6, 3), "Event 3 must remain an ordinary native contact without Confirm.");
        Ff7AutoPlayWorldRoute limited = Ff7AutoplayDriver.SearchWorldModelRoute(source, target, Preview, 1);
        Check(limited.Status == Ff7AutoPlayWorldNavigationStatus.SearchLimited && limited.Expanded == 1, "A bounded model search must preserve its exact node budget and report an unfinished search.");
        Ff7AutoPlayWorldRoute wrong = Ff7AutoplayDriver.SearchWorldModelRoute(source, target, (_, state) => new(true, null, state, ModelBoundaries: [new(6, 3)]), 8);
        Check(wrong.Status == Ff7AutoPlayWorldNavigationStatus.Blocked && wrong.Steps.Length == 0, "The wrong native event must not satisfy or admit the model goal.");
        Ff7AutoPlayWorldRoute noContact = Ff7AutoplayDriver.SearchWorldModelRoute(source, target, (_, state) => new(true, null, state with { Player = target.Target with { Model = state.Player.Model } }), 8);
        Check(noContact.Status != Ff7AutoPlayWorldNavigationStatus.Step, "A matching target position without a native contact boundary must not prove interaction.");
        Ff7AutoPlayWorldScriptObservation mesh = new("terrain", 3, [new(new(0, 0, 0), new(300, 0, 0), new(360, 0, 0), new(300, 0, 120), 0, 0)], new(true, null, 0, 0, [], [], [], []), "p", "g", [], true, null);
        Ff7AutoPlayWorldRoute foreignModel = Ff7AutoplayDriver.SearchWorldRoute(source, mesh, (_, state) => new(true, null, state, ModelBoundaries: [new(6, 3)]), [], 8);
        Check(foreignModel.Status == Ff7AutoPlayWorldNavigationStatus.Blocked && foreignModel.Steps.Length == 0, "Terrain navigation must treat unplanned native model contacts as execution barriers.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
