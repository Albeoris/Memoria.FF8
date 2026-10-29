using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Collections.Concurrent;
using Memoria.FF8.Localization.Authoring;
using Memoria.FF8.Ui;
using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8.GameUi;

public sealed record LocalizationWorkbenchLanguage(string Id, string Label, string? Text, bool IsTarget = false, bool IsSourceReference = false, bool IsScriptBase = false);
public sealed record LocalizationWorkbenchControlHit(string Id, string Text, UiRect Bounds);
public sealed record LocalizationWorkbenchWorkspace(float Width, float Height, bool Expanded, float OffsetX = 0, float OffsetY = 0, int Language = 0);
public enum LocalizationJournalTab { Dialogues, Changes }
public enum LocalizationSearchMode { Substring, Wildcard, Regex }

/// <summary>Presentation and input ownership for the authoring overlay; commits are delegated to its editing session.</summary>
public sealed class LocalizationWorkbenchController : IDisposable
{
    private readonly List<(UiRect Bounds, Action Click)> buttons = [];
    private IReadOnlyList<LocalizationWorkbenchLanguage> languages = [];
    private IReadOnlyList<LocalizationWorkbenchLanguage> journalLanguages = [];
    private bool disposed;
    private readonly List<LocalizationObservation> observations = [];
    private readonly List<LocalizationChange> changes = [];
    private readonly ConcurrentQueue<(LocalizationEditingSession Session, LocalizationCommitResult? Result, Exception? Error)> completedSaves = new();
    private Func<string, IReadOnlyList<string>>? validate;
    private int language;
    private int targetCaret;
    private int targetSelection;
    private UiEditorState? targetState;
    private bool confirmArmed = true;
    private bool journalFocus;
    private string projectLabel = "";
    private string status = "";
    private UiRect editorBounds;
    private UiRect journalBounds;
    private LocalizationConflictPane? conflictPane;
    private bool draggingJournal;
    private bool resizingJournal;
    private bool selectingText;
    private float dragX;
    private float dragY;
    private float journalOffsetX;
    private float journalOffsetY;
    public bool DisableRequested { get; private set; }
    public Action? Disabled { get; set; }
    public Action? DisableCancelled { get; set; }
    public Action? SelectProjectRequested { get; set; }
    public Action? BeginOriginAdjustmentRequested { get; set; }
    public Action<int, int>? AdjustOriginRequested { get; set; }
    public Action? ResetOriginRequested { get; set; }
    public string SourceRoleBadge { get; set; } = "";
    public bool PositionSupported { get; set; }
    public bool SystemTemplate { get; set; }
    public string NativeDictionaryLanguage { get; set; } = "en";
    public bool RequestDisable()
    {
        if (Session?.Mode == LocalizationSessionMode.Saving)
        {
            status = T("saving", "Saving Target — wait for the transaction to finish.");
            return false;
        }
        if (IsEditing && (Session!.IsDirty || Session.Conflict is not null))
        {
            DisableRequested = true;
            status = T("disable.dirty", "Unsaved Target: Preview/Save, Discard, or Cancel closing.");
            return false;
        }
        FinishDisable();
        return true;
    }
    private void FinishDisable()
    {
        if (IsEditing)
        {
            Session!.Cancel();
        }
        PreviewChanged?.Invoke(null);
        DisableRequested = false;
        Enabled = false;
        Disabled?.Invoke();
    }
    public void DiscardAndDisable()
    {
        if (Session?.Mode != LocalizationSessionMode.Saving)
        {
            FinishDisable();
        }
    }
    public void CancelDisable()
    {
        DisableRequested = false;
        DisableCancelled?.Invoke();
    }
    private readonly UiMultilineEditor searchEditor = new();
    public LocalizationWorkbenchController()
    {
        Editor.BeforeEdit = EnsureTarget;
        Editor.Changed = DraftChanged;
        searchEditor.Changed = () => Search = searchEditor.Text;
    }
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }
        disposed = true;
        searchCancellation?.Cancel();
        searchCancellation?.Dispose();
        searchCancellation = null;
        searchTask = null;
        searchAfter = 0;
    }
    public void SetJournalLanguages(IReadOnlyList<LocalizationWorkbenchLanguage> tabs)
    {
        journalLanguages = tabs.ToArray();
        JournalLanguage = Math.Clamp(JournalLanguage, 0, Math.Max(0, journalLanguages.Count - 1));
        journalRevision++;
    }
    public IReadOnlyList<LocalizationWorkbenchLanguage> JournalLanguages => journalLanguages;
    public int PreviewPage { get; private set; }
    public int PreviewPageCount { get; private set; } = 1;
    public int PreviewChoice { get; private set; }
    public IReadOnlyList<string> PreviewChoices { get; private set; } = [];
    public Action<int>? PreviewPageRequested { get; set; }
    public Action<int>? PreviewChoiceRequested { get; set; }
    public void SetPreviewPages(int count, IReadOnlyList<string>? choices = null)
    {
        PreviewPageCount = Math.Max(1, count);
        PreviewPage = Math.Clamp(PreviewPage, 0, PreviewPageCount - 1);
        PreviewChoices = choices?.ToArray() ?? [];
        PreviewChoice = Math.Clamp(PreviewChoice, 0, Math.Max(0, PreviewChoices.Count - 1));
    }
    public void BrowsePreviewPage(int delta)
    {
        if (Session?.Mode != LocalizationSessionMode.Preview)
        {
            return;
        }
        PreviewPage = Math.Clamp(PreviewPage + delta, 0, PreviewPageCount - 1);
        PreviewPageRequested?.Invoke(PreviewPage);
    }
    public void BrowsePreviewChoice(int delta)
    {
        if (Session?.Mode != LocalizationSessionMode.Preview || PreviewChoices.Count == 0)
        {
            return;
        }
        PreviewChoice = Math.Clamp(PreviewChoice + delta, 0, PreviewChoices.Count - 1);
        PreviewChoiceRequested?.Invoke(PreviewChoice);
    }
    public bool Enabled { get; set; }
    public bool PickText { get; set; } = true;
    public bool PreviewSelectedMod { get; set; } = true;
    public string? SelectedProjectId { get; set; }
    public void InvalidateJournal() => journalRevision++;
    public bool JournalExpanded { get; set; }
    public float JournalWidth { get; set; } = 600;
    public float JournalHeight { get; set; } = 320;
    public LocalizationJournalTab JournalTab { get; set; }
    public LocalizationSearchMode SearchMode { get; set; }
    public string Search { get; set; } = "";
    public bool SearchCaseSensitive { get; set; }
    private CancellationTokenSource? searchCancellation;
    private Task<JournalSearchResult>? searchTask;
    private string searchSignature = "";
    private long searchAfter;
    private long journalRevision;
    private LocalizationObservation[] matchingObservations = [];
    private LocalizationChange[] matchingChanges = [];
    public int JournalResultCount => matchingObservations.Length;
    public double LastSearchMilliseconds { get; private set; }
    public bool SearchPending => searchTask is not null || searchAfter != 0;
    private sealed record JournalSearchResult(LocalizationObservation[] Observations, LocalizationChange[] Changes, string? Error, double Milliseconds);
    public void LoadHistory(IEnumerable<LocalizationChange> history)
    {
        changes.Clear();
        changes.AddRange(history);
        journalRevision++;
    }
    public IReadOnlyList<LocalizationChange> ReadHistory() => changes.ToArray();
    public LocalizationWorkbenchWorkspace ReadWorkspace() => new(JournalWidth, JournalHeight, JournalExpanded, journalOffsetX, journalOffsetY, JournalLanguage);
    public void LoadWorkspace(LocalizationWorkbenchWorkspace workspace)
    {
        ResizeJournal(workspace.Width, workspace.Height);
        JournalExpanded = workspace.Expanded;
        journalOffsetX = float.IsFinite(workspace.OffsetX) ? workspace.OffsetX : 0;
        journalOffsetY = float.IsFinite(workspace.OffsetY) ? workspace.OffsetY : 0;
        JournalLanguage = Math.Max(0, workspace.Language);
    }
    public string? SearchError { get; private set; }
    public int JournalLanguage { get; private set; }
    public int JournalScroll { get; set; }
    public LocalizationEditingSession? Session { get; private set; }
    public bool IsEditing => Session is not null && Session!.Mode != LocalizationSessionMode.Closed;
    public bool CapturesSimulation => IsEditing;
    public bool OwnsKeyboard => IsEditing || journalFocus && JournalExpanded;
    public void BlurJournal() => journalFocus = false;
    public UiRect EditorBounds => editorBounds;
    public UiRect JournalBounds => journalBounds;
    public int EditorLanguage => language;
    public IReadOnlyList<LocalizationWorkbenchLanguage> EditorLanguages => languages;
    public string Status => status;
    public IReadOnlyList<LocalizationWorkbenchControlHit> ControlHits => controlHits;
    private readonly List<LocalizationWorkbenchControlHit> controlHits = [];
    public UiMultilineEditor Editor { get; } = new();
    public Task? PendingSave { get; private set; }
    public Func<string, string, string>? Localize { get; set; }
    public Action<string?>? PreviewChanged { get; set; }
    public Action<LocalizationCommitResult>? Saved { get; set; }
    public Action? Cancelled { get; set; }
    public Action<LocalizationObservation>? OpenObservation { get; set; }
    public Func<LocalizationObservation, int, string>? ObservationText { get; set; }
    public Action<LocalizationChange, bool>? ReplayRequested { get; set; }
    public Func<string, IReadOnlyList<string>>? Completions { get; set; }
    public IReadOnlyList<string> CompletionItems { get; private set; } = [];
    public int CompletionIndex { get; private set; }
    private Action<string>? copyText;
    private Func<string>? pasteText;
    public string? ClipboardProblem { get; private set; }
    public Action<string>? CopyText
    {
        get => copyText is null ? null : CopyClipboard;
        set
        {
            copyText = value;
            Editor.WriteClipboard = CopyClipboard;
            searchEditor.WriteClipboard = CopyClipboard;
        }
    }
    public Func<string>? PasteText
    {
        get => pasteText is null ? null : PasteClipboard;
        set
        {
            pasteText = value;
            Editor.ReadClipboard = PasteClipboard;
            searchEditor.ReadClipboard = PasteClipboard;
        }
    }
    private void CopyClipboard(string text)
    {
        try
        {
            copyText?.Invoke(text);
            ClipboardProblem = null;
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or DllNotFoundException or EntryPointNotFoundException)
        {
            ClipboardProblem = exception.Message;
            status = T("clipboard.failed", "Clipboard unavailable: ") + exception.Message;
        }
    }
    private string PasteClipboard()
    {
        try
        {
            string text = pasteText?.Invoke() ?? "";
            ClipboardProblem = null;
            return text;
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or DllNotFoundException or EntryPointNotFoundException)
        {
            ClipboardProblem = exception.Message;
            status = T("clipboard.failed", "Clipboard unavailable: ") + exception.Message;
            return "";
        }
    }
    public void Open(LocalizationEditingSession session, IReadOnlyList<LocalizationWorkbenchLanguage> tabs, string selectedProject, Func<string, IReadOnlyList<string>>? validator = null)
    {
        if (IsEditing && (Session!.IsDirty || Session.Conflict is not null))
        {
            status = T("dirty", "Preview/Save, Discard or Cancel the current draft before switching rows.");
            return;
        }
        ObjectDisposedException.ThrowIf(disposed, this);
        Session = session;
        languages = tabs.ToArray();
        PreviewPage = PreviewChoice = 0;
        SetPreviewPages(1);
        projectLabel = selectedProject;
        SelectedProjectId = session.Base.ProjectId;
        journalRevision++;
        validate = validator;
        language = 0;
        conflictPane = null;
        status = "";
        targetCaret = session.Caret;
        targetSelection = session.SelectionLength;
        targetState = null;
        Editor.SetText(TargetView());
        Editor.Select(targetCaret, targetSelection);
        Editor.Syntax = LocalizationWorkbenchSyntax.Highlight(Editor.Text, SystemTemplate);
        Editor.ReadOnly = false;
        journalFocus = false;
        CompletionItems = [];
        confirmArmed = false;
    }
    private string TargetView()
    {
        if (Session?.Draft is { Present: true, Text: { Length: > 0 } target })
        {
            return target;
        }
        return languages.FirstOrDefault(tab => tab.IsSourceReference && !string.IsNullOrEmpty(tab.Text))?.Text
            ?? languages.FirstOrDefault(tab => tab.IsScriptBase && !string.IsNullOrEmpty(tab.Text))?.Text
            ?? "";
    }
    private string T(string id, string fallback) => SystemTextCatalog.Translate(Localize, "localization.workbench." + id, fallback);
    public void RefreshDraft()
    {
        if (!IsEditing)
        {
            return;
        }
        language = 0;
        conflictPane = null;
        targetState = null;
        Editor.ReadOnly = false;
        Editor.SetText(TargetView());
        Editor.Syntax = LocalizationWorkbenchSyntax.Highlight(Editor.Text, SystemTemplate);
    }
    public void SelectLanguage(int index)
    {
        if (index < 0 || index >= languages.Count || !IsEditing)
        {
            return;
        }
        if (language == 0)
        {
            targetState = Editor.CaptureState();
            targetCaret = Editor.SelectionStart;
            targetSelection = Editor.SelectionLength;
        }
        language = index;
        conflictPane = null;
        Editor.SetText(index == 0 ? TargetView() : languages[index].Text ?? "");
        Editor.ReadOnly = index != 0;
        if (index == 0)
        {
            if (targetState is not null)
            {
                Editor.RestoreState(targetState);
            }
            else
            {
                Editor.Select(targetCaret, targetSelection);
            }
        }
    }
    public void SelectLanguageShortcut(int slot)
    {
        if (journalFocus || !IsEditing)
        {
            if (slot >= 0 && slot < journalLanguages.Count)
            {
                JournalLanguage = slot;
            }
        }
        else
        {
            SelectLanguage(slot);
        }
    }
    private void EnsureTarget()
    {
        if (!IsEditing || conflictPane is not null)
        {
            return;
        }
        if (language != 0)
        {
            SelectLanguage(0);
        }
        if (Session!.Mode == LocalizationSessionMode.Preview)
        {
            Session!.ReturnToEdit();
            PreviewChanged?.Invoke(null);
        }
    }
    private void DraftChanged()
    {
        if (Session is null)
        {
            return;
        }
        if (conflictPane is LocalizationConflictPane pane && Session!.Conflict is not null)
        {
            Session!.Conflict.Candidates[pane] = new(true, Editor.Text);
        }
        else
        {
            Session!.SetDraft(new(true, Editor.Text));
        }
        Editor.Syntax = LocalizationWorkbenchSyntax.Highlight(Editor.Text, SystemTemplate);
        status = T("draft", "Unsaved draft");
    }
    public void UseReferenceAsDraft()
    {
        if (!IsEditing || Session!.Mode == LocalizationSessionMode.Saving)
        {
            return;
        }
        string value = Editor.Text;
        SelectLanguage(0);
        Session!.SetDraft(new(true, value));
        Editor.SetText(value);
        status = T("draft", "Unsaved draft");
    }
    public void ClearTarget()
    {
        if (!IsEditing || Session!.Mode == LocalizationSessionMode.Saving)
        {
            return;
        }
        EnsureTarget();
        Session!.SetDraft(new(true, ""));
        Editor.SetText("");
        status = T("cleared", "Complete Target cleared, including every inline layout override. Preview and Save to apply.");
    }
    public void TextInput(string text)
    {
        if (journalFocus)
        {
            searchEditor.Insert(text);
            return;
        }
        if (!IsEditing || Session!.Mode == LocalizationSessionMode.Saving || text.Any(char.IsControl))
        {
            return;
        }
        Editor.Insert(text);
        if (text.Contains('{'))
        {
            ShowCompletion();
        }
    }
    public void SetComposition(string text)
    {
        if (IsEditing && Session!.Mode != LocalizationSessionMode.Saving)
        {
            Editor.SetComposition(text);
        }
    }
    public void ConfirmReleased() => confirmArmed = true;
    public bool HandleCommand(UiEditCommand command, bool shift = false, bool control = false)
    {
        if (Enabled && command == UiEditCommand.TogglePicking)
        {
            PickText = !PickText;
            return true;
        }
        if (journalFocus && JournalExpanded)
        {
            searchEditor.Command(command, shift, control);
            return true;
        }
        if (!IsEditing)
        {
            return false;
        }
        if (command is UiEditCommand.PreviewPreviousPage or UiEditCommand.PreviewNextPage)
        {
            BrowsePreviewPage(command == UiEditCommand.PreviewPreviousPage ? -1 : 1);
            return true;
        }
        if (command is UiEditCommand.PreviewPreviousChoice or UiEditCommand.PreviewNextChoice)
        {
            BrowsePreviewChoice(command == UiEditCommand.PreviewPreviousChoice ? -1 : 1);
            return true;
        }
        if (Session!.Mode == LocalizationSessionMode.Saving && command is not (UiEditCommand.Copy or UiEditCommand.SelectAll or UiEditCommand.Left or UiEditCommand.Right or UiEditCommand.Up or UiEditCommand.Down or UiEditCommand.Home or UiEditCommand.End))
        {
            return true;
        }
        if (Editor.IsComposing && command is UiEditCommand.Confirm or UiEditCommand.Cancel)
        {
            return true;
        }
        if (command == UiEditCommand.Cancel && CompletionItems.Count != 0)
        {
            CompletionItems = [];
            return true;
        }
        if (command == UiEditCommand.Complete)
        {
            ShowCompletion();
            return true;
        }
        if (CompletionItems.Count != 0 && command is UiEditCommand.Up or UiEditCommand.Down)
        {
            CompletionIndex = (CompletionIndex + (command == UiEditCommand.Up ? CompletionItems.Count - 1 : 1)) % CompletionItems.Count;
            return true;
        }
        if (command == UiEditCommand.AcceptCompletion || command == UiEditCommand.Confirm && CompletionItems.Count != 0)
        {
            if (CompletionItems.Count != 0)
            {
                AcceptCompletion(CompletionIndex);
            }
            return true;
        }
        if (command == UiEditCommand.Confirm)
        {
            if (!confirmArmed || PendingSave is { IsCompleted: false })
            {
                return true;
            }
            confirmArmed = false;
            if (Session!.Mode == LocalizationSessionMode.Preview)
            {
                PendingSave = SaveAsync();
            }
            else
            {
                Preview();
            }
            return true;
        }
        if (command == UiEditCommand.Cancel)
        {
            if (Session!.Mode == LocalizationSessionMode.Preview)
            {
                Session!.ReturnToEdit();
                PreviewChanged?.Invoke(null);
            }
            else
            {
                Session!.Cancel();
                PreviewChanged?.Invoke(null);
                Cancelled?.Invoke();
            }
            return true;
        }
        if (command == UiEditCommand.PageBreak)
        {
            if (PageBreakSupported)
            {
                Editor.Insert("{Next}");
            }
            else
            {
                status = T("page.unsupported", "This row does not support native page breaks.");
            }
            return true;
        }
        Editor.Command(command, shift, control);
        return true;
    }
    public bool PageBreakSupported { get; set; }
    private void ShowCompletion()
    {
        CompletionItems = Completions?.Invoke(Editor.Text[..Editor.Caret]) ?? LocalizationWorkbenchSyntax.Complete(Editor.Text[..Editor.Caret], SystemTemplate, NativeDictionaryLanguage);
        CompletionIndex = 0;
    }
    private void AcceptCompletion(int index)
    {
        if (index < CompletionItems.Count)
        {
            string item = CompletionItems[index];
            int start = Editor.Text.LastIndexOf('{', Math.Max(0, Editor.Caret - 1));
            if (start >= 0)
            {
                Editor.Select(start, Editor.Caret - start);
            }
            Editor.Insert(item);
        }
        CompletionItems = [];
    }
    public void Preview()
    {
        if (!IsEditing || Session!.Mode == LocalizationSessionMode.Saving)
        {
            return;
        }
        IReadOnlyList<string> errors = validate?.Invoke(Session!.Draft.Present ? Session.Draft.Text ?? "" : "") ?? [];
        if (errors.Count != 0)
        {
            status = string.Join("\n", errors);
            return;
        }
        Session!.Preview();
        PreviewChanged?.Invoke(TargetView());
        status = T("preview.status", "Preview — release Enter, then press Enter again to Save. Esc returns to Edit.");
    }
    private void SaveReviewed()
    {
        if (Session?.Mode == LocalizationSessionMode.Preview && (PendingSave is null || PendingSave.IsCompleted))
        {
            PendingSave = SaveAsync();
        }
    }
    private void EditConflict(LocalizationConflictPane pane)
    {
        if (Session?.Conflict is not LocalizationConflict conflict)
        {
            return;
        }
        conflictPane = pane;
        Editor.SetText(conflict.Candidates[pane].Text ?? "");
        Editor.ReadOnly = false;
    }
    private void PreviewConflict()
    {
        if (conflictPane is LocalizationConflictPane pane && Session?.Conflict is not null)
        {
            Session.SelectConflict(pane);
            conflictPane = null;
            Editor.SetText(Session.Draft.Text ?? "");
            Preview();
        }
    }
    private void ResetConflict()
    {
        if (conflictPane is LocalizationConflictPane pane && Session?.Conflict is LocalizationConflict conflict)
        {
            conflict.Reset(pane);
            Editor.SetText(conflict.Candidates[pane].Text ?? "");
        }
    }
    private void SelectJournalTab(LocalizationJournalTab tab)
    {
        JournalTab = tab;
        JournalScroll = 0;
    }
    private void SelectJournalLanguage(int slot)
    {
        JournalLanguage = slot;
        journalFocus = true;
    }
    private async Task SaveAsync()
    {
        LocalizationEditingSession session = Session!;
        status = T("saving", "Saving Target — wait for the transaction to finish.");
        try
        {
            LocalizationCommitResult result = await session.SaveAsync().ConfigureAwait(false);
            completedSaves.Enqueue((session, result, null));
        }
        catch (Exception error)
        {
            completedSaves.Enqueue((session, null, error));
        }
    }
    /// <summary>Called by the host update thread; publication and preview callbacks never run on a worker.</summary>
    public void Poll()
    {
        if (disposed)
        {
            return;
        }
        PollSearch();
        if (IsEditing && language == 0 && conflictPane is null)
        {
            Session!.Caret = Editor.SelectionStart;
            Session!.SelectionLength = Editor.SelectionLength;
        }
        while (completedSaves.TryDequeue(out var completion))
        {
            if (completion.Error is not null)
            {
                status = completion.Error.Message;
                completion.Session!.ReturnToEdit();
                continue;
            }
            LocalizationCommitResult result = completion.Result!;
            status = result.Message ?? T("save." + result.Status, result.Status.ToString());
            if (result.Status == LocalizationCommitStatus.Saved)
            {
                if (completion.Session.RecordChanges && completion.Session.Changes.Count > 0)
                {
                    LocalizationChange change = completion.Session.Changes[^1];
                    if (changes.All(existing => existing.Id != change.Id))
                    {
                        changes.Add(change);
                        journalRevision++;
                    }
                }
                PreviewChanged?.Invoke(null);
                if (DisableRequested)
                {
                    FinishDisable();
                }
            }
            Saved?.Invoke(result);
        }
    }
    public void Observe(LocalizationObservation observation)
    {
        observations.Add(observation);
        journalRevision++;
        if (observations.Count > 10000)
        {
            observations.RemoveAt(0);
        }
    }
    public void ResetJournalSize()
    {
        JournalWidth = 600;
        JournalHeight = 320;
        journalOffsetX = journalOffsetY = 0;
    }
    public void ResizeJournal(float width, float height)
    {
        JournalWidth = Math.Max(320, float.IsFinite(width) ? width : 600);
        JournalHeight = Math.Max(160, float.IsFinite(height) ? height : 320);
    }
    public bool OwnsPointer(float x, float y) => Enabled && (draggingJournal || resizingJournal || selectingText || IsEditing && editorBounds.Contains(x, y) || journalBounds.Contains(x, y));
    public bool Pointer(float x, float y, bool pressed, bool released)
    {
        if (released)
        {
            bool owned = draggingJournal || resizingJournal || selectingText;
            draggingJournal = resizingJournal = selectingText = false;
            return owned;
        }
        if (selectingText)
        {
            Editor.ExtendPointerSelection(x, y);
            return true;
        }
        if (draggingJournal)
        {
            journalOffsetX += x - dragX;
            journalOffsetY += y - dragY;
            dragX = x;
            dragY = y;
            return true;
        }
        if (resizingJournal)
        {
            ResizeJournal(JournalWidth - (x - dragX), JournalHeight - (y - dragY));
            dragX = x;
            dragY = y;
            return true;
        }
        if (pressed && Enabled && IsEditing && !journalBounds.Contains(x, y) && Editor.Bounds.Contains(x, y))
        {
            Editor.Pointer(x, y);
            selectingText = true;
            journalFocus = false;
            return true;
        }
        if (!pressed || !Enabled)
        {
            return false;
        }
        if (JournalExpanded && new UiRect(journalBounds.X, journalBounds.Y, 18, 18).Contains(x, y))
        {
            resizingJournal = true;
            dragX = x;
            dragY = y;
            return true;
        }
        if (new UiRect(journalBounds.Right - 28, journalBounds.Y, 28, 30).Contains(x, y))
        {
            draggingJournal = true;
            dragX = x;
            dragY = y;
            return true;
        }
        return Pointer(x, y);
    }
    public bool Pointer(float x, float y)
    {
        if (!OwnsPointer(x, y))
        {
            return false;
        }
        journalFocus = journalBounds.Contains(x, y);
        for (int index = buttons.Count - 1; index >= 0; index--)
        {
            if (buttons[index].Bounds.Contains(x, y))
            {
                buttons[index].Click();
                return true;
            }
        }
        if (Editor.Bounds.Contains(x, y))
        {
            journalFocus = false;
            Editor.Pointer(x, y);
        }
        else if (searchEditor.Bounds.Contains(x, y))
        {
            searchEditor.Pointer(x, y);
        }
        return true;
    }
    private void PollSearch()
    {
        string signature = string.Join("|", Search, SearchMode, SearchCaseSensitive, JournalLanguage, SelectedProjectId, journalRevision);
        if (signature != searchSignature)
        {
            searchSignature = signature;
            searchCancellation?.Cancel();
            searchAfter = Stopwatch.GetTimestamp() + Stopwatch.Frequency * 120 / 1000;
        }
        if (searchTask is { IsCompleted: true })
        {
            if (searchTask.IsCompletedSuccessfully && searchAfter == 0)
            {
                JournalSearchResult result = searchTask.Result;
                matchingObservations = result.Observations;
                matchingChanges = result.Changes;
                SearchError = result.Error;
                LastSearchMilliseconds = result.Milliseconds;
                JournalScroll = Math.Clamp(JournalScroll, 0, Math.Max(0, (JournalTab == LocalizationJournalTab.Dialogues ? matchingObservations.Length : matchingChanges.Length) - 1));
            }
            searchTask = null;
        }
        if (searchAfter != 0 && Stopwatch.GetTimestamp() >= searchAfter)
        {
            searchCancellation?.Dispose();
            searchCancellation = new CancellationTokenSource();
            CancellationToken token = searchCancellation.Token;
            LocalizationObservation[] dialogueSnapshot = observations.ToArray();
            LocalizationChange[] changeSnapshot = changes.Where(change => SelectedProjectId is null || change.Before.ProjectId == SelectedProjectId).ToArray();
            Func<LocalizationObservation, int, string>? textProvider = ObservationText;
            string[][]? languageSnapshot = textProvider is null ? null : dialogueSnapshot.Select(row => Enumerable.Range(0, journalLanguages.Count).Select(slot => textProvider(row, slot)).ToArray()).ToArray();
            string query = Search;
            LocalizationSearchMode mode = SearchMode;
            bool caseSensitive = SearchCaseSensitive;
            searchAfter = 0;
            searchTask = Task.Run(() => SearchSnapshot(dialogueSnapshot, changeSnapshot, languageSnapshot, query, mode, caseSensitive, token), token);
        }
    }
    private static JournalSearchResult SearchSnapshot(LocalizationObservation[] dialogues, LocalizationChange[] saved, string[][]? languageValues, string query, LocalizationSearchMode mode, bool caseSensitive, CancellationToken token)
    {
        Stopwatch timer = Stopwatch.StartNew();
        try
        {
            Regex? regex = mode == LocalizationSearchMode.Substring || query.Length == 0 ? null : new Regex(mode == LocalizationSearchMode.Wildcard ? Regex.Escape(query).Replace("\\*", ".*").Replace("\\?", ".") : query, RegexOptions.Compiled | RegexOptions.CultureInvariant | (caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase), TimeSpan.FromMilliseconds(20));
            bool Match(params string?[] values)
            {
                token.ThrowIfCancellationRequested();
                if (timer.ElapsedMilliseconds > 250)
                {
                    throw new TimeoutException("Search time limit reached; narrow the search.");
                }
                return query.Length == 0 || values.Any(value => value is not null && (regex is null ? value.Contains(query, caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase) : regex.IsMatch(value)));
            }
            LocalizationObservation[] resultDialogues = dialogues.Select((row, index) => (Row: row, Index: index)).Reverse().Where(item => Match(item.Row.Text, item.Row.Identity.Key, item.Row.Identity.File, item.Row.Context) || languageValues is not null && Match(languageValues[item.Index])).Select(item => item.Row).ToArray();
            LocalizationChange[] resultChanges = saved.Reverse().Where(row => Match(row.Before.Identity.Key, row.Before.Target.Text, row.After.Text, row.Summary)).ToArray();
            return new(resultDialogues, resultChanges, null, timer.Elapsed.TotalMilliseconds);
        }
        catch (Exception error) when (error is ArgumentException or RegexMatchTimeoutException or TimeoutException)
        {
            return new([], [], error.Message, timer.Elapsed.TotalMilliseconds);
        }
    }
    public void Draw(IUiCanvas canvas, UiRect viewport, UiTheme theme)
    {
        buttons.Clear();
        controlHits.Clear();
        if (!Enabled)
        {
            return;
        }
        void Label(string value, UiRect area, bool muted = false) => canvas.Text(value, 14, area, muted ? theme.Muted : theme.Foreground, UiAlignment.Start, UiTextFlow.SingleLine);
        void Button(string id, string fallback, UiRect area, Action click)
        {
            canvas.Fill(area.Inset(1), theme.BorderDark);
            Label(T(id, fallback), area.Inset(4));
            buttons.Add((area, click));
            controlHits.Add(new(id, fallback, area));
        }
        if (IsEditing)
        {
            float width = Math.Min(700, viewport.Width - 24);
            float height = Math.Min(430, viewport.Height - 50);
            editorBounds = new(viewport.X + 12, viewport.Y + 36, width, height);
            theme.Panel(canvas, editorBounds);
            float x = editorBounds.X + 10;
            float y = editorBounds.Y + 8;
            Button("project", projectLabel + " · " + SourceRoleBadge, new(x, y, width - 20, 22), () => SelectProjectRequested?.Invoke());
            Button("copy.key", Session!.Base.Identity.Key, new(x, y + 23, width - 20, 26), () => CopyText?.Invoke(Session!.Base.Identity.Key));
            float tabWidth = Math.Min(160, (width - 20) / Math.Max(1, languages.Count));
            for (int index = 0; index < languages.Count; index++)
            {
                int slot = index;
                Button("language." + languages[index].Id, (index == language ? "● " : "") + languages[index].Label + (index < 3 ? " Alt+" + (index + 1) : ""), new(x + index * tabWidth, y + 52, tabWidth, 28), () => SelectLanguage(slot));
            }
            if (Session!.Mode == LocalizationSessionMode.Preview)
            {
                Button("preview.page.previous", "← Page", new(x + width - 238, y + 82, 72, 23), () => BrowsePreviewPage(-1));
                Label((PreviewPage + 1) + "/" + PreviewPageCount, new(x + width - 164, y + 82, 50, 23));
                Button("preview.page.next", "Page →", new(x + width - 112, y + 82, 82, 23), () => BrowsePreviewPage(1));
            }
            string roleLabel;
            if (language != 0)
            {
                roleLabel = T("reference", "Read-only reference — editing returns to Target");
            }
            else if (Session!.Draft is { Present: true, Text: { Length: > 0 } })
            {
                roleLabel = T("target", "Target draft");
            }
            else
            {
                roleLabel = TargetView().Length == 0 ? T("no.text", "No text available") : T("inherited", "Inherited text — Target remains empty until edited and saved");
            }
            Label(roleLabel, new(x, y + 82, Session!.Mode == LocalizationSessionMode.Preview ? Math.Max(0, width - 250) : width - 20, 22), true);
            Editor.Arrange(new(x, y + 106, width - 20, Math.Max(55, height - 230)));
            Editor.Draw(canvas, theme);
            if (Session!.Mode == LocalizationSessionMode.Preview && PreviewChoices.Count != 0)
            {
                float choiceY = Editor.Bounds.Bottom - 27;
                Button("preview.choice.previous", "← Choice", new(x, choiceY, 84, 25), () => BrowsePreviewChoice(-1));
                Label(PreviewChoices[PreviewChoice], new(x + 88, choiceY, Math.Max(0, width - 198), 25));
                Button("preview.choice.next", "Choice →", new(editorBounds.Right - 100, choiceY, 88, 25), () => BrowsePreviewChoice(1));
            }
            float actionsY = editorBounds.Bottom - 108;
            Button("preview", "Preview", new(x, actionsY, 90, 29), Preview);
            if (Session!.Mode == LocalizationSessionMode.Preview)
            {
                Button("save", "Save", new(x + 92, actionsY, 75, 29), SaveReviewed);
            }
            Button("cancel", "Cancel", new(x + 169, actionsY, 75, 29), () => HandleCommand(UiEditCommand.Cancel));
            Button("copy.raw", "Copy raw", new(x + 246, actionsY, 95, 29), () => CopyText?.Invoke(Editor.SelectionLength > 0 ? Editor.SelectedText : Editor.Text));
            Button("copy.both", "Copy text + key", new(x + 343, actionsY, 136, 29), () => CopyText?.Invoke(Session!.Base.Identity.Key + "\n" + Editor.Text));
            if (language != 0)
            {
                Button("use.reference", "Use as draft", new(x + 481, actionsY, 130, 29), UseReferenceAsDraft);
            }
            else
            {
                Button("clear", "Clear complete Target", new(x + 481, actionsY, Math.Max(100, width - 501), 29), ClearTarget);
            }
            if (PositionSupported)
            {
                Button("origin.begin", "Adjust origin", new(x, actionsY + 32, 110, 24), () => BeginOriginAdjustmentRequested?.Invoke());
                Button("origin.left", "X−", new(x + 112, actionsY + 32, 46, 24), () => AdjustOriginRequested?.Invoke(-1, 0));
                Button("origin.right", "X+", new(x + 160, actionsY + 32, 46, 24), () => AdjustOriginRequested?.Invoke(1, 0));
                Button("origin.up", "Y−", new(x + 208, actionsY + 32, 46, 24), () => AdjustOriginRequested?.Invoke(0, -1));
                Button("origin.down", "Y+", new(x + 256, actionsY + 32, 46, 24), () => AdjustOriginRequested?.Invoke(0, 1));
                Button("origin.reset", "Reset origin", new(x + 304, actionsY + 32, 110, 24), () => ResetOriginRequested?.Invoke());
                Label(status, new(x + 418, actionsY + 32, Math.Max(0, width - 438), 24));
            }
            else
            {
                Label(status, new(x, actionsY + 32, width - 20, 24));
            }
            if (DisableRequested)
            {
                Button("discard.disable", "Discard and close", new(x + 310, actionsY + 60, 150, 25), DiscardAndDisable);
                Button("cancel.disable", "Cancel closing", new(x + 462, actionsY + 60, 130, 25), CancelDisable);
            }
            Label(T("shortcuts", "Enter: Preview / Save · Shift+Enter: line · Ctrl+Enter: page · Ctrl+Space: tags · Tab/Enter: accept tag"), new(x, actionsY + 60, width - 20, 24), true);
            if (Session!.Mode == LocalizationSessionMode.Conflict && Session!.Conflict is not null)
            {
                float paneWidth = (width - 20) / 3;
                foreach (LocalizationConflictPane pane in Enum.GetValues<LocalizationConflictPane>())
                {
                    int index = (int)pane;
                    Button("conflict." + pane, "Edit " + pane, new(x + index * paneWidth, y + 82, paneWidth, 24), () => EditConflict(pane));
                }
                Button("conflict.use", "Preview selected candidate", new(x, actionsY + 60, 220, 25), PreviewConflict);
                Button("conflict.reset", "Reset candidate", new(x + 225, actionsY + 60, 150, 25), ResetConflict);
            }
            for (int index = 0; index < Math.Min(5, CompletionItems.Count); index++)
            {
                int item = Math.Max(0, CompletionIndex - 4) + index;
                Button("completion." + item, (item == CompletionIndex ? "● " : "") + CompletionItems[item], new(x + 20, Editor.Bounds.Y + index * 26, Math.Min(320, width - 40), 26), () => AcceptCompletion(item));
            }
        }
        float journalWidth = Math.Min(viewport.Width - 16, JournalWidth);
        float journalHeight = JournalExpanded ? Math.Min(viewport.Height - 16, JournalHeight) : 32;
        journalBounds = new(Math.Clamp(viewport.Right - journalWidth - 8 + journalOffsetX, viewport.X, Math.Max(viewport.X, viewport.Right - journalWidth)), Math.Clamp(viewport.Bottom - journalHeight - 8 + journalOffsetY, viewport.Y, Math.Max(viewport.Y, viewport.Bottom - journalHeight)), journalWidth, journalHeight);
        theme.Panel(canvas, journalBounds);
        Button(JournalExpanded ? "journal.expanded" : "journal.collapsed", JournalExpanded ? "Journal ▾" : "Journal ▴", new(journalBounds.X, journalBounds.Y, 120, 30), () => JournalExpanded = !JournalExpanded);
        Button(PickText ? "picking.pick" : "picking.navigate", PickText ? "Pick text" : "Navigate", new(journalBounds.X + 122, journalBounds.Y, 110, 30), () => PickText = !PickText);
        Button(PreviewSelectedMod ? "selected.preview.on" : "selected.preview.off", PreviewSelectedMod ? "Selected mod preview: On" : "Selected mod preview: Off", new(journalBounds.X + 234, journalBounds.Y, Math.Max(60, journalWidth - 264), 30), () => PreviewSelectedMod = !PreviewSelectedMod);
        Label("↔", new(journalBounds.Right - 26, journalBounds.Y + 4, 24, 24));
        if (!JournalExpanded)
        {
            return;
        }
        float jx = journalBounds.X + 8;
        float jy = journalBounds.Y + 34;
        Button("dialogues", "Dialogues", new(jx, jy, 95, 25), () => SelectJournalTab(LocalizationJournalTab.Dialogues));
        Button("changes", "Changes", new(jx + 97, jy, 95, 25), () => SelectJournalTab(LocalizationJournalTab.Changes));
        Button("search.mode." + SearchMode, SearchMode.ToString(), new(jx + 194, jy, 110, 25), () => SearchMode = (LocalizationSearchMode)(((int)SearchMode + 1) % 3));
        Button("scroll.up", "↑", new(jx + 306, jy, 30, 25), () => JournalScroll = Math.Max(0, JournalScroll - 5));
        Button("scroll.down", "↓", new(jx + 338, jy, 30, 25), () => JournalScroll = Math.Min(Math.Max(0, (JournalTab == LocalizationJournalTab.Dialogues ? matchingObservations.Length : matchingChanges.Length) - 1), JournalScroll + 5));
        Button(SearchCaseSensitive ? "search.case.on" : "search.case.off", SearchCaseSensitive ? "Aa: On" : "Aa: Off", new(jx + 370, jy, 90, 25), () => SearchCaseSensitive = !SearchCaseSensitive);
        searchEditor.Arrange(new(jx, jy + 28, journalWidth - 16, 29));
        searchEditor.Draw(canvas, theme);
        float journalTabWidth = Math.Min(110, (journalWidth - 16) / Math.Max(1, journalLanguages.Count));
        for (int index = 0; index < journalLanguages.Count; index++)
        {
            int slot = index;
            Button("journal.language." + journalLanguages[index].Id, (JournalLanguage == index ? "● " : "") + journalLanguages[index].Label, new(jx + index * journalTabWidth, jy + 60, journalTabWidth, 23), () => SelectJournalLanguage(slot));
        }
        if (JournalTab == LocalizationJournalTab.Dialogues)
        {
            var rows = matchingObservations.AsEnumerable().Skip(JournalScroll).Take(Math.Max(1, (int)((journalHeight - 130) / 30))).ToArray();
            for (int index = 0; index < rows.Length; index++)
            {
                LocalizationObservation row = rows[index];
                Button("observation." + row.Id, row.Identity.Key + " · " + (ObservationText?.Invoke(row, JournalLanguage) ?? row.Text).Replace('\n', ' '), new(jx, jy + 84 + index * 30, journalWidth - 66, 28), () => OpenObservation?.Invoke(row));
                Button("copy", "Copy", new(journalBounds.Right - 56, jy + 84 + index * 30, 50, 28), () => CopyText?.Invoke(row.Identity.Key + "\n" + row.Text));
            }
        }
        else
        {
            var rows = matchingChanges.AsEnumerable().Skip(JournalScroll).Take(Math.Max(1, (int)((journalHeight - 130) / 30))).ToArray();
            for (int index = 0; index < rows.Length; index++)
            {
                LocalizationChange row = rows[index];
                Label(row.Before.Identity.Key + " · " + row.Summary, new(jx, jy + 84 + index * 30, journalWidth - 140, 28));
                Button("undo.change", "Undo", new(journalBounds.Right - 130, jy + 84 + index * 30, 60, 28), () => ReplayRequested?.Invoke(row, false));
                Button("redo.change", "Redo", new(journalBounds.Right - 68, jy + 84 + index * 30, 60, 28), () => ReplayRequested?.Invoke(row, true));
            }
        }
        if (SearchError is not null)
        {
            Label(SearchError, new(jx, journalBounds.Bottom - 24, journalWidth - 20, 24));
        }
        Label("↖", new(journalBounds.X, journalBounds.Y, 18, 18));
    }
}
