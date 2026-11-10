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
        cold.Begin("room", "native-b-request");
        cold.Arrive("room", "native-b-entry");
        cold.CompleteVisit("room", "native-b-covered-return");
        AutoPlayTourLedger repeated = new(JsonSerializer.Deserialize<AutoPlayTourState>(JsonSerializer.Serialize(cold.Capture()))!);
        repeated.PublishCatalog([room, story], true, "The already observed native baseline returned.");
        Check(repeated.Complete && repeated.Capture().Frontiers.Single(edge => edge.Id == "room").ReturnWitness == "native-local-covered-return", "Returning to a previously observed predicate must preserve its actual receipt instead of generating a repeat-visit loop.");
        Check(new AutoPlayEventCoverageLedger(new()).Capture().Tour is null, "Older coverage snapshots must not manufacture optional visits.");
        coverage.PublishCatalog([new("talk", "native-a")], true);
        coverage.Begin("talk", "native-request");
        coverage.Complete("talk", "native-return", "returned");
        coverage.Enter("side-room");
        coverage.Enter("field-b");
        Check(coverage.Current!.Events.Single().Status == AutoPlayEventCoverageStatus.Completed, "A native return to the same resource/story epoch must retain its already observed local interaction.");
        coverage.PublishCatalog([new("talk", "native-b")], true);
        Check(coverage.Current!.Events.Single().Status == AutoPlayEventCoverageStatus.Available, "A meaningful native predicate change after return must reopen that interaction.");
        RunNested();
    }

    private static void RunNested()
    {
        AutoPlayTourFrontier parent = new("parent", "root", "street", "resource-a", AutoPlayTourKind.OptionalGameplay, "Current street gameplay.");
        AutoPlayTourFrontier child = new("child", "street", "room", "resource-b", AutoPlayTourKind.OptionalGameplay, "Current room gameplay.", ParentId: "parent");
        AutoPlayTourFrontier sibling = child with { Id = "sibling", Destination = "other" };
        AutoPlayTourLedger nested = new(new("nested", [], CatalogReason: "Directory pending."));
        nested.PublishCatalog([parent, child, sibling], true, "Root exits classified.");
        nested.PublishDirectory(new("street", "source-a:flags0", false, "Children pending.", "parent"));
        Reject(() => nested.Begin("child", "fabricated-parent"), "A child began without an observed active parent.");
        nested.Begin("parent", "native-root-outbound");
        Reject(() => nested.Begin("child", "no-parent-arrival"), "A child began before its native parent arrival.");
        nested.Arrive("parent", "native-street-entry");
        Reject(() => nested.CompleteVisit("parent", "early-parent-return"), "Pending descendants falsely completed their parent.");
        nested.Begin("child", "covered-street-outbound");
        Reject(() => nested.CompleteVisit("child", "no-native-arrival"), "A child return completed without native entry.");
        nested.Arrive("child", "native-room-entry");
        Reject(() => nested.Begin("sibling", "parallel-branch"), "Two child branches became active simultaneously.");
        AutoPlayTourState capture = nested.Capture();
        AutoPlayTourLedger cold = new(JsonSerializer.Deserialize<AutoPlayTourState>(JsonSerializer.Serialize(capture))!);
        Check(cold.ActiveLeaf?.Id == "child" && cold.Capture().Frontiers.Single(edge => edge.Id == "parent").Status == AutoPlayTourStatus.Active, "Cold restore lost the active child or suspended parent.");
        capture.Directories![0] = capture.Directories[0] with { Complete = true };
        Check(!cold.Capture().Directories![0].Complete, "A directory snapshot mutated the live ledger.");
        cold.CompleteVisit("child", "covered-native-room-return");
        Check(cold.ActiveLeaf?.Id == "parent", "A child's native return did not resume its observed parent.");
        cold.Begin("sibling", "covered-street-other-outbound");
        cold.Arrive("sibling", "native-other-entry");
        cold.CompleteVisit("sibling", "covered-native-other-return");
        Reject(() => cold.CompleteVisit("parent", "incomplete-directory"), "An unreviewed source directory falsely completed its parent.");
        cold.PublishDirectory(new("street", "source-a:flags0", true, "All current children reviewed.", "parent"));
        cold.CompleteVisit("parent", "covered-native-street-return");
        Check(cold.Complete, "Observed nested returns and reviewed directories did not complete the tour.");
        AutoPlayTourState finished = cold.Capture();
        Check(finished.Frontiers.Single(edge => edge.Id == "child").ReturnWitness == "covered-native-room-return", "Parent completion replaced a child's native receipt.");
        Reject(() => new AutoPlayTourLedger(finished with { Directories = null }), "A completed nested parent restored without its reviewed source directory.");
        Reject(() => new AutoPlayTourLedger(finished with { Directories = [new("wrong-source", "resource", true, "Invalid binding.", "parent")] }), "A directory bound to the wrong native source was accepted.");
        Reject(() => new AutoPlayTourLedger(finished with { Frontiers = [parent with { ParentId = "child", Source = "room" }, child] }), "Cyclic parent links were accepted.");
        Reject(() => new AutoPlayTourLedger(finished with { Frontiers = [parent with { Status = AutoPlayTourStatus.Active, BeforeWitness = "a", EntryWitness = "b" }, parent with { Id = "other-root", Status = AutoPlayTourStatus.Active, BeforeWitness = "c", EntryWitness = "d" }], Directories = null }), "Multiple active root branches were accepted.");
        Reject(() => new AutoPlayTourLedger(finished with { Frontiers = [parent, child with { ParentId = "missing" }], Directories = null }), "A missing historical parent was invented.");
        AutoPlayTourLedger legacy = new(new("legacy", [parent with { Status = AutoPlayTourStatus.Active, BeforeWitness = "old-outbound", EntryWitness = "old-entry" }], true));
        Check(legacy.Capture().Directories is null && legacy.ActiveLeaf?.ParentId is null, "An older flat save acquired invented hierarchy or directories.");
        legacy.CompleteVisit("parent", "old-covered-native-return");
        Check(legacy.Complete, "Absent optional hierarchy fields broke an older flat visit.");
        string legacyJson = JsonSerializer.Serialize(legacy.Capture(), new JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull });
        AutoPlayTourLedger absentFields = new(JsonSerializer.Deserialize<AutoPlayTourState>(legacyJson)!);
        Check(absentFields.Complete && absentFields.Capture().Directories is null && absentFields.Capture().Frontiers[0].ParentId is null, "A legacy JSON save with absent hierarchy fields acquired invented state.");
    }

    private static void Reject(Action action, string message)
    {
        try
        {
            action();
        }
        catch (Exception error) when (error is InvalidOperationException or InvalidDataException)
        {
            return;
        }
        throw new InvalidOperationException(message);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
