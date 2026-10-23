using System.Collections.Immutable;
using Memoria.FF8.Mods;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

internal sealed record GuardianForceEditorContext(NativeCharacterVitals Source, NativeCharacterVitals? Current, ImmutableArray<GuardianForceAbilityPatchLayer> Layers, ImmutableArray<GuardianForceAbilityPatchDocument> LocalDocuments, ImmutableArray<string> ExcludedDocuments);
internal sealed record GuardianForceEditorReview(GuardianForceAbilityApplyPreview Preview, GuardianForceAbilityPatchDocument Document, bool ResetStartingStates, bool PackageOwnership = false, string? Intent = null);

public sealed partial class MenuController
{
    private Func<GuardianForceEditorContext>? readGuardianEditor;
    private Func<int, string>? guardianEditorName;
    private Func<int, string>? guardianAbilityName;
    private Func<int, string>? guardianAbilityDescription;
    private Func<ControlAction, string>? guardianBinding;
    private Func<GuardianForceAbilityPatchDocument, bool, System.Collections.Immutable.ImmutableArray<GuardianForceAbilityReset>, GuardianForceEditorReview>? reviewGuardianEditor;
    private Func<GuardianForceEditorReview, string>? applyGuardianEditor;
    private Func<GuardianForceAbilityPatchDocument, GuardianForceAbilityPackageDocument?, bool, GuardianForceAbilityPackageDocument?>? saveGuardianEditor;
    private GuardianForceAbilityDraft? guardianDraft;
    private GuardianForceAbilityPackageDocument? guardianPackage;
    private GuardianForceEditorReview? guardianReview;
    private bool guardianLocal;
    private int guardianSubject;
    private int guardianPane;
    private int guardianCatalogCursor;
    private int guardianRowCursor;
    private int guardianCatalogScroll;
    private int guardianRowScroll;
    private int guardianPreviewMode = 2;
    private string guardianSearch = "";
    private int guardianCategory;
    private string? guardianPending;
    private int guardianPendingAbility;
        private bool guardianCapacityWarning;
    private bool guardianResetDefaults;
    private bool guardianWithoutLayer;
    private Action? guardianAfterDiscard;
    private static readonly string[] GuardianCategories = ["All", "Junction", "Command", "Character", "Party", "GF", "Menu"];
    internal bool GuardianEditorEditionSupported { get; set; } = true;
    private Func<GuardianForceAbilityPatchDocument, GuardianForceEditorReview>? reviewGuardianPackageOwnership;
    private Func<string, GuardianForceEditorReview>? reviewRemoveGuardianLayer;
    private Func<GuardianForceEditorReview>? reviewGuardianRecovery;
    internal void ConnectGuardianRecovery(Func<GuardianForceEditorReview> review) => reviewGuardianRecovery = review;
    internal void ConnectGuardianPackageOwnership(Func<GuardianForceAbilityPatchDocument, GuardianForceEditorReview> review, Func<string, GuardianForceEditorReview> remove)
    {
        reviewGuardianPackageOwnership = review;
        reviewRemoveGuardianLayer = remove;
    }

    internal bool IsGuardianAbilityEditor => ScreenId == "gf-ability-editor";

    internal void ConnectGuardianForceEditor(Func<GuardianForceEditorContext> read, Func<int, string> gfName, Func<int, string> abilityName, Func<int, string> description, Func<ControlAction, string> binding, Func<GuardianForceAbilityPatchDocument, bool, System.Collections.Immutable.ImmutableArray<GuardianForceAbilityReset>, GuardianForceEditorReview> review, Func<GuardianForceEditorReview, string> apply, Func<GuardianForceAbilityPatchDocument, GuardianForceAbilityPackageDocument?, bool, GuardianForceAbilityPackageDocument?> save)
    {
        readGuardianEditor = read;
        guardianEditorName = gfName;
        guardianAbilityName = abilityName;
        guardianAbilityDescription = description;
        guardianBinding = binding;
        reviewGuardianEditor = review;
        applyGuardianEditor = apply;
        saveGuardianEditor = save;
        RegisterGuardianEditorPages();
        Register(new()
        {
            Id = "mods-create",
            Title = "Mods · Create Mods",
            Help = "Create a native gameplay mod or edit a compatible GF package.",
            Entries = _ => [new("installed", "Installed Mods", "Installed packages and enablement.", Destination: "mods"), new("gf", "GF Abilities", "Edit existing abilities for all sixteen learnable GFs. Classic only.", Destination: "gf-ability-targets", Enabled: GuardianEditorEditionSupported)],
            Details = _ => ["GF ability templates and earned progress have separate owners.", "Classic native Memoria gameplay packages.", "Summon-only entities have no learnable ability table."],
            BuildContent = (c, panel) => panel.Children.Add(c.BuildCreateModsPage())
        });
        Register(new()
        {
            Id = "gf-ability-targets",
            Title = "Create Mods · GF Abilities · Target",
            Help = "Choose exactly one editing owner. Disabled packages can be edited without activation.",
            Entries = _ => GuardianTargetEntries(),
            Details = _ => ["Create new mod opens an independent draft immediately. No file is written until Save mod.", "Edit existing mod includes compatible disabled packages.", readGuardianEditor!().Current is null ? "Load a game to edit this playthrough." : "Edit this playthrough changes its save-local contribution."]
        });
        Register(new()
        {
            Id = "gf-ability-editor",
            Title = "Create Mods · GF Abilities",
            Help = "Edit a single contribution. Previewing never changes your playthrough.",
            Entries = _ => [],
            BuildContent = (c, panel) => panel.Children.Add(new GuardianAbilityEditorElement(c) { Focusable = true, Id = "gf-editor-content" })
        });
        Register(new()
        {
            Id = "gf-ability-dirty",
            Title = "GF Abilities · Unsaved changes",
            Help = "Save or apply the selected draft, discard it, or cancel navigation.",
            Entries = _ => [new("save", guardianLocal ? "Apply to playthrough" : "Save mod", "Keep these changes.", Action: new("gf-editor-keep")), new("discard", "Discard", "Discard only this draft.", Action: new("gf-editor-discard")), new("cancel", "Cancel", "Continue editing.", Action: new("menu-back"))]
        });
        Register(new()
        {
            Id = "gf-ability-review",
            Title = "GF Abilities · Apply review",
            Help = "Review current progress and junction consequences before applying once.",
            Entries = _ => [new("reset", $"[{(guardianResetDefaults ? "x" : " ")}] Also apply edited starting states", "Only explicitly edited starting states; learned or unlearned with 0 AP. These writes occur once.", Action: new("gf-editor-review-reset")), new("apply", "Apply reviewed changes", "The current playthrough changes and needs a normal game save.", Action: new("gf-editor-apply")), new("cancel", "Cancel", "Keep the draft and live playthrough.", Action: new("menu-back"))],
            Details = _ => GuardianReviewDetails()
        });

    }

    internal void ConnectGuardianModReview(Func<IReadOnlyList<string>?> read, Func<string> accept)
    {
        Register(new()
        {
            Id = "gf-mod-review",
            Title = "Mods · GF changes review",
            Help = "The previous runtime generation remains active until this review is accepted.",
            Entries = _ => [new("accept", "Accept reviewed changes", "Revalidate and publish the pending mod generation.", Action: new("gf-editor-publish"), Enabled: read() is not null), new("back", "Back", "Keep the previous generation active.", Action: new("menu-back"))],
            Details = _ => read() ?? ["No pending GF progression changes."]
        });
        acceptGuardianModReview = accept;
    }

    private Func<string>? acceptGuardianModReview;

    private UiElement BuildCreateModsPage()
    {
        UiGrid page = Grid([1, 1.2f, 5], [1], 8);
        UiGrid tabs = Grid([1], [1, 1], 8);
        Place(tabs, new UiButton { Id = "installed", Text = "Installed Mods", FontSize = 22 * TextScale, Click = () => Activate(0) }, 0, 0);
        Place(tabs, new UiButton { Id = "create-current", Text = "Create Mods", FontSize = 22 * TextScale, Click = () => { } }, 0, 1);
        Place(page, tabs, 0);
        Place(page, new UiButton { Id = "gf", Text = "GF Abilities", FontSize = 24 * TextScale, Enabled = GuardianEditorEditionSupported, Click = () => Activate(1) }, 1);
        Place(page, Details(), 2);
        return page;
    }

    private IReadOnlyList<string> GuardianReviewDetails()
    {
        if (guardianReview is null)
        {
            return [];
        }
        List<string> details = [guardianReview.Intent ?? (guardianReview.PackageOwnership ? "Review contribution ownership/removal." : "Only the selected contribution is applied. Unrelated mods are not copied.")];
        foreach (GuardianForceAbilityPatchOperation operation in guardianReview.Document.Operations)
        {
            string name(int ability) => guardianAbilityName!(ability);
            string change = operation.Kind switch
            {
                GuardianForceAbilityOperationKind.EnsureAbility => "Ensure " + name(operation.Ability) + "; initially " + (operation.InitialLearned ? "Learned" : "Unlearned, 0 AP"),
                GuardianForceAbilityOperationKind.ReplaceAbility => "Replace " + name(operation.Ability) + " → " + name(operation.Replacement!.Value),
                GuardianForceAbilityOperationKind.SetInitialState => name(operation.Ability) + ": initially " + (operation.InitialLearned ? "Learned" : "Unlearned, 0 AP"),
                _ => "Display order: " + string.Join(" → ", operation.Order.Select(name))
            };
            details.Add(guardianEditorName!(operation.GuardianForce) + ": " + change);
        }
        details.AddRange(guardianReview.Preview.Consequences);
        if (guardianReview.Preview.Consequences.Length == 0)
        {
            details.Add("Current learned/AP state and junctions are preserved.");
        }
        details.Add("Focus this review list with the Tab binding; page through every consequence before applying.");
        return details;
    }

    private IReadOnlyList<MenuEntry> GuardianTargetEntries()
    {
        List<MenuEntry> entries = [new("new", "Create new mod", "Open an independent draft. No file is written yet.", Action: new("gf-editor-target", "new")), new("existing", "Edit existing mod", "Choose a compatible GF package, including disabled packages.", Destination: "mods-gf-packages"), new("local", "Edit this playthrough", readGuardianEditor?.Invoke().Current is null ? "Load a game to edit this playthrough." : "Continue the save-local contribution.", Action: new("gf-editor-target", "local"), Enabled: readGuardianEditor?.Invoke().Current is not null)];
        if (readGuardianEditor?.Invoke() is { } context)
        {
            if (context.Current?.GuardianForceOpaqueBaseline == true && context.Current.GuardianForceRecoveryProfile is not null)
            {
                entries.Add(new("recover", "Review recovered native GF layers", "Old changes are baked into the imported baseline. Reconcile before reactivating their native documents.", Action: new("gf-editor-recover")));
            }
            foreach (GuardianForceAbilityPatchDocument document in context.LocalDocuments)
            {
                entries.Add(new("local:" + document.DocumentId, "Playthrough contribution · " + document.DocumentId, "Edit only this separately owned local contribution.", Action: new("gf-editor-target", "local:" + document.DocumentId)));
            }
            foreach (GuardianForceAbilityPatchLayer layer in context.Layers.Where(layer => modService is null || !modService.Catalog.Files.Any(file => file.PackageId.Equals(layer.PackageId, StringComparison.OrdinalIgnoreCase) && file.RelativePath.Equals(GuardianForceAbilityMods.PayloadPath, StringComparison.OrdinalIgnoreCase))))
            {
                entries.Add(new("missing:" + layer.Document.DocumentId, "Remove retained missing GF payload · " + layer.PackageId, "Review removal of this saved contribution. Earned progress remains dormant.", Action: new("gf-editor-remove-missing", layer.Document.DocumentId)));
            }
        }
        return entries;
    }

    private void OpenGuardianDraft(string target)
    {
        GuardianForceEditorContext context = readGuardianEditor!();
        guardianPackage = null;
        guardianLocal = target == "local" || target.StartsWith("local:", StringComparison.Ordinal);
        GuardianForceAbilityPatchDocument document;
        if (target is "new" or "local" || target.StartsWith("local:", StringComparison.Ordinal))
        {
            document = target.StartsWith("local:", StringComparison.Ordinal) ? context.LocalDocuments.Single(document => document.DocumentId == target[6..]) : guardianLocal && context.LocalDocuments.Length > 0 ? context.LocalDocuments[^1] : new()
            {
                DocumentId = Guid.NewGuid().ToString("D")
            };
        }
        else
        {
            ModPackage package = modService!.Catalog.Packages.Single(package => package.Id == target);
            guardianPackage = GuardianForceAbilityPackageStore.Open(package);
            document = guardianPackage.Document;
        }
        List<GuardianForceAbilityPatchLayer> preceding = [];
        if (guardianLocal)
        {
            preceding.AddRange(context.Layers.Where(layer => !context.ExcludedDocuments.Contains(layer.Document.DocumentId)));
            preceding.AddRange(context.LocalDocuments.Where(doc => doc.DocumentId != document.DocumentId).Select(doc => new GuardianForceAbilityPatchLayer("Playthrough", doc)));
        }
        else if (guardianPackage is not null)
        {
            string selectedPackage = modService!.Catalog.Packages.Single(package => Path.GetFullPath(package.RootDirectory).Equals(guardianPackage.PackageDirectory, StringComparison.OrdinalIgnoreCase)).Id;
            preceding.AddRange(context.Layers.TakeWhile(layer => !layer.PackageId.Equals(selectedPackage, StringComparison.OrdinalIgnoreCase)).Where(layer => CompareGuardianPackageOrder(layer.PackageId, selectedPackage) < 0));
        }
        else
        {
            preceding.AddRange(context.Layers.Where(layer => !context.ExcludedDocuments.Contains(layer.Document.DocumentId)));
        }
        ImmutableArray<ImmutableArray<GuardianForceAbilityTemplate>> input = GuardianForceAbilityComposer.Compose(context.Source.ResolveGuardianForceTemplates(), preceding, context.Source.MenuDefinitions.AbilityPoints).Templates;
        guardianDraft = new(input, context.Source.MenuDefinitions.AbilityPoints, document);
        guardianPending = null;
        guardianReview = null;
        guardianPreview = false;
        guardianWithoutLayer = false;
        guardianSubject = 0;
        guardianRowCursor = 0;
        guardianCatalogCursor = 0;
        guardianSearch = "";
        guardianCategory = 0;
        GuardianFocus("catalog");
        Push("gf-ability-editor");
    }

    private GuardianForceAbilityComposition GuardianEditorComposition()
    {
        if (!guardianPreview)
        {
            return guardianDraft!.Preview;
        }
        GuardianForceEditorContext context = readGuardianEditor!();
        ImmutableArray<ImmutableArray<GuardianForceAbilityTemplate>> source = context.Source.ResolveGuardianForceTemplates();
        if (guardianPreviewMode == 0)
        {
            return GuardianForceAbilityComposer.Compose(source, [], context.Source.MenuDefinitions.AbilityPoints);
        }
        GuardianForceAbilityPatchDocument draft = guardianDraft!.ExportDocument;
        if (guardianPreviewMode == 1)
        {
            return GuardianForceAbilityComposer.Compose(source, [new("This mod only", draft)], context.Source.MenuDefinitions.AbilityPoints);
        }
        List<GuardianForceAbilityPatchLayer> layers = [];
        bool substituted = false;
        foreach (GuardianForceAbilityPatchLayer layer in context.Layers)
        {
            if (context.ExcludedDocuments.Contains(layer.Document.DocumentId))
            {
                continue;
            }
            if (layer.Document.DocumentId == draft.DocumentId)
            {
                substituted = true;
                if (!guardianWithoutLayer)
                {
                    layers.Add(layer with
                    {
                        Document = draft
                    });
                }
            }
            else
            {
                layers.Add(layer);
            }
        }
        if (!guardianLocal && !substituted && !guardianWithoutLayer)
        {
            ModPackage? selectedPackage = GuardianInstalledPackage;
            if (selectedPackage is null)
            {
                layers.Add(new("Selected draft", draft));
            }
            else
            {
                int insertion = layers.FindIndex(layer => CompareGuardianPackageOrder(layer.PackageId, selectedPackage.Id) > 0);
                layers.Insert(insertion < 0 ? layers.Count : insertion, new(selectedPackage.Id, draft));
            }
        }
        foreach (GuardianForceAbilityPatchDocument document in context.LocalDocuments)
        {
            if (document.DocumentId == draft.DocumentId)
            {
                substituted = true;
                if (!guardianWithoutLayer)
                {
                    layers.Add(new("Playthrough", draft));
                }
            }
            else
            {
                layers.Add(new("Playthrough", document));
            }
        }
        if (guardianLocal && !substituted && !guardianWithoutLayer)
        {
            layers.Add(new("Playthrough", draft));
        }
        return GuardianForceAbilityComposer.Compose(source, layers, context.Source.MenuDefinitions.AbilityPoints);
    }

    private ModPackage? GuardianInstalledPackage => guardianPackage is null ? null : modService?.Catalog.Packages.SingleOrDefault(package => Path.GetFullPath(package.RootDirectory).Equals(guardianPackage.PackageDirectory, StringComparison.OrdinalIgnoreCase));

    private int CompareGuardianPackageOrder(string left, string right)
    {
        IReadOnlyList<ModPackage>? packages = modService?.Catalog.Packages;
        int leftIndex = packages?.ToList().FindIndex(package => package.Id.Equals(left, StringComparison.OrdinalIgnoreCase)) ?? -1;
        int rightIndex = packages?.ToList().FindIndex(package => package.Id.Equals(right, StringComparison.OrdinalIgnoreCase)) ?? -1;
        return leftIndex >= 0 && rightIndex >= 0 ? leftIndex.CompareTo(rightIndex) : StringComparer.OrdinalIgnoreCase.Compare(left, right);
    }

    private int[] GuardianCatalog() => Enumerable.Range(1, readGuardianEditor!().Source.MenuDefinitions.AbilityPoints.Length - 1).Where(id => !string.IsNullOrWhiteSpace(guardianAbilityName!(id)) && guardianAbilityName(id).Contains(guardianSearch, StringComparison.CurrentCultureIgnoreCase) && (guardianCategory == 0 || GuardianCategory(id) == guardianCategory)).ToArray();
    private static int GuardianCategory(int id) => id < 20 ? 1 : id < 39 ? 2 : id < 78 ? 3 : id < 83 ? 4 : id < 92 ? 5 : 6;
    private GuardianForceAbilityTemplate? GuardianSelectedRow => guardianDraft is null ? null : GuardianEditorComposition().Templates[guardianSubject].ElementAtOrDefault(guardianRowCursor);

    private void GuardGuardianDraft(Action continuation)
    {
        if (guardianDraft?.Dirty == true)
        {
            guardianAfterDiscard = continuation;
            Push("gf-ability-dirty");
        }
        else
        {
            continuation();
        }
    }

    private void GuardianEdit(Action edit)
    {
        try
        {
            if (guardianPreview)
            {
                throw new InvalidOperationException("This comparison is read-only. Select This mod only or Current setup with the selected layer before editing.");
            }
            edit();
            var template = guardianDraft!.Preview.Templates[guardianSubject];
            var legacy = readGuardianEditor!().Source.MenuDefinitions.Learning[guardianSubject];
            bool exceedsCapacity = template.Length > 22 || template.Count(entry => !entry.InitialLearned || legacy.Any(rule => rule.Ability == entry.Ability)) > 21;
            if (!guardianCapacityWarning && exceedsCapacity)
            {
                guardianCapacityWarning = true;
                Message = "This GF exceeds original FFVIII ability capacity. Memoria saves preserve it; original-format export can lose abilities and progression.";
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or InvalidDataException)
        {
            Message = exception.Message;
        }
        guardianRowCursor = Math.Clamp(guardianRowCursor, 0, Math.Max(0, GuardianEditorComposition().Templates[guardianSubject].Length - 1));
        _view = null;
    }

    private void GuardianTool(int index)
    {
        try
        {
            GuardianToolCore(index);
        }
        catch (Exception exception) when (exception is InvalidOperationException or InvalidDataException or IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            Message = exception.Message;
        }
        _view = null;
    }

    private void GuardianToolCore(int index)
    {
        if (guardianDraft is null)
        {
            return;
        }
        if (guardianPreview && index is not (5 or 7 or 15))
        {
            Message = "Preview is read-only.";
            return;
        }
        GuardianForceAbilityTemplate? row = GuardianSelectedRow;
        switch (index)
        {
            case 0:
                if (GuardianCatalogCanAdd)
                {
                    int incoming = GuardianCatalog()[guardianCatalogCursor];
                    GuardianEdit(() => guardianDraft.Append(guardianSubject, incoming));
                    guardianRowCursor = guardianDraft.Preview.Templates[guardianSubject].ToList().FindIndex(entry => entry.Ability == incoming);
                    guardianPending = null;
                    GuardianFocus("rows");
                }
                else
                {
                    Message = "Already in this GF, or no catalog ability selected.";
                }
                break;
            case 1:
                if (GuardianCatalogCanAdd && row is not null)
                {
                    guardianPendingAbility = GuardianCatalog()[guardianCatalogCursor];
                    guardianPending = "replace";
                    guardianOperationOrigin = "catalog";
                    GuardianFocus("rows");
                }
                else
                {
                    Message = "Select an incoming ability not already in this GF and a target row.";
                }
                break;
            case 2:
                if (row is not null)
                {
                    guardianPendingAbility = row.Ability;
                    guardianPending = "swap";
                    guardianOperationOrigin = "rows";
                    GuardianFocus("rows");
                }
                break;
            case 3:
                GuardianUndo();
                break;
            case 4:
                if (row is not null)
                {
                    guardianRestoreAbility = row.Ability;
                    Push("mods-gf-restore-review");
                }
                break;
            case 5:
                GuardianTogglePreview();
                break;
            case 7:
                guardianWithoutLayer = !guardianWithoutLayer;
                break;
            case 8:
                GuardianStartSearch();
                break;
            case 9:
                int? selectedCatalog = GuardianCatalog().ElementAtOrDefault(guardianCatalogCursor);
                guardianCategory = (guardianCategory + 1) % GuardianCategories.Length;
                GuardianRefilter(selectedCatalog);
                break;
            case 10:
                guardianReview = reviewGuardianEditor!(guardianDraft.ExportDocument, guardianResetDefaults, guardianDraft.EditedStartingStates);
                Push("gf-ability-review");
                break;
            case 11:
            case 12:
                GuardianForceAbilityPackageDocument? saved = saveGuardianEditor!(guardianDraft.ExportDocument, guardianPackage, index == 12);
                if (saved is not null)
                {
                    guardianPackage = saved;
                    guardianLocal = false;
                    guardianDraft.Accept(saved.Document);
                    Message = "GF document saved. Runtime activation status is shown in Installed Mods.";
                    ContinueGuardianNavigation();
                }
                break;
            case 13:
                GuardGuardianDraft(() => Push("gf-ability-targets"));
                break;
            case 15:
                guardianPending = null;
                Push("mods-gf-select");
                break;
            case 14:
                if (GuardianInstalledPackage is null || reviewGuardianPackageOwnership is null)
                {
                    Message = "Install this package in a discovered mod directory before using it.";
                    break;
                }
                guardianReview = reviewGuardianPackageOwnership(guardianDraft.ExportDocument);
                Push("gf-ability-review");
                break;
        }
        _view = null;
    }

    private void ContinueGuardianNavigation()
    {
        Action? continuation = guardianAfterDiscard;
        guardianAfterDiscard = null;
        continuation?.Invoke();
    }

    private bool ExecuteGuardianEditorAction(MenuAction action)
    {
        if (!action.Kind.StartsWith("gf-editor-", StringComparison.Ordinal))
        {
            return false;
        }
        try
        {
            switch (action.Kind)
            {
                case "gf-editor-placement-accept":
                    Back();
                    guardianDraft!.RebaseInput(guardianPlacementInput);
                    guardianPending = null;
                    Message = "Package position reviewed. Inspect the recomputed draft, then Save mod to the reviewed destination.";
                    break;
                case "gf-editor-restore-accept":
                    Back();
                    GuardianEdit(() => guardianDraft!.RestoreInherited(guardianSubject, guardianRestoreAbility));
                    break;
                case "gf-editor-undo-accept":
                    Back();
                    GuardianEdit(() => guardianDraft!.Undo(guardianSubject, guardianUndoAbility));
                    break;
                case "gf-editor-command":
                    Back();
                    GuardianExecuteCommand(action.SubjectId);
                    break;
                case "gf-editor-publish":
                    Message = acceptGuardianModReview!();
                    break;
                case "gf-editor-recover":
                    guardianReview = reviewGuardianRecovery!();
                    Push("gf-ability-review");
                    break;
                case "gf-editor-remove-missing":
                    guardianReview = reviewRemoveGuardianLayer!(action.SubjectId);
                    Push("gf-ability-review");
                    break;
                case "gf-editor-target":
                    GuardGuardianDraft(() => OpenGuardianDraft(action.SubjectId));
                    break;
                case "gf-editor-discard":
                    guardianDraft = null;
                    Back();
                    Action? continuation = guardianAfterDiscard;
                    guardianAfterDiscard = null;
                    continuation?.Invoke();
                    break;
                case "gf-editor-keep":
                    Back();
                    GuardianTool(guardianLocal ? 10 : 11);
                    break;
                case "gf-editor-review-reset":
                    if (guardianReview?.PackageOwnership == true)
                    {
                        break;
                    }
                    guardianResetDefaults = !guardianResetDefaults;
                    guardianReview = reviewGuardianEditor!(guardianDraft!.ExportDocument, guardianResetDefaults, guardianDraft.EditedStartingStates);
                    break;
                case "gf-editor-apply":
                    string applied = applyGuardianEditor!(guardianReview!);
                    if (guardianLocal && guardianDraft?.ExportDocument.DocumentId == guardianReview!.Document.DocumentId)
                    {
                        guardianDraft.Accept(guardianReview.Document);
                    }
                    Back();
                    Message = applied;
                    ContinueGuardianNavigation();
                    break;

            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or InvalidDataException or IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            Message = exception.Message;
        }
        _view = null;
        return true;
    }
}
