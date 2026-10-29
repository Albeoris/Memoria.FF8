using Memoria.FF8.GameUi;
using Memoria.FF8.Localization.Authoring;
using Memoria.FF8.Resources.Localization;
using Memoria.FF8.Resources.Text;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class LocalizationWorkbenchTests(ITestOutputHelper output)
{
    [Fact]
    public void RenderableMixedAtlasWithoutNativeCodecPreviewsAndReferenceReportKeepsItsRole()
    {
        Memoria.FF8.Resources.Text.NativeTextContext context = new("Classic", "Fields", "map", "en");
        Memoria.FF8.Resources.Text.NativeFontProfile mixed = new()
        {
            Id = "mixed",
            Domains = [new("Classic", "Fields")],
            Atlases = [new("latin", "latin.png", 8, 12), new("cyrillic", "cyrillic.png", 8, 12)],
            Images = [new(0, "latin", 0, 0, 8, 12, 8), new(1, "cyrillic", 0, 0, 8, 12, 8)],
            Glyphs = [new("A", 0), new("\u042f", 1)]
        };
        Memoria.FF8.Resources.Text.NativeFontProfile reference = mixed with { Id = "reference", Codec = [new("20", "\u042f")] };
        FakeStore store = new("A\u042f");
        LocalizationEditingSession session = new(store, new PlainSyntax(), store.Snapshot);
        using LocalizationWorkbenchController ui = new();
        AuthoringCompatibilityReport Report(string text, string role, string? dataset) => AuthoringCompatibilityReport.Evaluate(true, true, role == "Target" ? mixed : reference, context, role, dataset, text, [text]);
        ui.Open(session, [new("target", "Target", "A\u042f", true), new("ru", "Reference", "\u042f", IsSourceReference: true)], "Mixed atlas", value => Report(value, "Target", "mixed").PresentationDiagnostics.Select(error => error.Message).ToArray());
        ui.CompatibilityReporter = Report;
        ui.Preview();
        Assert.Equal(LocalizationSessionMode.Preview, session.Mode);
        Assert.True(ui.CompatibilityReport!.CanPreview);
        Assert.False(ui.CompatibilityReport.NativeEncodingCompatible);
        Assert.Collection(ui.CompatibilityReport.EncodingDiagnostics, error => Assert.Equal((0, 1), (error.Position, error.Length)), error => Assert.Equal((1, 1), (error.Position, error.Length)));
        Assert.Equal("Unverified", ui.CompatibilityReport.Deployment);
        ui.SelectLanguage(1);
        Assert.Equal("Reference", ui.CompatibilityReport!.Role);
        Assert.Equal("ru", ui.CompatibilityReport.Dataset);
        Assert.Equal("reference", ui.CompatibilityReport.ProfileId);
        Assert.True(ui.CompatibilityReport.NativeEncodingCompatible);
        Assert.False(Report("?", "Target", "mixed").CanPreview);
        AuthoringCompatibilityReport system = AuthoringCompatibilityReport.Evaluate(false, true, null, context with { ResourceFamily = "System" }, "Target", "mixed", "\u042f", ["\u042f"]);
        Assert.True(system.CanPreview);
        Assert.False(system.NativeEncodingCompatible);
        Assert.Equal("MemoriaOnly", system.Deployment);
    }

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
    public void PastedCompleteControlDoesNotConsumeThePreviewConfirmation()
    {
        FakeStore store = new("old");
        LocalizationEditingSession session = new(store, new PlainSyntax(), store.Snapshot);
        using LocalizationWorkbenchController ui = new();
        ui.Open(session, [new("target", "Target", "old", true)], "Test mod");
        ui.HandleCommand(UiEditCommand.SelectAll);
        ui.TextInput("日本語 😀\r\n{Line}Complete{End}");
        Assert.Equal("日本語 😀\n{Line}Complete{End}", session.Draft.Text);
        Assert.Empty(ui.CompletionItems);
        ui.ConfirmReleased();
        ui.HandleCommand(UiEditCommand.Confirm);
        Assert.Equal(LocalizationSessionMode.Preview, session.Mode);
        Assert.Equal(0, store.Writes);
        Assert.NotEmpty(LocalizationWorkbenchSyntax.Complete("{Li"));
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

    [Fact]
    public async Task SuccessfulIndependentReplayDoesNotAppendAnotherHistoryEntry()
    {
        FakeStore store = new("old");
        LocalizationChange historical = new(Guid.NewGuid(), store.Snapshot, new(true, "original edit"), LocalizationEditFields.Body, "Earlier edit");
        LocalizationEditingSession session = new(store, new PlainSyntax(), store.Snapshot) { RecordChanges = false };
        session.SetDraft(new(true, "replayed"));
        using LocalizationWorkbenchController ui = new();
        ui.LoadHistory([historical]);
        ui.Open(session, [new("target", "Target", "old", true)], "Test mod");
        ui.Preview();
        ui.ConfirmReleased();
        ui.HandleCommand(UiEditCommand.Confirm);
        await ui.PendingSave!;
        ui.Poll();
        Assert.Equal(1, store.Writes);
        Assert.Empty(session.Changes);
        Assert.Equal(historical.Id, Assert.Single(ui.ReadHistory()).Id);
    }

    [Fact]
    public async Task ConflictWithUnchangedOursStillBlocksClosingAndOpeningAnotherRow()
    {
        FakeStore store = new("old") { ConflictOnCommit = true };
        LocalizationEditingSession session = new(store, new PlainSyntax(), store.Snapshot);
        using LocalizationWorkbenchController ui = new() { Enabled = true };
        ui.Open(session, [new("target", "Target", "old", true)], "Test mod");
        ui.Preview();
        ui.ConfirmReleased();
        ui.HandleCommand(UiEditCommand.Confirm);
        await ui.PendingSave!;
        ui.Poll();
        Assert.False(session.IsDirty);
        Assert.NotNull(session.Conflict);
        Assert.False(ui.RequestDisable());
        ui.Open(new(store, new PlainSyntax(), store.Snapshot), [new("target", "Target", "old", true)], "Another row");
        Assert.Same(session, ui.Session);
    }

    [Theory]
    [InlineData(1280, 768, false)]
    [InlineData(640, 480, false)]
    [InlineData(1280, 768, true)]
    public async Task EditorChromeSeparatesRoleKeyAndConflictRowsAndClipsWrappedReports(int width, int height, bool conflict)
    {
        FakeStore store = new("old") { ConflictOnCommit = conflict };
        LocalizationEditingSession session = new(store, new PlainSyntax(), store.Snapshot);
        using LocalizationWorkbenchController ui = new() { Enabled = true, PositionSupported = true, SourceRoleBadge = "Source Reference: Japanese · Script Base: English · Target: translated text" };
        ui.Open(session, [new("target", "Target", "old", true), new("en", "English reference", "Source", IsSourceReference: true)], "Example package · full project location");
        ui.CompatibilityReporter = (_, _, _) => new("System", "Modern", null, "Target", "translated text", new("Classic", "Field"), [], [new("unsupported", "The selected native encoding does not represent these characters. This remains advisory for Memoria rendering.")], "Unverified");
        if (conflict)
        {
            ui.Preview();
            ui.ConfirmReleased();
            ui.HandleCommand(UiEditCommand.Confirm);
            await ui.PendingSave!;
            ui.Poll();
        }
        ClipCanvas canvas = new(new(0, 0, width, height));
        ui.Draw(canvas, canvas.Viewport, new UiTheme());
        Assert.Empty(canvas.Clips);
        Assert.Equal("Example package", ui.ControlHits.Single(hit => hit.Id == "project").Text);
        var roles = Assert.Single(canvas.Texts, text => text.Text == ui.SourceRoleBadge);
        UiRect key = ui.ControlHits.Single(hit => hit.Id == "copy.key").Bounds;
        Assert.True(roles.Bounds.Bottom <= key.Y);
        Assert.Equal(UiTextFlow.Wrap, roles.Flow);
        var reports = canvas.Texts.Where(text => text.Text.StartsWith("Presentation:", StringComparison.Ordinal) || text.Text.StartsWith("Native encoding", StringComparison.Ordinal) || text.Text.StartsWith("Original deployment:", StringComparison.Ordinal)).ToArray();
        Assert.Equal(3, reports.Length);
        Assert.All(reports, text =>
        {
            Assert.Equal(UiTextFlow.Wrap, text.Flow);
            Assert.Equal(text.Bounds, text.Clip);
            Assert.True(text.Bounds.Height >= 34);
        });
        Assert.True(reports[0].Bounds.Bottom <= reports[1].Bounds.Y);
        Assert.True(reports[1].Bounds.Bottom <= reports[2].Bounds.Y);
        Assert.All(canvas.Texts, text => Assert.Equal(text.Clip, text.Clip.Intersect(canvas.Viewport)));
        Assert.All(ui.ControlHits, hit => Assert.Equal(hit.Bounds, hit.Bounds.Intersect(canvas.Viewport)));
        if (conflict)
        {
            UiRect pane = ui.ControlHits.Single(hit => hit.Id == "conflict.Ours").Bounds;
            Assert.True(pane.Y >= key.Bottom);
            Assert.True(pane.Bottom <= ui.Editor.Bounds.Y);
            Assert.DoesNotContain(canvas.Texts, text => text.Text.StartsWith("Enter: Preview", StringComparison.Ordinal));
        }
    }

    private sealed class ClipCanvas(UiRect viewport) : IUiCanvas
    {
        public UiRect Viewport { get; } = viewport;
        public Stack<UiRect> Clips { get; } = new();
        public List<(string Text, UiRect Bounds, UiRect Clip, UiTextFlow Flow)> Texts { get; } = [];
        public UiSize MeasureText(string text, float fontSize, float maximumWidth) => new(Math.Min(maximumWidth, text.Length * fontSize / 2), fontSize * 1.5f);
        public void Text(string text, float fontSize, UiRect bounds, UiColor color) => Text(text, fontSize, bounds, color, UiAlignment.Start, UiTextFlow.SingleLine);
        public void Text(string text, float fontSize, UiRect bounds, UiColor color, UiAlignment alignment) => Text(text, fontSize, bounds, color, alignment, UiTextFlow.SingleLine);
        public void Text(string text, float fontSize, UiRect bounds, UiColor color, UiAlignment alignment, UiTextFlow flow) => Texts.Add((text, bounds, Clips.TryPeek(out UiRect clip) ? clip : Viewport, flow));
        public void Fill(UiRect bounds, UiColor color)
        {
        }
        public void MenuPointer(UiRect bounds)
        {
        }
        public void Image(UiImageSource source, UiRect bounds)
        {
        }
        public void Line(float x1, float y1, float x2, float y2, UiColor color, float thickness)
        {
        }
        public void PushClip(UiRect bounds) => Clips.Push(bounds.Intersect(Clips.TryPeek(out UiRect clip) ? clip : Viewport));
        public void PopClip() => Clips.Pop();
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
        public bool ConflictOnCommit { get; init; }
        public Task<LocalizationRowSnapshot> ReadRowAsync(LocalizationRowIdentity identity, CancellationToken cancellation = default) => Task.FromResult(Snapshot);
        public Task<LocalizationCommitResult> CommitAsync(LocalizationRowSnapshot expected, LocalizationTargetValue value, CancellationToken cancellation = default)
        {
            if (ConflictOnCommit)
            {
                return Task.FromResult(new LocalizationCommitResult(LocalizationCommitStatus.Conflict, Snapshot with { Revision = "external", Target = new(true, "External") }));
            }
            Writes++;
            Snapshot = Snapshot with { Target = value, Revision = "two" };
            return Task.FromResult(new LocalizationCommitResult(LocalizationCommitStatus.Saved, Snapshot));
        }
    }
}
