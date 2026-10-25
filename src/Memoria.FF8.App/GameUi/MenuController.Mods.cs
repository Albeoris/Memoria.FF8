using Memoria.FF8.Mods;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    private IModService? modService;
    private IDisposable? modSubscription;
    private string modSearch = "";
    private int modFilter;
    private int modFormat;
    private string? selectedModId;
    private string? diagnosticModId;
    private int modSelectedOrdinal;
    private string modFocusedControlId = "commands";
    private int modPageSize = 1;
    private bool modInspectorOpen;
    private UiRect modTableBounds;
    private int modWheelDelta;
    private static readonly string[] ModFilters = ["All mods", "Enabled in settings", "Disabled in settings", "Needs attention"];
    private static readonly string[] ModFormats = ["All formats", "Tonberry", "Demaster", "Junction", "Memoria"];

    internal void ConnectMods(IModService service)
    {
        modSubscription?.Dispose();
        modService = service;
        modSubscription = service.Subscribe(_ => RestoreModSelection());
        Register(new MenuScreenDefinition { Id = "mods-create", Title = "Mods · Create Mods", Help = "An initialized gameplay editor is required.", Entries = _ => [new("unavailable", "GF Abilities unavailable in this preview", "Open the tool from the running game.", Enabled: false)] });
        Register(new MenuScreenDefinition { Id = "gf-mod-review", Title = "Mods · GF changes review", Help = "No gameplay editor is connected.", Entries = _ => [] });
        Register(new MenuScreenDefinition
        {
            Id = "mods-search",
            Title = "Mods · Search",
            Help = "Type a package name or ID. Confirm/Back finishes text entry; changes are kept.",
            Columns = 8,
            Entries = _ => [.. "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 -_.".Select((letter, index) => new MenuEntry("letter-" + index, letter.ToString(), "Add to package search.", Action: new MenuAction("mod-search-letter", Value: letter.ToString()))), new("delete", "Delete", "Remove the last search character.", Action: new("mod-search-delete")), new("clear", "Clear", "Show all package names.", Action: new("mod-search-clear")), new("done", "Done", "Return to the package list.", Action: new("menu-back"))],
            Details = _ => ["Search mods: " + modSearch + "│", "Matches package names and IDs."]
        });
        Register(new MenuScreenDefinition
        {
            Id = "mods",
            GalleryEligible = true,
            Title = "Mods",
            Help = "Requested settings are saved immediately. Status shows what is currently in use.",
            MainCommand = true,
            Entries = controller => controller.ModEntries(),
            Details = controller => controller.ModDetails(),
            BuildContent = (controller, panel) => panel.Children.Add(new ModListElement(controller))
        });
        Register(new MenuScreenDefinition
        {
            Id = "mods-actions", Title = "Mods · Actions", Help = "Package and manager actions.",
            Entries = _ => ModActions()
        });
        Register(new MenuScreenDefinition
        {
            Id = "mods-diagnostics", Title = "Mods · Diagnostics", Help = "Select a mod to inspect its diagnostics.",
            Entries = _ => ModDiagnosticGroups(),
            Details = _ => ModDiagnosticGroupDetails()
        });
        Register(new MenuScreenDefinition
        {
            Id = "mods-package-diagnostics", Title = "Mods · Package diagnostics", Help = "Diagnostics for the selected mod. Back returns to the mod groups.",
            Entries = _ => ModDiagnosticEntries(),
            Details = _ => ModDiagnosticDetails()
        });
    }

    private IReadOnlyList<MenuEntry> ModActions()
    {
        List<MenuEntry> actions =
        [
            new("selection", "Enable / Disable selected mod", "Save the selected package's requested setting.", Action: new("mod-selected-toggle")),
            new("inspect", "Package details", "Inspect the retained selection.", Action: new("mod-inspect")),
            new("search", "Search mods", "Search names and IDs.", Destination: "mods-search"),
            new("status", "Status: " + ModFilters[modFilter], "Change requested-state filter.", Action: new("mod-filter")),
            new("format", "Format: " + ModFormats[modFormat], "Change package format filter.", Action: new("mod-format")),
            new("clear", "Clear filters", "Clear search, status and format.", Action: new("mod-clear")),
            new("refresh", "Refresh", "Rescan packages.", Action: new("mod-refresh")),
            new("create", "Create Mods", "Open authoring tools.", Destination: "mods-create"),
            new("diagnostics", "View diagnostics", "Inspect all current problems.", Destination: "mods-diagnostics"),
            new("policy", "Automatically enable new mods", "Explicit choices are kept. Turning off preserves existing packages.", Action: new("mod-auto")),
            new("controls", "Controls", "View configured bindings.", Destination: "config-controls")
        ];
        if (HasModReview)
        {
            actions.Insert(2, new("gf-review", "Review GF changes", "Review the pending gameplay proposal.", Destination: "gf-mod-review"));
        }
        return actions;
    }

    private bool HasModReview => modService!.View.Diagnostics.Any(diagnostic => diagnostic.Code == "Memoria.GuardianForceReview");

    internal void RefreshModView()
    {
        if (ScreenId == "mods")
        {
            RestoreModSelection();
        }
    }

    private IReadOnlyList<MenuEntry> ModEntries()
    {
        List<MenuEntry> entries =
        [
            new("installed-tab", "Installed Mods", "Installed packages and status.", Action: new("mod-installed-tab")),
            new("create-tab", "Create Mods", "Author a gameplay mod inside the game.", Destination: "mods-create"),
            new("mod-search", "Search mods", "Search names and IDs.", Destination: "mods-search"),
            new("mod-filter", ModFilters[modFilter], "Filter the latest saved requested setting.", Action: new("mod-filter")),
            new("mod-format", ModFormats[modFormat], "Filter package format.", Action: new("mod-format")),
            new("mod-auto", "Automatically enable new mods", "Explicit choices are kept; turning off preserves existing packages.", Action: new("mod-auto")),
            new("mod-refresh", "Refresh", "Rescan packages in both game and application Mods directories and retry rejected changes.", Action: new("mod-refresh"))
        ];
        entries.AddRange(FilteredModViews().Select(view => new MenuEntry(view.Package.Id, view.Package.Name, view.StatusText, Action: new("mod-toggle", view.Package.Id))));
        if (HasModReview)
        {
            entries.Add(new("gf-review", "Review GF changes · Pending", "Review pending gameplay consequences.", Destination: "gf-mod-review"));
        }
        return entries;
    }

    private ModPackageView[] FilteredModViews()
    {
        return modService!.View.Packages.Where(view => (view.Package.Name.Contains(modSearch, StringComparison.OrdinalIgnoreCase) || view.Package.Id.Contains(modSearch, StringComparison.OrdinalIgnoreCase)) && (modFilter switch
        {
            1 => view.Package.RequestedEnabled,
            2 => !view.Package.RequestedEnabled,
            3 => view.NeedsAttention,
            _ => true
        }) && (modFormat == 0 || view.Package.Format.ToString() == ModFormats[modFormat])).ToArray();
    }

    private ModPackageView? SelectedModView => FilteredModViews().FirstOrDefault(view => view.Package.Id.Equals(selectedModId, StringComparison.OrdinalIgnoreCase));

    private void RestoreModSelection(bool firstWhenMissing = false)
    {
        ModPackageView[] packages = FilteredModViews();
        if (!packages.Any(view => view.Package.Id.Equals(selectedModId, StringComparison.OrdinalIgnoreCase)))
        {
            selectedModId = packages.ElementAtOrDefault(firstWhenMissing ? 0 : Math.Clamp(modSelectedOrdinal, 0, Math.Max(0, packages.Length - 1)))?.Package.Id;
        }
        if (selectedModId is not null)
        {
            _selections["mods"] = Math.Max(0, ModEntries().ToList().FindIndex(entry => entry.Id.Equals(selectedModId, StringComparison.OrdinalIgnoreCase)));
        }
        _view = null;
    }

    private void SelectMod(string id)
    {
        selectedModId = id;
        modSelectedOrdinal = Math.Max(0, Array.FindIndex(FilteredModViews(), view => view.Package.Id.Equals(id, StringComparison.OrdinalIgnoreCase)));
        _selections["mods"] = Math.Max(0, ModEntries().ToList().FindIndex(entry => entry.Id.Equals(id, StringComparison.OrdinalIgnoreCase)));
    }

    private void MoveModSelection(int direction, bool page)
    {
        ModPackageView[] packages = FilteredModViews();
        if (packages.Length == 0)
        {
            return;
        }
        int index = Math.Max(0, Array.FindIndex(packages, view => view.Package.Id.Equals(selectedModId, StringComparison.OrdinalIgnoreCase)));
        int next = page ? Math.Min(packages.Length - 1, Math.Clamp(index / modPageSize + direction, 0, (packages.Length - 1) / modPageSize) * modPageSize + index % modPageSize) : Math.Clamp(index + direction, 0, packages.Length - 1);
        SelectMod(packages[next].Package.Id);
        _view = null;
    }

    internal void HandleModsWheel(int x, int y, int delta)
    {
        var point = BuildView().ToDesignPoint(x, y);
        if (!modTableBounds.Contains(point.X, point.Y))
        {
            modWheelDelta = 0;
            return;
        }
        modWheelDelta += delta;
        while (Math.Abs(modWheelDelta) >= 120)
        {
            int direction = modWheelDelta > 0 ? -1 : 1;
            MoveModSelection(direction, true);
            modWheelDelta += direction * 120;
        }
    }

    public bool EnterModSearchCharacter(char character)
    {
        if (!IsOpen || ScreenId != "mods-search")
        {
            return false;
        }
        if (character == '\b')
        {
            modSearch = modSearch.Length == 0 ? "" : modSearch[..^1];
        }
        else if (!char.IsControl(character) && modSearch.Length < 100)
        {
            modSearch += character;
        }
        RestoreModSelection(true);
        return true;
    }

    private IReadOnlyList<string> ModDetails()
    {
        ModPackageView? view = SelectedModView;
        List<string> details = [];
        if (view is not null)
        {
            details.Add(view.Package.Name);
            details.Add(view.Package.Format.ToString());
            details.Add("Enabled in settings: " + (view.Package.RequestedEnabled ? "On" : "Off"));
            details.Add("In use: " + view.StatusText);
            if (view.Package.JunctionMetadata?.Behaviors.Contains(JunctionBehavior.InstantManualSkip) == true)
            {
                details.Add("Instant manual skip is built into the engine and is always enabled.");
            }
            if (!string.IsNullOrEmpty(view.Problem))
            {
                details.Add(view.Problem);
            }
        }
        else
        {
            details.Add("Select a mod to view details.");
        }
        return details;
    }

    private IReadOnlyList<MenuEntry> ModDiagnosticGroups()
    {
        List<MenuEntry> entries = [new("retry", "Retry / Refresh", "Rescan and validate through the mod service.", Action: new("mod-refresh"))];
        foreach (var group in modService!.View.Diagnostics.GroupBy(diagnostic => diagnostic.PackageId, StringComparer.OrdinalIgnoreCase))
        {
            string name = group.Key ?? "Global transaction";
            entries.Add(new("diagnostic-mod-" + group.Key, name + " · " + group.Count(), "View diagnostics: " + name, Action: new("mod-diagnostics-open", group.Key ?? ""), Destination: "mods-package-diagnostics"));
        }
        if (entries.Count == 1)
        {
            entries.Add(new("none", "No current diagnostics", "No problems reported."));
        }
        return entries;
    }

    private ModDiagnostic[] CurrentModDiagnostics()
    {
        return modService!.View.Diagnostics.Where(diagnostic => string.Equals(diagnostic.PackageId, diagnosticModId, StringComparison.OrdinalIgnoreCase)).ToArray();
    }

    private IReadOnlyList<MenuEntry> ModDiagnosticEntries()
    {
        ModDiagnostic[] diagnostics = CurrentModDiagnostics();
        return [new("retry", "Retry / Refresh", "Rescan and validate through the mod service.", Action: new("mod-refresh")), .. (diagnostics.Length == 0 ? new[] { new MenuEntry("none", "No current diagnostics", "No problems reported for this group.") } : diagnostics.Select((diagnostic, index) => new MenuEntry("diagnostic-" + index, diagnostic.Code, diagnostic.Message)))];
    }

    private IReadOnlyList<string> ModDiagnosticGroupDetails()
    {
        if (SelectedEntry?.Action is { Kind: "mod-diagnostics-open" } action)
        {
            string? packageId = action.SubjectId.Length == 0 ? null : action.SubjectId;
            int count = modService!.View.Diagnostics.Count(diagnostic => string.Equals(diagnostic.PackageId, packageId, StringComparison.OrdinalIgnoreCase));
            return [packageId ?? "Global transaction", "Diagnostics: " + count, "Confirm to view this group's messages."];
        }
        return ["Diagnostics grouped by mod.", "Select a mod to view its messages."];
    }

    private IReadOnlyList<string> ModDiagnosticDetails()
    {
        List<string> details = [diagnosticModId ?? "Global transaction"];
        if (SelectedEntry?.Id.StartsWith("diagnostic-", StringComparison.Ordinal) == true && int.TryParse(SelectedEntry.Id[11..], out int index) && CurrentModDiagnostics().ElementAtOrDefault(index) is { } diagnostic)
        {
            details.Add(diagnostic.Code);
            details.Add(diagnostic.Message);
            if (diagnostic.RelativePath is not null)
            {
                details.Add(diagnostic.RelativePath);
            }
        }
        return details;
    }

    private bool ExecuteModAction(MenuAction action)
    {
        if (!action.Kind.StartsWith("mod-", StringComparison.Ordinal))
        {
            return false;
        }
        switch (action.Kind)
        {
            case "mod-diagnostics-open":
                diagnosticModId = action.SubjectId.Length == 0 ? null : action.SubjectId;
                _selections["mods-package-diagnostics"] = 0;
                return true;
            case "mod-installed-tab":
                return true;
            case "mod-search-letter":
                foreach (char letter in action.Value)
                {
                    EnterModSearchCharacter(letter);
                }
                return true;
            case "mod-search-delete":
                EnterModSearchCharacter('\b');
                return true;
            case "mod-search-clear":
                modSearch = "";
                RestoreModSelection();
                return true;
            case "mod-clear":
                modSearch = "";
                modFilter = 0;
                modFormat = 0;
                RestoreModSelection();
                return true;
            case "mod-filter":
                modFilter = (modFilter + 1) % ModFilters.Length;
                RestoreModSelection(true);
                return true;
            case "mod-format":
                modFormat = (modFormat + 1) % ModFormats.Length;
                RestoreModSelection(true);
                return true;
            case "mod-inspect":
                if (ScreenId == "mods-actions")
                {
                    Back();
                }
                modInspectorOpen = true;
                modFocusedControlId = "mod-inspector";
                _view = null;
                return true;
            case "mod-selected-toggle":
                if (selectedModId is not null)
                {
                    return ExecuteModAction(new("mod-toggle", selectedModId));
                }
                return true;
        }
        if (IsFixtureContext)
        {
            Message = "Preview / Fixture: live mod changes are unavailable.";
            return true;
        }
        IModService service = modService ?? throw new InvalidOperationException("The mod service is not connected.");
        ModOperationResult result;
        switch (action.Kind)
        {
            case "mod-auto":
                result = service.SetAutoEnableNewMods(!service.View.AutoEnableNewMods);
                break;
            case "mod-toggle":
                selectedModId = action.SubjectId;
                ModPackageView? view = service.View.Packages.FirstOrDefault(view => view.Package.Id.Equals(action.SubjectId, StringComparison.OrdinalIgnoreCase));
                if (view is null || !view.CanToggle)
                {
                    Message = view?.Problem ?? "This package is unavailable or incompatible with the current edition.";
                    _view = null;
                    return true;
                }
                result = service.SetEnabled(view.Package.Id, !view.Package.RequestedEnabled);
                break;
            case "mod-refresh":
                if (service.View.Outcome != ModUpdateOutcome.Updating)
                {
                    service.Refresh();
                }
                result = ModOperationResult.Completed;
                break;
            default:
                throw new NotSupportedException($"Unknown mod action {action.Kind}.");
        }
        Message = result.Success ? "" : result.Error ?? "The change could not be saved.";
        RestoreModSelection();
        return true;
    }
}
