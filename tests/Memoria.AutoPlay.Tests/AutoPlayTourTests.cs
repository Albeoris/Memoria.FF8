using System.Text.Json;
using Memoria.AutoPlay;

internal static class AutoPlayTourTests
{
    public static void Run()
    {
        AutoPlayTourLedger tour = new(new("region", [], CatalogReason: "The destination catalog is pending."));
        AutoPlayTourFrontier room = new("room", "street", "room", "predicate-a", AutoPlayTourKind.OptionalGameplay, "A reachable optional room remains unvisited.");
        AutoPlayTourFrontier story = new("story", "street", "next", "predicate-a", AutoPlayTourKind.StoryExit, "Story departure follows the optional tour.");
        tour.PublishCatalog([room, story], true, "All exits classified.");
        Check(!tour.Complete, "Classifying an optional room must not falsely complete its gameplay visit.");
        tour.Begin("room", "native-source");
        bool rejected = false;
        try
        {
            tour.CompleteVisit("room", "unrelated-return");
        }
        catch (InvalidOperationException)
        {
            rejected = true;
        }
        Check(rejected, "A return without native room entry must not complete the frontier.");
        tour.Arrive("room", "native-entry");
        AutoPlayTourLedger cold = new(JsonSerializer.Deserialize<AutoPlayTourState>(JsonSerializer.Serialize(tour.Capture()))!);
        Check(!cold.Complete, "Cold restore in an optional room must retain its unfinished return.");
        cold.CompleteVisit("room", "native-local-covered-return");
        Check(cold.Complete, "A covered native entry and return must complete the required optional visit.");
        Check(cold.Capture().Frontiers.Single(edge => edge.Id == "story").Status == AutoPlayTourStatus.Excluded, "A classified story exit must remain explicitly excluded, never falsely visited.");
        AutoPlayEventCoverageLedger coverage = new();
        coverage.UpdateTour(cold.Capture());
        coverage.Enter("field-a");
        coverage.Enter("field-b");
        AutoPlayEventCoverageState saved = coverage.Capture();
        Check(new AutoPlayTourLedger(saved.Tour!).Complete, "Local field scope transitions must preserve the regional tour.");
        saved.Tour!.Frontiers[0] = room;
        Check(new AutoPlayTourLedger(coverage.Capture().Tour!).Complete, "Returned snapshot arrays must not mutate the live tour.");
        cold.PublishCatalog([room with { Availability = "predicate-b" }, story], true, "Native availability changed.");
        Check(!cold.Complete, "A changed native availability predicate must reopen its required room visit.");
        Check(new AutoPlayEventCoverageLedger(new()).Capture().Tour is null, "Older coverage snapshots must not manufacture optional visits.");
        coverage.PublishCatalog([new("talk", "native-a")], true);
        coverage.Begin("talk", "native-request");
        coverage.Complete("talk", "native-return", "returned");
        coverage.Enter("side-room");
        coverage.Enter("field-b");
        Check(coverage.Current!.Events.Single().Status == AutoPlayEventCoverageStatus.Completed, "A native return to the same resource/story epoch must retain its already observed local interaction.");
        coverage.PublishCatalog([new("talk", "native-b")], true);
        Check(coverage.Current!.Events.Single().Status == AutoPlayEventCoverageStatus.Available, "A meaningful native predicate change after return must reopen that interaction.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
