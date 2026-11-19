using Memoria.FF8.Mods;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    private IModLibraryService? modLibrary;
    private ModLibrarySnapshot? modLibrarySnapshot;
    private string catalogSearch = "";
    private bool catalogSearchEditing;
    private int catalogAlphabet;
    private readonly UiMultilineEditor catalogSearchEditor = new() { Id = "library-search" };

    internal void ConnectCatalogClipboard(Func<string> paste, Action<string> copy)
    {
        catalogSearchEditor.ReadClipboard = () =>
        {
            int available = 100 - catalogSearchEditor.Text.Length + catalogSearchEditor.SelectionLength;
            return string.Concat(paste().Where(character => !char.IsControl(character)).Take(available));
        };
        catalogSearchEditor.WriteClipboard = copy;
    }

    internal void EditCatalogSearch(UiEditCommand command, bool extend)
    {
        if (catalogSearchEditing && ScreenId == "mods-catalog")
        {
            catalogSearchEditor.Command(command, extend);
        }
        else if (command == UiEditCommand.Backspace && IsCatalogSearchEditing)
        {
            EnterCatalogSearchCharacter('\b');
        }
    }
    private UiScrollPanel? catalogDetailsScroll;
    private float catalogDetailsOffset;
    private UiScrollPanel? catalogInspectorScroll;
    private float catalogInspectorOffset;
    private string catalogCategory = "All categories";
    private int catalogFilter;
    private string? selectedCatalogId;
    private RemoteModEntry? retainedCatalogEntry;
    private Guid? selectedDownloadId;
    private bool catalogInspectorOpen;
    private bool catalogNarrow;
    private static readonly string[] CatalogFilters = ["All mods", "Check required", "Installed", "Downloads", "Needs attention", "Official mods"];
    private static string CatalogIdentity(RemoteModEntry entry) => entry.SourceId + ":" + entry.Id;

    internal void ConnectModLibrary(IModLibraryService service)
    {
        modLibrary = service;
        modLibrarySnapshot = service.View;
        ScreenOpened -= RefreshCatalogOnOpen;
        ScreenOpened += RefreshCatalogOnOpen;
        foreach (string id in new[] { "mods-catalog", "mods-downloads" })
        {
            Register(new MenuScreenDefinition
            {
                Id = id,
                Title = id == "mods-catalog" ? "Mods · Catalog" : "Mods · Downloads",
                Help = "Select to inspect. Downloads are checked before a separate Install action.",
                GalleryEligible = true,
                Entries = _ => LibraryEntries(),
                Details = _ => LibraryDetails(),
                BuildContent = (_, panel) => panel.Children.Add(new ModCatalogElement(this))
            });
        }
        Register(new MenuScreenDefinition
        {
            Id = "mods-catalog-search", Title = "Mods · Search catalog", Columns = 8,
            Help = "Enter a search with the controller. Choose Done to return to the catalog.",
            Entries = _ => CatalogSearchEntries(),
            Details = _ => [catalogSearch + "│"]
        });
        Register(new MenuScreenDefinition
        {
            Id = "mods-library-details", Title = "Mods · Details", Help = "Full package and download details.",
            Entries = _ => [],
            BuildContent = (_, panel) => panel.Children.Add(new CatalogDetailsElement(this))
        });
        RegisterModPreviewScreen();
        _view = null;
    }

    private void RefreshCatalogOnOpen(string screen)
    {
        if (screen == "mods-catalog" && modLibrary is { } service && !service.View.Catalog.IsRefreshing && (service.View.Catalog.IsStale || service.View.Catalog.FetchedAt is null))
        {
            service.RefreshCatalog();
        }
    }

    private bool IsLibraryScreen => ScreenId is "mods-catalog" or "mods-downloads";
    private bool IsDownloads => ScreenId == "mods-downloads";
    private ModLibraryJob? SelectedDownload => modLibrarySnapshot?.Jobs.FirstOrDefault(job => job.Id == selectedDownloadId);
    private const string CatalogMemoriaId = "package:Memoria";
    private sealed record CatalogRow(string Id, string Name, string Status, RemoteModEntry? Entry = null);
    private bool IsCatalogMemoriaSelected => selectedCatalogId == CatalogMemoriaId;
    private RemoteModEntry? SelectedCatalog => modLibrarySnapshot?.Catalog.Entries.FirstOrDefault(entry => CatalogIdentity(entry) == selectedCatalogId) ?? (IsCatalogMemoriaSelected ? null : retainedCatalogEntry);
    private ModPackageView? AssociatedMod(RemoteModEntry entry) => modService?.View.Packages.FirstOrDefault(package => package.Package.JunctionMetadata?.Id == entry.Id);
    private ModLibraryInstallation? ManagedMod(RemoteModEntry entry) => modLibrarySnapshot?.Installations.FirstOrDefault(receipt => receipt.SourceId == entry.SourceId && receipt.Id == entry.Id);
    private bool ManagedRepairNeeded(RemoteModEntry entry) => ManagedMod(entry) is { Pending: false, Available: false };
    private bool CatalogUpdateAvailable(RemoteModEntry entry) => ManagedMod(entry) is { Pending: false } receipt && entry.CompareVersion(receipt.Version) is > 0;
    private bool CatalogEntryListed(RemoteModEntry entry) => modLibrarySnapshot?.Catalog.Entries.Any(item => CatalogIdentity(item) == CatalogIdentity(entry)) == true;
    private ModLibraryJob? EntryJob(RemoteModEntry entry) => modLibrarySnapshot?.Jobs.LastOrDefault(job => CatalogIdentity(job.Entry) == CatalogIdentity(entry) && (job.Entry.Version == entry.Version || entry.CompareVersion(job.Entry.Version) == 0) && job.Phase is not ("Cancelled" or "Installed"));
    private bool CanDownload(RemoteModEntry entry) => entry.Official is null ? modLibrarySnapshot?.TargetSupported == true : modLibrarySnapshot?.OfficialSupported == true && entry.CompatibilityError is null && entry.Links.Count > 0;

    private string CatalogStatus(RemoteModEntry entry)
    {
        if (ManagedMod(entry)?.PendingRemoval == true)
        {
            return "Uninstall pending | Waiting to apply";
        }
        if (entry.Official is null)
        {
            return LegacyCatalogStatus(entry);
        }
        if (EntryJob(entry) is { } job)
        {
            return job.Phase;
        }
        if (ManagedMod(entry) is { } local)
        {
            if (local.Pending)
            {
                return SystemText("mods.official.waiting", "Waiting to apply");
            }
            if (!local.Available)
            {
                return SystemText("mods.official.repair", "Repair required");
            }
            string installed = string.IsNullOrEmpty(local.Version) ? SystemText("mods.official.legacy", "Installed · Legacy copy") : $"Installed {local.Version}";
            return CatalogUpdateAvailable(entry) ? installed + " · " + (entry.CompatibilityError ?? SystemText("mods.official.update", "Update available")) : installed;
        }
        return entry.CompatibilityError ?? SystemText("mods.official.new", "New · Download required");
    }

    private string LegacyCatalogStatus(RemoteModEntry entry) => ManagedRepairNeeded(entry) && EntryJob(entry) is null or { Phase: "Installed" } ? "Managed installation missing · Repair required" : EntryJob(entry)?.Phase ?? (!CatalogEntryListed(entry) ? "No longer listed" : ManagedMod(entry) is { } receipt ? receipt.Pending ? "Waiting to apply" : CatalogUpdateAvailable(entry) ? "Update available" : "Installed" : AssociatedMod(entry) is not null ? "Installed manually" : modLibrarySnapshot?.TargetSupported == true ? "Check required" : "Unavailable for this edition");
    private string[] CatalogCategories => ["All categories", .. (modLibrarySnapshot?.Catalog.Entries ?? []).Select(entry => entry.Category).Where(category => category.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(category => category, StringComparer.OrdinalIgnoreCase)];
    private RemoteModEntry[] FilteredCatalog() => (modLibrarySnapshot?.Catalog.Entries ?? []).Select(PresentCatalogEntry).Where(entry =>
        (catalogSearch.Length == 0 || (entry.Name + " " + entry.Author + " " + entry.Category + " " + entry.Description).Contains(catalogSearch, StringComparison.OrdinalIgnoreCase)) &&
        (catalogCategory == "All categories" || entry.Category.Equals(catalogCategory, StringComparison.OrdinalIgnoreCase)) &&
        (catalogFilter switch { 1 => CanDownload(entry) && EntryJob(entry) is null && AssociatedMod(entry) is null && ManagedMod(entry) is null, 2 => AssociatedMod(entry) is not null || ManagedMod(entry) is not null, 3 => EntryJob(entry) is not null, 4 => EntryJob(entry)?.Error is not null, 5 => entry.Official is not null, _ => true }))
        .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase).ThenBy(CatalogIdentity, StringComparer.Ordinal).ToArray();

    private CatalogRow[] CatalogRows()
    {
        RemoteModEntry[] filtered = FilteredCatalog();
        List<CatalogRow> rows = [];
        if (filtered.Any(entry => entry.Official is not null))
        {
            rows.Add(new(CatalogMemoriaId, ModRuntime.MemoriaPackageId, MemoriaAvailability("Memoria")));
        }
        rows.AddRange(filtered.Where(entry => entry.Official is null).Select(entry => new CatalogRow(CatalogIdentity(entry), entry.Name, CatalogStatus(entry), entry)));
        return rows.ToArray();
    }

    private IReadOnlyList<MenuEntry> LibraryEntries() => IsDownloads
        ? (modLibrarySnapshot?.Jobs ?? []).Select(job => new MenuEntry(job.Id.ToString(), job.Entry.Name, job.Phase)).ToArray()
        : CatalogRows().Select(row => new MenuEntry(row.Id, row.Name, row.Status)).ToArray();

    private IReadOnlyList<string> LibraryDetails()
    {
        if (!IsDownloads && IsCatalogMemoriaSelected && selectedDownloadId is null)
        {
            return [ModRuntime.MemoriaPackageId, MemoriaAvailability("Memoria"), .. OfficialEntries.Select(PresentCatalogEntry).Select(entry => entry.Name + " · " + CatalogStatus(entry))];
        }
        ModLibraryJob? job = IsDownloads || ScreenId == "mods-library-details" && selectedDownloadId is not null ? SelectedDownload : SelectedCatalog is { } selected ? EntryJob(selected) : null;
        RemoteModEntry? entry = job?.Entry ?? SelectedCatalog;
        if (entry is null)
        {
            return ["Select a mod to inspect its requirements."];
        }
        entry = PresentCatalogEntry(entry);
        List<string> details = [entry.Name, entry.Author, "Available version: " + entry.Version, "Category: " + entry.Category, "Source: " + entry.SourceId, "Upstream edition: " + entry.Edition, "Language: " + entry.Language, "Engine support: package check required", entry.Description];
        if (entry.CompatibilityError is not null)
        {
            details.Add(entry.CompatibilityError);
        }
        if (!CatalogEntryListed(entry))
        {
            details.Add("No longer listed in the catalog. Existing local installations and jobs are preserved.");
        }
        if (job is not null)
        {
            details.Add(job.Phase + " · " + DownloadProgress(job));
            details.Add(job.Details ?? "");
            details.Add(job.Error ?? "");
            if (job.CanOpenDownloadPage)
            {
                details.Add("This provider needs an interactive download. Open download page explicitly; a standalone .iroj can be installed in Mods/Junction afterward.");
            }
        }
        if (AssociatedMod(entry) is { } local)
        {
            details.Add("Local match: " + local.Package.Name + " · " + ModStatusText(local));
            details.Add("Installed version: " + (local.Package.JunctionMetadata?.Version ?? "unknown"));
            details.Add("Enabled in settings: " + (local.Package.RequestedEnabled ? "On" : "Off"));
            if (ManagedMod(entry) is null)
            {
                details.Add("Association does not authorize replacing a manual installation.");
            }
        }
        if (ManagedMod(entry) is { } receipt)
        {
            details.Add("Managed version: " + receipt.Version + (receipt.Pending ? " · Waiting to apply" : ""));
            if (!receipt.Available)
            {
                details.Add("The managed package is missing. Download and check a repair before installing.");
            }
            details.Add(receipt.Error ?? "");
        }
        details.Add(modLibrarySnapshot?.Error ?? "");
        details.Add(modLibrarySnapshot?.Catalog.Error ?? "");
        if (!string.IsNullOrWhiteSpace(entry.Requirements))
        {
            details.Add("Requirements: " + entry.Requirements);
        }
        if (!string.IsNullOrWhiteSpace(entry.ReleaseNotes))
        {
            details.Add("Release notes: " + entry.ReleaseNotes);
        }
        details.Add(entry.UnsupportedMetadata);
        return details.Where(line => line.Length > 0).ToArray();
    }

    private IReadOnlyList<MenuEntry> CatalogSearchEntries()
    {
        var page = NamePages[catalogAlphabet];
        return
        [
            .. page.Characters.Where(character => character != '\0' && character != ' ').Distinct().Select((letter, index) => new MenuEntry("letter-" + index, letter.ToString(), "Add to search.", Action: new("mod-catalog-letter", Value: letter.ToString()))),
            new("space", "Space", "Add a space.", Action: new("mod-catalog-letter", Value: " ")),
            new("alphabet", page.Title, "Change character page.", Action: new("mod-catalog-alphabet")),
            new("delete", "Delete", "Remove the last character.", Action: new("mod-catalog-delete")),
            new("clear", "Clear", "Clear the search.", Action: new("mod-catalog-clear-search")),
            new("done", "Done", "Return to the catalog.", Action: new("menu-back"))
        ];
    }

    private void OpenLibraryDetails()
    {
        catalogDetailsOffset = 0;
        Push("mods-library-details");
    }

    private void BeginCatalogSearch()
    {
        modFocusedControlId = "library-search";
        if (CurrentControlDevice == ControlDevice.Controller)
        {
            Push("mods-catalog-search");
        }
        else
        {
            catalogSearchEditor.SetText(catalogSearch);
            catalogSearchEditor.Select(catalogSearch.Length, 0);
            catalogSearchEditor.Changed = () => ChangeCatalogSearch(catalogSearchEditor.Text);
            catalogSearchEditing = true;
            _view = null;
        }
    }

    private void ChangeCatalogSearch(string search)
    {
        catalogSearch = search;
        retainedCatalogEntry = null;
        selectedCatalogId = null;
        _view = null;
    }

    private bool EnterCatalogSearchCharacter(char character)
    {
        if (!IsCatalogSearchEditing)
        {
            return false;
        }
        if (catalogSearchEditing && ScreenId == "mods-catalog")
        {
            if (character == '\b')
            {
                catalogSearchEditor.Command(UiEditCommand.Backspace);
            }
            else if (!char.IsControl(character) && catalogSearch.Length - catalogSearchEditor.SelectionLength < 100)
            {
                catalogSearchEditor.Insert(character.ToString());
            }
            return true;
        }
        if (character == '\b' && catalogSearch.Length > 0)
        {
            int[] characters = System.Globalization.StringInfo.ParseCombiningCharacters(catalogSearch);
            ChangeCatalogSearch(catalogSearch[..characters[^1]]);
        }
        else if (!char.IsControl(character) && catalogSearch.Length < 100)
        {
            ChangeCatalogSearch(catalogSearch + character);
        }
        return true;
    }

    private void RefreshLibraryView()
    {
        if (modLibrary is null || modLibrarySnapshot?.Revision == modLibrary.View.Revision)
        {
            return;
        }
        modLibrarySnapshot = modLibrary.View;
        if (ScreenId == "mod-settings")
        {
            RefreshModSettingsRows();
        }
        if (IsLibraryScreen || ScreenId is "mods" or "mods-library-details")
        {
            _view = null;
        }
    }

    private static string DownloadProgress(ModLibraryJob job)
    {
        static string Bytes(double value) => value >= 1073741824 ? $"{value / 1073741824:0.00} GiB" : $"{value / 1048576:0.0} MiB";
        string progress = job.TotalBytes is > 0 ? $"{Bytes(job.ReceivedBytes)} / {Bytes(job.TotalBytes.Value)}" : Bytes(job.ReceivedBytes) + " received · total unknown";
        if (job.BytesPerSecond > 0)
        {
            progress += " · " + Bytes(job.BytesPerSecond) + "/s";
            if (job.Phase == "Downloading" && job.TotalBytes is { } total && total > job.ReceivedBytes)
            {
                double remaining = (total - job.ReceivedBytes) / job.BytesPerSecond;
                progress += remaining < 60 ? " · less than a minute remaining" : $" · about {Math.Ceiling(remaining / 60):0} min remaining";
            }
        }
        return progress;
    }
}
