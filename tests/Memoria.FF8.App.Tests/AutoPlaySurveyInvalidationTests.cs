using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Events;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlaySurveyInvalidationTests
{
    [Fact]
    public void AFlagReadOnlyByAnotherVisitRearmsThatVisitWithoutRearmingTheSource()
    {
        Dictionary<string, AutoPlayVisitGuard> guards = new(StringComparer.Ordinal)
        {
            ["source"] = Guard(1, 1300, "source-input"),
            ["remote"] = Guard(2, 1400, "remote-before"),
            ["unaffected"] = Guard(3, 1500, "unchanged")
        };
        AutoPlaySurveyBaseline baseline = new(1, 1, "program-1", guards);
        string before = JsonSerializer.Serialize(baseline);

        IReadOnlyList<string> changed = AutoPlayController.FindChangedSurveyVisits(baseline, guards, field => $"program-{field}", reads => reads[0].Address switch
        {
            1300 => "source-input",
            1400 => "remote-after",
            _ => "unchanged"
        });

        Assert.Equal(new[] { "remote" }, changed);
        Assert.Equal(before, JsonSerializer.Serialize(baseline));
    }

    [Fact]
    public void HistoricalPostInitFingerprintsAndNewMetadataDoNotInvalidateAnUnchangedActualBaseline()
    {
        AutoPlayVisitGuard actual = Guard(2, 1400, "actual-input");
        AutoPlaySurveyBaseline baseline = new(1, 1, "program-1", new() { ["remote"] = actual });
        Dictionary<string, AutoPlayVisitGuard> current = new()
        {
            ["remote"] = actual with { Fingerprint = "historical-post-init-output" },
            ["newly-observed"] = Guard(3, 1500, "new-output")
        };

        Assert.Empty(AutoPlayController.FindChangedSurveyVisits(baseline, current, field => $"program-{field}", _ => "actual-input"));
    }

    [Fact]
    public void AuthenticatedTravelDoesNotRearmTheSurveyButANewActualPathStillDoes()
    {
        AutoPlayVisitGuard actual = Guard(2, 1400, "before-route");
        AutoPlaySurveyBaseline baseline = new(1, 1, "program-1", new() { ["remote"] = actual }, "canonical-progress");
        string before = JsonSerializer.Serialize(baseline);

        Assert.Empty(AutoPlayController.FindChangedSurveyVisits(baseline, baseline.Guards, field => $"program-{field}", _ => "after-route", recordedTravel: true, currentCanonicalProgressFingerprint: "canonical-progress"));
        Assert.Equal(new[] { "remote" }, AutoPlayController.FindChangedSurveyVisits(baseline, baseline.Guards, field => $"program-{field}", _ => "after-route", recordedTravel: false));
        Assert.Equal(before, JsonSerializer.Serialize(baseline));
    }

    [Theory]
    [InlineData("before", "before", false)]
    [InlineData("before", "after", true)]
    [InlineData("before", null, true)]
    [InlineData(null, "after", true)]
    public void RecordedTravelRequiresKnownUnchangedCanonicalProgressToSkipRearming(string? previous, string? current, bool rearmed)
    {
        AutoPlaySurveyBaseline baseline = new(1, 1, "program-1", new() { ["remote"] = Guard(2, 1400, "before-route") }, previous);

        IReadOnlyList<string> changed = AutoPlayController.FindChangedSurveyVisits(baseline, baseline.Guards, field => $"program-{field}", _ => "after-route", recordedTravel: true, currentCanonicalProgressFingerprint: current);

        Assert.Equal(rearmed ? new[] { "remote" } : [], changed);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("legacy")]
    [InlineData("different-domain")]
    [InlineData("different-field")]
    [InlineData("changed-resource")]
    [InlineData("unknown-input")]
    public void UnknownOrChangedDomainsRearmOnlyTheirAffectedVisit(string reason)
    {
        AutoPlayVisitGuard actual = Guard(2, 1400, "actual-input");
        AutoPlaySurveyBaseline baseline = new(1, 1, "program-1", new() { ["remote"] = actual, ["unaffected"] = Guard(3, 1500, "actual-input") });
        Dictionary<string, AutoPlayVisitGuard> current = new(baseline.Guards);
        switch (reason)
        {
            case "missing":
                current.Remove("remote");
                break;
            case "legacy":
                current["remote"] = actual with { Version = 1 };
                break;
            case "different-domain":
                current["remote"] = actual with { Reads = ImmutableArray.Create(new NativePersistentPredicateRead("PSHM_W", 1400, 2)) };
                break;
            case "different-field":
                current["remote"] = actual with { FieldId = 4 };
                break;
        }

        IReadOnlyList<string> changed = AutoPlayController.FindChangedSurveyVisits(baseline, current, field => reason == "changed-resource" && field == 2 ? "new-program" : $"program-{field}", reads => reason == "unknown-input" && reads[0].Address == 1400 ? null : "actual-input");

        Assert.Equal(new[] { "remote" }, changed);
    }

    [Fact]
    public void CanonicalPartyChangesAffectEveryGuardFingerprintIncludingAnEmptyReadSet()
    {
        AutoPlayVisitGuard empty = new(AutoPlayVisitGuard.CurrentVersion, 2, "program-2", [], "before-party");
        AutoPlaySurveyBaseline baseline = new(1, 1, "program-1", new() { ["remote"] = empty });

        Assert.Equal(new[] { "remote" }, AutoPlayController.FindChangedSurveyVisits(baseline, baseline.Guards, field => $"program-{field}", _ => "after-party"));
    }

    [Fact]
    public void ASerializedBaselinePreservesItsActualReadDomainAndComparison()
    {
        AutoPlaySurveyBaseline baseline = new(1, 1, "program-1", new() { ["remote"] = Guard(2, 1400, "actual-input") });
        AutoPlaySurveyBaseline restored = JsonSerializer.Deserialize<AutoPlaySurveyBaseline>(JsonSerializer.Serialize(baseline))!;

        Assert.Equal(JsonSerializer.Serialize(baseline), JsonSerializer.Serialize(restored));
        Assert.Empty(AutoPlayController.FindChangedSurveyVisits(restored, restored.Guards, field => $"program-{field}", _ => "actual-input"));
    }

    [Fact]
    public void NativeReadFailuresRemainVisibleRatherThanBecomingARefreshReceipt()
    {
        AutoPlaySurveyBaseline baseline = new(1, 1, "program-1", new() { ["remote"] = Guard(2, 1400, "actual-input") });

        Assert.Throws<NotSupportedException>(() => AutoPlayController.FindChangedSurveyVisits(baseline, baseline.Guards, _ => throw new NotSupportedException("The original program cannot be read."), _ => "actual-input"));
        Assert.Throws<NotSupportedException>(() => AutoPlayController.FindChangedSurveyVisits(baseline, baseline.Guards, _ => "program-2", _ => throw new NotSupportedException("The current native owner is unsupported.")));
        Assert.Throws<InvalidDataException>(() => AutoPlayController.FindChangedSurveyVisits(baseline with { Version = 2 }, baseline.Guards, _ => "program-2", _ => "actual-input"));
    }

    private static AutoPlayVisitGuard Guard(int fieldId, int address, string fingerprint)
    {
        return new(AutoPlayVisitGuard.CurrentVersion, fieldId, $"program-{fieldId}", ImmutableArray.Create(new NativePersistentPredicateRead("PSHM_B", address, 1)), fingerprint);
    }
}
