using System.Globalization;
using Memoria.FF8.Resources.Localization;
using Memoria.FF8.Ui;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private bool authoringControlPointerPressed;
    private bool authoringControlConfirmHeld;
    private bool authoringControlCancelHeld;

    /// <summary>Local control input enters the same editor, picker and transaction paths as on-screen input.</summary>
    private bool TryDispatchLocalizationAuthoringControl(ControlRequest request, UiControlCommand command)
    {
        bool input = request.Command.Equals("ui-input", StringComparison.OrdinalIgnoreCase);
        if (request.Command.Equals("ui-close", StringComparison.OrdinalIgnoreCase) && localizationWorkbench.IsEditing)
        {
            localizationWorkbench.HandleCommand(UiEditCommand.Cancel);
            drainUiButtons = true;
            return true;
        }
        if (!input)
        {
            return false;
        }
        bool explicitAuthoring = command.Edit is not null || command.Text is not null || command.Language is not null || command.Release is not null || command.AuthoringKey is not null;
        if (!localizationWorkbench.Enabled)
        {
            if (explicitAuthoring)
            {
                throw new InvalidOperationException("Enable translation authoring in Debug Room → Localization before sending editor input.");
            }
            return false;
        }
        if (localizationWorkbench.OwnsKeyboard && command.Input is not null && !explicitAuthoring)
        {
            throw new InvalidOperationException("The workbench owns keyboard input; send an Edit command rather than menu navigation.");
        }
        if (command.Release == true)
        {
            authoringControlConfirmHeld = authoringControlCancelHeld = false;
            authoringTextInput?.ObserveCommandKeys(false, false);
            localizationWorkbench.ConfirmReleased();
        }
        if (command.AuthoringKey is string key)
        {
            OpenAuthoringControlKey(key);
        }
        if (command.Language is int language)
        {
            localizationWorkbench.SelectLanguageShortcut(language);
        }
        if (command.Text is string text)
        {
            if (!localizationWorkbench.OwnsKeyboard)
            {
                throw new InvalidOperationException("Open the editor or focus journal search before sending committed text.");
            }
            if (authoringTextInput is null)
            {
                localizationWorkbench.TextInput(text);
            }
            else
            {
                authoringTextInput.ReceiveText(text);
            }
        }
        if (command.Edit is UiEditCommand edit)
        {
            if (!Enum.IsDefined(edit))
            {
                throw new ArgumentOutOfRangeException(nameof(command), "Unknown authoring editor command.");
            }
            bool held = edit == UiEditCommand.Confirm && authoringControlConfirmHeld || edit == UiEditCommand.Cancel && authoringControlCancelHeld;
            if (edit == UiEditCommand.Confirm)
            {
                authoringControlConfirmHeld = true;
            }
            if (edit == UiEditCommand.Cancel)
            {
                authoringControlCancelHeld = true;
            }
            if (!held && authoringTextInput?.ShouldSuppressCommand(edit) != true)
            {
                if (authoringCandidates is not null)
                {
                    HandleAuthoringCandidateCommand(edit);
                }
                else if (localizationWorkbench.OwnsKeyboard || edit == UiEditCommand.TogglePicking)
                {
                    localizationWorkbench.HandleCommand(edit, command.Shift, command.Control);
                }
                else
                {
                    throw new InvalidOperationException("The workbench does not own keyboard input.");
                }
            }
        }
        if (command.X is float x && command.Y is float y)
        {
            if (!float.IsFinite(x) || !float.IsFinite(y))
            {
                throw new ArgumentOutOfRangeException(nameof(command), "Pointer coordinates must be finite.");
            }
            bool clicked = command.Pressed && !authoringControlPointerPressed;
            bool released = !command.Pressed && authoringControlPointerPressed;
            bool owned = PointerAuthoringCandidates(x, y, clicked);
            if (!owned && (localizationWorkbench.OwnsPointer(x, y) || localizationWorkbench.IsEditing || authoringControlPointerPressed))
            {
                owned = localizationWorkbench.Pointer(x, y, clicked, released);
            }
            if (!owned && clicked && localizationWorkbench.PickText && !localizationWorkbench.OwnsPointer(x, y))
            {
                localizationWorkbench.BlurJournal();
                owned = PickAuthoringText(x, y);
            }
            authoringControlPointerPressed = command.Pressed;
            if (owned || explicitAuthoring || localizationWorkbench.IsEditing)
            {
                return true;
            }
        }
        return explicitAuthoring;
    }

    private void OpenAuthoringControlKey(string key)
    {
        AuthoringHit? visible = authoringHits.LastOrDefault(hit => hit.Binding.Key.Equals(key, StringComparison.Ordinal));
        if (visible is not null)
        {
            OpenAuthoringBinding(visible);
            return;
        }
        if (!TryResolveAuthoringCatalogKey(key, out LocalizationTextBinding? binding))
        {
            throw new InvalidOperationException("The requested key is neither a visible bound occurrence nor a registered System catalog key.");
        }
        OpenAuthoringBinding(new(binding!, default, Detached: true));
    }

    internal static bool TryResolveAuthoringCatalogKey(string key, out LocalizationTextBinding? binding)
    {
        const string prefix = "$en_" + SystemTextCatalog.File + "_";
        binding = null;
        if (!key.StartsWith(prefix, StringComparison.Ordinal) || !SystemTextCatalog.Defaults.TryGetValue(key[prefix.Length..], out string? source))
        {
            return false;
        }
        binding = new(key, source, source, source) { Category = "System", File = SystemTextCatalog.File, BaseKey = key };
        return true;
    }

    private object LocalizationAuthoringControlStatus()
    {
        var session = localizationWorkbench.Session;
        UiRect viewport = new(GraphicsDevice.Viewport.X, GraphicsDevice.Viewport.Y, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
        string? selectedKey = session?.Base.Identity.Key;
        LocalizationTextBinding? resolution = selectedKey is null || resourceHost is null ? null : resourceHost.Resources.ResolveText(selectedKey, session!.Base.Row.Source ?? "").Binding;
        return new
        {
            localizationWorkbench.Enabled,
            localizationWorkbench.PickText,
            localizationWorkbench.PreviewSelectedMod,
            localizationWorkbench.OwnsKeyboard,
            localizationWorkbench.CapturesSimulation,
            SelectedPackage = authoringSelection?.Package.Id,
            SelectedProject = authoringSelection?.Project.Manifest.ProjectId,
            ProjectPath = authoringSelection?.Package.RootDirectory,
            LoadingProject = authoringSelectionTask is not null,
            LoadingRow = authoringRowTask is not null,
            Mode = session?.Mode.ToString(),
            Identity = session?.Base.Identity,
            Draft = session?.Draft,
            Dirty = session?.IsDirty ?? false,
            Revision = session?.Base.Revision,
            EditorText = localizationWorkbench.Editor.Text,
            localizationWorkbench.EditorLanguage,
            ReferenceReadOnly = localizationWorkbench.Editor.ReadOnly,
            References = localizationWorkbench.EditorLanguages.Select(tab => new { tab.Id, tab.Label, tab.IsTarget, tab.IsSourceReference, tab.IsScriptBase, ReadOnly = !tab.IsTarget }).ToArray(),
            JournalLanguages = localizationWorkbench.JournalLanguages.Select(tab => new { tab.Id, tab.Label, tab.IsTarget, tab.IsSourceReference, tab.IsScriptBase }).ToArray(),
            localizationWorkbench.JournalLanguage,
            localizationWorkbench.JournalExpanded,
            localizationWorkbench.EditorBounds,
            localizationWorkbench.JournalBounds,
            EditorSelection = new { localizationWorkbench.Editor.Caret, localizationWorkbench.Editor.SelectionStart, localizationWorkbench.Editor.SelectionLength },
            Controls = localizationWorkbench.ControlHits,
            localizationWorkbench.Status,
            Notice = authoringNotice,
            InputProblem = authoringTextInput?.PlatformProblem,
            IsComposing = authoringTextInput?.IsComposing ?? false,
            SavePending = localizationWorkbench.PendingSave is { IsCompleted: false },
            Changes = localizationWorkbench.ReadHistory().Select(change => new { change.Id, change.Before.ProjectId, change.Before.Identity, change.Summary, change.Fields }).ToArray(),
            Conflict = session?.Conflict is { } conflict ? new { Base = conflict.Base.Target, conflict.Ours, Theirs = conflict.Theirs.Target, Candidates = conflict.Candidates } : null,
            Preview = new { localizationWorkbench.PreviewPage, localizationWorkbench.PreviewPageCount, localizationWorkbench.PreviewChoice, localizationWorkbench.PreviewChoices },
            Native = new { FontMode = uiPreferences.TextFontMode.ToString(), LayoutMode = uiPreferences.TextLayoutMode.ToString(), RequestedProfile = uiPreferences.NativeFontProfileId, EffectiveProfile = nativeTextFont?.Profile?.Id, Ready = nativeTextFont?.Ready ?? false, Diagnostic = nativeTextFont?.Diagnostic, Revision = nativeTextFont?.Revision },
            CatalogGeneration = resourceHost?.Resources.CatalogGeneration,
            Simulation = new { this.session.State.FieldId, Scene = this.session.State.Scene.ToString(), Frame = events?.Frame, DialogueOperation = world?.CurrentDialogue?.Operation, NativeText = world?.CurrentDialogue?.Text, FirstChoice = world?.CurrentDialogue?.FirstChoice, LastChoice = world?.CurrentDialogue?.LastChoice, InitialChoice = world?.CurrentDialogue?.InitialChoice, CancelChoice = world?.CurrentDialogue?.CancelChoice, SelectedChoice = selectedChoice, Decisions = events?.Decisions.Count ?? 0, Outcomes = events?.DialogueOutcomes.Count ?? 0 },
            SelectedKeyResolution = resolution is null ? null : new { resolution.Key, resolution.Template, resolution.Text, resolution.PackageId, resolution.ProjectId },
            Occurrence = editingAuthoringBinding is null ? null : new { editingAuthoringBinding.Key, editingAuthoringBinding.Source, editingAuthoringBinding.Template, editingAuthoringBinding.Text, Arguments = editingAuthoringBinding.Arguments.Select(value => Convert.ToString(value, CultureInfo.InvariantCulture)).ToArray(), editingAuthoringBinding.Operation, editingAuthoringBinding.ResourceFingerprint, Detached = editingAuthoringHit?.Detached ?? false },
            Candidates = authoringCandidates?.Select(hit => new { hit.Binding.Key, hit.Binding.Operation }).ToArray(),
            VisibleHits = authoringHits.Select((hit, order) => new { hit.Binding.Key, hit.Binding.Category, hit.Binding.File, hit.Binding.Template, hit.Binding.Text, Arguments = hit.Binding.Arguments.Select(value => Convert.ToString(value, CultureInfo.InvariantCulture)).ToArray(), hit.Binding.Operation, hit.DrawGroup, DrawOrder = order, Bounds = hit.Bounds.Intersect(viewport) }).Where(hit => !hit.Bounds.IsEmpty).ToArray()
        };
    }
}
