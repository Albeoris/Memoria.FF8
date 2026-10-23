namespace Memoria.FF8.Mods.Tests;

public sealed class ModServiceViewTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "mod-service-view-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void RapidTogglesUsePersistedPreferencesBeforePublication()
    {
        Write("example", "original");
        using ModRuntime runtime = Runtime();
        Publish(runtime);
        Assert.True(runtime.SetEnabled("Demaster/example", false).Success);
        ModServiceView disabled = runtime.View;
        ModPackageView row = Assert.Single(disabled.Packages);
        Assert.False(row.Package.RequestedEnabled);
        Assert.Equal(ModEffectiveStatus.Active, row.CommittedStatus);
        Assert.Equal("Active · disabling…", row.StatusText);
        Assert.True(runtime.SetEnabled(row.Package.Id, !row.Package.RequestedEnabled).Success);
        Assert.True(Assert.Single(runtime.View.Packages).Package.RequestedEnabled);
        Publish(runtime);
        Assert.Equal(ModUpdateOutcome.Idle, runtime.View.Outcome);
        Assert.Equal("Active", Assert.Single(runtime.View.Packages).StatusText);
        Assert.False(Assert.Single(disabled.Packages).Package.RequestedEnabled);
    }

    [Fact]
    public void DisableFailureKeepsSavedRequestAndCommittedRuntimeDistinct()
    {
        Write("example", "original");
        Write("unrelated", "other");
        bool reject = false;
        using ModRuntime runtime = Runtime(_ => new(!reject));
        Publish(runtime);
        reject = true;
        Assert.True(runtime.SetEnabled("Demaster/example", false).Success);
        Assert.False(runtime.PreparePending(force: true));
        ModPackageView row = runtime.View.Packages.Single(package => package.Package.Name == "example");
        Assert.False(row.Package.RequestedEnabled);
        Assert.Equal(ModEffectiveStatus.Active, row.CommittedStatus);
        Assert.Equal("Active · disable failed", row.StatusText);
        Assert.True(row.NeedsAttention);
        Assert.False(runtime.View.Packages.Single(package => package.Package.Name == "unrelated").NeedsAttention);
        reject = false;
        runtime.Refresh();
        Publish(runtime);
        Assert.Equal("Disabled", runtime.View.Packages.Single(package => package.Package.Name == "example").StatusText);
        Assert.DoesNotContain(runtime.View.Diagnostics, diagnostic => diagnostic.Code == "reload-failed");
    }

    [Fact]
    public void FailedNewCandidateRemainsInspectableAndCanBeDisabledToRecover()
    {
        using ModRuntime runtime = Runtime(candidate => new(!candidate.Packages.Any(package => package.RequestedEnabled)));
        Publish(runtime);
        Write("candidate", "content");
        runtime.Refresh();
        Assert.False(runtime.PreparePending(force: true));
        ModPackageView row = Assert.Single(runtime.View.Packages);
        Assert.Null(row.CommittedStatus);
        Assert.Equal("Not active · enable failed", row.StatusText);
        Assert.True(row.CanToggle);
        Assert.True(runtime.SetEnabled(row.Package.Id, false).Success);
        Publish(runtime);
        Assert.Equal("Disabled", Assert.Single(runtime.View.Packages).StatusText);
    }

    [Fact]
    public void FailedContentUpdateFlagsOnlyChangedPackage()
    {
        Write("example", "original");
        Write("unrelated", "other");
        bool reject = false;
        using ModRuntime runtime = Runtime(_ => new(!reject));
        Publish(runtime);
        Write("example", "updated content");
        reject = true;
        runtime.Refresh();
        Assert.False(runtime.PreparePending(force: true));
        Assert.Equal("Active · update failed", runtime.View.Packages.Single(package => package.Package.Name == "example").StatusText);
        Assert.False(runtime.View.Packages.Single(package => package.Package.Name == "unrelated").NeedsAttention);
    }

    [Fact]
    public void RemovalKeepsRunningPackageVisibleUntilCommit()
    {
        Write("example", "original");
        using ModRuntime runtime = Runtime();
        Publish(runtime);
        Directory.Delete(Path.Combine(root, "Mods", "Demaster", "example"), true);
        runtime.Refresh();
        Assert.True(runtime.PreparePending(force: true));
        ModPackageView row = Assert.Single(runtime.View.Packages);
        Assert.Equal(ModEffectiveStatus.Active, row.CommittedStatus);
        Assert.Equal("Active · disabling…", row.StatusText);
        Assert.False(row.CanToggle);
        Assert.True(runtime.CommitPending());
        Assert.Empty(runtime.View.Packages);
    }

    [Theory]
    [InlineData("Memoria.GuardianForceReview", ModUpdateOutcome.ReviewRequired)]
    [InlineData("Memoria.GuardianForceBoundary", ModUpdateOutcome.Blocked)]
    public void TypedDiagnosticsIdentifyReviewAndBoundaryWithoutParsingMessages(string code, ModUpdateOutcome expected)
    {
        Write("example", "original");
        using ModRuntime runtime = Runtime(_ => new(false, Diagnostics: [new(code, "Any translated text")]));
        Assert.False(runtime.PreparePending(force: true));
        Assert.Equal(expected, runtime.View.Outcome);
        Assert.True(Assert.Single(runtime.View.Packages).NeedsAttention);
    }

    [Fact]
    public void PostCommitFailureShowsPublishedStateAndSurvivesUnchangedRefresh()
    {
        Write("example", "original");
        using ModRuntime runtime = Runtime(_ => new(true, AfterCommit: () => throw new InvalidOperationException("Consumer failed")));
        Publish(runtime);
        Assert.Equal(ModUpdateOutcome.PublishedWithWarnings, runtime.View.Outcome);
        Assert.Equal("Active", Assert.Single(runtime.View.Packages).StatusText);
        Assert.False(Assert.Single(runtime.View.Packages).NeedsAttention);
        runtime.RequestRefresh();
        Assert.False(runtime.PreparePending(force: true));
        Assert.Equal(ModUpdateOutcome.PublishedWithWarnings, runtime.View.Outcome);
        Assert.Single(runtime.View.Diagnostics, diagnostic => diagnostic.Code == "subscriber-failed");
    }

    [Fact]
    public void FailedPersistenceRetainsLastConfirmedCheckboxValue()
    {
        Write("example", "original");
        using ModRuntime runtime = Runtime();
        Publish(runtime);
        Directory.CreateDirectory(Path.Combine(root, "Mods", "Demaster", "example", "mod.ini"));
        Assert.False(runtime.SetEnabled("Demaster/example", false).Success);
        Assert.True(Assert.Single(runtime.View.Packages).Package.RequestedEnabled);
        Assert.Equal(ModEffectiveStatus.Active, Assert.Single(runtime.View.Packages).CommittedStatus);
        Assert.Equal("Active · setting not saved", Assert.Single(runtime.View.Packages).StatusText);
        Assert.True(Assert.Single(runtime.View.Packages).NeedsAttention);
    }

    [Fact]
    public void DefaultProjectionUsesSavedRequestsAndCurrentTypedDiagnostics()
    {
        ModPackage committed = new("Demaster/example", "example", ModFormat.Demaster, true, true, ModEffectiveStatus.Active, "Mods/Demaster/example");
        ModCatalogSnapshot catalog = new(1, [committed], []);
        ModServiceView view = ModServiceView.Create(catalog, [committed with { RequestedEnabled = false }], true, true, [new("Memoria.GuardianForceReview", "Translated explanation")]);
        Assert.Equal(ModUpdateOutcome.ReviewRequired, view.Outcome);
        Assert.False(Assert.Single(view.Packages).Package.RequestedEnabled);
        Assert.Equal(ModEffectiveStatus.Active, Assert.Single(view.Packages).CommittedStatus);
        Assert.True(Assert.Single(view.Packages).NeedsAttention);
    }

    [Fact]
    public void OwnedRuntimeFailureDoesNotInventAContentUpdate()
    {
        Write("example", "original");
        using ModRuntime runtime = Runtime();
        Publish(runtime);
        runtime.ReportFailure(new("replacement-failed", "An in-use resource failed", "Demaster/example", "test.png"));
        ModPackageView row = Assert.Single(runtime.View.Packages);
        Assert.Equal("Active", row.StatusText);
        Assert.Equal("An in-use resource failed", row.Problem);
        Assert.True(row.NeedsAttention);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void StaleFailedCandidateRequiresRediscoveryBeforeSavingPreference(bool removed)
    {
        Write("candidate", "original");
        using ModRuntime runtime = Runtime(_ => new(false));
        Assert.False(runtime.PreparePending(force: true));
        string directory = Path.Combine(root, "Mods", "Demaster", "candidate");
        if (removed)
        {
            Directory.Delete(directory, true);
        }
        else
        {
            Write("candidate", "a replacement with different content");
        }
        Assert.False(runtime.SetEnabled("Demaster/candidate", false).Success);
        Assert.False(File.Exists(Path.Combine(directory, "mod.ini")));
        Assert.Equal(!removed, Directory.Exists(directory));
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }

    private ModRuntime Runtime(Func<ModCatalogSnapshot, ModPreparationResult>? prepare = null)
    {
        return new(root, prepare: prepare, watch: false, debounce: TimeSpan.Zero);
    }

    private void Write(string package, string text)
    {
        string directory = Path.Combine(root, "Mods", "Demaster", package);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "test.png"), text);
    }

    private static void Publish(ModRuntime runtime)
    {
        Assert.True(runtime.PreparePending(force: true));
        Assert.True(runtime.CommitPending());
    }
}
