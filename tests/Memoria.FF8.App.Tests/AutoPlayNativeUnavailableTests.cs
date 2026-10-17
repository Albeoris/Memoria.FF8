using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayNativeUnavailableTests
{
    [Fact]
    public void ActualNativeUnavailableArrivalRestoresItsOwnBeforeEventSaveAndColdResumeImportsThatSave()
    {
        string manifest = TestResources.FixturePath("App.Tests.AutoPlayNativeUnavailableTests.1");
        byte[] originalManifest = File.ReadAllBytes(manifest);
        AutoPlayRunState initial = JsonSerializer.Deserialize<AutoPlayRunState>(originalManifest, AutoPlayController.JsonOptions)!;
        string snapshot = Path.Combine(Path.GetDirectoryName(manifest)!, initial.Snapshot!);
        byte[] originalSnapshot = File.ReadAllBytes(snapshot);
        GameState original = GameSession.Load(snapshot);
        string output = Path.Combine(Path.GetTempPath(), "ff8-native-unavailable-" + Guid.NewGuid().ToString("N"));
        GameSession session = GameSession.CreateColdProcess();
        GameWorld? world = null;
        try
        {
            GameWorld GetWorld()
            {
                return world ??= new(session, TestResources.GameDirectory, validateWorldPresentation: false);
            }
            void Restore(GameState state)
            {
                world?.Dispose();
                world = null;
                session.Restore(state);
            }
            AutoPlayController controller = new(session, GetWorld, Restore, output);
            controller.ResumeFrom(manifest);
            Assert.Null(controller.State.ActiveBeforeCheckpoint);
            AutoPlayEventCoverage Attempt(AutoPlayController current) => current.State.Coverage.Visits.Single(visit => visit.Visit.Key == "125:140").Events.Single(item => item.Event.Id == "125:line:18");
            for (int tick = 0; tick < 24 && Attempt(controller).UnavailableAttempts.Count == 0 && controller.Error is null; tick++)
            {
                controller.Advance(1);
            }
            Assert.Null(controller.Error);
            AutoPlayEventCoverage attempt = Attempt(controller);
            Assert.False(attempt.Unavailable);
            Assert.False(attempt.Completed);
            Assert.Null(attempt.BlockedReason);
            Assert.Null(attempt.Conditions!.ActiveEntry);
            Assert.Empty(attempt.Conditions.ConsumedEntries);
            Assert.Empty(attempt.Conditions.ConsumedExits);
            Assert.Empty(attempt.Routes);
            string targetId = Assert.Single(attempt.UnavailableAttempts).TargetId!;
            Assert.Equal(targetId, Assert.Single(attempt.UnavailableVariants));
            Assert.True(attempt.Event.Variants!.Count > 1);
            Assert.Null(controller.State.ActiveEvent);
            Assert.Equal(original.FieldId, session.State.FieldId);
            Assert.True(JsonElement.DeepEquals(original.EventState!.Value, GetWorld().Events.Capture()));
            Assert.True(controller.State.LastProgressScore > initial.LastProgressScore);
            string unavailableManifest = Assert.Single(Directory.GetFiles(output, "unavailable-*.run.json", SearchOption.AllDirectories), path => !Path.GetFileName(path).StartsWith("unavailable-return-", StringComparison.Ordinal));
            AutoPlayRunState unavailable = JsonSerializer.Deserialize<AutoPlayRunState>(File.ReadAllText(unavailableManifest), AutoPlayController.JsonOptions)!;
            Assert.NotNull(unavailable.ActiveBeforeCheckpoint);
            Assert.NotNull(unavailable.ActiveAbsenceGuardKey);
            Assert.Equal(targetId, unavailable.ActiveTargetId);
            Assert.Equal(121, GameSession.Load(Path.Combine(Path.GetDirectoryName(unavailableManifest)!, unavailable.Snapshot!)).FieldId);
            byte[] beforeBytes = File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(unavailableManifest)!, unavailable.ActiveBeforeCheckpoint));
            Assert.False(Attempt(controller).Completed);

            controller.Stop();
            AutoPlayController cold = new(session, GetWorld, Restore, output);
            cold.ResumeFrom(unavailableManifest);
            Assert.Equal(targetId, cold.State.ActiveTargetId);
            string resumedManifest = Directory.GetFiles(output, "resume-start.run.json", SearchOption.AllDirectories).Single(path => Path.GetDirectoryName(path) != Path.GetDirectoryName(unavailableManifest));
            Assert.False(Path.IsPathRooted(cold.State.ActiveBeforeCheckpoint!));
            Assert.Equal(beforeBytes, File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(resumedManifest)!, cold.State.ActiveBeforeCheckpoint!)));
            for (int tick = 0; tick < 4 && Attempt(cold).UnavailableAttempts.Count == 0 && cold.Error is null; tick++)
            {
                cold.Advance(1);
            }
            Assert.Null(cold.Error);
            Assert.False(Attempt(cold).Unavailable);
            Assert.False(Attempt(cold).Completed);
            Assert.Single(Attempt(cold).UnavailableAttempts);
            Assert.Equal(targetId, Assert.Single(Attempt(cold).UnavailableVariants));
            Assert.Equal(original.FieldId, session.State.FieldId);
            Assert.True(JsonElement.DeepEquals(original.EventState!.Value, GetWorld().Events.Capture()));
            for (int tick = 0; tick < 24 && Attempt(cold).UnavailableAttempts.Count < 2 && cold.Error is null; tick++)
            {
                cold.Advance(1);
            }
            Assert.Null(cold.Error);
            Assert.Equal(2, Attempt(cold).UnavailableAttempts.Count);
            Assert.Equal(2, Attempt(cold).UnavailableAttempts.Select(item => item.TargetId).Distinct(StringComparer.Ordinal).Count());
            Assert.False(Attempt(cold).Unavailable);
            Assert.False(Attempt(cold).Completed);
            Assert.Equal(originalManifest, File.ReadAllBytes(manifest));
            Assert.Equal(originalSnapshot, File.ReadAllBytes(snapshot));
        }
        finally
        {
            world?.Dispose();
            if (Directory.Exists(output))
            {
                Directory.Delete(output, true);
            }
        }
    }

    [Fact]
    public void OneUnavailableGeometryDoesNotSuppressOtherVariantsOrReplayItsOldRoute()
    {
        AutoPlayPlanner planner = new();
        planner.Observe(new("source", "region"), [new("door", AutoPlayEventKind.Transition, ConditionSignature: "entry", Variants: ["left", "right"])]);
        planner.Completed("source", "door", "destination", observedStoryChanged: false, selections: [], targetId: "left");
        planner.Observe(new("destination", "region"), [new("talk", AutoPlayEventKind.Interaction)]);
        planner.MarkUnavailable("source", "door", new("entry", "destination", "left-guard", "left-input", "left"));
        planner.RefreshUnavailable("source", new Dictionary<string, string> { ["left-guard"] = "left-input" });
        AutoPlayEventCoverage item = planner.State.Visits.Single(visit => visit.Visit.Key == "source").Events.Single();
        Assert.False(item.Unavailable);
        Assert.Equal("left", Assert.Single(item.UnavailableVariants));
        Assert.False(planner.Plan("source")!.IsTravel);
        planner.MarkUnavailable("source", "door", new("entry", "other", "right-guard", "right-input", "right"));
        planner.RefreshUnavailable("source", new Dictionary<string, string> { ["left-guard"] = "left-input", ["right-guard"] = "right-input" });
        Assert.True(item.Unavailable);
        Assert.Null(planner.Plan("source"));
        planner.RefreshUnavailable("source", new Dictionary<string, string> { ["left-guard"] = "changed", ["right-guard"] = "right-input" });
        Assert.False(item.Unavailable);
        Assert.Equal("right", Assert.Single(item.UnavailableVariants));
        Assert.NotNull(planner.Plan("source"));
    }

    [Fact]
    public void NewlyObservedVariantIsNotCoveredByAnEarlierUnavailableVariant()
    {
        AutoPlayPlanner planner = new();
        planner.Observe(new("source", "region"), [new("door", AutoPlayEventKind.Interaction, ConditionSignature: "entry", Variants: ["left"])]);
        planner.MarkUnavailable("source", "door", new("entry", "destination", "guard", "known", "left"));
        Assert.Null(planner.Plan("source"));
        planner.Observe(new("source", "region"), [new("door", AutoPlayEventKind.Interaction, ConditionSignature: "entry", Variants: ["left", "right"])]);
        Assert.NotNull(planner.Plan("source"));
        string before = JsonSerializer.Serialize(planner.State);
        Assert.Throws<InvalidOperationException>(() => planner.MarkUnavailable("source", "door", new("entry", "destination", "guard", "known", "unobserved")));
        Assert.Equal(before, JsonSerializer.Serialize(planner.State));
    }

    [Fact]
    public void NativeUnavailableAttemptNeverConsumesAnEventAndChangedSourceConditionsReopenIt()
    {
        AutoPlayPlanner planner = Source();
        planner.BeginEvent("source", "door");
        planner.MarkUnavailable("source", "door", new("entry", "destination", "guard", "original"));
        AutoPlayEventCoverage item = planner.State.Visits.Single().Events.Single();
        Assert.False(item.Completed);
        Assert.Null(item.Conditions!.ActiveEntry);
        Assert.Empty(item.Conditions.ConsumedEntries);
        Assert.Empty(item.Conditions.ConsumedExits);
        Assert.Null(planner.Plan("source"));
        planner = new(JsonSerializer.Deserialize<AutoPlayPlannerState>(JsonSerializer.Serialize(planner.State))!);
        planner.Observe(new("source", "region"), [new("door", AutoPlayEventKind.Transition, ConditionSignature: "new-source")]);
        Assert.NotNull(planner.Plan("source"));
    }

    [Fact]
    public void ChangedPersistentGuardReopensAHistoricallyCompletedEventExactlyOnceAndSameUnavailableGuardBlocksOldTravel()
    {
        AutoPlayPlanner planner = Source();
        planner.Completed("source", "door", "destination", observedStoryChanged: false, selections: []);
        planner.Observe(new("destination", "region"), [new("talk", AutoPlayEventKind.Interaction)]);
        planner.Completed("destination", "talk");
        planner.MarkUnavailable("source", "door", new("entry", "destination", "guard", "blocked-input"));
        planner.RefreshUnavailable("source", new Dictionary<string, string> { ["guard"] = "blocked-input" });
        Assert.Null(planner.Plan("source"));
        planner.RefreshUnavailable("source", new Dictionary<string, string> { ["guard"] = "changed-input" });
        Assert.False(planner.Plan("source")!.IsTravel);
        planner.Completed("source", "door", "destination", observedStoryChanged: false, selections: []);
        planner.RefreshUnavailable("source", new Dictionary<string, string> { ["guard"] = "changed-input" });
        Assert.Null(planner.Plan("source"));
        planner.RefreshUnavailable("source", new Dictionary<string, string> { ["guard"] = "blocked-input" });
        Assert.Null(planner.Plan("source"));
        Assert.True(planner.State.Visits.Single(visit => visit.Visit.Key == "source").Events.Single().Unavailable);
    }

    [Fact]
    public void UnknownGuardRefusesWithoutMutatingCoverageAndDuplicateAttemptIsNotNewProgress()
    {
        AutoPlayPlanner planner = Source();
        AutoPlayUnavailableAttempt attempt = new("entry", "destination", "guard", "known");
        planner.MarkUnavailable("source", "door", attempt);
        planner.MarkUnavailable("source", "door", attempt);
        Assert.Single(planner.State.Visits.Single().Events.Single().UnavailableAttempts);
        string before = JsonSerializer.Serialize(planner.State);
        Assert.Throws<InvalidOperationException>(() => planner.RefreshUnavailable("source", new Dictionary<string, string>()));
        Assert.Equal(before, JsonSerializer.Serialize(planner.State));
        Assert.Throws<InvalidOperationException>(() => planner.MarkUnavailable("source", "door", attempt with { SourceConditionSignature = "different" }));
        Assert.Equal(before, JsonSerializer.Serialize(planner.State));
    }

    [Theory]
    [InlineData("missing-checkpoint")]
    [InlineData("unknown-path")]
    [InlineData("selected-option")]
    [InlineData("pending-choice")]
    [InlineData("primary-continuation")]
    public void BacktrackingRefusesMissingSourceOrAnyUnaccountedChoice(string condition)
    {
        AutoPlayRunState state = new() { ActiveVisit = "source", ActiveEvent = "door", ActiveConditionSignature = "entry", ActiveBeforeCheckpoint = "before.json", ActiveTargetId = "door", ActiveSelections = [] };
        switch (condition)
        {
            case "missing-checkpoint":
                state.ActiveBeforeCheckpoint = null;
                break;
            case "unknown-path":
                state.ActiveSelections = null;
                break;
            case "selected-option":
                state.ActiveSelections!.Add(new("prompt", 0));
                break;
            case "pending-choice":
                state.Coverage.Choices.Add(new() { Key = "prompt", OptionCount = 2, ActiveOption = 0 });
                break;
            case "primary-continuation":
                state.PrimaryContinuation = "continuation.json";
                break;
        }
        string before = JsonSerializer.Serialize(state);
        Assert.Throws<NotSupportedException>(() => AutoPlayController.ValidateNativeBacktrack(state));
        Assert.Equal(before, JsonSerializer.Serialize(state));
    }

    [Fact]
    public void UnknownSourceReadsOrNonstableDestinationInputsNeverAuthorizeSuppression()
    {
        EventPredicateState exact = new("source", [], []);
        Assert.Throws<NotSupportedException>(() => AutoPlayController.ValidateNativeBacktrackPredicates("source", exact with { DeferredOperations = [7] }, "guard", "guard"));
        Assert.Throws<NotSupportedException>(() => AutoPlayController.ValidateNativeBacktrackPredicates("source", exact with { Fingerprint = "changed" }, "guard", "guard"));
        Assert.Throws<NotSupportedException>(() => AutoPlayController.ValidateNativeBacktrackPredicates("source", exact, "guard", null));
        Assert.Throws<NotSupportedException>(() => AutoPlayController.ValidateNativeBacktrackPredicates("source", exact, "guard", "changed"));
        AutoPlayController.ValidateNativeBacktrackPredicates("source", exact, "guard", "guard");
    }

    [Fact]
    public void ChangedDestinationProgramReopensWithoutReadingObsoleteDependenciesAndUnknownCurrentOwnersRefuse()
    {
        NativePlayerAbsenceReceipt receipt = new(1, 1, "original-program", [], "original-guard");
        string changed = AutoPlayController.ReadUnavailableGuard(receipt, "new-program", _ => throw new InvalidOperationException("Obsolete dependencies must not be read."));
        AutoPlayPlanner planner = Source();
        planner.MarkUnavailable("source", "door", new("entry", "destination", "guard", receipt.Fingerprint));
        planner.RefreshUnavailable("source", new Dictionary<string, string> { ["guard"] = changed });
        Assert.NotNull(planner.Plan("source"));
        Assert.Throws<NotSupportedException>(() => AutoPlayController.ReadUnavailableGuard(receipt, receipt.ProgramHash, _ => null));
        Assert.Throws<InvalidDataException>(() => AutoPlayController.ReadUnavailableGuard(receipt with { Version = 2 }, receipt.ProgramHash, _ => "anything"));
    }

    [Fact]
    public void AReturnConsumesOnlyItsObservedEntryAvailabilityAndLeavesChangedExitInputsPending()
    {
        AutoPlayPlanner planner = Source();
        planner.MarkUnavailable("source", "door", new("entry", "destination", "guard", "blocked"));
        planner.RefreshUnavailable("source", new Dictionary<string, string> { ["guard"] = "entry-input" });
        AutoPlayEventCoverage item = planner.State.Visits.Single().Events.Single();
        string entry = item.AvailabilityFingerprint!;
        planner.RefreshUnavailable("source", new Dictionary<string, string> { ["guard"] = "exit-input" });
        string exit = item.AvailabilityFingerprint!;
        planner.Completed("source", "door", entryAvailabilityFingerprint: entry, availabilityFingerprintAuthoritative: true);
        Assert.Contains(entry, item.CompletedAvailabilityFingerprints);
        Assert.DoesNotContain(exit, item.CompletedAvailabilityFingerprints);
        Assert.NotNull(planner.Plan("source"));
    }

    [Fact]
    public void FieldProgramHashUsesOnlyTheCurrentProgramMemberAndRechecksMutableOverrides()
    {
        string manifest = TestResources.FixturePath("App.Tests.AutoPlayNativeUnavailableTests.1");
        AutoPlayRunState state = JsonSerializer.Deserialize<AutoPlayRunState>(File.ReadAllText(manifest), AutoPlayController.JsonOptions)!;
        int field = GameSession.Load(Path.Combine(Path.GetDirectoryName(manifest)!, state.Snapshot!)).FieldId;
        ProgramOverride source = new([1, 2, 3, 4]);
        using GameAssets assets = new(TestResources.GameDirectory, source);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(source.Bytes)), assets.ReadFieldProgramHash(field));
        source.Bytes = [4, 3, 2, 1];
        Assert.Equal(Convert.ToHexString(SHA256.HashData(source.Bytes)), assets.ReadFieldProgramHash(field));
        Assert.Equal(2, source.ProgramReads);
        Assert.Equal(0, source.PresentationReads);
    }

    private static AutoPlayPlanner Source()
    {
        AutoPlayPlanner planner = new();
        planner.Observe(new("source", "region"), [new("door", AutoPlayEventKind.Transition, ConditionSignature: "entry")]);
        return planner;
    }

    private sealed class ProgramOverride(byte[] bytes) : IAssetOverrideSource
    {
        public byte[] Bytes { get; set; } = bytes;
        public int ProgramReads { get; private set; }
        public int PresentationReads { get; private set; }

        public bool TryRead(string logicalPath, out byte[] content)
        {
            content = Bytes;
            if (logicalPath.EndsWith(".map", StringComparison.OrdinalIgnoreCase) || logicalPath.EndsWith(".mim", StringComparison.OrdinalIgnoreCase) || logicalPath.EndsWith(".one", StringComparison.OrdinalIgnoreCase))
            {
                PresentationReads++;
            }
            if (!logicalPath.EndsWith(".jsm", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            ProgramReads++;
            return true;
        }
    }
}
