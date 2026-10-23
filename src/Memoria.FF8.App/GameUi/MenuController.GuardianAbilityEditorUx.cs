using System.Collections.Immutable;
using Memoria.FF8.Mods;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    private ImmutableArray<ImmutableArray<GuardianForceAbilityTemplate>> guardianPlacementInput;
    private IReadOnlyList<string> guardianPlacementChanges = [];
    private bool guardianPreview;
    private int guardianUndoAbility;
    private int guardianRestoreAbility;
    private bool guardianSearchEditing;
    private int guardianSearchCaret;
    private string guardianFocus = "catalog";
    private string guardianOperationOrigin = "rows";
    private int guardianRowsPerColumn = 12;
    private int guardianColumns = 2;
    private int guardianCatalogCapacity = 12;
    private int guardianWheelDelta;
    private bool? guardianWheelCatalog;
    private UiRect guardianCatalogBounds;
    private UiRect guardianRowsBounds;
    private readonly List<(string Id, Action Activate)> guardianControls = [];
    private (int Subject, int Row, int Catalog, string Focus)? guardianEditorView;
    internal bool IsGuardianSearchEditing => IsGuardianAbilityEditor && guardianSearchEditing;
    internal bool IsGuardianAbilityWorkspace => IsGuardianAbilityEditor || ScreenId.StartsWith("mods-gf-", StringComparison.Ordinal) || ScreenId.StartsWith("gf-ability-", StringComparison.Ordinal);

    private string GuardianTargetTitle() => guardianLocal ? "This playthrough · Local changes" : guardianPackage is null ? "New mod · Untitled mod" : "Editing mod · " + Path.GetFileName(guardianPackage.PackageDirectory);
    private bool GuardianCatalogCanAdd => !guardianPreview && GuardianCatalog().ElementAtOrDefault(guardianCatalogCursor) is > 0 and var ability && !guardianDraft!.Preview.Templates[guardianSubject].Any(row => row.Ability == ability);
    private bool GuardianCanCommitOperation => !guardianPreview && GuardianSelectedRow is { } row && (guardianPending == "swap" ? row.Ability != guardianPendingAbility : guardianPending == "replace" && !guardianDraft!.Preview.Templates[guardianSubject].Any(entry => entry.Ability == guardianPendingAbility));

    private void RegisterGuardianEditorPages()
    {
        Register(new()
        {
            Id = "mods-gf-placement-review",
            Title = "GF Abilities · Package order review",
            Help = "The chosen package position changes the editable input. No file has been written.",
            Entries = _ => [new("accept", "Use reviewed package position", "Recompute the editable draft at this position. Review it, then Save mod again.", Action: new("gf-editor-placement-accept")), new("cancel", "Cancel", "Keep the current authoring input.", Action: new("menu-back"))],
            Details = _ => guardianPlacementChanges
        });
        Register(new()
        {
            Id = "mods-gf-restore-review",
            Title = "GF Abilities · Restore inherited value",
            Help = "Review the contribution and dependent changes before restoring inherited fields.",
            Entries = _ => [new("accept", "Restore reviewed contribution", "Author the reviewed restore as a new draft command.", Action: new("gf-editor-restore-accept")), new("cancel", "Cancel", "Keep the draft.", Action: new("menu-back"))],
            Details = _ => guardianDraft!.RestoreInheritedReview(guardianSubject, guardianRestoreAbility, guardianAbilityName!)
        });
        Register(new()
        {
            Id = "mods-gf-undo-review",
            Title = "GF Abilities · Undo review",
            Help = "Undo also removes later dependent edits. Unrelated edits remain.",
            Entries = _ => [new("accept", "Undo reviewed group", "Remove the listed dependent edits together.", Action: new("gf-editor-undo-accept")), new("cancel", "Cancel", "Keep the draft.", Action: new("menu-back"))],
            Details = _ => guardianDraft!.UndoReview(guardianSubject, guardianUndoAbility, guardianAbilityName!)
        });
        Register(new()
        {
            Id = "mods-gf-packages",
            Title = "GF Abilities · Edit existing mod",
            Help = "Compatible GF packages can be edited while disabled.",
            Entries = _ => modService?.InstalledPackages.Where(package => package.Format == ModFormat.Memoria && File.Exists(Path.Combine(package.RootDirectory, GuardianForceAbilityMods.PayloadPath))).Select(package => new MenuEntry(package.Id, package.Name + " · " + package.Status, "Edit this package's GF contribution. Save offers a copy when needed.", Action: new("gf-editor-target", package.Id), Enabled: package.Status != ModEffectiveStatus.IncompatibleEdition)).ToArray() ?? []
        });
        Register(new()
        {
            Id = "mods-gf-actions",
            Title = "GF Abilities · Actions",
            Help = "Actions apply to the selected editing target and focused selection.",
            Entries = _ => GuardianActionEntries()
        });
        Register(new()
        {
            Id = "mods-gf-details",
            Title = "GF Abilities · Details",
            Help = "Inspect the selected entry without changing it.",
            Entries = _ => [new("back", "Back", "Return to the editor.", Action: new("menu-back"))],
            Details = _ => GuardianDetailLines()
        });
        Register(new()
        {
            Id = "mods-gf-controls",
            Title = "GF Abilities · Controls",
            Help = "Bindings follow the active input device and configured profile.",
            Entries = _ => [new("back", "Back", "Return to the editor.", Action: new("menu-back"))],
            Details = _ => ["Up / Down: previous / next ability", "Left / Right or Page Up / Down: full page", guardianBinding!(ControlAction.CameraLeft) + " / " + guardianBinding!(ControlAction.CameraRight) + ": catalog / GF focus", guardianBinding!(ControlAction.RotateLeft) + " / " + guardianBinding!(ControlAction.RotateRight) + ": previous / next GF", guardianBinding!(ControlAction.GuardianSwitchColumn) + ": switch column", guardianBinding!(ControlAction.Card) + ": toggle Start learned", guardianBinding!(ControlAction.Menu) + ": Actions", "Tab / Shift+Tab: next / previous control", "Click a row to select. Use named actions or checkboxes to edit."]
        });
        Register(new()
        {
            Id = "mods-gf-select",
            Title = "GF Abilities · Guardian Force",
            Help = "Changing GF keeps draft edits to every GF.",
            Entries = _ => Enumerable.Range(0, 16).Select(id => new MenuEntry("gf-" + id, guardianEditorName!(id), "Select this GF.", Action: new("gf-editor-command", "subject:" + id))).ToArray()
        });
    }

    private IReadOnlyList<MenuEntry> GuardianActionEntries()
    {
        List<MenuEntry> entries = [];
        void Add(string id, string label, string help, bool enabled = true) => entries.Add(new(id, label, help, Action: new("gf-editor-command", id), Enabled: enabled));
        if (guardianPreview)
        {
            Add("preview", "Back to editor", "Restore the exact editing view.");
            Add("mode:0", "Source", "Loaded source; externally modified data may be an opaque baseline.");
            Add("mode:1", guardianLocal ? "These local changes only" : "This mod only", "Selected contribution over source, with unresolved dependencies shown.");
            Add("mode:2", "Current setup", "Ordered stack with the selected draft substituted once.");
            if (guardianPreviewMode == 2)
            {
                Add("include", (guardianWithoutLayer ? "[ ]" : "[x]") + " Include selected layer", "Simulate inclusion without changing activation.");
            }
        }
        else
        {
            if (guardianPane == 0)
            {
                Add("tool:0", "Add", GuardianCatalogCanAdd ? "Append the selected ability." : "Already in this GF, or no matching ability.", GuardianCatalogCanAdd);
                Add("tool:1", "Replace", "Choose a GF target, then explicitly confirm replacement.", GuardianCatalogCanAdd && GuardianSelectedRow is not null);
            }
            else
            {
                Add("tool:2", "Swap", "Choose a second ability to exchange positions.", GuardianSelectedRow is not null);
                Add("toggle", "Toggle Start learned", "Template only; current progress is preserved.", GuardianSelectedRow is not null);
                Add("tool:3", "Undo", GuardianUndoText(), GuardianSelectedRow is { } row && guardianDraft!.RowDirty(guardianSubject, row.Ability));
                Add("tool:4", "Restore inherited value", "Remove this layer's selected contribution and dependent operations.", GuardianSelectedRow is not null);
            }
            Add("preview", "Preview", "Read-only comparison workspace.");
            Add("tool:" + (guardianLocal ? 10 : 11), guardianLocal ? "Apply changes" : "Save mod", "Keep the selected contribution.");
            if (!guardianLocal)
            {
                Add("tool:10", "Apply to this playthrough", "Review progression and junction consequences; does not save the package.", readGuardianEditor!().Current is not null);
                Add("tool:12", "Save as new mod", "Write an independent package.");
                Add("tool:14", "Use package", "Review local ownership and reactivate the saved package contribution.", GuardianInstalledPackage is not null && reviewGuardianPackageOwnership is not null);
            }
            Add("search", "Search abilities", "Type using a keyboard. Controller users can use category and paging.");
            Add("clear", "Clear search", "Show all names in the selected category.", guardianSearch.Length > 0);
            Add("tool:9", "Filter category · " + GuardianCategories[guardianCategory], "Choose the next category.");
            Add("mod-details", "Mod details", "Layer ownership, identity, position and activation are separate from file state.");
            Add("tool:13", "Change editing target", "Handle unsaved changes before selecting another owner.");
            Add("discard", "Discard changes", "Review before discarding the selected draft.", guardianDraft?.Dirty == true);
        }
        Add("select", "Select GF", "Choose any of the sixteen learnable GFs.");
        Add("column", "Switch column", "Move to the other GF column at the same visible row.");
        Add("details", "Details", "Full description, provenance and diagnostic information.");
        Add("controls", "Controls", "Show bindings and interaction reference.");
        return entries;
    }

    private void GuardianExecuteCommand(string command)
    {
        if (command.StartsWith("tool:", StringComparison.Ordinal))
        {
            GuardianTool(int.Parse(command[5..], System.Globalization.CultureInfo.InvariantCulture));
        }
        else if (command.StartsWith("subject:", StringComparison.Ordinal))
        {
            guardianSubject = int.Parse(command[8..], System.Globalization.CultureInfo.InvariantCulture);
            guardianRowCursor = 0;
        }
        else if (command.StartsWith("mode:", StringComparison.Ordinal) && guardianPreview)
        {
            guardianPreviewMode = int.Parse(command[5..], System.Globalization.CultureInfo.InvariantCulture);
            guardianRowCursor = 0;
        }
        else
        {
            switch (command)
            {
                case "preview":
                    GuardianTogglePreview();
                    break;
                case "include":
                    guardianWithoutLayer = !guardianWithoutLayer;
                    break;
                case "toggle":
                    GuardianToggleRow(guardianRowCursor);
                    break;
                case "search":
                    GuardianStartSearch();
                    break;
                case "clear":
                    int? ability = GuardianCatalog().ElementAtOrDefault(guardianCatalogCursor);
                    guardianSearch = "";
                    GuardianRefilter(ability);
                    break;
                case "select":
                    Push("mods-gf-select");
                    break;
                case "column":
                    GuardianFocus("rows");
                    GuardianSwitchColumn();
                    break;
                case "details":
                    GuardianDetails();
                    break;
                case "controls":
                    Push("mods-gf-controls");
                    break;
                case "mod-details":
                    guardianModDetails = true;
                    Push("mods-gf-details");
                    break;
                case "discard":
                    GuardGuardianDraft(() => Back());
                    break;
            }
        }
    }

    internal bool ReviewGuardianPackagePlacement(string packageId, GuardianForceAbilityPatchDocument document)
    {
        GuardianForceEditorContext context = readGuardianEditor!();
        var preceding = context.Layers.Where(layer => layer.Document.DocumentId != document.DocumentId && !context.ExcludedDocuments.Contains(layer.Document.DocumentId) && CompareGuardianPackageOrder(layer.PackageId, packageId) < 0).ToArray();
        var input = GuardianForceAbilityComposer.Compose(context.Source.ResolveGuardianForceTemplates(), preceding, context.Source.MenuDefinitions.AbilityPoints).Templates;
        var proposed = GuardianForceAbilityComposer.Compose(input, [new(packageId, document)], context.Source.MenuDefinitions.AbilityPoints);
        var expected = guardianDraft!.Preview;
        List<string> changes = [];
        foreach (int gf in document.Operations.Select(operation => operation.GuardianForce).Distinct())
        {
            if (!expected.Templates[gf].SequenceEqual(proposed.Templates[gf]))
            {
                changes.Add(guardianEditorName!(gf) + ": " + string.Join(", ", expected.Templates[gf].Select(entry => guardianAbilityName!(entry.Ability) + (entry.InitialLearned ? " [learned]" : ""))) + " → " + string.Join(", ", proposed.Templates[gf].Select(entry => guardianAbilityName!(entry.Ability) + (entry.InitialLearned ? " [learned]" : ""))));
            }
        }
        changes.AddRange(proposed.Outcomes.Where(outcome => outcome.Status is not ("Applied" or "AlreadySatisfied")).Select(outcome => outcome.Message));
        if (changes.Count > 0)
        {
            guardianPlacementInput = input;
            guardianPlacementChanges = changes.Prepend("Selected package: " + packageId + ". Its real position differs materially from the current authoring input.").ToArray();
            Push("mods-gf-placement-review");
            return false;
        }
        guardianDraft.RebaseInput(input);
        return true;
    }

    private bool guardianModDetails;
    private IReadOnlyList<string> GuardianDetailLines()
    {
        if (guardianModDetails)
        {
            return [GuardianTargetTitle(), guardianPackage?.PackageDirectory ?? "No package destination selected.", "Document: " + guardianDraft!.ExportDocument.DocumentId, guardianPackage is null && !guardianLocal ? "Provisional position: after native GF layers, before playthrough local changes. Save determines the final package order." : "The selected layer is composed over its preceding input.", "Saving, runtime activation and normal game saving have separate states."];
        }
        int ability = guardianPane == 0 ? GuardianCatalog().ElementAtOrDefault(guardianCatalogCursor) : GuardianSelectedRow?.Ability ?? 0;
        GuardianForceAbilityComposition composition = GuardianEditorComposition();
        GuardianForceAbilityTemplate? row = composition.Templates[guardianSubject].FirstOrDefault(entry => entry.Ability == ability);
        NativeGuardianForceAbility? current = readGuardianEditor!().Current?.GuardianForceAbilities(guardianSubject).FirstOrDefault(entry => entry.Ability == ability);
        List<string> lines = [guardianAbilityName!(ability), guardianAbilityDescription!(ability), "Catalog cost: " + readGuardianEditor!().Source.MenuDefinitions.AbilityPoints[ability] + " AP", "Start learned affects the template; current progress is preserved unless explicitly applied.", current is null ? "No current progress / not acquired." : current.Learned ? "Current save: learned" : $"Current save: {current.Progress} / {current.RequiredPoints} AP"];
        if (row is not null)
        {
            lines.Add(row.PrerequisiteAbility is int prerequisite ? "Requires " + guardianAbilityName!(prerequisite) : "Required GF level: " + row.RequiredLevel);
            lines.Add(row.SupportedCondition ? "Availability condition supported." : "Unsupported source condition; see diagnostics.");
        }
        lines.AddRange(composition.Origins.Where(origin => origin.GuardianForce == guardianSubject && origin.Ability == ability).Select(origin => origin.PackageId + " · " + origin.Aspect));
        lines.AddRange(composition.Outcomes.Where(outcome => outcome.GuardianForce == guardianSubject && outcome.Status is not ("Applied" or "AlreadySatisfied")).Select(outcome => outcome.Message));
        return lines;
    }

    private void GuardianDetails()
    {
        guardianModDetails = false;
        Push("mods-gf-details");
    }

    private void GuardianReturnFromDialog()
    {
        Action? continuation = guardianAfterDiscard;
        Back();
        guardianAfterDiscard = continuation;
    }

    private void GuardianChangeSubject(int subject)
    {
        int? selected = GuardianSelectedRow?.Ability;
        guardianPending = null;
        guardianSubject = subject;
        var rows = GuardianEditorComposition().Templates[subject];
        int found = rows.ToList().FindIndex(row => row.Ability == selected);
        guardianRowCursor = found >= 0 ? found : Math.Clamp(guardianRowCursor, 0, Math.Max(0, rows.Length - 1));
    }

    private string GuardianPreviewStatus()
    {
        if (guardianPreviewMode == 0)
        {
            return readGuardianEditor!().Current?.GuardianForceOpaqueBaseline == true ? "Opaque external baseline; prior attribution is baked into source." : "Loaded source before managed authored layers.";
        }
        if (guardianPreviewMode == 2 && GuardianInstalledPackage is null && !guardianLocal)
        {
            return "Simulated candidate · Provisional position: after native layers, before local changes.";
        }
        if (guardianPreviewMode == 2 && !guardianWithoutLayer && guardianPackage is not null && !readGuardianEditor!().Layers.Any(layer => layer.Document.DocumentId == guardianDraft!.ExportDocument.DocumentId))
        {
            return "Preview as enabled · Package activation is unchanged.";
        }
        return "Read-only comparison · Current progress is preserved.";
    }

    private void GuardianFocus(string id)
    {
        if (guardianPending is not null && id is not ("rows" or "gf-prev-page" or "gf-next-page" or "operation-confirm" or "operation-cancel"))
        {
            guardianPending = null;
        }
        if (id != "search")
        {
            guardianSearchEditing = false;
        }
        guardianFocus = id;
        if (id == "catalog")
        {
            guardianPane = 0;
        }
        else if (id == "rows")
        {
            guardianPane = 1;
        }
    }

    private void GuardianTraverseFocus(bool reverse)
    {
        int index = guardianControls.FindIndex(control => control.Id == guardianFocus);
        if (guardianControls.Count > 0)
        {
            GuardianFocus(guardianControls[(index + (reverse ? guardianControls.Count - 1 : 1) + guardianControls.Count) % guardianControls.Count].Id);
        }
    }

    private void GuardianSelectCatalog(int index)
    {
        GuardianFocus("catalog");
        guardianCatalogCursor = index;
        _view = null;
    }

    private void GuardianSelectRow(int index)
    {
        GuardianFocus("rows");
        guardianRowCursor = index;
        _view = null;
    }

    private void GuardianPage(bool catalog, int direction)
    {
        int capacity = catalog ? guardianCatalogCapacity : guardianRowsPerColumn * guardianColumns;
        capacity = Math.Max(1, capacity);
        int count = catalog ? GuardianCatalog().Length : GuardianEditorComposition().Templates[guardianSubject].Length;
        int cursor = catalog ? guardianCatalogCursor : guardianRowCursor;
        int page = cursor / capacity;
        int destination = Math.Clamp(page + direction, 0, Math.Max(0, (count - 1) / capacity));
        int selection = Math.Max(0, Math.Min(destination * capacity + cursor % capacity, count - 1));
        if (catalog)
        {
            guardianCatalogCursor = selection;
        }
        else
        {
            guardianRowCursor = selection;
        }
        _view = null;
    }

    private void GuardianSwitchColumn()
    {
        if (guardianColumns != 2 || guardianFocus != "rows")
        {
            return;
        }
        int capacity = guardianRowsPerColumn * 2;
        int start = guardianRowCursor / capacity * capacity;
        int slot = guardianRowCursor % capacity;
        int targetStart = start + (slot < guardianRowsPerColumn ? guardianRowsPerColumn : 0);
        int count = GuardianEditorComposition().Templates[guardianSubject].Length;
        if (targetStart < count)
        {
            guardianRowCursor = Math.Min(targetStart + slot % guardianRowsPerColumn, count - 1);
        }
    }

    private void GuardianToggleRow(int index)
    {
        if (guardianPreview || guardianPending is not null)
        {
            Message = guardianPreview ? "Preview is read-only." : "Finish or cancel the pending operation first.";
            return;
        }
        GuardianSelectRow(index);
        if (GuardianSelectedRow is { } row)
        {
            GuardianEdit(() => guardianDraft!.Toggle(guardianSubject, row.Ability));
        }
    }

    private void GuardianConfirmOperation()
    {
        if (!GuardianCanCommitOperation || GuardianSelectedRow is not { } row)
        {
            Message = guardianPending == "swap" ? "Choose a different ability." : "The incoming ability must not already be in this GF.";
            return;
        }
        int follow = guardianPendingAbility;
        if (guardianPending == "replace")
        {
            GuardianEdit(() => guardianDraft!.Replace(guardianSubject, row.Ability, follow));
        }
        else if (guardianPending == "swap")
        {
            GuardianEdit(() => guardianDraft!.Swap(guardianSubject, follow, row.Ability));
        }
        guardianPending = null;
        guardianRowCursor = Math.Max(0, guardianDraft!.Preview.Templates[guardianSubject].ToList().FindIndex(entry => entry.Ability == follow));
        GuardianFocus("rows");
    }

    private void GuardianCancelOperation()
    {
        string origin = guardianOperationOrigin;
        guardianPending = null;
        GuardianFocus(origin);
        _view = null;
    }

    private string GuardianOperationText() => guardianPending is null ? "" : guardianPending == "swap" ? "Swap " + guardianAbilityName!(guardianPendingAbility) + " with " + (GuardianSelectedRow is { } row ? guardianAbilityName!(row.Ability) : "a target") : "Replace " + (GuardianSelectedRow is { } target ? guardianAbilityName!(target.Ability) : "a target") + " with " + guardianAbilityName!(guardianPendingAbility);
    private string GuardianUndoText() => GuardianSelectedRow is { } row ? guardianDraft!.UndoDescription(guardianSubject, row.Ability, guardianAbilityName!) : "No selected entry";
    private string GuardianConfirmHint() => guardianFocus == "catalog" ? "Confirm: Add / Replace" : guardianFocus != "rows" ? "Confirm: " + guardianFocus : guardianPreview ? "Confirm: Details" : guardianPending is not null ? "Confirm: " + (guardianPending == "swap" ? "Swap" : "Replace") : GuardianSelectedRow is { } row && guardianDraft!.RowDirty(guardianSubject, row.Ability) ? "Confirm: " + GuardianUndoText() : "Confirm: Actions";

    private void GuardianOpenActions()
    {
        guardianPending = null;
        guardianSearchEditing = false;
        Push("mods-gf-actions");
    }

    private void GuardianTogglePreview()
    {
        guardianPending = null;
        guardianSearchEditing = false;
        if (!guardianPreview)
        {
            guardianEditorView = (guardianSubject, guardianRowCursor, guardianCatalogCursor, guardianFocus);
            guardianPreview = true;
            guardianPreviewMode = 2;
            bool active = guardianLocal || GuardianInstalledPackage is null || readGuardianEditor!().Layers.Any(layer => layer.Document.DocumentId == guardianDraft!.ExportDocument.DocumentId);
            guardianWithoutLayer = !active;
            GuardianFocus("rows");
        }
        else
        {
            guardianPreview = false;
            if (guardianEditorView is { } view)
            {
                guardianSubject = view.Subject;
                guardianRowCursor = view.Row;
                guardianCatalogCursor = view.Catalog;
                GuardianFocus(view.Focus);
            }
        }
        _view = null;
    }

    private void GuardianRefilter(int? ability)
    {
        int index = Array.IndexOf(GuardianCatalog(), ability ?? 0);
        guardianCatalogCursor = Math.Max(0, index);
        guardianSearchCaret = Math.Clamp(guardianSearchCaret, 0, guardianSearch.Length);
        _view = null;
    }

    private void GuardianClearSearch()
    {
        int? selected = GuardianCatalog().ElementAtOrDefault(guardianCatalogCursor);
        guardianSearch = "";
        guardianSearchCaret = 0;
        GuardianRefilter(selected);
    }

    private void GuardianStartSearch()
    {
        if (guardianPreview)
        {
            return;
        }
        GuardianFocus("search");
        guardianSearchEditing = true;
        guardianSearchCaret = guardianSearch.Length;
        _view = null;
    }

    internal bool EnterGuardianSearchCharacter(char character)
    {
        if (!IsGuardianSearchEditing)
        {
            return false;
        }
        int? selected = GuardianCatalog().ElementAtOrDefault(guardianCatalogCursor);
        if (character == '\b' && guardianSearchCaret > 0)
        {
            guardianSearch = guardianSearch.Remove(--guardianSearchCaret, 1);
        }
        else if (!char.IsControl(character) && guardianSearch.Length < 100)
        {
            guardianSearch = guardianSearch.Insert(guardianSearchCaret++, character.ToString());
        }
        GuardianRefilter(selected);
        return true;
    }

    internal void HandleGuardianAbilityEditorWheel(int x, int y, int delta)
    {
        var point = BuildView().ToDesignPoint(x, y);
        bool catalog = !guardianPreview && guardianCatalogBounds.Contains(point.X, point.Y);
        bool rows = guardianRowsBounds.Contains(point.X, point.Y);
        if ((!catalog && !rows) || guardianPending is not null && !rows || guardianSearchEditing)
        {
            guardianWheelDelta = 0;
            return;
        }
        if (guardianWheelCatalog != catalog)
        {
            guardianWheelDelta = 0;
            guardianWheelCatalog = catalog;
        }
        guardianWheelDelta += delta;
        while (Math.Abs(guardianWheelDelta) >= 120)
        {
            int direction = guardianWheelDelta > 0 ? -1 : 1;
            GuardianPage(catalog, direction);
            guardianWheelDelta += direction * 120;
        }
    }

    private bool NavigateGuardianEditor(MenuInput input)
    {
        if (IsGuardianAbilityWorkspace && !IsGuardianAbilityEditor && input == MenuInput.Actions)
        {
            if (ScreenId == "mods-gf-actions")
            {
                Back();
            }
            return true;
        }
        if (!IsGuardianAbilityEditor || guardianDraft is null)
        {
            return false;
        }
        if (guardianSearchEditing)
        {
            switch (input)
            {
                case MenuInput.Confirm:
                case MenuInput.Cancel:
                    guardianSearchEditing = false;
                    GuardianFocus("catalog");
                    break;
                case MenuInput.Left:
                    guardianSearchCaret = Math.Max(0, guardianSearchCaret - 1);
                    break;
                case MenuInput.Right:
                    guardianSearchCaret = Math.Min(guardianSearch.Length, guardianSearchCaret + 1);
                    break;
                case MenuInput.Actions:
                    guardianSearchEditing = false;
                    GuardianOpenActions();
                    break;
                case MenuInput.Tab:
                case MenuInput.ReverseTab:
                    guardianSearchEditing = false;
                    GuardianTraverseFocus(input == MenuInput.ReverseTab);
                    break;
            }
            _view = null;
            return true;
        }
        if (input == MenuInput.Cancel)
        {
            if (guardianPending is not null)
            {
                GuardianCancelOperation();
            }
            else if (guardianPreview)
            {
                GuardianTogglePreview();
            }
            else
            {
                GuardGuardianDraft(() => Back());
            }
        }
        else if (input == MenuInput.Actions)
        {
            GuardianOpenActions();
        }
        else if (input is MenuInput.FocusLeft or MenuInput.FocusRight)
        {
            GuardianFocus(input == MenuInput.FocusLeft && !guardianPreview ? "catalog" : "rows");
        }
        else if (input is MenuInput.Tab or MenuInput.ReverseTab)
        {
            GuardianTraverseFocus(input == MenuInput.ReverseTab);
        }
        else if (input == MenuInput.SwitchColumn)
        {
            GuardianSwitchColumn();
        }
        else if (input is MenuInput.PageLeft or MenuInput.PageRight)
        {
            guardianPending = null;
            guardianSubject = (guardianSubject + (input == MenuInput.PageLeft ? 15 : 1)) % 16;
            guardianRowCursor = 0;
        }
        else if (input is MenuInput.Up or MenuInput.Down or MenuInput.PageUp or MenuInput.PageDown or MenuInput.Left or MenuInput.Right)
        {
            int direction = input is MenuInput.Up or MenuInput.PageUp or MenuInput.Left ? -1 : 1;
            if (guardianFocus is "catalog" or "rows")
            {
                if (input is MenuInput.Left or MenuInput.Right or MenuInput.PageUp or MenuInput.PageDown)
                {
                    GuardianPage(guardianFocus == "catalog", direction);
                }
                else if (guardianFocus == "catalog")
                {
                    guardianCatalogCursor = Math.Clamp(guardianCatalogCursor + direction, 0, Math.Max(0, GuardianCatalog().Length - 1));
                }
                else
                {
                    guardianRowCursor = Math.Clamp(guardianRowCursor + direction, 0, Math.Max(0, GuardianEditorComposition().Templates[guardianSubject].Length - 1));
                }
            }
        }
        else if (input == MenuInput.Alternate && guardianFocus == "rows")
        {
            GuardianToggleRow(guardianRowCursor);
        }
        else if (input == MenuInput.Confirm)
        {
            if (guardianFocus is not ("catalog" or "rows"))
            {
                guardianControls.FirstOrDefault(control => control.Id == guardianFocus).Activate?.Invoke();
            }
            else if (guardianPreview)
            {
                GuardianDetails();
            }
            else if (guardianFocus == "rows" && guardianPending is not null)
            {
                GuardianConfirmOperation();
            }
            else if (guardianFocus == "rows" && GuardianSelectedRow is { } row && guardianDraft.RowDirty(guardianSubject, row.Ability))
            {
                GuardianUndo();
            }
            else
            {
                GuardianOpenActions();
            }
        }
        _view = null;
        return true;
    }

    private void GuardianUndo()
    {
        if (GuardianSelectedRow is not { } row)
        {
            return;
        }
        if (guardianDraft!.UndoRequiresReview(guardianSubject, row.Ability))
        {
            guardianUndoAbility = row.Ability;
            Push("mods-gf-undo-review");
            return;
        }
        GuardianEdit(() => guardianDraft.Undo(guardianSubject, row.Ability));
    }
}
