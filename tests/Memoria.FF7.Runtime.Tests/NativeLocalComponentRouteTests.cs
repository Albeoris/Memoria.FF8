using System.Text.Json;
using Memoria.AutoPlay;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Runtime;

internal static class NativeLocalComponentRouteTests
{
    internal static void Run()
    {
        CheckComponentChain();
        CheckRecordedStep();
        Console.WriteLine("Local native component route advice and recorded-step guards passed.");
    }

    private static void CheckComponentChain()
    {
        PcWalkmeshTriangle[] mesh = Enumerable.Range(0, 3).Select(index => new PcWalkmeshTriangle(index, new(index * 300, 0, 0), new(index * 300 + 100, 0, 0), new(index * 300, 100, 0), -1, -1, -1)).ToArray();
        Ff7Actor player = new() { Slot = 0, Entity = 1, Model = 0, Triangle = 0, X4096 = 20 << 12, Y4096 = 20 << 12, CollisionRadius = 10 };
        PcFieldGateway gateway = new(new(620, 20, 0), new(630, 20, 0), 0, 0, 0, 600, 0, 0);
        Ff7Line[] lines = [new(0, 4, 20, 20, 0, 30, 20, 0), new(1, 7, 320, 20, 0, 330, 20, 0)];
        Ff7AutoPlayTarget first = new("first", 4, 2, Ff7AutoPlayTargetKind.LinePush, "first-cf", false, default, MayChangeNavigation: true) { PossibleJumpLandings = [new(1, 10, new(320, 20, 1, 17), true)] };
        Ff7AutoPlayTarget second = new("second", 7, 2, Ff7AutoPlayTargetKind.LinePush, "second-cf", false, default, MayChangeNavigation: true) { PossibleJumpLandings = [new(1, 20, new(620, 20, 2, 17), true)] };
        byte[] blocked = [0];
        string before = JsonSerializer.Serialize(new { mesh, player, gateway, lines, first, second, blocked });
        bool Connects(Ff7AutoPlayTarget initial, Ff7AutoPlayTarget next, IReadOnlyList<Ff7Line>? actualLines = null, byte[]? mask = null) => Ff7AutoPlayPolicy.CanEstablishFieldGatewayComponentRoute(mesh, mask ?? blocked, player, gateway, initial, [initial, next], actualLines ?? lines);
        Check(Connects(first, second), "Two literal native LINE component steps did not expose the destination approach.");
        Check(!Connects(first, second with { PossibleJumpLandings = [new(1, 20, new(20, 20, 0, 17), true)] }), "A cyclic component chain did not terminate without a gateway path.");
        Check(!Connects(first, second, []), "Missing actual LINE contacts authorized a component route.");
        Check(!Connects(first, second, [lines[0] with { Enabled = 0 }, lines[1]]), "A disabled first LINE authorized a component route.");
        Check(!Connects(first, second, mask: [2]), "A blocked intermediate landing authorized a component route.");
        foreach (Ff7AutoPlayTarget invalid in new[]
        {
            first with { AnalysisComplete = false },
            first with { MayChangeNavigation = false },
            first with { ChangesStory = true },
            first with { ChangesField = true },
            first with { PossibleJumpLandings = [] },
            first with { PossibleJumpLandings = [new(2, 10, new(320, 20, 1, 17), true)] },
            first with { PossibleJumpLandings = [new(1, 10, new(320, 20, 1, 17), false)] },
            first with { PossibleJumpLandings = [new(1, 10, new(500, 20, 1, 17), true)] }
        })
        {
            Check(!Connects(invalid, second), "Unknown, unsupported, wrong-owner or invalid endpoint metadata authorized a component route.");
        }
        Check(before == JsonSerializer.Serialize(new { mesh, player, gateway, lines, first, second, blocked }), "Component advice mutated native state or geometry.");
    }

    private static void CheckRecordedStep()
    {
        Ff7AutoPlayTarget target = new("line", 4, 2, Ff7AutoPlayTargetKind.LinePush, "condition", false, default, MayChangeNavigation: true);
        Ff7AutoPlayObservation observation = new("visit", "program", 1, [target], true, true) { NavigationKey = "lower" };
        Ff7AutoPlayState state = new() { DestinationField = 600 };
        var planner = Ff7AutoPlayPolicy.CreatePlanner(state);
        AutoPlayEvent entry = new(target.Id, AutoPlayEventKind.Interaction, ConditionSignature: target.ConditionFingerprint);
        planner.Observe(new("visit", "program"), [entry]);
        planner.ObserveNavigation("visit", "lower", [entry]);
        planner.ObserveNavigation("visit", "upper", [entry]);
        planner.Completed("visit", target.Id, "visit", observedStoryChanged: false, entrySignature: target.ConditionFingerprint, entrySignatureAuthoritative: true, targetId: target.Id);
        Check(!Ff7AutoPlayPolicy.HasRecordedLocalNavigationStep(state, observation, target), "Legacy Completed without a real component route authorized replay.");
        AutoPlayEventRoute route = new("visit", target.ConditionFingerprint, false, [], target.Id);
        state.Coverage.Visits.Single().Events.Single(item => item.Event.Id == target.Id).Routes.Add(route);
        planner.RecordNavigationRoute("lower", "upper", target.Id, route);
        string before = JsonSerializer.Serialize(state.Coverage);
        Check(Ff7AutoPlayPolicy.HasRecordedLocalNavigationStep(state, observation, target), "An exact recorded native local component step was not reusable.");
        Check(!Ff7AutoPlayPolicy.HasRecordedLocalNavigationStep(state, observation, target with { ConditionFingerprint = "changed" }), "An old condition authorized component replay.");
        Check(!Ff7AutoPlayPolicy.HasRecordedLocalNavigationStep(state, observation with { NavigationKey = "other" }, target), "A different source component authorized replay.");
        Check(!Ff7AutoPlayPolicy.HasRecordedLocalNavigationStep(state, observation with { VisitId = "other" }, target), "An unknown destination visit authorized replay.");
        state.DestinationField = null;
        Check(!Ff7AutoPlayPolicy.HasRecordedLocalNavigationStep(state, observation, target), "A completed local step became intrinsic novelty without explicit destination intent.");
        Check(before == JsonSerializer.Serialize(state.Coverage), "Recorded-step advice changed native completion or route history.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
