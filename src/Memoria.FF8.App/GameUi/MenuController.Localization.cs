using Memoria.FF8.Mods.Localization;
using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8.GameUi;

internal sealed record LocalizationDatasetOption(string Id, string Label, bool Available = true, string? Problem = null, bool ScriptBaseAvailable = true);
internal sealed record LocalizationAuthoringServices(string MemoriaRoot, Func<IReadOnlyList<LocalizationDatasetOption>> ReadDatasets, Func<LocalizationAuthoringRequest, CancellationToken, IProgress<string>, LocalizationProject> Prepare, Action? Refresh = null, Func<IReadOnlyList<string>>? ReadTargetIds = null);

public sealed partial class MenuController
{
    private LocalizationAuthoringServices? localizationAuthoring;
    private string localizationSource = "";
    private string localizationScriptBase = "";
    private string localizationTarget = "";
    private string localizationPackage = "My localization";
    private string localizationFormat = "json";
    private string localizationLayout = "single";
    private string localizationDestination = "";
    private string localizationInput = "";
    private string localizationTextField = "target";
    private int localizationTextPage;
    private bool localizationConverting;
    private bool localizationPatching;
    private readonly HashSet<string> localizationReferences = new(StringComparer.OrdinalIgnoreCase);
    private LocalizationProject? localizationPreview;
    private CancellationTokenSource? localizationCancellation;
    private Task<LocalizationProject>? localizationPreparation;
    private Task? localizationWriting;
    private volatile string localizationProgress = "";
    private string localizationDrawnProgress = "";
    internal bool IsLocalizationTextEditing => IsOpen && ScreenId == "localization-text";

    private void RefreshLocalizationView()
    {
        if (ScreenId != "localization-review")
        {
            return;
        }
        PollLocalizationOperation();
        if (localizationDrawnProgress != localizationProgress)
        {
            localizationDrawnProgress = localizationProgress;
            _view = null;
        }
    }

    internal void ConnectLocalizationAuthoring(LocalizationAuthoringServices services)
    {
        localizationAuthoring = services;
        Register(new() { Id = "mods-create", Title = "Mods · Create Mods", Help = "Create gameplay mods or translation projects.", HelpText = c => c.SystemText("mods.create.help.localization", "Create gameplay mods or translation projects."), Entries = _ => CreateModEntries(), BuildContent = (c, panel) => panel.Children.Add(c.BuildCreateModsPage()) });
        Register(new() { Id = "localization-new", Title = "New localization", Help = "Source is the main prose reference; script base selects the field dialogue topology. Target accepts a custom translation name.", Entries = _ => LocalizationOptions(), Details = _ => LocalizationOptionsDetails() });
        Register(new() { Id = "localization-convert", Title = "Convert localization", Help = "Convert an existing project into a new package. No installed game is required.", Entries = _ => LocalizationOptions(), Details = _ => LocalizationOptionsDetails() });
        Register(new() { Id = "localization-patch", Title = "New localization patch", Help = "Choose a base project. The patch keeps its Target and source identities, with empty translations for your changes.", Entries = _ => LocalizationOptions(), Details = _ => LocalizationOptionsDetails() });
        Register(new() { Id = "localization-datasets", Title = "Localization · Languages", Help = "Only successfully probed datasets are available. Confirm toggles references or selects the requested language.", Entries = _ => LocalizationDatasets() });
        Register(new() { Id = "localization-text", Title = "Localization · Text entry", Help = "Type using the keyboard, or select characters with the controller. Target is a nonempty custom identifier, for example ru (Literary edition).", Columns = 8, Entries = _ => LocalizationTextEntries(), Details = _ => [LocalizationTextValue()] });
        Register(new() { Id = "localization-review", Title = "Localization · Review and progress", Help = "Review the exact destination, languages, categories and source gaps before creating the disabled package.", Entries = _ => LocalizationReviewEntries(), Details = _ => LocalizationReviewDetails() });
    }

    private IReadOnlyList<MenuEntry> LocalizationOptions()
    {
        List<MenuEntry> entries = [];
        if (localizationConverting || localizationPatching)
        {
            entries.Add(new("input", (localizationPatching ? SystemText("ui.localizationoptions.entry.label.base.package", "Base package: ") : SystemText("ui.localizationoptions.entry.label.input.package", "Input package: ")) + localizationInput, SystemText("ui.localizationoptions.entry.help.choose.a.package.containing.localization.localization.manifest.json", "Choose a package containing Localization/localization.manifest.json."), Action: new("localization-input")));
            if (localizationPatching)
            {
                entries.Add(new("package", SystemText("ui.localizationoptions.entry.label.package.name", "Package name: ") + localizationPackage, SystemText("ui.localizationoptions.entry.help.the.patch.has.its.own.package.identity.and", "The patch has its own package identity and inherits the base Target."), Action: new("localization-text", "package")));
            }
        }
        else
        {
            entries.Add(new("source", SystemText("ui.localizationoptions.entry.label.source", "Source: ") + localizationSource, SystemText("ui.localizationoptions.entry.help.main.prose.reference.does.not.change.installed.scripts", "Main prose reference; does not change installed scripts."), Action: new("localization-language", "source")));
            entries.Add(new("base", SystemText("ui.localizationoptions.entry.label.field.script.base", "Field script base: ") + localizationScriptBase, SystemText("ui.localizationoptions.entry.help.authoring.topology.does.not.switch.the.running.installation", "Authoring topology; does not switch the running installation."), Action: new("localization-language", "base")));
            entries.Add(new("target", SystemText("ui.localizationoptions.entry.label.target", "Target: ") + localizationTarget, SystemText("ui.localizationoptions.entry.help.use.a.distinct.custom.identifier.for.an.independent", "Use a distinct custom identifier for an independent translation."), Action: new("localization-text", "target")));
            entries.Add(new("package", SystemText("ui.localizationoptions.entry.label.package.name", "Package name: ") + localizationPackage, SystemText("ui.localizationoptions.entry.help.a.friendly.package.name.independent.of.target", "A friendly package name, independent of Target."), Action: new("localization-text", "package")));
            entries.Add(new("references", SystemText("ui.localizationoptions.entry.label.references", "References: ") + string.Join(", ", localizationReferences), SystemText("ui.localizationoptions.entry.help.toggle.additional.available.language.datasets", "Toggle additional available language datasets."), Action: new("localization-language", "references")));
        }
        entries.Add(new("format", SystemText("ui.localizationoptions.entry.label.format", "Format: .") + localizationFormat, SystemText("ui.localizationoptions.entry.help.change.format.while.preserving.entered.options", "Change format while preserving entered options."), Action: new("localization-format")));
        entries.Add(new("layout", SystemText("ui.localizationoptions.entry.label.layout", "Layout: ") + (localizationLayout == "single" ? SystemText("ui.localizationoptions.entry.label.all.in.one", "All-in-one") : SystemText("ui.localizationoptions.entry.label.separate.category.file.payloads", "Separate category/file payloads")), SystemText("ui.localizationoptions.entry.help.change.payload.layout", "Change payload layout."), Action: new("localization-layout")));
        entries.Add(new("destination", SystemText("ui.localizationoptions.entry.label.destination", "Destination: ") + localizationDestination, SystemText("ui.localizationoptions.entry.help.choose.the.parent.folder.a.new.child.package", "Choose the parent folder; a new child package directory will be used."), Action: new("localization-destination")));
        entries.Add(new("preview", SystemText("ui.localizationoptions.entry.label.prepare.preview", "Prepare preview"), SystemText("ui.localizationoptions.entry.help.extract.and.validate.the.project.before.any.package", "Extract and validate the project before any package is written."), Action: new("localization-preview")));
        entries.Add(new("back", SystemText("ui.localizationoptions.entry.label.back", "Back"), SystemText("ui.localizationoptions.entry.help.keep.entered.options", "Keep entered options."), Action: new("menu-back")));
        return entries;
    }

    private IReadOnlyList<string> LocalizationOptionsDetails() => [SystemText("ui.localizationoptionsdetails.details.new.packages.are.disabled.activation.remains.explicit.in", "New packages are disabled. Activation remains explicit in Installed Mods."), SystemText("ui.localizationoptionsdetails.details.existing.packages.and.translator.edits.are.preserved.choose", "Existing packages and translator edits are preserved; choose a new destination."), SystemText("ui.localizationoptionsdetails.details.target.ids.are.labels.not.filenames.or.worksheet", "Target IDs are labels, not filenames or worksheet names."), Message];

    private IReadOnlyList<MenuEntry> LocalizationDatasets()
    {
        return [.. localizationAuthoring!.ReadDatasets().Select(dataset => new MenuEntry(dataset.Id, (localizationTextField == "references" ? "[" + (localizationReferences.Contains(dataset.Id) ? SystemText("ui.localizationdatasets.entry.label.x", "x") : " ") + "] " : "") + dataset.Label, dataset.Problem ?? (localizationTextField == "base" && !dataset.ScriptBaseAvailable ? SystemText("ui.localizationdatasets.entry.help.a.translation.contribution.is.a.prose.reference.choose", "A translation contribution is a prose reference; choose an installed field script language for the script base.") : SystemText("ui.localizationdatasets.entry.help.available.dataset", "Available dataset.")), Action: new("localization-select-language", dataset.Id), Enabled: dataset.Available && (localizationTextField != "base" || dataset.ScriptBaseAvailable))), new("done", SystemText("ui.localizationdatasets.entry.label.done", "Done"), SystemText("ui.localizationdatasets.entry.help.keep.selection", "Keep selection."), Action: new("menu-back"))];
    }

    private string LocalizationTextValue() => localizationTextField == "package" ? localizationPackage : localizationTarget;

    private IReadOnlyList<MenuEntry> LocalizationTextEntries() => [.. NamePages[localizationTextPage].Characters.Where(character => character != '\0').Concat(" ").Distinct().Select((letter, index) => new MenuEntry("letter-" + index, letter.ToString(), SystemText("ui.localizationtextentries.entry.help.append.character", "Append character."), Action: new("localization-letter", Value: letter.ToString()))), new("alphabet", SystemText("ui.localizationtextentries.entry.label.character.page", "Character page: ") + NamePages[localizationTextPage].Title, SystemText("ui.localizationtextentries.entry.help.cycle.english.symbols.and.cyrillic.controller.character.pages", "Cycle English, Symbols and Cyrillic controller character pages."), Action: new("localization-alphabet")), new("delete", SystemText("ui.localizationtextentries.entry.label.delete", "Delete"), SystemText("ui.localizationtextentries.entry.help.remove.the.last.character", "Remove the last character."), Action: new("localization-delete")), new("clear", SystemText("ui.localizationtextentries.entry.label.clear", "Clear"), SystemText("ui.localizationtextentries.entry.help.clear.entered.text", "Clear entered text."), Action: new("localization-clear")), new("done", SystemText("ui.localizationtextentries.entry.label.done", "Done"), SystemText("ui.localizationtextentries.entry.help.keep.entered.text", "Keep entered text."), Action: new("menu-back"))];

    public bool EnterLocalizationCharacter(char character)
    {
        if (!IsOpen || ScreenId != "localization-text")
        {
            return false;
        }
        string value = LocalizationTextValue();
        if (character == '\b' && value.Length > 0)
        {
            value = value[..^(value.Length > 1 && char.IsSurrogatePair(value, value.Length - 2) ? 2 : 1)];
        }
        else if (!char.IsControl(character) && value.Length < 200)
        {
            value += character;
        }
        if (localizationTextField == "package")
        {
            localizationPackage = value;
        }
        else
        {
            localizationTarget = value;
        }
        _view = null;
        return true;
    }

    private bool TryHandleLocalizationAction(MenuAction action)
    {
        if (!action.Kind.StartsWith("localization-", StringComparison.Ordinal))
        {
            return false;
        }
        try
        {
            HandleLocalizationAction(action);
        }
        catch (Exception error) when (error is IOException or InvalidDataException or ArgumentException or InvalidOperationException or UnauthorizedAccessException)
        {
            Message = error.Message;
        }
        return true;
    }

    private void HandleLocalizationAction(MenuAction action)
    {
        if (HandleLocalizationStackAction(action))
        {
            return;
        }
        switch (action.Kind)
        {
            case "localization-new":
            case "localization-convert":
            case "localization-patch":
                localizationConverting = action.Kind == "localization-convert";
                localizationPatching = action.Kind == "localization-patch";
                if (!localizationConverting && !localizationPatching && localizationSource.Length == 0)
                {
                    IReadOnlyList<LocalizationDatasetOption> datasets = localizationAuthoring!.ReadDatasets();
                    LocalizationDatasetOption? initial = datasets.FirstOrDefault(dataset => dataset.Available);
                    localizationSource = initial?.Id ?? "";
                    localizationScriptBase = datasets.FirstOrDefault(dataset => dataset.Available && dataset.ScriptBaseAvailable)?.Id ?? "";
                    if (localizationScriptBase != localizationSource && localizationScriptBase.Length > 0)
                    {
                        localizationReferences.Add(localizationScriptBase);
                    }
                }
                Push(action.Kind);
                break;
            case "localization-language":
            case "localization-text":
                localizationTextField = action.SubjectId;
                Push(action.Kind == "localization-language" ? "localization-datasets" : "localization-text");
                break;
            case "localization-select-language":
                if (localizationTextField == "references")
                {
                    if (!localizationReferences.Add(action.SubjectId))
                    {
                        localizationReferences.Remove(action.SubjectId);
                    }
                }
                else
                {
                    if (localizationTextField == "source")
                    {
                        localizationSource = action.SubjectId;
                    }
                    else
                    {
                        localizationScriptBase = action.SubjectId;
                    }
                    if (localizationScriptBase != localizationSource)
                    {
                        localizationReferences.Add(localizationScriptBase);
                    }
                    Back();
                }
                break;
            case "localization-letter":
                EnterLocalizationCharacter(action.Value[0]);
                break;
            case "localization-alphabet":
                localizationTextPage = (localizationTextPage + 1) % NamePages.Length;
                break;
            case "localization-delete":
                EnterLocalizationCharacter('\b');
                break;
            case "localization-clear":
                if (localizationTextField == "package")
                {
                    localizationPackage = "";
                }
                else
                {
                    localizationTarget = "";
                }
                break;
            case "localization-format":
                localizationFormat = localizationFormat == "json" ? "xlsx" : "json";
                break;
            case "localization-layout":
                localizationLayout = localizationLayout == "single" ? "split" : "single";
                break;
            case "localization-input":
            case "localization-destination":
                BrowseLocalizationFolderAsync(action.Kind);
                break;
            case "localization-preview":
                BeginLocalizationPreview();
                break;
            case "localization-write":
                LocalizationProject project = localizationPreview ?? throw new InvalidOperationException("Prepare a project preview first.");
                string destination = localizationDestination;
                string format = localizationFormat;
                string layout = localizationLayout;
                localizationCancellation = new();
                CancellationToken token = localizationCancellation.Token;
                localizationProgress = "Writing package…";
                localizationWriting = Task.Run(() => LocalizationProjectIO.WriteNew(project, destination, format, layout, token), token);
                localizationPreview = null;
                break;
            case "localization-cancel":
                localizationCancellation?.Cancel();
                localizationProgress = "Cancelling…";
                break;
        }
    }

    private async void BrowseLocalizationFolderAsync(string kind)
    {
        try
        {
            string title = kind == "localization-input" ? SystemText("ui.handlelocalizationaction.title.choose.the.existing.localization.package", "Choose the existing localization package") : SystemText("ui.handlelocalizationaction.title.choose.a.parent.folder.for.the.new.localization", "Choose a parent folder for the new localization package");
            string? path = await ModelBrowserDialogs.PathAsync(ModelBrowserDialogs.Desktop.SelectFolderAsync(new(title, localizationAuthoring!.MemoriaRoot)));
            if (path is not null)
            {
                if (kind == "localization-input")
                {
                    localizationInput = path;
                }
                else
                {
                    localizationDestination = Path.Combine(path, "Localization-" + Guid.NewGuid().ToString("N")[..8]);
                }
            }
        }
        catch (Exception error) when (error is IOException or ArgumentException or InvalidOperationException or UnauthorizedAccessException)
        {
            Message = error.Message;
        }
        _view = null;
    }

    private void BeginLocalizationPreview()
    {
        if (localizationPreparation is not null || localizationWriting is not null)
        {
            throw new InvalidOperationException("An operation is already running.");
        }
        if (localizationDestination.Length == 0)
        {
            throw new InvalidOperationException("Choose a destination parent folder first.");
        }
        if (Directory.Exists(localizationDestination) || File.Exists(localizationDestination))
        {
            throw new IOException("The destination exists. Choose a new package directory to preserve existing edits.");
        }
        if (!localizationConverting && !localizationPatching && (string.IsNullOrWhiteSpace(localizationTarget) || localizationTarget.Any(char.IsControl)))
        {
            throw new InvalidDataException("Target must be a nonempty identifier without control characters.");
        }
        if (!localizationConverting && !localizationPatching && localizationAuthoring!.ReadTargetIds?.Invoke().Contains(localizationTarget, StringComparer.OrdinalIgnoreCase) == true)
        {
            throw new InvalidDataException("An independent translation needs a Target ID distinct from existing languages and translations.");
        }
        LocalizationAuthoringRequest request = new(localizationSource, localizationScriptBase, localizationTarget, localizationReferences.ToArray(), localizationPackage);
        string input = localizationInput;
        bool convert = localizationConverting;
        bool patch = localizationPatching;
        string packageName = localizationPackage;
        localizationPreview = null;
        localizationCancellation = new();
        CancellationToken token = localizationCancellation.Token;
        localizationProgress = "Preparing project…";
        IProgress<string> progress = new LocalizationProgress(text => localizationProgress = text);
        localizationPreparation = Task.Run(() =>
        {
            token.ThrowIfCancellationRequested();
            LocalizationProject result = patch ? LocalizationProjectExport.CreatePatch(LocalizationProjectIO.Read(input), packageName) : convert ? LocalizationProjectIO.Read(input) : localizationAuthoring!.Prepare(request, token, progress);
            token.ThrowIfCancellationRequested();
            return result;
        }, token);
        Push("localization-review");
    }

    private void PollLocalizationOperation()
    {
        try
        {
            if (localizationPreparation?.IsCompleted == true)
            {
                Task<LocalizationProject> operation = localizationPreparation;
                localizationPreparation = null;
                localizationPreview = operation.GetAwaiter().GetResult();
                localizationProgress = "Preview ready. Confirm Write package to create it.";
            }
            if (localizationWriting?.IsCompleted == true)
            {
                Task operation = localizationWriting;
                localizationWriting = null;
                operation.GetAwaiter().GetResult();
                localizationProgress = "Created disabled package: " + localizationDestination;
                localizationAuthoring?.Refresh?.Invoke();
            }
        }
        catch (OperationCanceledException)
        {
            localizationProgress = "Cancelled. No new package was published.";
        }
        catch (Exception error)
        {
            localizationProgress = "Failed: " + error.Message;
        }
        finally
        {
            if (localizationPreparation is null && localizationWriting is null)
            {
                localizationCancellation?.Dispose();
                localizationCancellation = null;
            }
        }
    }

    private IReadOnlyList<MenuEntry> LocalizationReviewEntries()
    {
        PollLocalizationOperation();
        if (localizationPreparation is not null || localizationWriting is not null)
        {
            return [new("cancel", SystemText("ui.localizationreviewentries.entry.label.cancel.operation", "Cancel operation"), SystemText("ui.localizationreviewentries.entry.help.cancel.extraction.or.writing.no.partial.package.is", "Cancel extraction or writing; no partial package is published."), Action: new("localization-cancel"))];
        }
        return [new("write", SystemText("ui.localizationreviewentries.entry.label.write.package", "Write package"), SystemText("ui.localizationreviewentries.entry.help.create.this.reviewed.project.as.a.new.disabled", "Create this reviewed project as a new disabled package."), Action: new("localization-write"), Enabled: localizationPreview is not null), new("back", SystemText("ui.localizationreviewentries.entry.label.back", "Back"), SystemText("ui.localizationreviewentries.entry.help.return.to.the.preserved.project.options", "Return to the preserved project options."), Action: new("menu-back"))];
    }

    private IReadOnlyList<string> LocalizationReviewDetails()
    {
        PollLocalizationOperation();
        List<string> details = [localizationProgress, SystemText("ui.localizationreviewdetails.details.destination", "Destination: ") + localizationDestination, SystemText("ui.localizationreviewdetails.details.format", "Format: ") + localizationFormat + SystemText("ui.localizationreviewdetails.details.layout", " · Layout: ") + localizationLayout];
        if (localizationPreview is { } project)
        {
            details.Add(SystemText("ui.localizationreviewdetails.details.source", "Source: ") + project.Manifest.SourceLanguage + SystemText("ui.localizationreviewdetails.details.script.base", " · Script base: ") + project.Manifest.FieldScriptBaseLanguage);
            details.Add(SystemText("ui.localizationreviewdetails.details.target", "Target: ") + project.Manifest.TargetLanguage);
            if (project.Manifest.BaseProjectId is string baseProject)
            {
                details.Add(SystemText("ui.localizationreviewdetails.details.patch.of.project", "Patch of project: ") + baseProject);
            }
            details.Add(SystemText("ui.localizationreviewdetails.details.references", "References: ") + string.Join(", ", project.Manifest.ReferenceLanguages.Values));
            details.Add(SystemText("ui.localizationreviewdetails.details.categories", "Categories: ") + string.Join(", ", project.Entries.Select(row => row.Category).Distinct().Order()));
            details.Add(SystemText("ui.localizationreviewdetails.details.records", "Records: ") + project.Entries.Count + SystemText("ui.localizationreviewdetails.details.missing.source", " · Missing source: ") + project.Entries.Count(row => !row.SourcePresent));
            if (project.Manifest.ExtensionData.TryGetValue("sourceGaps", out System.Text.Json.JsonElement gaps) && gaps.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                details.Add(SystemText("ui.localizationreviewdetails.details.unavailable.resource.category.combinations", "Unavailable resource/category combinations: ") + gaps.GetArrayLength());
            }
        }
        return details;
    }

    private sealed class LocalizationProgress(Action<string> report) : IProgress<string>
    {
        public void Report(string value) => report(value);
    }
}
