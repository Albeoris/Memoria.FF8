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
    private int guardianToolbarCursor;
    private int guardianCatalogScroll;
    private int guardianRowScroll;
    private int guardianPreviewMode = 2;
    private bool guardianPreviewEnabled;
    private string guardianSearch = "";
    private int guardianCategory;
    private string? guardianPending;
    private int guardianPendingAbility;
    private int guardianSwapSecond;
    private bool guardianCapacityWarning;
    private bool guardianResetDefaults;
    private bool guardianWithoutLayer;
    private Action? guardianAfterDiscard;
    private static readonly string[] GuardianCategories = ["All", "Junction", "Command", "Character", "Party", "GF", "Menu"];
    private static readonly string[] GuardianTools = ["Append", "Replace", "Swap positions", "Undo row", "Restore inherited", "Preview", "Preview as enabled", "Without this layer", "Search", "Category", "Apply once", "Save mod", "Save As", "Target", "Use package"];
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
            Entries = _ => GuardianTargetEntries()
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
        Register(new()
        {
            Id = "gf-ability-search",
            Title = "GF Abilities · Search",
            Help = "Type an ability name or choose letters. Back retains the search.",
            Columns = 8,
            Entries = _ => [.. "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 -%".Select((letter, i) => new MenuEntry("letter-" + i, letter.ToString(), "Add a search character.", Action: new("gf-editor-letter", Value: letter.ToString()))), new("delete", "Delete", "Delete character.", Action: new("gf-editor-delete")), new("clear", "Clear", "Show all abilities.", Action: new("gf-editor-clear")), new("done", "Done", "Return to editor.", Action: new("menu-back"))],
            Details = _ => ["Search: " + guardianSearch]
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
        List<MenuEntry> entries = [new("new", "New mod draft", "Start an independent contribution.", Action: new("gf-editor-target", "new")), new("local", "Current playthrough · local changes", "Continue the save-local contribution.", Action: new("gf-editor-target", "local"), Enabled: readGuardianEditor?.Invoke().Current is not null)];
        if (modService is not null)
        {
            foreach (ModPackage package in modService.Catalog.Packages.Where(package => package.Format == ModFormat.Memoria && File.Exists(Path.Combine(package.RootDirectory, GuardianForceAbilityMods.PayloadPath))))
            {
                entries.Add(new(package.Id, package.Name + " · " + package.Status, "Edit this package's GF document, including when disabled.", Action: new("gf-editor-target", package.Id), Enabled: package.Status != ModEffectiveStatus.IncompatibleEdition));
            }
        }
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
        Push("gf-ability-editor");
    }

    private GuardianForceAbilityComposition GuardianEditorComposition()
    {
        GuardianForceEditorContext context = readGuardianEditor!();
        ImmutableArray<ImmutableArray<GuardianForceAbilityTemplate>> source = context.Source.ResolveGuardianForceTemplates();
        if (guardianPreviewMode == 0)
        {
            return GuardianForceAbilityComposer.Compose(source, [], context.Source.MenuDefinitions.AbilityPoints);
        }
        GuardianForceAbilityPatchDocument draft = guardianDraft!.ExportDocument;
        if (guardianPreviewMode == 1)
        {
            return GuardianForceAbilityComposer.Compose(source, guardianWithoutLayer ? [] : [new("This mod only", draft)], context.Source.MenuDefinitions.AbilityPoints);
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
        if (!guardianLocal && !substituted && !guardianWithoutLayer && (guardianPackage is null || guardianPreviewEnabled))
        {
            if (guardianPackage is null)
            {
                layers.Add(new("Selected draft", draft));
            }
            else
            {
                string selectedPackage = modService!.Catalog.Packages.Single(package => Path.GetFullPath(package.RootDirectory).Equals(guardianPackage.PackageDirectory, StringComparison.OrdinalIgnoreCase)).Id;
                int insertion = layers.FindIndex(layer => CompareGuardianPackageOrder(layer.PackageId, selectedPackage) > 0);
                layers.Insert(insertion < 0 ? layers.Count : insertion, new(selectedPackage, draft));
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

    internal bool EnterGuardianSearchCharacter(char character)
    {
        if (ScreenId != "gf-ability-search")
        {
            return false;
        }
        if (character == '\b')
        {
            guardianSearch = guardianSearch.Length > 0 ? guardianSearch[..^1] : "";
        }
        else if (!char.IsControl(character) && guardianSearch.Length < 100)
        {
            guardianSearch += character;
        }
        guardianCatalogCursor = 0;
        _view = null;
        return true;
    }

    private bool NavigateGuardianEditor(MenuInput input)
    {
        if (!IsGuardianAbilityEditor || guardianDraft is null)
        {
            return false;
        }
        if (input == MenuInput.Cancel)
        {
            if (guardianPending is not null)
            {
                guardianPending = null;
            }
            else
            {
                GuardGuardianDraft(() => Back());
            }
        }
        else if (input is MenuInput.FocusLeft or MenuInput.FocusRight)
        {
            guardianPane = input == MenuInput.FocusLeft ? 0 : 1;
        }
        else if (input == MenuInput.Tab)
        {
            guardianPane = (guardianPane + 1) % 3;
        }
        else if (input is MenuInput.PageLeft or MenuInput.PageRight)
        {
            guardianSubject = (guardianSubject + (input == MenuInput.PageLeft ? 15 : 1)) % 16;
            guardianRowCursor = 0;
            guardianRowScroll = 0;
            guardianPending = null;
        }
        else if (input is MenuInput.Up or MenuInput.Down or MenuInput.PageUp or MenuInput.PageDown or MenuInput.Left or MenuInput.Right)
        {
            int step = input is MenuInput.Up or MenuInput.PageUp or MenuInput.Left ? -1 : 1;
            if (input is MenuInput.PageUp or MenuInput.PageDown)
            {
                step *= 8;
            }
            if (guardianPane == 0)
            {
                guardianCatalogCursor = Math.Clamp(guardianCatalogCursor + step, 0, Math.Max(0, GuardianCatalog().Length - 1));
            }
            else if (guardianPane == 1)
            {
                guardianRowCursor = Math.Clamp(guardianRowCursor + step, 0, Math.Max(0, GuardianEditorComposition().Templates[guardianSubject].Length - 1));
            }
            else
            {
                guardianToolbarCursor = (guardianToolbarCursor + step + GuardianTools.Length) % GuardianTools.Length;
            }
        }
        else if (input == MenuInput.Alternate && guardianPane == 1 && GuardianSelectedRow is { } entry)
        {
            GuardianEdit(() => guardianDraft.Toggle(guardianSubject, entry.Ability));
        }
        else if (input == MenuInput.Confirm)
        {
            if (guardianPane == 2)
            {
                GuardianTool(guardianToolbarCursor);
            }
            else if (guardianPane == 0)
            {
                int[] catalog = GuardianCatalog();
                if (catalog.Length > 0)
                {
                    guardianPendingAbility = catalog[Math.Min(guardianCatalogCursor, catalog.Length - 1)];
                    guardianPending = "choose";
                    Message = guardianAbilityName!(guardianPendingAbility) + ": choose Append or Replace in the toolbar (Tab).";
                    guardianPane = 2;
                    guardianToolbarCursor = 0;
                }
            }
            else if (GuardianSelectedRow is { } selected)
            {
                if (guardianPending == "replace")
                {
                    Message = "Replace " + guardianAbilityName!(selected.Ability) + " with " + guardianAbilityName(guardianPendingAbility) + ". Confirm Replace in the toolbar.";
                    guardianPending = "replace-confirm";
                    guardianPane = 2;
                    guardianToolbarCursor = 1;
                }
                else if (guardianPending == "swap")
                {
                    guardianSwapSecond = selected.Ability;
                    guardianPending = "swap-confirm";
                    Message = "Swap " + guardianAbilityName!(guardianPendingAbility) + " and " + guardianAbilityName(selected.Ability) + ". Confirm Swap positions in the toolbar.";
                    guardianPane = 2;
                    guardianToolbarCursor = 2;
                }
                else if (guardianDraft.RowDirty(guardianSubject, selected.Ability))
                {
                    GuardianEdit(() => guardianDraft.Undo(guardianSubject, selected.Ability));
                }
            }
        }
        _view = null;
        return true;
    }

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
            if (guardianPreviewMode == 0 || guardianWithoutLayer)
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
        GuardianForceAbilityTemplate? row = GuardianSelectedRow;
        switch (index)
        {
            case 0:
                if (guardianPending is null && GuardianCatalog() is { Length: > 0 } appendCatalog)
                {
                    guardianPendingAbility = appendCatalog[Math.Min(guardianCatalogCursor, appendCatalog.Length - 1)];
                    guardianPending = "choose";
                }
                if (guardianPending is "choose" or "replace" or "replace-confirm")
                {
                    GuardianEdit(() => guardianDraft.Append(guardianSubject, guardianPendingAbility));
                    guardianPending = null;
                }
                break;
            case 1:
                if (guardianPending == "replace-confirm" && row is not null)
                {
                    GuardianEdit(() => guardianDraft.Replace(guardianSubject, row.Ability, guardianPendingAbility));
                    guardianPending = null;
                }
                else
                {
                    int[] catalog = GuardianCatalog();
                    if (catalog.Length > 0)
                    {
                        guardianPendingAbility = catalog[Math.Min(guardianCatalogCursor, catalog.Length - 1)];
                        guardianPending = "replace";
                        guardianPane = 1;
                        Message = "Choose the GF row to replace with " + guardianAbilityName!(guardianPendingAbility) + ".";
                    }
                }
                break;
            case 2:
                if (guardianPending == "swap-confirm")
                {
                    GuardianEdit(() => guardianDraft.Swap(guardianSubject, guardianPendingAbility, guardianSwapSecond));
                    guardianPending = null;
                }
                else if (row is not null)
                {
                    guardianPendingAbility = row.Ability;
                    guardianPending = "swap";
                    guardianPane = 1;
                    Message = "Swap " + guardianAbilityName!(row.Ability) + ": choose a different GF row and confirm.";
                }
                break;
            case 3:
                if (row is not null)
                {
                    GuardianEdit(() => guardianDraft.Undo(guardianSubject, row.Ability));
                }
                break;
            case 4:
                if (row is not null)
                {
                    GuardianEdit(() => guardianDraft.RestoreInherited(guardianSubject, row.Ability));
                }
                break;
            case 5:
                guardianPreviewMode = (guardianPreviewMode + 1) % 3;
                break;
            case 6:
                guardianPreviewEnabled = !guardianPreviewEnabled;
                break;
            case 7:
                guardianWithoutLayer = !guardianWithoutLayer;
                break;
            case 8:
                Push("gf-ability-search");
                break;
            case 9:
                guardianCategory = (guardianCategory + 1) % GuardianCategories.Length;
                guardianCatalogCursor = 0;
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
            case 14:
                guardianReview = reviewGuardianPackageOwnership!(guardianDraft.ExportDocument);
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
                    if (guardianDraft?.ExportDocument.DocumentId == guardianReview!.Document.DocumentId)
                    {
                        guardianDraft.Accept(guardianReview.Document);
                    }
                    Back();
                    Message = applied;
                    ContinueGuardianNavigation();
                    break;
                case "gf-editor-letter":
                    EnterGuardianSearchCharacter(action.Value[0]);
                    break;
                case "gf-editor-delete":
                    EnterGuardianSearchCharacter('\b');
                    break;
                case "gf-editor-clear":
                    guardianSearch = "";
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
