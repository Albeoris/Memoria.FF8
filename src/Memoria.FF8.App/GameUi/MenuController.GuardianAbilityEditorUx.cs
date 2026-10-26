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

    private string GuardianTargetTitle() => guardianLocal ? SystemText("ui.guardiantargettitle.details.this.playthrough.local.changes", "This playthrough · Local changes") : guardianPackage is null ? SystemText("ui.guardiantargettitle.details.new.mod.untitled.mod", "New mod · Untitled mod") : FormatSystemText("ui.guardiantargettitle.details.editing.mod.template", "Editing mod \u00B7 {0}", Path.GetFileName(guardianPackage.PackageDirectory));
    private bool GuardianCatalogCanAdd => !guardianPreview && GuardianCatalog().ElementAtOrDefault(guardianCatalogCursor) is > 0 and var ability && !guardianDraft!.Preview.Templates[guardianSubject].Any(row => row.Ability == ability);
    private bool GuardianCanCommitOperation => !guardianPreview && GuardianSelectedRow is { } row && (guardianPending == "swap" ? row.Ability != guardianPendingAbility : guardianPending == "replace" && !guardianDraft!.Preview.Templates[guardianSubject].Any(entry => entry.Ability == guardianPendingAbility));

    private void RegisterGuardianEditorPages()
    {
        Register(new()
        {
            Id = "mods-gf-placement-review",
            Title = "GF Abilities · Package order review",
            Help = "The chosen package position changes the editable input. No file has been written.",
            Entries = _ => [new("accept", SystemText("ui.registerguardianeditorpages.entry.label.use.reviewed.package.position", "Use reviewed package position"), SystemText("ui.registerguardianeditorpages.entry.help.recompute.the.editable.draft.at.this.position.review", "Recompute the editable draft at this position. Review it, then Save mod again."), Action: new("gf-editor-placement-accept")), new("cancel", SystemText("ui.registerguardianeditorpages.entry.label.cancel", "Cancel"), SystemText("ui.registerguardianeditorpages.entry.help.keep.the.current.authoring.input", "Keep the current authoring input."), Action: new("menu-back"))],
            Details = _ => guardianPlacementChanges
        });
        Register(new()
        {
            Id = "mods-gf-restore-review",
            Title = "GF Abilities · Restore inherited value",
            Help = "Review the contribution and dependent changes before restoring inherited fields.",
            Entries = _ => [new("accept", SystemText("ui.registerguardianeditorpages.entry.label.restore.reviewed.contribution", "Restore reviewed contribution"), SystemText("ui.registerguardianeditorpages.entry.help.author.the.reviewed.restore.as.a.new.draft", "Author the reviewed restore as a new draft command."), Action: new("gf-editor-restore-accept")), new("cancel", SystemText("ui.registerguardianeditorpages.entry.label.cancel", "Cancel"), SystemText("ui.registerguardianeditorpages.entry.help.keep.the.draft", "Keep the draft."), Action: new("menu-back"))],
            Details = _ => guardianDraft!.RestoreInheritedReview(guardianSubject, guardianRestoreAbility, guardianAbilityName!, SystemText)
        });
        Register(new()
        {
            Id = "mods-gf-undo-review",
            Title = "GF Abilities · Undo review",
            Help = "Undo also removes later dependent edits. Unrelated edits remain.",
            Entries = _ => [new("accept", SystemText("ui.registerguardianeditorpages.entry.label.undo.reviewed.group", "Undo reviewed group"), SystemText("ui.registerguardianeditorpages.entry.help.remove.the.listed.dependent.edits.together", "Remove the listed dependent edits together."), Action: new("gf-editor-undo-accept")), new("cancel", SystemText("ui.registerguardianeditorpages.entry.label.cancel", "Cancel"), SystemText("ui.registerguardianeditorpages.entry.help.keep.the.draft", "Keep the draft."), Action: new("menu-back"))],
            Details = _ => guardianDraft!.UndoReview(guardianSubject, guardianUndoAbility, guardianAbilityName!, SystemText)
        });
        Register(new()
        {
            Id = "mods-gf-packages",
            Title = "GF Abilities · Edit existing mod",
            Help = "Compatible GF packages can be edited while disabled.",
            Entries = _ => modService?.InstalledPackages.Where(package => package.Format == ModFormat.Memoria && File.Exists(Path.Combine(package.RootDirectory, GuardianForceAbilityMods.PayloadPath))).Select(package => new MenuEntry(package.Id, package.Name + " · " + package.Status, SystemText("ui.registerguardianeditorpages.entry.help.edit.this.package.s.gf.contribution.save.offers", "Edit this package's GF contribution. Save offers a copy when needed."), Action: new("gf-editor-target", package.Id), Enabled: package.Status != ModEffectiveStatus.IncompatibleEdition)).ToArray() ?? []
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
            Entries = _ => [new("back", SystemText("ui.registerguardianeditorpages.entry.label.back", "Back"), SystemText("ui.registerguardianeditorpages.entry.help.return.to.the.editor", "Return to the editor."), Action: new("menu-back"))],
            Details = _ => GuardianDetailLines()
        });
        Register(new()
        {
            Id = "mods-gf-controls",
            Title = "GF Abilities · Controls",
            Help = "Bindings follow the active input device and configured profile.",
            Entries = _ => [new("back", SystemText("ui.registerguardianeditorpages.entry.label.back", "Back"), SystemText("ui.registerguardianeditorpages.entry.help.return.to.the.editor", "Return to the editor."), Action: new("menu-back"))],
            Details = _ => [SystemText("ui.registerguardianeditorpages.details.up.down.previous.next.ability", "Up / Down: previous / next ability"), SystemText("ui.registerguardianeditorpages.details.left.right.or.page.up.down.full.page", "Left / Right or Page Up / Down: full page"), FormatSystemText("ui.registerguardianeditorpages.details.catalog.gf.focus.template", "{0} / {1}: catalog / GF focus", guardianBinding!(ControlAction.CameraLeft), guardianBinding!(ControlAction.CameraRight)), FormatSystemText("ui.registerguardianeditorpages.details.previous.next.gf.template", "{0} / {1}: previous / next GF", guardianBinding!(ControlAction.RotateLeft), guardianBinding!(ControlAction.RotateRight)), FormatSystemText("ui.registerguardianeditorpages.details.switch.column.template", "{0}: switch column", guardianBinding!(ControlAction.GuardianSwitchColumn)), FormatSystemText("ui.registerguardianeditorpages.details.toggle.start.learned.template", "{0}: toggle Start learned", guardianBinding!(ControlAction.Card)), FormatSystemText("ui.registerguardianeditorpages.details.actions.template", "{0}: Actions", guardianBinding!(ControlAction.Menu)), SystemText("ui.registerguardianeditorpages.details.tab.shift.tab.next.previous.control", "Tab / Shift+Tab: next / previous control"), SystemText("ui.registerguardianeditorpages.details.click.a.row.to.select.use.named.actions", "Click a row to select. Use named actions or checkboxes to edit.")]
        });
        Register(new()
        {
            Id = "mods-gf-select",
            Title = "GF Abilities · Guardian Force",
            Help = "Changing GF keeps draft edits to every GF.",
            Entries = _ => Enumerable.Range(0, 16).Select(id => new MenuEntry("gf-" + id, guardianEditorName!(id), SystemText("ui.registerguardianeditorpages.entry.help.select.this.gf", "Select this GF."), Action: new("gf-editor-command", "subject:" + id))).ToArray()
        });
    }

    private IReadOnlyList<MenuEntry> GuardianActionEntries()
    {
        List<MenuEntry> entries = [];
        void Add(string id, string label, string help, bool enabled = true) => entries.Add(new(id, label, help, Action: new("gf-editor-command", id), Enabled: enabled));
        if (guardianPreview)
        {
            Add("preview", SystemText("ui.guardianactionentries.entry.label.back.to.editor", "Back to editor"), SystemText("ui.guardianactionentries.entry.help.restore.the.exact.editing.view", "Restore the exact editing view."));
            Add("mode:0", SystemText("ui.guardianactionentries.entry.label.source", "Source"), SystemText("ui.guardianactionentries.entry.help.loaded.source.externally.modified.data.may.be.an", "Loaded source; externally modified data may be an opaque baseline."));
            Add("mode:1", guardianLocal ? SystemText("ui.guardianactionentries.entry.label.these.local.changes.only", "These local changes only") : SystemText("ui.guardianactionentries.entry.label.this.mod.only", "This mod only"), SystemText("ui.guardianactionentries.entry.help.selected.contribution.over.source.with.unresolved.dependencies.shown", "Selected contribution over source, with unresolved dependencies shown."));
            Add("mode:2", SystemText("ui.guardianactionentries.entry.label.current.setup", "Current setup"), SystemText("ui.guardianactionentries.entry.help.ordered.stack.with.the.selected.draft.substituted.once", "Ordered stack with the selected draft substituted once."));
            if (guardianPreviewMode == 2)
            {
                Add("include", FormatSystemText("ui.guardianactionentries.entry.label.x.template", "{0} Include selected layer", (guardianWithoutLayer ? "[ ]" : SystemText("ui.guardianactionentries.entry.label.x", "[x]"))), SystemText("ui.guardianactionentries.entry.help.simulate.inclusion.without.changing.activation", "Simulate inclusion without changing activation."));
            }
        }
        else
        {
            if (guardianPane == 0)
            {
                Add("tool:0", SystemText("ui.guardianactionentries.entry.label.add", "Add"), GuardianCatalogCanAdd ? SystemText("ui.guardianactionentries.entry.help.append.the.selected.ability", "Append the selected ability.") : SystemText("ui.guardianactionentries.entry.help.already.in.this.gf.or.no.matching.ability", "Already in this GF, or no matching ability."), GuardianCatalogCanAdd);
                Add("tool:1", SystemText("ui.guardianactionentries.entry.label.replace", "Replace"), SystemText("ui.guardianactionentries.entry.help.choose.a.gf.target.then.explicitly.confirm.replacement", "Choose a GF target, then explicitly confirm replacement."), GuardianCatalogCanAdd && GuardianSelectedRow is not null);
            }
            else
            {
                Add("tool:2", SystemText("ui.guardianactionentries.entry.label.swap", "Swap"), SystemText("ui.guardianactionentries.entry.help.choose.a.second.ability.to.exchange.positions", "Choose a second ability to exchange positions."), GuardianSelectedRow is not null);
                Add("toggle", SystemText("ui.guardianactionentries.entry.label.toggle.start.learned", "Toggle Start learned"), SystemText("ui.guardianactionentries.entry.help.template.only.current.progress.is.preserved", "Template only; current progress is preserved."), GuardianSelectedRow is not null);
                Add("tool:3", SystemText("ui.guardianactionentries.entry.label.undo", "Undo"), GuardianUndoText(), GuardianSelectedRow is { } row && guardianDraft!.RowDirty(guardianSubject, row.Ability));
                Add("tool:4", SystemText("ui.guardianactionentries.entry.label.restore.inherited.value", "Restore inherited value"), SystemText("ui.guardianactionentries.entry.help.remove.this.layer.s.selected.contribution.and.dependent", "Remove this layer's selected contribution and dependent operations."), GuardianSelectedRow is not null);
            }
            Add("preview", SystemText("ui.guardianactionentries.entry.label.preview", "Preview"), SystemText("ui.guardianactionentries.entry.help.read.only.comparison.workspace", "Read-only comparison workspace."));
            Add("tool:" + (guardianLocal ? 10 : 11), guardianLocal ? SystemText("ui.guardianactionentries.entry.label.apply.changes", "Apply changes") : SystemText("ui.guardianactionentries.entry.label.save.mod", "Save mod"), SystemText("ui.guardianactionentries.entry.help.keep.the.selected.contribution", "Keep the selected contribution."));
            if (!guardianLocal)
            {
                Add("tool:10", SystemText("ui.guardianactionentries.entry.label.apply.to.this.playthrough", "Apply to this playthrough"), SystemText("ui.guardianactionentries.entry.help.review.progression.and.junction.consequences.does.not.save", "Review progression and junction consequences; does not save the package."), readGuardianEditor!().Current is not null);
                Add("tool:12", SystemText("ui.guardianactionentries.entry.label.save.as.new.mod", "Save as new mod"), SystemText("ui.guardianactionentries.entry.help.write.an.independent.package", "Write an independent package."));
                Add("tool:14", SystemText("ui.guardianactionentries.entry.label.use.package", "Use package"), SystemText("ui.guardianactionentries.entry.help.review.local.ownership.and.reactivate.the.saved.package", "Review local ownership and reactivate the saved package contribution."), GuardianInstalledPackage is not null && reviewGuardianPackageOwnership is not null);
            }
            Add("search", SystemText("ui.guardianactionentries.entry.label.search.abilities", "Search abilities"), SystemText("ui.guardianactionentries.entry.help.type.using.a.keyboard.controller.users.can.use", "Type using a keyboard. Controller users can use category and paging."));
            Add("clear", SystemText("ui.guardianactionentries.entry.label.clear.search", "Clear search"), SystemText("ui.guardianactionentries.entry.help.show.all.names.in.the.selected.category", "Show all names in the selected category."), guardianSearch.Length > 0);
            Add("tool:9", FormatSystemText("ui.guardianactionentries.entry.label.filter.category.template", "Filter category \u00B7 {0}", GuardianCategoryLabel(guardianCategory)), SystemText("ui.guardianactionentries.entry.help.choose.the.next.category", "Choose the next category."));
            Add("mod-details", SystemText("ui.guardianactionentries.entry.label.mod.details", "Mod details"), SystemText("ui.guardianactionentries.entry.help.layer.ownership.identity.position.and.activation.are.separate", "Layer ownership, identity, position and activation are separate from file state."));
            Add("tool:13", SystemText("ui.guardianactionentries.entry.label.change.editing.target", "Change editing target"), SystemText("ui.guardianactionentries.entry.help.handle.unsaved.changes.before.selecting.another.owner", "Handle unsaved changes before selecting another owner."));
            Add("discard", SystemText("ui.guardianactionentries.entry.label.discard.changes", "Discard changes"), SystemText("ui.guardianactionentries.entry.help.review.before.discarding.the.selected.draft", "Review before discarding the selected draft."), guardianDraft?.Dirty == true);
        }
        Add("select", SystemText("ui.guardianactionentries.entry.label.select.gf", "Select GF"), SystemText("ui.guardianactionentries.entry.help.choose.any.of.the.sixteen.learnable.gfs", "Choose any of the sixteen learnable GFs."));
        Add("column", SystemText("ui.guardianactionentries.entry.label.switch.column", "Switch column"), SystemText("ui.guardianactionentries.entry.help.move.to.the.other.gf.column.at.the", "Move to the other GF column at the same visible row."));
        Add("details", SystemText("ui.guardianactionentries.entry.label.details", "Details"), SystemText("ui.guardianactionentries.entry.help.full.description.provenance.and.diagnostic.information", "Full description, provenance and diagnostic information."));
        Add("controls", SystemText("ui.guardianactionentries.entry.label.controls", "Controls"), SystemText("ui.guardianactionentries.entry.help.show.bindings.and.interaction.reference", "Show bindings and interaction reference."));
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
        changes.AddRange(proposed.Outcomes.Where(outcome => outcome.Status is not ("Applied" or "AlreadySatisfied")).Select(outcome => outcome.FormatMessage(SystemText)));
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
            return [GuardianTargetTitle(), guardianPackage?.PackageDirectory ?? SystemText("ui.guardiandetaillines.details.no.package.destination.selected", "No package destination selected."), FormatSystemText("ui.guardiandetaillines.details.document.template", "Document: {0}", guardianDraft!.ExportDocument.DocumentId), guardianPackage is null && !guardianLocal ? SystemText("ui.guardiandetaillines.details.provisional.position.after.native.gf.layers.before.playthrough", "Provisional position: after native GF layers, before playthrough local changes. Save determines the final package order.") : SystemText("ui.guardiandetaillines.details.the.selected.layer.is.composed.over.its.preceding", "The selected layer is composed over its preceding input."), SystemText("ui.guardiandetaillines.details.saving.runtime.activation.and.normal.game.saving.have", "Saving, runtime activation and normal game saving have separate states.")];
        }
        int ability = guardianPane == 0 ? GuardianCatalog().ElementAtOrDefault(guardianCatalogCursor) : GuardianSelectedRow?.Ability ?? 0;
        GuardianForceAbilityComposition composition = GuardianEditorComposition();
        GuardianForceAbilityTemplate? row = composition.Templates[guardianSubject].FirstOrDefault(entry => entry.Ability == ability);
        NativeGuardianForceAbility? current = readGuardianEditor!().Current?.GuardianForceAbilities(guardianSubject).FirstOrDefault(entry => entry.Ability == ability);
        List<string> lines = [guardianAbilityName!(ability), guardianAbilityDescription!(ability), FormatSystemText("ui.guardiandetaillines.lines.catalog.cost.template", "Catalog cost: {0} AP", readGuardianEditor!().Source.MenuDefinitions.AbilityPoints[ability]), SystemText("ui.guardiandetaillines.lines.start.learned.affects.the.template.current.progress.is", "Start learned affects the template; current progress is preserved unless explicitly applied."), current is null ? SystemText("ui.guardiandetaillines.lines.no.current.progress.not.acquired", "No current progress / not acquired.") : current.Learned ? SystemText("ui.guardiandetaillines.lines.current.save.learned", "Current save: learned") : FormatSystemText("ui.guardiandetaillines.lines.current.save.ap", "Current save: {0} / {1} AP", current.Progress, current.RequiredPoints)];
        if (row is not null)
        {
            lines.Add(row.PrerequisiteAbility is int prerequisite ? FormatSystemText("ui.guardiandetaillines.details.requires.template", "Requires {0}", guardianAbilityName!(prerequisite)) : FormatSystemText("ui.guardiandetaillines.details.required.gf.level.template", "Required GF level: {0}", row.RequiredLevel));
            lines.Add(row.SupportedCondition ? SystemText("ui.guardiandetaillines.details.availability.condition.supported", "Availability condition supported.") : SystemText("ui.guardiandetaillines.details.unsupported.source.condition.see.diagnostics", "Unsupported source condition; see diagnostics."));
        }
        lines.AddRange(composition.Origins.Where(origin => origin.GuardianForce == guardianSubject && origin.Ability == ability).Select(origin => origin.PackageId + " · " + origin.Aspect));
        lines.AddRange(composition.Outcomes.Where(outcome => outcome.GuardianForce == guardianSubject && outcome.Status is not ("Applied" or "AlreadySatisfied")).Select(outcome => outcome.FormatMessage(SystemText)));
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
            return readGuardianEditor!().Current?.GuardianForceOpaqueBaseline == true ? SystemText("ui.guardianpreviewstatus.details.opaque.external.baseline.prior.attribution.is.baked.into", "Opaque external baseline; prior attribution is baked into source.") : SystemText("ui.guardianpreviewstatus.details.loaded.source.before.managed.authored.layers", "Loaded source before managed authored layers.");
        }
        if (guardianPreviewMode == 2 && GuardianInstalledPackage is null && !guardianLocal)
        {
            return SystemText("ui.guardianpreviewstatus.details.simulated.candidate.provisional.position.after.native.layers.before", "Simulated candidate · Provisional position: after native layers, before local changes.");
        }
        if (guardianPreviewMode == 2 && !guardianWithoutLayer && guardianPackage is not null && !readGuardianEditor!().Layers.Any(layer => layer.Document.DocumentId == guardianDraft!.ExportDocument.DocumentId))
        {
            return SystemText("ui.guardianpreviewstatus.details.preview.as.enabled.package.activation.is.unchanged", "Preview as enabled · Package activation is unchanged.");
        }
        return SystemText("ui.guardianpreviewstatus.details.read.only.comparison.current.progress.is.preserved", "Read-only comparison · Current progress is preserved.");
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
            SetSystemMessage(() => guardianPreview ? SystemText("ui.guardiantogglerow.message.preview.is.read.only", "Preview is read-only.") : SystemText("ui.guardiantogglerow.message.finish.or.cancel.the.pending.operation.first", "Finish or cancel the pending operation first."));
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
            SetSystemMessage(() => guardianPending == "swap" ? SystemText("ui.guardianconfirmoperation.message.choose.a.different.ability", "Choose a different ability.") : SystemText("ui.guardianconfirmoperation.message.the.incoming.ability.must.not.already.be.in", "The incoming ability must not already be in this GF."));
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

    private string GuardianOperationText() => guardianPending is null ? "" : guardianPending == "swap" ? FormatSystemText("ui.guardianoperationtext.details.swap.template", "Swap {0} with {1}", guardianAbilityName!(guardianPendingAbility), (GuardianSelectedRow is { } row ? guardianAbilityName!(row.Ability) : SystemText("ui.guardianoperationtext.details.a.target", "a target"))) : FormatSystemText("ui.guardianoperationtext.details.replace.template", "Replace {0} with {1}", (GuardianSelectedRow is { } target ? guardianAbilityName!(target.Ability) : SystemText("ui.guardianoperationtext.details.a.target", "a target")), guardianAbilityName!(guardianPendingAbility));
    private string GuardianUndoText() => GuardianSelectedRow is { } row ? guardianDraft!.UndoDescription(guardianSubject, row.Ability, guardianAbilityName!, SystemText) : SystemText("ui.guardianundotext.details.no.selected.entry", "No selected entry");
    private string GuardianConfirmHint()
    {
        if (guardianFocus == "catalog")
        {
            return SystemText("guardian.confirm.add-replace", "Add / Replace");
        }
        if (guardianFocus != "rows")
        {
            return GuardianControlLabel(guardianFocus);
        }
        if (guardianPreview)
        {
            return SystemText("guardian.confirm.details", "Details");
        }
        if (guardianPending is not null)
        {
            return guardianPending == "swap" ? SystemText("guardian.confirm.swap", "Swap") : SystemText("guardian.confirm.replace", "Replace");
        }
        return GuardianSelectedRow is { } row && guardianDraft!.RowDirty(guardianSubject, row.Ability) ? GuardianUndoText() : SystemText("guardian.confirm.actions", "Actions");
    }

    private string GuardianControlLabel(string control) => control switch
    {
        "search" => SystemText("guardian.control.search", "Search"),
        "clear-search" => SystemText("guardian.control.clear-search", "Clear search"),
        "category" => SystemText("guardian.control.category", "Category"),
        "select" => SystemText("guardian.control.select", "Select GF"),
        "column" => SystemText("guardian.control.column", "Switch column"),
        "details" => SystemText("guardian.control.details", "Details"),
        _ => SystemText("guardian.control.activate", "Activate")
    };


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
