using Memoria.FF8.GameUi;
using Memoria.FF8.Localization.Authoring;
using Memoria.FF8.Resources.Localization;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class LocalizationWorkbenchTests(ITestOutputHelper output)
{
    [Fact]
    public void EmptyTargetInspectionDoesNotMaterializeAndReferenceEditUsesTargetFallback()
    {
        FakeStore store = new("");
        LocalizationEditingSession session = new(store, new PlainSyntax(), store.Snapshot);
        LocalizationWorkbenchController ui = new();
        ui.Open(session, [new("target", "Target", "", true), new("jp", "Reference", "Japanese", IsSourceReference: true), new("en", "Base", "English", IsScriptBase: true)], "Test mod");
        Assert.Equal("Japanese", ui.Editor.Text);
        ui.SelectLanguage(2);
        Assert.False(session.IsDirty);
        ui.TextInput("!");
        Assert.Equal("!Japanese", session.Draft.Text);
        Assert.Equal(0, store.Writes);
    }

    [Fact]
    public async Task ConfirmRequiresReleaseBeforePreviewAndBeforeSave()
    {
        FakeStore store = new("old");
        LocalizationEditingSession session = new(store, new PlainSyntax(), store.Snapshot);
        LocalizationWorkbenchController ui = new();
        ui.Open(session, [new("target", "Target", "old", true)], "Test mod");
        bool notified = false;
        ui.Saved = _ => notified = true;
        ui.TextInput("new");
        ui.HandleCommand(UiEditCommand.Confirm);
        Assert.Equal(LocalizationSessionMode.Edit, session.Mode);
        ui.ConfirmReleased();
        ui.HandleCommand(UiEditCommand.Confirm);
        Assert.Equal(LocalizationSessionMode.Preview, session.Mode);
        ui.HandleCommand(UiEditCommand.Confirm);
        Assert.Equal(0, store.Writes);
        ui.ConfirmReleased();
        ui.HandleCommand(UiEditCommand.Confirm);
        await ui.PendingSave!;
        Assert.Equal(1, store.Writes);
        Assert.False(notified);
        ui.Poll();
        Assert.True(notified);
    }

    [Fact]
    public void ReferenceSelectionPreservesTargetUndoAndClosingDirtyRequiresDecision()
    {
        FakeStore store = new("old");
        LocalizationEditingSession session = new(store, new PlainSyntax(), store.Snapshot);
        LocalizationWorkbenchController ui = new() { Enabled = true };
        ui.Open(session, [new("target", "Target", "old", true), new("jp", "Reference", "Japanese")], "Test mod");
        ui.TextInput("new");
        ui.SelectLanguage(1);
        ui.SelectLanguage(0);
        ui.HandleCommand(UiEditCommand.Undo);
        Assert.Equal("old", ui.Editor.Text);
        ui.TextInput("edited");
        Assert.False(ui.RequestDisable());
        Assert.True(ui.Enabled);
        ui.CancelDisable();
        Assert.True(session.IsDirty);
        ui.DiscardAndDisable();
        Assert.False(ui.Enabled);
        Assert.Equal("old", session.Draft.Text);
    }

    [Fact]
    public async Task TenThousandDialogueSearchUsesBackgroundSnapshotAndCachedResults()
    {
        LocalizationWorkbenchController ui = new();
        for (int index = 0; index < 10000; index++)
        {
            ui.Observe(new(Guid.NewGuid(), DateTimeOffset.UtcNow, new("Field", "map", "key-" + index), "Dialogue sample " + index));
        }
        foreach (LocalizationSearchMode mode in Enum.GetValues<LocalizationSearchMode>())
        {
            ui.SearchMode = mode;
            ui.Search = mode == LocalizationSearchMode.Substring ? "9999" : mode == LocalizationSearchMode.Wildcard ? "*9999" : "sample [0-9]{4}$";
            ui.Poll();
            using CancellationTokenSource deadline = new(TimeSpan.FromSeconds(5));
            while (ui.SearchPending)
            {
                await Task.Delay(5, deadline.Token);
                ui.Poll();
            }
            Assert.Null(ui.SearchError);
            Assert.Equal(mode == LocalizationSearchMode.Regex ? 9000 : 1, ui.JournalResultCount);
            output.WriteLine($"10000 observations, {mode}: {ui.LastSearchMilliseconds:F3} ms worker search (120 ms debounce excluded)");
        }
        LocalizationWorkbenchWorkspace workspace = new(640, 350, true, -20, -30, 2);
        ui.LoadWorkspace(workspace);
        Assert.Equal(workspace, ui.ReadWorkspace());
        ui.LoadHistory([]);
        Assert.Empty(ui.ReadHistory());
    }

    [Fact]
    public void TargetFallbackDoesNotUseUnrelatedReferenceAndPreservesMeaningfulWhitespace()
    {
        FakeStore store = new("");
        using LocalizationWorkbenchController ui = new();
        LocalizationWorkbenchLanguage[] tabs = [new("target", "Target", "", true), new("extra", "Other", "Do not inherit"), new("primary", "Reference", "", IsSourceReference: true), new("base", "Base", "", IsScriptBase: true)];
        ui.Open(new(store, new PlainSyntax(), store.Snapshot), tabs, "Test mod");
        Assert.Equal("", ui.Editor.Text);
        tabs[3] = tabs[3] with { Text = "  " };
        ui.Open(new(store, new PlainSyntax(), store.Snapshot), tabs, "Test mod");
        Assert.Equal("  ", ui.Editor.Text);
        tabs[2] = tabs[2] with { Text = "Primary" };
        ui.Open(new(store, new PlainSyntax(), store.Snapshot), tabs, "Test mod");
        Assert.Equal("Primary", ui.Editor.Text);
        ui.Open(new(store, new PlainSyntax(), store.Snapshot with { Target = new(false, "Not authored") }), tabs, "Test mod");
        Assert.Equal("Primary", ui.Editor.Text);
        Assert.Equal(0, store.Writes);
    }

    [Fact]
    public void ProjectJournalLanguagesStayIndependentOfSelectedEditorRow()
    {
        FakeStore store = new("Target");
        using LocalizationWorkbenchController ui = new();
        ui.SetJournalLanguages([new("target", "Project Target", null, true), new("jp", "Project reference", null, IsSourceReference: true)]);
        ui.SelectLanguageShortcut(1);
        Assert.Equal(1, ui.JournalLanguage);
        ui.Open(new(store, new PlainSyntax(), store.Snapshot), [new("target", "Row Target", "Target", true)], "Test mod");
        Assert.Equal(1, ui.JournalLanguage);
        Assert.Equal("Project reference", ui.JournalLanguages[1].Label);
        Assert.Equal("Target", ui.Editor.Text);
    }

    [Fact]
    public void PreviewPageAndChoiceBrowsingOnlyRequestsPresentationAndNeverCommits()
    {
        FakeStore store = new("Target");
        LocalizationEditingSession session = new(store, new PlainSyntax(), store.Snapshot);
        using LocalizationWorkbenchController ui = new();
        ui.Open(session, [new("target", "Target", "Target", true)], "Test mod");
        List<int> pages = [];
        List<int> choices = [];
        ui.PreviewPageRequested = pages.Add;
        ui.PreviewChoiceRequested = choices.Add;
        ui.SetPreviewPages(3, ["Yes", "No"]);
        ui.BrowsePreviewPage(1);
        Assert.Empty(pages);
        ui.Preview();
        ui.BrowsePreviewPage(1);
        ui.BrowsePreviewChoice(1);
        ui.BrowsePreviewPage(10);
        Assert.Equal(new[] { 1, 2 }, pages);
        Assert.Equal(new[] { 1 }, choices);
        ui.SetPreviewPages(1);
        Assert.Equal(0, ui.PreviewPage);
        Assert.False(session.IsDirty);
        Assert.Equal(0, store.Writes);
    }

    [Fact]
    public void DisposalCancelsDebouncedSearchAndPreventsFurtherPolling()
    {
        LocalizationWorkbenchController ui = new();
        ui.Search = "query";
        ui.Poll();
        Assert.True(ui.SearchPending);
        ui.Dispose();
        ui.Poll();
        Assert.False(ui.SearchPending);
        ui.Dispose();
    }

    private sealed class PlainSyntax : ILocalizationTargetSyntax
    {
        public LocalizationTargetParts Parse(string? value) => new(value ?? "", null, null, null);
        public string Compose(LocalizationTargetParts value) => value.Body;
        public LocalizationTargetValue SetOrigin(LocalizationTargetValue target, string source, int x, int y) => throw new NotSupportedException();
    }
    private sealed class FakeStore(string target) : ILocalizationProjectStore
    {
        public LocalizationRowSnapshot Snapshot { get; private set; } = new("project", new("Field", "test", "key"), new LocalizationRecord { Key = "key", Target = target }, new(true, target), "one");
        public int Writes { get; private set; }
        public Task<LocalizationRowSnapshot> ReadRowAsync(LocalizationRowIdentity identity, CancellationToken cancellation = default) => Task.FromResult(Snapshot);
        public Task<LocalizationCommitResult> CommitAsync(LocalizationRowSnapshot expected, LocalizationTargetValue value, CancellationToken cancellation = default)
        {
            Writes++;
            Snapshot = Snapshot with { Target = value, Revision = "two" };
            return Task.FromResult(new LocalizationCommitResult(LocalizationCommitStatus.Saved, Snapshot));
        }
    }
}
