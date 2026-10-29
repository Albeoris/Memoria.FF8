using Albeoris.Games.FF8.Jsm.Messages;
using Memoria.FF8.Assets.Localization;
using Memoria.FF8.Events;
using Memoria.FF8.GameUi;
using Memoria.FF8.Localization.Authoring;
using Memoria.FF8.Mods;
using Memoria.FF8.Mods.Localization;
using Memoria.FF8.Resources.Localization;
using Memoria.FF8.Resources.Text;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private sealed record AuthoringHit(LocalizationTextBinding Binding, UiRect Bounds, DialogueState? Dialogue = null, bool Detached = false)
    {
        public DialogueWindow? Window { get; init; }
        public int DrawGroup { get; init; }
        public Memoria.FF8.Resources.ITextTermSource? Terms { get; init; }
        public Func<string, string>? FormatTemplate { get; init; }
        public Func<ushort, string>? ButtonBindings { get; init; }
        public NativeTextContext? NativeContext { get; init; }
        public float? FontSize { get; init; }
        public string? FontFamily { get; init; }
        public float? MaximumWidth { get; init; }
    }
    private sealed record AuthoringSelection(ModPackage Package, LocalizationProject Project, LocalizationCatalog Catalog, IReadOnlyDictionary<string, LocalizationRecord> Rows, string InputIdentity, LocalizationInputRevision? Inputs);
    private readonly LocalizationWorkbenchController localizationWorkbench = new();
    private readonly List<AuthoringHit> authoringHits = [];
    private readonly Dictionary<int, DialogueState> observedAuthoringDialogues = [];
    private IReadOnlyDictionary<string, LocalizationSourceEntry>? authoringKnownRows;
    private readonly DialoguePager authoringPreviewPager = new();
    private IReadOnlyList<DialogueVisualPage>? authoringPreviewPages;
    private string? authoringPreviewPagesKey;
    private readonly Dictionary<Guid, LocalizationTextBinding> observedAuthoringBindings = [];
    private readonly Dictionary<Guid, AuthoringHit> observedAuthoringHits = [];
    private readonly HashSet<(string Channel, string Id)> observedAuthoringLifecycles = [];
    private readonly Dictionary<int, long> observedAuthoringOccurrenceIds = [];
    private AuthoringHit? editingAuthoringHit;
    private bool authoringChooseProjectRequested;
    private readonly FieldMessageTargetSyntax authoringSyntax = new();
    private readonly CancellationTokenSource authoringCancellation = new();
    private AuthoringSelection? authoringSelection;
    private Task<AuthoringSelection>? authoringSelectionTask;
    private Task<LocalizationRowSnapshot>? authoringRowTask;
    private LocalizationProjectStore? authoringStore;
    private LocalizationTextBinding? editingAuthoringBinding;
    private DialogueState? editingAuthoringDialogue;
    private string? authoringDraftPreview;
    private string authoringNotice = "";
    private KeyboardState authoringPreviousKeys;
    private readonly AuthoringKeyboardInput authoringKeyboard = new();
    private LocalizationAuthoringTextInput? authoringTextInput;
    private readonly LocalizationAuthoringClipboard authoringClipboard = new();
    private UiEditCommand? authoringRebinding;
    private bool authoringRebindingWaitForRelease;
    private bool authoringPreviousMouse;
    private bool authoringOriginActive;
    private bool authoringOriginDragging;
    private UiRect authoringOriginHandle;
    private PixelBounds authoringDialogueViewport;
    private string? authoringRememberedProject;
    private long authoringOperation;
    private long authoringSelectionOperation;
    private bool authoringSelectionEnableWhenReady = true;
    private long authoringRowOperation;
    private Task<(LocalizationRowSnapshot Current, LocalizationTargetValue Proposed)>? authoringReplayTask;
    private bool authoringOriginVisible;
    private EventEngine? observedAuthoringEngine;


    private void ConnectLocalizationWorkbench()
    {
        gameMenus!.ConnectLocalizationWorkbench(AuthoringEntries, AuthoringAction);
        gameMenus.ReadNativeFontProfiles = () => nativeTextFont?.AvailableProfileIds ?? ["Auto"];
        InitializeAuthoringWorkspace();
        authoringTextInput = new(localizationWorkbench.TextInput);
        gameMenus.Register(new MenuScreenDefinition { Id = "localization-catalog", Title = "Localization · Canonical catalog", Help = "Inspect exact rows without picking workbench chrome. Reference data remains read-only.", TitleText = _ => AuthoringHostText("catalog.title", "Localization · Canonical catalog"), HelpText = _ => AuthoringHostText("catalog.help", "Inspect exact rows without picking workbench chrome. Reference data remains read-only."), Entries = _ => AuthoringCatalogEntries(), GalleryEligible = false });
        gameMenus.Register(new MenuScreenDefinition { Id = "localization-shortcuts", Title = "Localization · Editor shortcuts", Help = "Choose a command, release the current key, then press its new shortcut. Escape cancels capture.", TitleText = _ => AuthoringHostText("shortcuts.title", "Localization · Editor shortcuts"), HelpText = _ => AuthoringHostText("shortcuts.help", "Choose a command, release the current key, then press its new shortcut. Escape cancels capture."), Entries = _ => AuthoringShortcutEntries(), GalleryEligible = false });
        localizationWorkbench.ObservationText = AuthoringObservationText;
        localizationWorkbench.CompatibilityRevision = () => $"{uiPreferences.TextFontMode}:{uiPreferences.TextLayoutMode}:{nativeTextFont?.Revision}";
        localizationWorkbench.Localize = LocalizeUiText;
        localizationWorkbench.CopyText = authoringClipboard.Copy;
        localizationWorkbench.PasteText = authoringClipboard.Paste;
        localizationWorkbench.PreviewChanged = value =>
        {
            LocalizationEditingSession? session = localizationWorkbench.Session;
            bool removing = value is not null && session is not null && (session.IsDirty || !session.RecordChanges) && (!session.Draft.Present || string.IsNullOrEmpty(session.Draft.Text));
            authoringDraftPreview = removing ? "" : value is not null && session is not null && string.IsNullOrEmpty(session.Draft.Text) ? AuthoringGhostPreview(session.Base.Row) : value;
            authoringPreviewPagesKey = null;
            if (editingAuthoringDialogue is null || removing || value is null)
            {
                resourceHost?.SetAuthoringDraftPreview(editingAuthoringBinding?.Key, removing ? "" : value, authoringSelection?.Package.Id, authoringSelection?.Project.Manifest.ProjectId);
            }
            gameMenus?.InvalidateLiveData();
        };
        localizationWorkbench.Saved = result =>
        {
            authoringNotice = result.Message ?? result.Status.ToString();
            authoringDraftPreview = null;
            resourceHost?.SetAuthoringDraftPreview(null, null, null, null);
            if (result.Status == LocalizationCommitStatus.Saved)
            {
                ForgetRecoveredAuthoringDraft();
                resourceHost!.Mods.Refresh();
                ReloadAuthoringSelection();
            }
            gameMenus?.InvalidateLiveData();
        };
        localizationWorkbench.Cancelled = () =>
        {
            ForgetRecoveredAuthoringDraft();
            authoringDraftPreview = null;
            resourceHost?.SetAuthoringDraftPreview(null, null, null, null);
            authoringOriginActive = authoringOriginDragging = false;
            drainUiButtons = true;
            gameMenus?.InvalidateLiveData();
            SynchronizeUiMoviePause();
        };
        localizationWorkbench.Disabled = DisableLocalizationWorkbench;
        localizationWorkbench.SelectProjectRequested = () =>
        {
            if (localizationWorkbench.IsEditing && (localizationWorkbench.Session!.IsDirty || localizationWorkbench.Session.Conflict is not null))
            {
                authoringChooseProjectRequested = true;
                localizationWorkbench.RequestDisable();
            }
            else
            {
                if (localizationWorkbench.IsEditing)
                {
                    localizationWorkbench.HandleCommand(UiEditCommand.Cancel);
                }
                gameMenus.Open("localization-workbench", true);
            }
        };
        localizationWorkbench.DisableCancelled += () => authoringChooseProjectRequested = false;
        localizationWorkbench.OpenObservation = occurrence =>
        {
            if (observedAuthoringBindings.TryGetValue(occurrence.Id, out LocalizationTextBinding? binding))
            {
                OpenAuthoringBinding(observedAuthoringHits.TryGetValue(occurrence.Id, out AuthoringHit? hit) ? hit with { Detached = true } : new(binding, default, Detached: true));
            }
        };
        localizationWorkbench.ReplayRequested = BeginAuthoringReplay;
        localizationWorkbench.BeginOriginAdjustmentRequested = () => authoringOriginActive = true;
        localizationWorkbench.AdjustOriginRequested = (x, y) => NudgeAuthoringOrigin(x, y);
        localizationWorkbench.ResetOriginRequested = ResetAuthoringOrigin;
        localizationWorkbench.PreviewPageRequested = page =>
        {
            if (authoringPreviewPages is { } pages)
            {
                authoringPreviewPager.SelectPreviewPage(page, pages);
            }
        };
        if (uiCanvas is not null)
        {
            uiCanvas.InspectText = (binding, bounds) =>
            {
                if (localizationWorkbench.Enabled && !(gameMenus?.IsOpen == true && gameMenus.ScreenId.StartsWith("localization-", StringComparison.Ordinal)))
                {
                    RegisterAuthoringHits([new(binding, bounds)]);
                }
            };
            uiCanvas.InspectTextBatch = RegisterAuthoringHits;
        }
    }

    private IReadOnlyList<MenuEntry> AuthoringEntries()
    {
        List<MenuEntry> entries =
        [
            new("authoring-toggle", FormatAuthoringHostText("toggle.state", "{0}: {1}", LocalizeUiText(SystemTextCatalog.Key("localization.workbench.toggle"), "Translation authoring mode"), AuthoringHostText(localizationWorkbench.Enabled ? "state.on" : "state.off", localizationWorkbench.Enabled ? "On" : "Off")), AuthoringHostText("toggle.help", "Authoring starts Off after every application launch."), Action: new("localization-workbench", "toggle")),
            new("authoring-preview", FormatAuthoringHostText("preview.state", "Preview selected mod: {0}", AuthoringHostText(localizationWorkbench.PreviewSelectedMod ? "state.on" : "state.off", localizationWorkbench.PreviewSelectedMod ? "On" : "Off")), AuthoringHostText("preview.help", "Temporary preview does not enable a package or reorder the saved stack."), Action: new("localization-workbench", "preview")),
            new("authoring-shortcuts", AuthoringHostText("shortcuts.label", "Editor shortcuts"), AuthoringHostText("shortcuts.entry.help", "Rebind editor commands, including native page break and Pick text / Navigate."), "localization-shortcuts"),
            new("authoring-fonts", AuthoringHostText("fonts.label", "Text presentation"), AuthoringHostText("fonts.help", "Choose system/native fonts and layout under Config → Interface → Text and fonts."), "config-text")
        ];
        foreach (ModPackage package in resourceHost!.Mods.Catalog.Packages.Where(package => package.HasLocalization))
        {
            entries.Add(new("authoring-project-" + package.Id, FormatAuthoringHostText("project.label", "{0}{1}", package.Name, authoringSelection?.Package.Id == package.Id ? " ✓" : ""), FormatAuthoringHostText("project.details", "{0} · {1}", package.RootDirectory, AuthoringHostText("package.status." + package.Status, package.Status.ToString())), Action: new("localization-workbench", "select:" + package.Id)));
        }
        if (entries.Count == 4)
        {
            entries.Add(new("authoring-create", AuthoringHostText("create.label", "Create a localization project"), AuthoringHostText("create.help", "Create a project first; disabled projects can be edited."), "localization-new"));
        }
        entries.AddRange(AuthoringProjectActions());
        if (authoringNotice.Length != 0)
        {
            entries.Add(new("authoring-status", authoringNotice, "", Enabled: false));
        }
        return entries;
    }

    private IReadOnlyList<MenuEntry> AuthoringShortcutEntries()
    {
        return authoringKeyboard.Settings.Bindings.Select(binding => new MenuEntry("shortcut-" + binding.Key, FormatAuthoringHostText("shortcut.binding", "{0} · {1}", AuthoringShortcutLabel(binding.Key), binding.Value), AuthoringHostText("shortcut.help", "Editor-only shortcut. Completion handles Enter and Escape before preview/save."), Action: new MenuAction("localization-workbench", "shortcut:" + binding.Key))).ToArray();
    }

    private string AuthoringHostText(string id, string original) => LocalizeUiText(SystemTextCatalog.Key("localization.workbench.host." + id), original);

    private string FormatAuthoringHostText(string id, string original, params object?[] arguments) => LocalizationTextComposition.Format(AuthoringHostText(id, original), arguments);

    private string AuthoringShortcutLabel(UiEditCommand command)
    {
        string id = "localization.workbench.host.shortcut." + command;
        return LocalizeUiText(SystemTextCatalog.Key(id), SystemTextCatalog.Defaults.GetValueOrDefault(id, command.ToString()));
    }

    private void AuthoringAction(string action)
    {
        if (HandleAuthoringProjectAction(action))
        {
            return;
        }
        if (action.StartsWith("shortcut:", StringComparison.Ordinal) && Enum.TryParse(action[9..], out UiEditCommand shortcut))
        {
            authoringRebinding = shortcut;
            authoringRebindingWaitForRelease = true;
            authoringNotice = FormatAuthoringHostText("shortcut.capture", "Release the current key, then press the shortcut for {0}. Escape cancels.", AuthoringShortcutLabel(shortcut));
            gameMenus?.InvalidateLiveData();
            return;
        }
        if (action == "toggle")
        {
            if (localizationWorkbench.Enabled)
            {
                localizationWorkbench.RequestDisable();
                return;
            }
            localizationWorkbench.Enabled = true;
            authoringKeyboard.Reset(Keyboard.GetState());
            LocalizationTextTrace.Enabled = true;
            gameMenus!.InvalidateLiveData();
            gameMenus.Close();
            return;
        }
        if (action == "preview")
        {
            localizationWorkbench.PreviewSelectedMod = !localizationWorkbench.PreviewSelectedMod;
            PublishAuthoringPreview();
            gameMenus!.InvalidateLiveData();
            return;
        }
        if (action.StartsWith("select:", StringComparison.Ordinal))
        {
            if (localizationWorkbench.IsEditing && (localizationWorkbench.Session!.IsDirty || localizationWorkbench.Session.Conflict is not null))
            {
                authoringNotice = "Preview/Save, Discard or Cancel the current draft before changing the editing mod.";
                return;
            }
            ModPackage package = resourceHost!.Mods.Catalog.Packages.Single(package => package.Id == action[7..]);
            StartAuthoringSelection(package);
        }
    }

    private long observedAuthoringCatalogGeneration = -1;
    private static string AuthoringInputIdentity(ModCatalogSnapshot catalog, string packageId)
    {
        return string.Join('\n', catalog.Files.Where(file => file.PackageId == packageId && file.RelativePath.StartsWith("Localization/", StringComparison.OrdinalIgnoreCase)).Select(file => file.RelativePath + ":" + file.Revision));
    }

    private void ReloadAuthoringSelection()
    {
        if (authoringSelection is { } selected)
        {
            StartAuthoringSelection(selected.Package);
        }
    }

    private void StartAuthoringSelection(ModPackage package, bool? enableWhenReady = null)
    {
        authoringOperation++;
        authoringSelectionOperation = authoringOperation;
        authoringSelectionEnableWhenReady = enableWhenReady ?? localizationWorkbench.Enabled;
        authoringRowTask = null;
        var descriptor = resourceHost!.Resources.Source;
        ModCatalogSnapshot candidate = resourceHost.Mods.Catalog;
        if (authoringSelectionTask is { } abandoned)
        {
            _ = abandoned.ContinueWith(completed =>
            {
                if (completed.IsCompletedSuccessfully)
                {
                    completed.Result.Inputs?.Dispose();
                }
                _ = completed.Exception;
            }, TaskScheduler.Default);
        }
        authoringSelectionTask = Task.Run(() =>
        {
            LocalizationPreparation prepared = LocalizationRuntime.PrepareAuthoringProject(candidate, package.Id, descriptor, authoringCancellation.Token, watchForPublication: true);
            LocalizationProject project = prepared.Layers.Single(layer => layer.PackageId == package.Id).Project;
            return new AuthoringSelection(package, project, prepared.Catalog, project.Entries.ToDictionary(row => row.Key, StringComparer.Ordinal), AuthoringInputIdentity(candidate, package.Id), prepared.InputRevision);
        }, authoringCancellation.Token);
        authoringNotice = "Reading localization project…";
    }

    private void PublishAuthoringPreview()
    {
        if (resourceHost is null)
        {
            return;
        }
        resourceHost.SetAuthoringLocalizationPreview(localizationWorkbench.Enabled && localizationWorkbench.PreviewSelectedMod ? authoringSelection?.Catalog : null);
        gameMenus?.InvalidateLiveData();
    }

    private void DisableLocalizationWorkbench()
    {
        ForgetRecoveredAuthoringDraft();
        authoringOperation++;
        authoringRowTask = null;
        authoringDraftPreview = null;
        resourceHost?.SetAuthoringDraftPreview(null, null, null, null);
        authoringOriginActive = authoringOriginDragging = false;
        authoringSelection?.Inputs?.Dispose();
        if (authoringSelection is { } selected)
        {
            authoringSelection = selected with { Inputs = null };
        }
        LocalizationTextTrace.Enabled = NativeGameFonts;
        authoringHits.Clear();
        authoringCandidates = null;
        PublishAuthoringPreview();
        drainUiButtons = true;
        SynchronizeUiMoviePause();
        if (authoringChooseProjectRequested)
        {
            authoringChooseProjectRequested = false;
            gameMenus?.Open("localization-workbench", true);
        }
    }

    private void OpenAuthoringBinding(AuthoringHit hit)
    {
        if (authoringSelection is null || authoringStore is null)
        {
            authoringNotice = "Choose an editing localization project in Debug Room → Localization.";
            gameMenus?.Open("localization-workbench", true);
            return;
        }
        if (localizationWorkbench.IsEditing && (localizationWorkbench.Session!.IsDirty || localizationWorkbench.Session.Conflict is not null))
        {
            authoringNotice = "Finish or cancel the current draft before selecting another occurrence.";
            return;
        }
        LocalizationRecord? row = authoringSelection.Rows.GetValueOrDefault(hit.Binding.Key);
        authoringRowOperation = authoringOperation;
        if (hit.NativeContext is null)
        {
            hit = hit with { NativeContext = AuthoringTextContext(hit.Binding) };
        }
        if (hit.Terms is null && !hit.Detached)
        {
            hit = hit with { Terms = authoringTermSnapshot };
        }
        if (hit.ButtonBindings is null && !hit.Detached)
        {
            hit = hit with { ButtonBindings = keyboard.CaptureNativeButtonBindingPrompt() };
        }
        if (!hit.Detached && hit.Binding.Names.Count == 0 && events is not null)
        {
            hit = hit with { Binding = hit.Binding with { Names = new Dictionary<int, string>(events.Scene.Names), MessageVariables = Enumerable.Range(0, 8).ToDictionary(slot => slot, slot => events.MessageVariables?.At(slot)) } };
        }
        editingAuthoringHit = hit;
        editingAuthoringBinding = hit.Binding;
        editingAuthoringDialogue = hit.Dialogue;
        authoringRowTask = row is null
            ? ReadVerifiedAuthoringRow(authoringStore, hit.Binding, authoringSelection.Project.Manifest)
            : authoringStore.ReadRowAsync(new(row.Category, row.File, row.Key), authoringCancellation.Token);
    }

    private async Task<LocalizationRowSnapshot> ReadVerifiedAuthoringRow(LocalizationProjectStore store, LocalizationTextBinding binding, LocalizationManifest manifest)
    {
        string gameDirectory = options.GameDirectory;
        string language = resourceHost!.Resources.Source.Language;
        return await Task.Run(async () =>
        {
            authoringKnownRows ??= LocalizationSourceExporter.Read(gameDirectory, language, authoringCancellation.Token).Entries.Concat(SystemTextCatalog.SourceEntries()).DistinctBy(entry => entry.Key).ToDictionary(entry => entry.Key, StringComparer.Ordinal);
            if (!authoringKnownRows.TryGetValue(binding.Key, out LocalizationSourceEntry? known))
            {
                throw new InvalidDataException("No verified canonical source row is available for " + binding.Key);
            }
            if (known.Category == "Fields" && binding.ResourceFingerprint is { } currentHash && known.Metadata.TryGetValue("JSM SHA256", out string? sourceHash) && !string.Equals(currentHash, sourceHash, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("The canonical source row does not match this occurrence's current script binding.");
            }
            LocalizationRecord prepared = LocalizationProjectBuilder.CreateVerifiedRecord(known, manifest);
            return await store.ReadOrPrepareRowAsync(prepared, authoringCancellation.Token).ConfigureAwait(false);
        }, authoringCancellation.Token).ConfigureAwait(false);
    }

    private IReadOnlyList<LocalizationWorkbenchLanguage> AuthoringLanguages(LocalizationRecord row)
    {
        LocalizationManifest manifest = authoringSelection!.Project.Manifest;
        LocalizationSourceRole primary = LocalizationSourceRoles.Resolve(manifest);
        List<LocalizationWorkbenchLanguage> tabs = [new("target", manifest.TargetLanguage + " · Target", row.Target, true)];
        foreach (string language in new[] { primary.Language, manifest.FieldScriptBaseLanguage }.Concat(manifest.ReferenceLanguages.Values).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            string? label = manifest.ReferenceLanguages.FirstOrDefault(pair => pair.Value.Equals(language, StringComparison.OrdinalIgnoreCase)).Key;
            string? value = label is null || !row.ReferencePresence.GetValueOrDefault(label, true) ? null : row.References.GetValueOrDefault(label);
            if (language.Equals(manifest.SourceLanguage, StringComparison.OrdinalIgnoreCase))
            {
                value ??= row.SourcePresent ? row.Source : null;
            }
            tabs.Add(new(language, language + (language == primary.Language ? " · Source Reference" : "") + (language == manifest.FieldScriptBaseLanguage ? " · Source Scripts" : ""), value, IsSourceReference: language == primary.Language, IsScriptBase: language == manifest.FieldScriptBaseLanguage));
        }
        return tabs;
    }

    private string AuthoringObservationText(LocalizationObservation occurrence, int slot)
    {
        if (authoringSelection is not { } selected || !selected.Rows.TryGetValue(occurrence.Identity.Key, out LocalizationRecord? row))
        {
            return slot == 0 ? occurrence.Text : "Unavailable";
        }
        if (slot == 0)
        {
            if (row.TargetPresent && !string.IsNullOrEmpty(row.Target))
            {
                return row.Target;
            }
            try
            {
                return LocalizationSourceRoles.Seed(selected.Project.Manifest, row).Text;
            }
            catch (InvalidOperationException)
            {
                return "No text available";
            }
        }
        LocalizationManifest manifest = selected.Project.Manifest;
        string primary = LocalizationSourceRoles.Resolve(manifest).Language;
        string[] languages = new[] { primary, manifest.FieldScriptBaseLanguage }.Concat(manifest.ReferenceLanguages.Values).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if ((uint)(slot - 1) >= languages.Length)
        {
            return "Unavailable";
        }
        string language = languages[slot - 1];
        string? column = manifest.ReferenceLanguages.FirstOrDefault(pair => pair.Value.Equals(language, StringComparison.OrdinalIgnoreCase)).Key;
        return (column is null || !row.ReferencePresence.GetValueOrDefault(column, true) ? null : row.References.GetValueOrDefault(column)) ?? (language == manifest.SourceLanguage && row.SourcePresent ? row.Source : null) ?? "Unavailable";
    }

    private void PollLocalizationAuthoring()
    {
        if (authoringSelectionTask is { IsCompleted: true } selectionTask)
        {
            authoringSelectionTask = null;
            if (authoringSelectionOperation != authoringOperation)
            {
                if (selectionTask.IsCompletedSuccessfully)
                {
                    selectionTask.Result.Inputs?.Dispose();
                }
                _ = selectionTask.Exception;
                return;
            }
            try
            {
                AuthoringSelection selected = selectionTask.GetAwaiter().GetResult();
                try
                {
                    selected.Inputs?.ValidatePublication();
                }
                catch
                {
                    selected.Inputs?.Dispose();
                    throw;
                }
                authoringSelection?.Inputs?.Dispose();
                authoringSelection = selected;
                authoringStore = new(selected.Package.RootDirectory);
                authoringRememberedProject = selected.Package.Id;
                localizationWorkbench.SelectedProjectId = selected.Project.Manifest.ProjectId;
                localizationWorkbench.InvalidateJournal();
                localizationWorkbench.SetJournalLanguages(AuthoringLanguages(new LocalizationRecord()));
                authoringNotice = selected.Project.Manifest.TargetLanguage + " · " + selected.Package.RootDirectory + " · write access checked on save";
                if (authoringSelectionEnableWhenReady)
                {
                    localizationWorkbench.Enabled = true;
                    authoringKeyboard.Reset(Keyboard.GetState());
                    LocalizationTextTrace.Enabled = true;
                    gameMenus?.Close();
                }
                PublishAuthoringPreview();
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or OperationCanceledException)
            {
                authoringNotice = exception.Message;
            }
        }
        if (authoringReplayTask is { IsCompleted: true } replayTask)
        {
            authoringReplayTask = null;
            try
            {
                var replay = replayTask.GetAwaiter().GetResult();
                if (authoringRowOperation == authoringOperation)
                {
                    var draft = new LocalizationEditingSession(authoringStore!, authoringSyntax, replay.Current) { RecordChanges = false };
                    draft.SetDraft(replay.Proposed);
                    localizationWorkbench.Open(draft, AuthoringLanguages(replay.Current.Row), authoringSelection!.Package.Name, ValidateAuthoringTarget);
                    authoringNotice = "Review Current → Proposed replay before saving. Later unowned components are preserved.";
                }
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or InvalidOperationException or OperationCanceledException)
            {
                authoringNotice = exception.Message;
            }
        }
        if (authoringRowTask is { IsCompleted: true } rowTask)
        {
            authoringRowTask = null;
            if (authoringRowOperation != authoringOperation)
            {
                return;
            }
            try
            {
                LocalizationRowSnapshot row = rowTask.GetAwaiter().GetResult();
                localizationWorkbench.SystemTemplate = row.Identity.Category == "System";
                localizationWorkbench.PageBreakSupported = row.Identity.Category != "System";
                localizationWorkbench.NativeDictionaryLanguage = resourceHost!.Resources.ResourceLanguage;
                localizationWorkbench.PositionSupported = row.Identity.Category == "Fields" && editingAuthoringDialogue is not null;
                localizationWorkbench.SourceRoleBadge = LocalizationSourceRoles.Resolve(authoringSelection!.Project.Manifest).Explanation;
                localizationWorkbench.Open(new(authoringStore!, authoringSyntax, row), AuthoringLanguages(row.Row), authoringSelection.Package.Name, ValidateAuthoringTarget);
                SynchronizeUiMoviePause();
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or OperationCanceledException)
            {
                authoringNotice = exception.Message;
            }
        }
    }

    private string AuthoringGhostPreview(LocalizationRecord row)
    {
        string source = LocalizationSourceRoles.Seed(authoringSelection!.Project.Manifest, row).Text;
        if (row.Category != "Fields")
        {
            return source;
        }
        LocalizationTargetParts reference = authoringSyntax.Parse(source);
        LocalizationTargetParts native = authoringSyntax.Parse(row.Source);
        return authoringSyntax.Compose(native with { Body = reference.Body });
    }

    private IReadOnlyList<string> ValidateAuthoringTarget(string target)
    {
        if (authoringSelection is null || localizationWorkbench.Session is null)
        {
            return ["No editing project is selected."];
        }
        try
        {
            LocalizationRecord original = localizationWorkbench.Session.Base.Row;
            localizationWorkbench.CompatibilityReporter = (value, role, dataset) =>
            {
                LocalizationTextBinding binding = editingAuthoringBinding ?? new LocalizationTextBinding(localizationWorkbench.Session!.Base.Identity.Key, original.Source ?? "", value, value) { Category = original.Category, File = original.File };
                NativeTextContext context = editingAuthoringHit?.NativeContext ?? AuthoringTextContext(binding);
                if (original.Category == "Fields")
                {
                    context = context with { Field = original.File };
                }
                dataset = role == "Target" ? authoringSelection!.Project.Manifest.TargetLanguage : dataset;
                NativeFontProfile? profile = nativeTextFont?.ProfileFor(context, role, dataset);
                try
                {
                    string body = authoringSyntax.Parse(value).Body;
                    bool missingArguments = original.Category == "System" && System.Text.CompositeFormat.Parse(value).MinimumArgumentCount > (editingAuthoringBinding?.Arguments.Count ?? 0);
                    IEnumerable<string> runs = missingArguments ? [] : ParseAuthoringPreview(value, OriginalTextLayout).Pages.SelectMany(page => page.Lines).SelectMany(line => line.Runs).Where(run => !run.IsButton).Select(run => run.Text);
                    return AuthoringCompatibilityReport.Evaluate(NativeGameFonts, OriginalTextLayout, profile, context, role, dataset, body, runs);
                }
                catch (Exception exception) when (exception is InvalidDataException or ArgumentException or FormatException or InvalidOperationException or NotSupportedException)
                {
                    return AuthoringCompatibilityReport.Evaluate(NativeGameFonts, OriginalTextLayout, profile, context, role, dataset, value, [], [new("presentation", exception.Message)]);
                }
            };
            if (target.Length == 0 && !localizationWorkbench.Session.IsDirty && localizationWorkbench.Session.RecordChanges)
            {
                target = AuthoringGhostPreview(original);
            }
            LocalizationRecord edited = original with { Target = target, TargetPresent = true };
            IReadOnlyList<string> rowErrors = authoringSyntax.ValidateCandidate(edited);
            if (rowErrors.Count != 0)
            {
                return rowErrors;
            }
            AuthoringCompatibilityReport report = localizationWorkbench.CompatibilityReporter!(target, "Target", authoringSelection.Project.Manifest.TargetLanguage);
            return report.PresentationDiagnostics.Select(error => error.Message).ToArray();
        }
        catch (Exception exception) when (exception is InvalidDataException or ArgumentException or FormatException or InvalidOperationException or NotSupportedException)
        {
            return [exception.Message];
        }
    }

    private void BeginAuthoringReplay(LocalizationChange change, bool redo)
    {
        if (authoringSelection?.Project.Manifest.ProjectId != change.Before.ProjectId || authoringStore is null)
        {
            authoringNotice = "Select the original project before replaying this edit.";
            return;
        }
        authoringRowOperation = authoringOperation;
        authoringReplayTask = PrepareAuthoringReplay(change, redo);
    }

    private async Task<(LocalizationRowSnapshot Current, LocalizationTargetValue Proposed)> PrepareAuthoringReplay(LocalizationChange change, bool redo)
    {
        LocalizationProjectStore store = authoringStore!;
        LocalizationManifest manifest = authoringSelection!.Project.Manifest;
        LocalizationRowSnapshot current = await store.ReadRowAsync(change.Before.Identity, authoringCancellation.Token).ConfigureAwait(false);
        LocalizationTargetValue proposed = LocalizationReplay.Propose(authoringSyntax, change, current.Target, redo, () => LocalizationSourceRoles.Seed(manifest, current.Row).Text);
        return (current, proposed);
    }

    private bool UpdateLocalizationAuthoring(TimeSpan elapsed)
    {
        localizationWorkbench.Poll();
        PollAuthoringTerms();
        PollAuthoringWorkspace();
        PollLocalizationAuthoring();
        PollAuthoringRecovery();
        if (localizationWorkbench.Enabled && authoringSelection?.Inputs is { } inputs && authoringSelectionTask is null)
        {
            try
            {
                inputs.ValidatePublication();
            }
            catch (IOException)
            {
                ReloadAuthoringSelection();
            }
        }
        if (resourceHost is not null && resourceHost.Mods.Catalog.Generation != observedAuthoringCatalogGeneration)
        {
            observedAuthoringCatalogGeneration = resourceHost.Mods.Catalog.Generation;
            if (localizationWorkbench.Enabled && authoringSelection is { } selected && authoringSelectionTask is null && AuthoringInputIdentity(resourceHost.Mods.Catalog, selected.Package.Id) != selected.InputIdentity)
            {
                StartAuthoringSelection(selected.Package);
            }
        }
        if (!ReferenceEquals(observedAuthoringEngine, events))
        {
            observedAuthoringEngine = events;
            observedAuthoringDialogues.Clear();
            observedAuthoringOccurrenceIds.Clear();
        }
        if (events is not null)
        {
            HashSet<int> channels = VisibleDialogues.Select(dialogue => dialogue.Channel).ToHashSet();
            foreach (int channel in observedAuthoringDialogues.Keys.Where(channel => !channels.Contains(channel)).ToArray())
            {
                observedAuthoringDialogues.Remove(channel);
                observedAuthoringOccurrenceIds.Remove(channel);
            }
        }
        KeyboardState keys = Keyboard.GetState();
        if (authoringRebinding is not null)
        {
            Keys[] capturedKeys = keys.GetPressedKeys().Where(key => key is not (Keys.LeftControl or Keys.RightControl or Keys.LeftShift or Keys.RightShift or Keys.LeftAlt or Keys.RightAlt)).ToArray();
            if (capturedKeys.Length == 0)
            {
                authoringRebindingWaitForRelease = false;
            }
            else if (!authoringRebindingWaitForRelease)
            {
                if (capturedKeys[0] == Keys.Escape)
                {
                    authoringRebinding = null;
                    authoringNotice = "Shortcut capture cancelled.";
                }
                else
                {
                    try
                    {
                        var chord = new AuthoringKeyChord(capturedKeys[0], keys.IsKeyDown(Keys.LeftControl) || keys.IsKeyDown(Keys.RightControl), keys.IsKeyDown(Keys.LeftShift) || keys.IsKeyDown(Keys.RightShift), keys.IsKeyDown(Keys.LeftAlt) || keys.IsKeyDown(Keys.RightAlt));
                        authoringKeyboard.Configure(authoringKeyboard.Settings.Rebind(authoringRebinding.Value, chord));
                        authoringRebinding = null;
                        authoringNotice = "Editor shortcut saved.";
                    }
                    catch (InvalidDataException exception)
                    {
                        authoringNotice = exception.Message;
                        authoringRebindingWaitForRelease = true;
                    }
                }
                authoringKeyboard.Reset(keys);
                gameMenus?.InvalidateLiveData();
            }
            return true;
        }
        if (!IsActive || !localizationWorkbench.Enabled || !localizationWorkbench.OwnsKeyboard)
        {
            authoringTextInput?.Reset();
        }
        if (!localizationWorkbench.Enabled)
        {
            authoringKeyboard.Reset(keys);
            return false;
        }
        bool confirmHeld = keys.IsKeyDown(Keys.Enter) || authoringKeyboard.IsHeld(UiEditCommand.Confirm, keys);
        if (!confirmHeld)
        {
            localizationWorkbench.ConfirmReleased();
        }
        bool alt = keys.IsKeyDown(Keys.LeftAlt) || keys.IsKeyDown(Keys.RightAlt);
        for (int index = 0; index < 3; index++)
        {
            if (alt && keys.IsKeyDown(Keys.D1 + index) && authoringPreviousKeys.IsKeyUp(Keys.D1 + index))
            {
                localizationWorkbench.SelectLanguageShortcut(index);
            }
        }
        foreach (AuthoringKeyCommand command in authoringKeyboard.Poll(keys, elapsed))
        {
            if (authoringCandidates is not null)
            {
                HandleAuthoringCandidateCommand(command.Command);
            }
            else if (localizationWorkbench.OwnsKeyboard || command.Command == UiEditCommand.TogglePicking)
            {
                localizationWorkbench.HandleCommand(command.Command, command.Shift, command.Control);
            }
        }
        MouseState mouse = Mouse.GetState();
        bool pressed = IsActive && mouse.LeftButton == ButtonState.Pressed;
        bool clicked = pressed && !authoringPreviousMouse;
        bool released = !pressed && authoringPreviousMouse;
        bool ownsPointer = localizationWorkbench.OwnsPointer(mouse.X, mouse.Y);
        if (PointerAuthoringCandidates(mouse.X, mouse.Y, clicked))
        {
            ownsPointer = true;
        }
        else if (clicked && !ownsPointer && !localizationWorkbench.IsEditing)
        {
            localizationWorkbench.BlurJournal();
        }
        if (ownsPointer || localizationWorkbench.IsEditing)
        {
            localizationWorkbench.Pointer(mouse.X, mouse.Y, pressed, released);
        }
        else if (clicked && localizationWorkbench.PickText)
        {
            ownsPointer = PickAuthoringText(mouse.X, mouse.Y);
        }
        if (authoringOriginVisible && clicked && authoringOriginHandle.Contains(mouse.X, mouse.Y))
        {
            authoringOriginDragging = true;
        }
        if (authoringOriginDragging && pressed)
        {
            float scale = authoringDialogueViewport.Width / 320f;
            SetAuthoringOrigin((int)Math.Round((mouse.X - authoringDialogueViewport.X) / scale), (int)Math.Round((mouse.Y - authoringDialogueViewport.Y) / scale));
        }
        if (released)
        {
            authoringOriginDragging = false;
        }
        authoringPreviousMouse = pressed;
        authoringPreviousKeys = keys;
        bool captured = authoringCandidates is not null || localizationWorkbench.CapturesSimulation || ownsPointer && pressed || authoringOriginDragging || authoringRowTask is not null;
        if (captured)
        {
            SynchronizeUiMoviePause();
        }
        return captured;
    }

    private void NudgeAuthoringOrigin(int deltaX, int deltaY)
    {
        if (localizationWorkbench.Session is not { } session || editingAuthoringDialogue is not { } native)
        {
            return;
        }
        int x = editingAuthoringHit?.Window?.X ?? native.X;
        int y = editingAuthoringHit?.Window?.Y ?? native.Y;
        if (FieldMessageContext.TryParseTag(0, session.Draft.Text ?? "", out FieldMessageContext? context, out _) && context!.Position is { } authored)
        {
            x = authored.X;
            y = authored.Y;
        }
        SetAuthoringOrigin(x + deltaX, y + deltaY);
    }

    private void SetAuthoringOrigin(int x, int y)
    {
        if (localizationWorkbench.Session is not { } draft || authoringSelection is null || editingAuthoringDialogue is null)
        {
            return;
        }
        try
        {
            string source = LocalizationSourceRoles.Seed(authoringSelection.Project.Manifest, draft.Base.Row).Text;
            draft.SetDraft(authoringSyntax.SetOrigin(draft.Draft, source, draft.Base.Row.Source ?? "", Math.Clamp(x, 0, 319), Math.Clamp(y, 0, 239)));
            localizationWorkbench.RefreshDraft();
            authoringDraftPreview = draft.Draft.Text;
            authoringOriginActive = true;
            gameMenus?.InvalidateLiveData();
        }
        catch (Exception exception) when (exception is InvalidDataException or InvalidOperationException)
        {
            authoringNotice = exception.Message;
        }
    }

    private void ResetAuthoringOrigin()
    {
        if (localizationWorkbench.Session is { } draft)
        {
            LocalizationTargetParts parts = authoringSyntax.Parse(draft.Draft.Text);
            draft.SetDraft(new(true, authoringSyntax.Compose(parts with { Position = null })));
            localizationWorkbench.RefreshDraft();
            authoringDraftPreview = draft.Draft.Text;
            gameMenus?.InvalidateLiveData();
        }
    }

    private LocalizationTextBinding? DialogueAuthoringBinding(DialogueState dialogue)
    {
        if (TryGeneratedAuthoringDialogue(dialogue, out GeneratedAuthoringDialogue? generated))
        {
            return generated!.Binding;
        }
        if (dialogue.IsNamePrompt || dialogue.MessageId < 0 || resourceHost is null)
        {
            return null;
        }
        string key = session.State.Scene == Memoria.FF8.Runtime.SceneKind.WorldMap
            ? LocalizationSourceExporter.IndexedKey(resourceHost.Resources.Source.Language, "messages_world", dialogue.MessageId)
            : world?.Field is { } field ? FieldLocalizationKey.Create(resourceHost.Resources.Source.Language, field.Name, dialogue.MessageId) : "";
        if (key.Length == 0)
        {
            return null;
        }
        LocalizationFieldText? fieldText = ResolveLocalizedField(dialogue);
        if (fieldText is not null)
        {
            key = resourceHost.Resources.Localization.Values.FirstOrDefault(pair => ReferenceEquals(pair.Value.Field, fieldText)).Key ?? key;
        }
        return resourceHost.Resources.ResolveText(key, dialogue.Text).Binding with
        {
            Category = session.State.Scene == SceneKind.WorldMap ? "World" : "Fields",
            File = session.State.Scene == SceneKind.WorldMap ? "messages_world" : world?.Field?.Name,
            Operation = dialogue.Operation,
            BaseKey = fieldText?.BaseKey ?? key,
            ResourceFingerprint = localizedFieldScriptHash,
            Names = new Dictionary<int, string>(events?.Scene.Names ?? []),
            MessageVariables = events is null ? new Dictionary<int, int?>() : Enumerable.Range(0, 8).ToDictionary(slot => slot, slot => events!.MessageVariables?.At(slot))
        };
    }

    private DialogueState AuthoringDialoguePreview(DialogueState native, DialogueState display)
    {
        if (authoringDraftPreview is null || !IsEditingAuthoringOccurrence(native))
        {
            return display;
        }
        if (authoringDraftPreview.Length == 0)
        {
            return display;
        }
        if (FieldMessageContext.TryParseTag(0, authoringDraftPreview, out FieldMessageContext? context, out int length))
        {
            return display with { Text = FormatAuthoringDialogueTemplate(authoringDraftPreview[length..]), X = context!.Position?.X ?? display.X, Y = context.Position?.Y ?? display.Y, FirstChoice = context.Choices?.FirstLine ?? display.FirstChoice, LastChoice = context.Choices?.LastLine ?? display.LastChoice, InitialChoice = context.Choices?.BeginLine ?? display.InitialChoice, CancelChoice = context.Choices?.CancelLine ?? display.CancelChoice };
        }
        return display with { Text = FormatAuthoringDialogueTemplate(authoringDraftPreview) };
    }

    private NativeTextContext AuthoringTextContext(LocalizationTextBinding binding)
    {
        return MonoGameUiCanvas.NativeContextForBinding(binding, NativeDialogueContext);
    }

    private string FormatAuthoringDialogueTemplate(string template)
    {
        return editingAuthoringHit?.FormatTemplate is { } format ? format(template) : template;
    }

    private void ObserveAuthoringDialogue(DialogueState dialogue, PixelBounds panel, PixelBounds viewport, PixelBounds nativeOrigin)
    {
        LocalizationTextBinding? binding = DialogueAuthoringBinding(dialogue);
        if (binding is null)
        {
            return;
        }
        NativeWorldMessage? worldMessage = session.State.Scene == SceneKind.WorldMap ? world?.WorldScene?.State.EventUi?.Windows.LastOrDefault(message => message.Channel == dialogue.Channel) : null;
        long occurrence = worldMessage is not null ? world!.WorldScene!.State.EventUi!.PresentationOccurrenceId(worldMessage.WindowId) : session.State.Scene == SceneKind.WorldMap ? 0 : events?.DialogueOccurrenceId(dialogue.Channel) ?? 0;
        if (!observedAuthoringDialogues.TryGetValue(dialogue.Channel, out DialogueState? previous) || (observedAuthoringOccurrenceIds.GetValueOrDefault(dialogue.Channel, -1) != occurrence || previous.MessageId != dialogue.MessageId || previous.Operation != dialogue.Operation))
        {
            observedAuthoringDialogues[dialogue.Channel] = dialogue;
            observedAuthoringOccurrenceIds[dialogue.Channel] = occurrence;
            Guid id = Guid.NewGuid();
            observedAuthoringBindings[id] = binding;
            observedAuthoringHits[id] = new(binding, new(panel.X, panel.Y, panel.Width, panel.Height), dialogue) { Window = events?.Scene.DialogueWindows.GetValueOrDefault(dialogue.Channel), Terms = authoringTermSnapshot, NativeContext = AuthoringTextContext(binding), ButtonBindings = keyboard.CaptureNativeButtonBindingPrompt(), FormatTemplate = TryGeneratedAuthoringDialogue(dialogue, out var generatedObservation) ? generatedObservation!.FormatTemplate : null };
            localizationWorkbench.Observe(new(id, DateTimeOffset.UtcNow, new(binding.Category ?? "Fields", binding.File ?? "messages_world", binding.Key), dialogue.Text, $"{session.State.Scene} · channel {dialogue.Channel} · operation {dialogue.Operation}"));
            if (observedAuthoringBindings.Count > 10000)
            {
                Guid evicted = observedAuthoringBindings.Keys.First();
                observedAuthoringBindings.Remove(evicted);
                observedAuthoringHits.Remove(evicted);
            }
        }
        if (localizationWorkbench.Enabled)
        {
            authoringHits.Add(new(binding, new(panel.X, panel.Y, panel.Width, panel.Height), dialogue) { DrawGroup = ++authoringDrawGroup, Terms = authoringTermSnapshot, NativeContext = AuthoringTextContext(binding), Window = events?.Scene.DialogueWindows.GetValueOrDefault(dialogue.Channel), FormatTemplate = TryGeneratedAuthoringDialogue(dialogue, out var generatedHit) ? generatedHit!.FormatTemplate : null });
        }
        if (IsEditingAuthoringOccurrence(dialogue))
        {
            authoringDialogueViewport = viewport;
            authoringOriginVisible = authoringOriginActive || !OriginalTextLayout && (panel.X != nativeOrigin.X || panel.Y != nativeOrigin.Y);
            authoringOriginHandle = new(nativeOrigin.X - 4, nativeOrigin.Y - 4, 8, 8);
        }
    }

    private void ObserveBoundAuthoringDialogue(LocalizationTextBinding binding, string channel, string occurrenceId, string rendered)
    {
        if (!observedAuthoringLifecycles.Add((channel, occurrenceId)))
        {
            return;
        }
        Guid id = Guid.NewGuid();
        observedAuthoringBindings[id] = binding;
        observedAuthoringHits[id] = new(binding, default, Detached: true) { Terms = authoringTermSnapshot, NativeContext = AuthoringTextContext(binding), ButtonBindings = keyboard.CaptureNativeButtonBindingPrompt() };
        LocalizationRecord? row = authoringSelection?.Rows.GetValueOrDefault(binding.Key);
        localizationWorkbench.Observe(new(id, DateTimeOffset.UtcNow, new(row?.Category ?? binding.Category ?? channel.Split('/')[0], row?.File ?? binding.File ?? channel, binding.Key), rendered, channel + " · " + occurrenceId));
        if (observedAuthoringLifecycles.Count > 10000)
        {
            observedAuthoringLifecycles.Remove(observedAuthoringLifecycles.First());
        }
        if (observedAuthoringBindings.Count > 10000)
        {
            Guid evicted = observedAuthoringBindings.Keys.First();
            observedAuthoringBindings.Remove(evicted);
            observedAuthoringHits.Remove(evicted);
        }
    }

    private void DrawLocalizationAuthoring(Rectangle bounds)
    {
        if (!localizationWorkbench.Enabled || uiCanvas is null)
        {
            return;
        }
        MouseState mouse = Mouse.GetState();
        if (localizationWorkbench.PickText && !localizationWorkbench.IsEditing && authoringHits.LastOrDefault(hit => hit.Bounds.Contains(mouse.X, mouse.Y)) is { } hovered)
        {
            uiCanvas.Fill(hovered.Bounds, new UiColor(255, 200, 220, 35));
            uiCanvas.Text(hovered.Binding.Key, 14, new(mouse.X + 12, mouse.Y + 12, 650, 26), new UiColor(255, 220, 235));
        }
        if (authoringOriginVisible && localizationWorkbench.IsEditing)
        {
            uiCanvas.Fill(authoringOriginHandle, new UiColor(255, 190, 210));
        }
        Action<LocalizationTextBinding, UiRect>? inspect = uiCanvas.InspectText;
        var inspectBatch = uiCanvas.InspectTextBatch;
        uiCanvas.InspectTextBatch = null;
        uiCanvas.InspectText = null;
        bool nativeFonts = uiCanvas.UseNativeFonts;
        uiCanvas.UseNativeFonts = false;
        try
        {
            localizationWorkbench.Draw(uiCanvas, new(bounds.X, bounds.Y, bounds.Width, bounds.Height), uiTheme);
            DrawAuthoringCandidates(new(bounds.X, bounds.Y, bounds.Width, bounds.Height));
            DrawDetachedAuthoringPreview(bounds);
            if (authoringNotice.Length != 0)
            {
                float noticeY = localizationWorkbench.IsEditing ? localizationWorkbench.EditorBounds.Bottom + 8 : bounds.Y + 48;
                float noticeWidth = localizationWorkbench.IsEditing ? localizationWorkbench.EditorBounds.Width : bounds.Width - 24;
                UiRect noticeBounds = new(bounds.X + 12, noticeY, Math.Max(0, noticeWidth), Math.Max(0, Math.Min(64, bounds.Bottom - noticeY - 8)));
                if (!noticeBounds.IsEmpty)
                {
                    uiCanvas.Text(authoringNotice, 14, noticeBounds, new UiColor(255, 220, 235), UiAlignment.Start, UiTextFlow.Wrap);
                }
            }
        }
        finally
        {
            uiCanvas.InspectText = inspect;
            uiCanvas.InspectTextBatch = inspectBatch;
            uiCanvas.UseNativeFonts = nativeFonts;
        }
    }
}
