using Memoria.FF8.Mods;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    private IModService? modService;
    private IDisposable? modSubscription;
    private string modSearch = "";
    private int modFilter;
    private string? selectedModId;
    private static readonly string[] ModFilters = ["All packages", "Requested enabled", "Requested disabled", "Needs attention"];
    private const int ModToolbarCount = 4;

    internal void ConnectMods(IModService service)
    {
        modSubscription?.Dispose();
        modService = service;
        modSubscription = service.Subscribe(_ => RestoreModSelection());
        Register(new MenuScreenDefinition
        {
            Id = "mods-search",
            Title = "Config · Mods · Search",
            Help = "Type a package name or ID, or choose characters. Back keeps the filter and returns to Mods.",
            Columns = 8,
            Entries = _ => [.. "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 -_.".Select((letter, index) => new MenuEntry("letter-" + index, letter.ToString(), "Add to package search.", Action: new MenuAction("mod-search-letter", Value: letter.ToString()))), new("delete", "Delete", "Remove the last search character.", Action: new("mod-search-delete")), new("clear", "Clear", "Show all package names.", Action: new("mod-search-clear")), new("done", "Done", "Return to the package list.", Action: new("menu-back"))],
            Details = _ => ["Search: " + modSearch, "Matches package names and IDs."]
        });
        Register(new MenuScreenDefinition
        {
            Id = "mods",
            GalleryEligible = true,
            Title = "Config · Mods",
            Help = "Enable mod packages. Changes apply after validation; movies use their selected files until playback ends.",
            MainCommand = true,
            Entries = controller => controller.ModEntries(),
            Details = controller => controller.ModDetails(),
            BuildContent = (controller, panel) => panel.Children.Add(controller.BuildModContent())
        });
    }

    internal void RefreshModView()
    {
        if (ScreenId == "mods")
        {
            _view = null;
        }
    }

    private IReadOnlyList<MenuEntry> ModEntries()
    {
        IModService service = modService!;
        List<MenuEntry> entries =
        [
            new("mod-search", "Search: " + (modSearch.Length == 0 ? "All names" : modSearch), "Find packages by name or ID. Keyboard and an on-screen character picker are available.", Destination: "mods-search"),
            new("mod-filter", "Filter: " + ModFilters[modFilter], "Cycle requested enable state or packages requiring attention.", Action: new MenuAction("mod-filter")),
            new("mod-auto", $"[{(service.AutoEnableNewMods ? "x" : " ")}] Automatically enable new mods", "Keep explicit disables; preserve existing mods when turning off.", Action: new MenuAction("mod-auto")),
            new("mod-refresh", "Refresh", "Rescan packages in both game and application Mods directories and retry rejected changes.", Action: new MenuAction("mod-refresh"))
        ];
        foreach (ModPackage package in FilteredModPackages())
        {
            ModEffectiveStatus effectiveStatus = EffectiveModStatus(package);
            string status = effectiveStatus switch
            {
                ModEffectiveStatus.Active => "Active",
                ModEffectiveStatus.Disabled => "Disabled",
                ModEffectiveStatus.IncompatibleEdition => "Incompatible edition",
                ModEffectiveStatus.NoUsableEntries => "No usable entries",
                _ => "Reload failed"
            };
            entries.Add(new MenuEntry(package.Id, $"[{(package.RequestedEnabled ? "x" : " ")}] {package.Name}   ·   {package.Format}   ·   {status}", status, Action: new MenuAction("mod-toggle", package.Id), Enabled: package.Status != ModEffectiveStatus.IncompatibleEdition));
        }
        return entries;
    }

    private ModEffectiveStatus EffectiveModStatus(ModPackage package)
    {
        return package.Status != ModEffectiveStatus.IncompatibleEdition && modService!.Diagnostics.Any(diagnostic => diagnostic.PackageId?.Equals(package.Id, StringComparison.OrdinalIgnoreCase) == true && diagnostic.Code is "replacement-failed" or "reload-failed") ? ModEffectiveStatus.ReloadFailure : package.Status;
    }

    private IEnumerable<ModPackage> FilteredModPackages()
    {
        return modService!.Catalog.Packages.Where(package => (package.Name.Contains(modSearch, StringComparison.OrdinalIgnoreCase) || package.Id.Contains(modSearch, StringComparison.OrdinalIgnoreCase)) && (modFilter switch
        {
            1 => package.RequestedEnabled,
            2 => !package.RequestedEnabled,
            3 => EffectiveModStatus(package) is ModEffectiveStatus.IncompatibleEdition or ModEffectiveStatus.NoUsableEntries or ModEffectiveStatus.ReloadFailure,
            _ => true
        }));
    }

    private void RestoreModSelection()
    {
        if (selectedModId is not null)
        {
            int index = ModEntries().ToList().FindIndex(entry => entry.Id == selectedModId);
            _selections["mods"] = index >= 0 ? index : Math.Min(ModToolbarCount, ModEntries().Count - 1);
        }
        _view = null;
    }

    public bool EnterModSearchCharacter(char character)
    {
        if (!IsOpen || ScreenId != "mods-search")
        {
            return false;
        }
        selectedModId ??= ModEntries().ElementAtOrDefault(_selections.GetValueOrDefault("mods"))?.Id;
        if (character == '\b')
        {
            modSearch = modSearch.Length == 0 ? "" : modSearch[..^1];
        }
        else if (!char.IsControl(character) && modSearch.Length < 100)
        {
            modSearch += character;
        }
        RestoreModSelection();
        return true;
    }

    private IReadOnlyList<string> ModDetails()
    {
        IModService service = modService!;
        ModPackage? package = service.Catalog.Packages.FirstOrDefault(package => package.Id == SelectedEntry?.Id);
        List<string> details = [service.IsReloadPending ? "Reload pending" : $"{FilteredModPackages().Count()} matching / {service.Catalog.Packages.Count} packages"];
        if (package is not null)
        {
            selectedModId = package.Id;
            details.Add(package.Name);
            details.Add("Requested: " + (package.RequestedEnabled ? "Enabled" : "Disabled"));
            details.Add("Effective: " + EffectiveModStatus(package));
            details.Add("Format: " + package.Format);
        }
        details.AddRange(service.Diagnostics.Where(diagnostic => package is null || diagnostic.PackageId?.Equals(package.Id, StringComparison.OrdinalIgnoreCase) == true).Take(4).Select(diagnostic => diagnostic.Message));
        return details;
    }

    private UiElement BuildModContent()
    {
        return new ModListElement(this);
    }

    private bool ExecuteModAction(MenuAction action)
    {
        if (!action.Kind.StartsWith("mod-", StringComparison.Ordinal))
        {
            return false;
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
            case "mod-filter":
                modFilter = (modFilter + 1) % ModFilters.Length;
                RestoreModSelection();
                return true;
            case "mod-auto":
                result = service.SetAutoEnableNewMods(!service.AutoEnableNewMods);
                break;
            case "mod-toggle":
                selectedModId = action.SubjectId;
                ModPackage? package = service.Catalog.Packages.FirstOrDefault(package => package.Id == action.SubjectId);
                if (package is null || package.Status == ModEffectiveStatus.IncompatibleEdition)
                {
                    Message = "This package is unavailable or incompatible with the current edition.";
                    _view = null;
                    return true;
                }
                result = service.SetEnabled(package.Id, !package.RequestedEnabled);
                break;
            case "mod-refresh":
                service.Refresh();
                result = ModOperationResult.Completed;
                break;
            default:
                throw new NotSupportedException($"Unknown mod action {action.Kind}.");
        }
        Message = result.Success ? "Change queued; current resources remain active until validation succeeds." : result.Error ?? "The change could not be saved.";
        _view = null;
        return true;
    }
}
