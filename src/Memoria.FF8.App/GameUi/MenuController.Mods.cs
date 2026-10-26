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

    internal void DisconnectMods()
    {
        modSubscription?.Dispose();
        modSubscription = null;
        modService = null;
    }

    internal void ConnectMods(IModService service)
    {
        modSubscription?.Dispose();
        modService = service;
        modSubscription = service.Subscribe(_ => RestoreModSelection());
        Register(new MenuScreenDefinition { Id = "mods-create", Title = "Mods · Create Mods", Help = "An initialized gameplay editor is required.", Entries = _ => [new("unavailable", SystemText("ui.connectmods.entry.label.gf.abilities.unavailable.in.this.preview", "GF Abilities unavailable in this preview"), SystemText("ui.connectmods.entry.help.open.the.tool.from.the.running.game", "Open the tool from the running game."), Enabled: false)] });
        Register(new MenuScreenDefinition { Id = "gf-mod-review", Title = "Mods · GF changes review", Help = "No gameplay editor is connected.", Entries = _ => [] });
        Register(new MenuScreenDefinition
        {
            Id = "mods-search",
            Title = "Mods · Search",
            Help = "Type a package name or ID. Confirm/Back finishes text entry; changes are kept.",
            Columns = 8,
            Entries = _ => [.. "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 -_.".Select((letter, index) => new MenuEntry("letter-" + index, letter.ToString(), SystemText("ui.connectmods.entry.help.add.to.package.search", "Add to package search."), Action: new MenuAction("mod-search-letter", Value: letter.ToString()))), new("delete", SystemText("ui.connectmods.entry.label.delete", "Delete"), SystemText("ui.connectmods.entry.help.remove.the.last.search.character", "Remove the last search character."), Action: new("mod-search-delete")), new("clear", SystemText("ui.connectmods.entry.label.clear", "Clear"), SystemText("ui.connectmods.entry.help.show.all.package.names", "Show all package names."), Action: new("mod-search-clear")), new("done", SystemText("ui.connectmods.entry.label.done", "Done"), SystemText("ui.connectmods.entry.help.return.to.the.package.list", "Return to the package list."), Action: new("menu-back"))],
            Details = _ => [FormatSystemText("ui.connectmods.details.search.mods.template", "Search mods: {0}\u2502", modSearch), SystemText("ui.connectmods.details.matches.package.names.and.ids", "Matches package names and IDs.")]
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
            new("selection", SystemText("ui.modactions.entry.label.enable.disable.selected.mod", "Enable / Disable selected mod"), SystemText("ui.modactions.entry.help.save.the.selected.package.s.requested.setting", "Save the selected package's requested setting."), Action: new("mod-selected-toggle")),
            new("inspect", SystemText("ui.modactions.entry.label.package.details", "Package details"), SystemText("ui.modactions.entry.help.inspect.the.retained.selection", "Inspect the retained selection."), Action: new("mod-inspect")),
            new("search", SystemText("ui.modactions.entry.label.search.mods", "Search mods"), SystemText("ui.modactions.entry.help.search.names.and.ids", "Search names and IDs."), Destination: "mods-search"),
            new("status", FormatSystemText("ui.modactions.entry.label.status.template", "Status: {0}", ModFilters[modFilter]), SystemText("ui.modactions.entry.help.change.requested.state.filter", "Change requested-state filter."), Action: new("mod-filter")),
            new("format", FormatSystemText("ui.modactions.entry.label.format.template", "Format: {0}", ModFormats[modFormat]), SystemText("ui.modactions.entry.help.change.package.format.filter", "Change package format filter."), Action: new("mod-format")),
            new("clear", SystemText("ui.modactions.entry.label.clear.filters", "Clear filters"), SystemText("ui.modactions.entry.help.clear.search.status.and.format", "Clear search, status and format."), Action: new("mod-clear")),
            new("refresh", SystemText("ui.modactions.entry.label.refresh", "Refresh"), SystemText("ui.modactions.entry.help.rescan.packages", "Rescan packages."), Action: new("mod-refresh")),
            new("create", SystemText("ui.modactions.entry.label.create.mods", "Create Mods"), SystemText("ui.modactions.entry.help.open.authoring.tools", "Open authoring tools."), Destination: "mods-create"),
            new("diagnostics", SystemText("ui.modactions.entry.label.view.diagnostics", "View diagnostics"), SystemText("ui.modactions.entry.help.inspect.all.current.problems", "Inspect all current problems."), Destination: "mods-diagnostics"),
            new("policy", SystemText("ui.modactions.entry.label.automatically.enable.new.mods", "Automatically enable new mods"), SystemText("ui.modactions.entry.help.explicit.choices.are.kept.turning.off.preserves.existing", "Explicit choices are kept. Turning off preserves existing packages."), Action: new("mod-auto")),
            new("controls", SystemText("ui.modactions.entry.label.controls", "Controls"), SystemText("ui.modactions.entry.help.view.configured.bindings", "View configured bindings."), Destination: "config-controls")
        ];
        if (HasModReview)
        {
            actions.Insert(2, new("gf-review", SystemText("ui.modactions.entry.label.review.gf.changes", "Review GF changes"), SystemText("ui.modactions.entry.help.review.the.pending.gameplay.proposal", "Review the pending gameplay proposal."), Destination: "gf-mod-review"));
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
            new("installed-tab", SystemText("ui.modentries.entry.label.installed.mods", "Installed Mods"), SystemText("ui.modentries.entry.help.installed.packages.and.status", "Installed packages and status."), Action: new("mod-installed-tab")),
            new("create-tab", SystemText("ui.modentries.entry.label.create.mods", "Create Mods"), SystemText("ui.modentries.entry.help.author.a.gameplay.mod.inside.the.game", "Author a gameplay mod inside the game."), Destination: "mods-create"),
            new("mod-search", SystemText("ui.modentries.entry.label.search.mods", "Search mods"), SystemText("ui.modentries.entry.help.search.names.and.ids", "Search names and IDs."), Destination: "mods-search"),
            new("mod-filter", ModFilters[modFilter], SystemText("ui.modentries.entry.help.filter.the.latest.saved.requested.setting", "Filter the latest saved requested setting."), Action: new("mod-filter")),
            new("mod-format", ModFormats[modFormat], SystemText("ui.modentries.entry.help.filter.package.format", "Filter package format."), Action: new("mod-format")),
            new("mod-auto", SystemText("ui.modentries.entry.label.automatically.enable.new.mods", "Automatically enable new mods"), SystemText("ui.modentries.entry.help.explicit.choices.are.kept.turning.off.preserves.existing", "Explicit choices are kept; turning off preserves existing packages."), Action: new("mod-auto")),
            new("mod-refresh", SystemText("ui.modentries.entry.label.refresh", "Refresh"), SystemText("ui.modentries.entry.help.rescan.packages.in.both.game.and.application.mods", "Rescan packages in both game and application Mods directories and retry rejected changes."), Action: new("mod-refresh"))
        ];
        entries.AddRange(FilteredModViews().Select(view => new MenuEntry(view.Package.Id, view.Package.Name, ModStatusText(view), Action: new("mod-toggle", view.Package.Id))));
        if (HasModReview)
        {
            entries.Add(new("gf-review", SystemText("ui.modentries.entry.label.review.gf.changes.pending", "Review GF changes · Pending"), SystemText("ui.modentries.entry.help.review.pending.gameplay.consequences", "Review pending gameplay consequences."), Destination: "gf-mod-review"));
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
            details.Add(FormatSystemText("ui.moddetails.details.enabled.in.settings.template", "Enabled in settings: {0}", (view.Package.RequestedEnabled ? SystemText("ui.moddetails.details.on", "On") : SystemText("ui.moddetails.details.off", "Off"))));
            details.Add(FormatSystemText("ui.moddetails.details.in.use.template", "In use: {0}", ModStatusText(view)));
            if (view.Package.JunctionMetadata?.Behaviors.Contains(JunctionBehavior.InstantManualSkip) == true)
            {
                details.Add(SystemText("ui.moddetails.details.instant.manual.skip.is.built.into.the.engine", "Instant manual skip is built into the engine and is always enabled."));
            }
            if (!string.IsNullOrEmpty(view.Problem))
            {
                details.Add(ModProblemText(view)!);
            }
        }
        else
        {
            details.Add(SystemText("ui.moddetails.details.select.a.mod.to.view.details", "Select a mod to view details."));
        }
        return details;
    }

    private IReadOnlyList<MenuEntry> ModDiagnosticGroups()
    {
        List<MenuEntry> entries = [new("retry", SystemText("ui.moddiagnosticgroups.entry.label.retry.refresh", "Retry / Refresh"), SystemText("ui.moddiagnosticgroups.entry.help.rescan.and.validate.through.the.mod.service", "Rescan and validate through the mod service."), Action: new("mod-refresh"))];
        foreach (var group in modService!.View.Diagnostics.GroupBy(diagnostic => diagnostic.PackageId, StringComparer.OrdinalIgnoreCase))
        {
            string name = group.Key ?? "Global transaction";
            entries.Add(new("diagnostic-mod-" + group.Key, name + " · " + group.Count(), FormatSystemText("ui.moddiagnosticgroups.entry.help.view.diagnostics.template", "View diagnostics: {0}", name), Action: new("mod-diagnostics-open", group.Key ?? ""), Destination: "mods-package-diagnostics"));
        }
        if (entries.Count == 1)
        {
            entries.Add(new("none", SystemText("ui.moddiagnosticgroups.entry.label.no.current.diagnostics", "No current diagnostics"), SystemText("ui.moddiagnosticgroups.entry.help.no.problems.reported", "No problems reported.")));
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
        return [new("retry", SystemText("ui.moddiagnosticentries.entry.label.retry.refresh", "Retry / Refresh"), SystemText("ui.moddiagnosticentries.entry.help.rescan.and.validate.through.the.mod.service", "Rescan and validate through the mod service."), Action: new("mod-refresh")), .. (diagnostics.Length == 0 ? new[] { new MenuEntry("none", SystemText("ui.moddiagnosticentries.entry.label.no.current.diagnostics", "No current diagnostics"), SystemText("ui.moddiagnosticentries.entry.help.no.problems.reported.for.this.group", "No problems reported for this group.")) } : diagnostics.Select((diagnostic, index) => new MenuEntry("diagnostic-" + index, diagnostic.Code, diagnostic.Message)))];
    }

    private IReadOnlyList<string> ModDiagnosticGroupDetails()
    {
        if (SelectedEntry?.Action is { Kind: "mod-diagnostics-open" } action)
        {
            string? packageId = action.SubjectId.Length == 0 ? null : action.SubjectId;
            int count = modService!.View.Diagnostics.Count(diagnostic => string.Equals(diagnostic.PackageId, packageId, StringComparison.OrdinalIgnoreCase));
            return [packageId ?? SystemText("ui.moddiagnosticgroupdetails.details.global.transaction", "Global transaction"), FormatSystemText("ui.moddiagnosticgroupdetails.details.diagnostics.template", "Diagnostics: {0}", count), SystemText("ui.moddiagnosticgroupdetails.details.confirm.to.view.this.group.s.messages", "Confirm to view this group's messages.")];
        }
        return [SystemText("ui.moddiagnosticgroupdetails.details.diagnostics.grouped.by.mod", "Diagnostics grouped by mod."), SystemText("ui.moddiagnosticgroupdetails.details.select.a.mod.to.view.its.messages", "Select a mod to view its messages.")];
    }

    private IReadOnlyList<string> ModDiagnosticDetails()
    {
        List<string> details = [diagnosticModId ?? SystemText("ui.moddiagnosticdetails.details.global.transaction", "Global transaction")];
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
            SetSystemMessage(() => SystemText("ui.executemodaction.message.preview.fixture.live.mod.changes.are.unavailable", "Preview / Fixture: live mod changes are unavailable."));
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
                    SetSystemMessage(() => (view is null ? null : ModProblemText(view)) ?? SystemText("ui.executemodaction.message.this.package.is.unavailable.or.incompatible.with.the", "This package is unavailable or incompatible with the current edition."));
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
        SetSystemMessage(() => result.Success ? "" : result.Error ?? SystemText("ui.executemodaction.message.the.change.could.not.be.saved", "The change could not be saved."));
        RestoreModSelection();
        return true;
    }
}
