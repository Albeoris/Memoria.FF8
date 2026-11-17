using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Memoria.FF8;
using Memoria.FF8.Mods;
using Memoria.FF8.Platform;

namespace Memoria.Launcher;

internal sealed partial class LauncherWindow
{
    private readonly ListBox launcherModRows = new() { MinWidth = 180, Background = LauncherSurface, BorderBrush = LauncherBorder, BorderThickness = new Thickness(1) };
    private readonly StackPanel launcherModDetails = new() { Spacing = 12 };
    private readonly ScrollViewer launcherModDetailScroll = new() { VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto };
    private readonly StackPanel launcherModCompletion = new() { Spacing = 6 };
    private readonly TextBlock launcherModActionStatus = new() { Foreground = LauncherMuted, TextWrapping = TextWrapping.Wrap, MaxLines = 2, TextTrimming = TextTrimming.WordEllipsis, IsVisible = false };
    private readonly Dictionary<string, ListBoxItem> launcherModItems = new(StringComparer.Ordinal);
    private LauncherModRow[] launcherModVisible = [];
    private string? launcherModSelectedKey;
    private bool launcherModChangingRows;

    private sealed record LauncherModRow(string Key, string Name, string Description, string Category, ModPackageView? Installed, RemoteModEntry? Entry);

    internal sealed record LauncherModCategory(string Name, int Count)
    {
        public override string ToString() => $"{Name} ({Count})";
    }
    private readonly TextBox launcherModSearch = new() { Watermark = "Search mods...", MinWidth = 220 };
    private readonly ComboBox launcherModCategory = new() { MinWidth = 160, ItemsSource = new[] { "All categories" }, SelectedIndex = 0 };
    private readonly TextBlock launcherModSummary = new() { Foreground = LauncherMuted, TextWrapping = TextWrapping.Wrap, MaxLines = 2, TextTrimming = TextTrimming.WordEllipsis };
    private readonly TabControl launcherModTabs = new();
    private readonly DispatcherTimer launcherModTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private ModRuntime? launcherMods;
    private ModLibraryService? launcherModLibrary;
    private string? launcherModIdentity;
    private long launcherModRevision = -1;
    private long launcherModGeneration = -1;
    private int launcherModInitialization;
    private bool launcherModChangingFilter;
    private Task launcherModStopping = Task.CompletedTask;

    private Control CreateModsPage()
    {
        Grid page = new() { RowDefinitions = new RowDefinitions("Auto,*"), RowSpacing = 10 };
        StackPanel header = new() { Spacing = 6 };
        Grid heading = new() { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 8 };
        heading.Children.Add(new TextBlock { Text = "Mods", FontSize = 25, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        Button refresh = new() { Content = "Refresh mods", HorizontalAlignment = HorizontalAlignment.Right, MinHeight = 0, Padding = new Thickness(10, 5) };
        refresh.Click += async (_, _) => await CheckOfficialModsAsync(force: true);
        Grid.SetColumn(refresh, 1);
        heading.Children.Add(refresh);
        header.Children.Add(heading);
        launcherModTabs.FontSize = 14;
        launcherModTabs.MinHeight = 0;
        launcherModTabs.Height = 34;
        launcherModTabs.ItemsSource = new[] { new TabItem { Header = "Installed", FontSize = 14, MinHeight = 0, Height = 34, Padding = new Thickness(8, 4) }, new TabItem { Header = "Catalog", FontSize = 14, MinHeight = 0, Height = 34, Padding = new Thickness(8, 4) } };
        launcherModTabs.SelectionChanged += (_, _) => RenderLauncherMods();
        launcherModTabs.SelectedIndex = 0;
        Grid filters = new() { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 12 };
        filters.Children.Add(launcherModSearch);
        Grid.SetColumn(launcherModCategory, 1);
        filters.Children.Add(launcherModCategory);
        launcherModSearch.TextChanged += (_, _) => RenderLauncherMods();
        launcherModCategory.SelectionChanged += (_, _) =>
        {
            if (!launcherModChangingFilter)
            {
                RenderLauncherMods();
            }
        };
        header.Children.Add(launcherModTabs);
        header.Children.Add(filters);
        header.Children.Add(launcherModCompletion);
        header.Children.Add(launcherModSummary);
        header.Children.Add(launcherModActionStatus);
        page.Children.Add(header);
        Grid collection = new() { ColumnDefinitions = new ColumnDefinitions("2*,10,3*") };
        collection.ColumnDefinitions[0].MinWidth = 180;
        collection.ColumnDefinitions[2].MinWidth = 220;
        collection.Children.Add(launcherModRows);
        GridSplitter splitter = new() { ResizeDirection = GridResizeDirection.Columns, HorizontalAlignment = HorizontalAlignment.Stretch, Background = LauncherBorder };
        Grid.SetColumn(splitter, 1);
        collection.Children.Add(splitter);
        launcherModDetailScroll.Content = launcherModDetails;
        Grid.SetColumn(launcherModDetailScroll, 2);
        collection.Children.Add(launcherModDetailScroll);
        Grid.SetRow(collection, 1);
        page.Children.Add(collection);
        launcherModRows.SelectionChanged += (_, _) =>
        {
            if (!launcherModChangingRows && launcherModRows.SelectedItem is ListBoxItem { Tag: string key })
            {
                launcherModSelectedKey = key;
                launcherModDetailScroll.Offset = default;
                RenderLauncherModDetails();
            }
        };
        launcherModTimer.Tick += (_, _) => PollLauncherMods();
        return page;
    }

    private void UpdateOfficialModOverview()
    {
        RenderLauncherMods();
    }

    private async Task InitializeLauncherModsAsync(bool refresh = false)
    {
        if (busy || runningGame is not null)
        {
            return;
        }
        if (installations.SelectedItem is not GameInstallation selected)
        {
            await StopLauncherModLibraryAsync();
            RenderLauncherMods();
            return;
        }
        if (EffectiveResourceLanguage is not { } language)
        {
            await StopLauncherModLibraryAsync();
            RenderLauncherMods();
            launcherModSummary.Text = "Choose an installation with supported game language resources to manage mods.";
            return;
        }
        var edition = GameInstallationSelection.ResolveEdition(selected.Directory);
        string root = ApplicationInstallation.ResolveModsDirectory(AppContext.BaseDirectory, HostPlatforms.Current, ApplicationPaths.Current.Configuration);
        string identity = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(root + "\0" + Path.GetFullPath(selected.Directory) + "\0" + edition + "\0" + language)))[..16].ToLowerInvariant();
        if (identity == launcherModIdentity && launcherModLibrary is not null)
        {
            if (refresh)
            {
                launcherModLibrary.RefreshCatalog();
                launcherMods?.Refresh();
            }
            return;
        }
        int initialization = ++launcherModInitialization;
        await StopLauncherModLibraryAsync(invalidateInitialization: false);
        if (initialization != launcherModInitialization || busy || runningGame is not null || !Equals(installations.SelectedItem, selected))
        {
            return;
        }
        try
        {
            string profile = $"{edition}/{language}/{identity}";
            bool junction = ModCompatibility.IsCompatible(edition, ModFormat.Junction) && language.Equals("en", StringComparison.OrdinalIgnoreCase);
            ModPackageInstaller installer = new(ModDirectoryResourceLease.StateDirectory(root), root, profile, junction);
            // Apply managed transactions only at the same exclusive boundary used by game startup.
            await Task.Run(() =>
            {
                using ModDirectoryResourceLease lease = ModDirectoryResourceLease.Acquire(root, true);
                if (lease.IsWriter)
                {
                    installer.RecoverAndApply();
                }
            });
            if (initialization != launcherModInitialization || busy || runningGame is not null || !Equals(installations.SelectedItem, selected))
            {
                return;
            }
            launcherMods = new ModRuntime(ApplicationInstallation.DataDirectory, format => ModCompatibility.IsCompatible(edition, format), watch: false, additionalBaseDirectories: [selected.Directory], modsDirectory: root, edition: edition.ToString(), language: language);
            launcherMods.PreparePending(true);
            launcherMods.CommitPending();
            launcherModLibrary = new ModLibraryService(Path.Combine(ApplicationPaths.Current.Cache, "mod-library", identity), installer, launcherMods, profile, junction, openDownloadPage: OpenLauncherModDownloadPage, officialCacheDirectory: OfficialModCatalog.DefaultCacheDirectory, officialSupported: true);
            launcherModIdentity = identity;
            launcherModLibrary.RefreshCatalog();
            launcherModTimer.Start();
            PollLauncherMods();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException or InvalidOperationException)
        {
            launcherModSummary.Text = "Could not open the mod library. " + exception.Message;
            launcherModSummary.Foreground = LauncherProblem;
        }
    }

    private async Task StopLauncherModLibraryAsync(bool invalidateInitialization = true)
    {
        if (invalidateInitialization)
        {
            launcherModInitialization++;
        }
        launcherModTimer.Stop();
        ModLibraryService? library = launcherModLibrary;
        ModRuntime? mods = launcherMods;
        launcherModLibrary = null;
        launcherMods = null;
        launcherModIdentity = null;
        launcherModActionStatus.Text = "";
        launcherModActionStatus.IsVisible = false;
        launcherModRevision = -1;
        launcherModGeneration = -1;
        if (library is not null)
        {
            launcherModStopping = library.StopAsync();
        }
        await launcherModStopping;
        mods?.Dispose();
    }

    private void PollLauncherMods()
    {
        if (launcherMods is null || launcherModLibrary is null)
        {
            return;
        }
        launcherMods.PreparePending();
        launcherMods.CommitPending();
        ModLibrarySnapshot snapshot = launcherModLibrary.View;
        if (snapshot.Revision != launcherModRevision || launcherMods.Catalog.Generation != launcherModGeneration)
        {
            launcherModRevision = snapshot.Revision;
            launcherModGeneration = launcherMods.Catalog.Generation;
            RenderLauncherMods();
        }
    }

    private IReadOnlyList<ModPackageView> LauncherInstalledModViews()
    {
        if (launcherMods is null)
        {
            return [];
        }
        ModServiceView grouped = launcherMods.View;
        return ModServiceView.Create(launcherMods.Catalog, launcherMods.InstalledPackages, grouped.AutoEnableNewMods, grouped.IsReloadPending, grouped.Diagnostics, grouped.Outcome).Packages;
    }

    private void RenderLauncherMods()
    {
        launcherModCompletion.Children.Clear();
        if (launcherMods is null || launcherModLibrary is null)
        {
            launcherModRows.ItemsSource = null;
            launcherModItems.Clear();
            launcherModVisible = [];
            launcherModDetails.Children.Clear();
            launcherModSummary.Text = runningGame is not null ? "Close the running game to manage mods here, or use Mods in the game menu." : "Choose an installation to open your mod collection.";
            return;
        }
        ModLibrarySnapshot snapshot = launcherModLibrary.View;
        string category = (launcherModCategory.SelectedItem as LauncherModCategory)?.Name ?? "All categories";
        string search = launcherModSearch.Text?.Trim() ?? "";
        bool installedTab = launcherModTabs.SelectedIndex == 0;
        IReadOnlyList<ModPackageView> packages = LauncherInstalledModViews();
        string[] target = snapshot.TargetProfile.Split('/');
        RemoteModEntry[] entries = LauncherKnownModEntries(snapshot).Where(entry => entry.Official is null || OfficialModCatalog.TargetMatches(entry.Official.Target, target[0], target[1])).ToArray();
        LauncherModRow[] rows = installedTab
            ? packages.Where(package => !package.Package.IsSettingsGroup).Select(package =>
            {
                RemoteModEntry? entry = entries.FirstOrDefault(entry => FindLauncherPackage(entry, [package]) is not null);
                return new LauncherModRow("installed:" + package.Package.Id, package.Package.Name, entry?.Description ?? package.Package.Descriptor?.DescriptionForLanguage(SelectedResourceLanguage) ?? "", entry?.Category ?? package.Package.Format.ToString(), package, entry);
            }).ToArray()
            : entries.Select(entry => new LauncherModRow(entry.SourceId + ":" + entry.Id, entry.Name, entry.Description, entry.Category, FindLauncherPackage(entry, packages), entry)).ToArray();
        LauncherModRow[] searched = rows.Where(row => MatchesLauncherModFilter(row.Name, row.Description, row.Category, search, "All categories")).ToArray();
        LauncherModCategory[] categories = CreateLauncherModCategories(searched.Select(row => row.Category));
        launcherModChangingFilter = true;
        launcherModCategory.ItemsSource = categories;
        launcherModCategory.SelectedItem = categories.FirstOrDefault(item => item.Name.Equals(category, StringComparison.OrdinalIgnoreCase)) ?? categories[0];
        launcherModChangingFilter = false;
        category = ((LauncherModCategory)launcherModCategory.SelectedItem!).Name;
        launcherModVisible = searched.Where(row => category == "All categories" || row.Category.Equals(category, StringComparison.OrdinalIgnoreCase)).ToArray();
        launcherModSummary.Foreground = snapshot.Error is null && snapshot.Catalog.Error is null ? LauncherMuted : LauncherProblem;
        launcherModSummary.Text = snapshot.Error ?? snapshot.Catalog.Error ?? (snapshot.Catalog.IsRefreshing ? "Refreshing catalog..." : $"{launcherModVisible.Length} matching mods | {snapshot.Jobs.Count(job => job.CanPause || job.CanResume || job.CanInstall)} active downloads");
        RemoteModEntry[] missing = entries.Where(entry => IsLauncherDownloadComponent(entry) && FindLauncherPackage(entry, packages) is null).ToArray();
        if (missing.Length > 0)
        {
            int eligible = missing.Count(entry => LauncherModDownloadAllowed(entry, snapshot));
            Button complete = new() { Content = $"Download missing components ({eligible})", HorizontalAlignment = HorizontalAlignment.Left, MinHeight = 0, Padding = new Thickness(10, 5), IsEnabled = !busy && eligible > 0 };
            complete.Click += (_, _) => QueueMissingLauncherMods(missing, snapshot);
            launcherModCompletion.Children.Add(complete);
            string[] reasons = missing.Select(entry => LauncherModDownloadBlocker(entry, snapshot)).OfType<string>().Distinct().ToArray();
            if (reasons.Length > 0)
            {
                TextBlock unavailable = LauncherDescription($"{missing.Length - eligible} unavailable: " + string.Join(" ", reasons));
                unavailable.MaxLines = 1;
                unavailable.TextTrimming = TextTrimming.WordEllipsis;
                ToolTip.SetTip(unavailable, string.Join("\n", reasons));
                launcherModCompletion.Children.Add(unavailable);
            }
        }
        string[] keys = launcherModVisible.Select(row => row.Key).ToArray();
        bool changed = !launcherModItems.Keys.SequenceEqual(keys);
        launcherModChangingRows = true;
        if (changed)
        {
            launcherModItems.Clear();
            foreach (LauncherModRow row in launcherModVisible)
            {
                launcherModItems.Add(row.Key, new ListBoxItem { Tag = row.Key, Padding = new Thickness(10, 8), HorizontalContentAlignment = HorizontalAlignment.Stretch });
            }
            launcherModRows.ItemsSource = launcherModItems.Values.ToArray();
        }
        foreach (LauncherModRow row in launcherModVisible)
        {
            ModLibraryJob? job = row.Entry is null ? null : snapshot.Jobs.FirstOrDefault(item => item.Entry.Id == row.Entry.Id && item.Entry.SourceId == row.Entry.SourceId);
            StackPanel label = new() { Spacing = 3 };
            label.Children.Add(new TextBlock { Text = row.Name, TextWrapping = TextWrapping.Wrap, FontWeight = FontWeight.SemiBold });
            label.Children.Add(new TextBlock { Text = job is null ? row.Installed?.StatusText ?? row.Category : LauncherModJobStatus(job), Foreground = job?.Error is null ? LauncherMuted : LauncherProblem, TextWrapping = TextWrapping.Wrap, FontSize = 12 });
            launcherModItems[row.Key].Content = label;
        }
        string? previousSelection = launcherModSelectedKey;
        launcherModSelectedKey = SelectLauncherModKey(keys, launcherModSelectedKey);
        if (previousSelection != launcherModSelectedKey)
        {
            launcherModDetailScroll.Offset = default;
        }
        launcherModRows.SelectedItem = launcherModSelectedKey is null ? null : launcherModItems[launcherModSelectedKey];
        launcherModChangingRows = false;
        RenderLauncherModDetails();
    }

    internal static IEnumerable<RemoteModEntry> LauncherKnownModEntries(ModLibrarySnapshot snapshot)
    {
        IEnumerable<RemoteModEntry> retained = snapshot.Jobs.Where(job => job.CanDeleteDownload || job.CanPause || job.CanResume || job.CanInstall || snapshot.Installations.Any(installed => installed.SourceId == job.Entry.SourceId && installed.Id == job.Entry.Id && installed.Available)).Select(job => job.Entry with { IsStale = true });
        return snapshot.Catalog.Entries.Concat(retained).DistinctBy(entry => (entry.SourceId, entry.Id));
    }

    internal static LauncherModCategory[] CreateLauncherModCategories(IEnumerable<string> categories)
    {
        string[] values = categories.ToArray();
        return new[] { new LauncherModCategory("All categories", values.Length) }.Concat(values.Where(value => value.Length > 0).GroupBy(value => value, StringComparer.OrdinalIgnoreCase).OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase).Select(group => new LauncherModCategory(group.Key, group.Count()))).ToArray();
    }

    internal static string? SelectLauncherModKey(IReadOnlyList<string> keys, string? selected)
    {
        return selected is not null && keys.Contains(selected) ? selected : keys.FirstOrDefault();
    }

    private static string LauncherModJobStatus(ModLibraryJob job)
    {
        return job.Phase + (job.TotalBytes is > 0 ? $" | {job.ReceivedBytes * 100 / job.TotalBytes}%" : job.ReceivedBytes > 0 ? $" | {job.ReceivedBytes / (1024 * 1024)} MB" : "");
    }

    private void RenderLauncherModDetails()
    {
        Vector offset = launcherModDetailScroll.Offset;
        launcherModDetails.Children.Clear();
        LauncherModRow? selected = launcherModVisible.FirstOrDefault(row => row.Key == launcherModSelectedKey);
        if (selected is not null && launcherModLibrary is not null)
        {
            launcherModDetails.Children.Add(CreateLauncherModCard(selected.Installed, selected.Entry, launcherModLibrary.View));
        }
        else
        {
            launcherModDetails.Children.Add(LauncherDescription("No mods match your filters."));
        }
        launcherModDetailScroll.Offset = offset;
    }

    private void QueueMissingLauncherMods(RemoteModEntry[] missing, ModLibrarySnapshot snapshot)
    {
        RemoteModEntry[] allowed = missing.Where(entry => LauncherModDownloadAllowed(entry, snapshot)).ToArray();
        foreach (RemoteModEntry entry in allowed)
        {
            launcherModLibrary?.Queue(entry);
        }
        int blocked = missing.Length - allowed.Length;
        launcherModActionStatus.IsVisible = true;
        launcherModActionStatus.Text = allowed.Length == 0
            ? "No eligible downloads. Select a component to see the reason."
            : $"Requested {allowed.Length} downloads." + (blocked > 0 ? $" Unavailable: {blocked}." : "");
        if (allowed.Length > 0)
        {
            launcherModSearch.Text = "";
            launcherModChangingFilter = true;
            launcherModCategory.SelectedIndex = 0;
            launcherModChangingFilter = false;
            launcherModSelectedKey = allowed[0].SourceId + ":" + allowed[0].Id;
        }
        launcherModTabs.SelectedIndex = 1;
        RenderLauncherMods();
    }

    internal static bool MatchesLauncherModFilter(string name, string description, string category, string search, string selectedCategory)
    {
        return (selectedCategory == "All categories" || category.Equals(selectedCategory, StringComparison.OrdinalIgnoreCase)) && (search.Length == 0 || name.Contains(search, StringComparison.OrdinalIgnoreCase) || description.Contains(search, StringComparison.OrdinalIgnoreCase));
    }

    private static ModPackageView? FindLauncherPackage(RemoteModEntry entry, IReadOnlyList<ModPackageView> packages)
    {
        return packages.FirstOrDefault(view => entry.Official is { } component
            ? OfficialModCatalog.SameComponent(view.Package.Official?.Id, component.Id) || OfficialModCatalog.SameComponent(view.Package.Descriptor?.Id, component.Id) || view.Package.Id == component.RuntimePackageId
            : view.Package.JunctionMetadata?.Id == entry.Id);
    }

    internal static bool IsLauncherDownloadComponent(RemoteModEntry entry)
    {
        return entry.Official?.Delivery == "download";
    }

    internal static string? LauncherModDownloadBlocker(RemoteModEntry entry, ModLibrarySnapshot snapshot)
    {
        if (entry.IsStale)
        {
            return "Refresh the catalog before downloading stale entries.";
        }
        if (entry.Official is { Delivery: "engine" })
        {
            return "This component is included with the engine.";
        }
        if (entry.CompatibilityError is not null)
        {
            return entry.CompatibilityError;
        }
        if (entry.Links.Count == 0)
        {
            return "No download is published. Refresh the catalog when online.";
        }
        if (entry.Official is null ? !snapshot.TargetSupported : !snapshot.OfficialSupported)
        {
            return "Downloads are unavailable for the selected installation.";
        }
        return null;
    }

    private static bool LauncherModDownloadAllowed(RemoteModEntry entry, ModLibrarySnapshot snapshot)
    {
        return LauncherModDownloadBlocker(entry, snapshot) is null;
    }

    private Control CreateLauncherModCard(ModPackageView? installed, RemoteModEntry? entry, ModLibrarySnapshot snapshot)
    {
        StackPanel body = new() { Spacing = 8 };
        string description = entry?.Description ?? installed?.Package.Descriptor?.DescriptionForLanguage(SelectedResourceLanguage) ?? $"{installed?.Package.Format} mod";
        TextBlock summary = LauncherDescription(description);
        ModLibraryInstallation? managed = entry is null ? null : snapshot.Installations.FirstOrDefault(item => item.Id == entry.Id && item.SourceId == entry.SourceId);
        ModLibraryJob? job = entry is null ? null : snapshot.Jobs.FirstOrDefault(item => item.Entry.Id == entry.Id && item.Entry.SourceId == entry.SourceId);
        string state = managed is { PendingRemoval: true } ? "Removal scheduled for next game start" : managed is { Pending: true } ? "Ready for next game start" : installed?.StatusText ?? "Not installed";
        if (job is not null && managed is not { Pending: true } && managed is not { PendingRemoval: true })
        {
            state = LauncherModJobStatus(job);
        }
        TextBlock badge = LauncherDescription(state);
        badge.Foreground = installed?.Package.RequestedEnabled == true && installed.NeedsAttention == false && job is null ? LauncherSuccess : LauncherMuted;
        body.Children.Add(badge);
        if (job is { CanPause: true })
        {
            body.Children.Add(new ProgressBar { Minimum = 0, Maximum = 100, Value = job.TotalBytes is > 0 ? job.ReceivedBytes * 100d / job.TotalBytes.Value : 0, IsIndeterminate = job.TotalBytes is null or <= 0, Height = 6 });
        }
        string? problem = job?.Error ?? managed?.Error ?? installed?.Problem ?? (entry is null || installed is not null ? null : LauncherModDownloadBlocker(entry, snapshot));
        if (!string.IsNullOrEmpty(problem))
        {
            TextBlock error = LauncherDescription(problem);
            error.Foreground = entry?.Official is { Delivery: "engine" } && job?.Error is null && installed?.Problem is null ? LauncherMuted : LauncherProblem;
            body.Children.Add(error);
        }
        WrapPanel actions = new() { Orientation = Orientation.Horizontal };
        if (installed is not null)
        {
            CheckBox enabled = new() { Content = "Enabled", IsChecked = installed.Package.RequestedEnabled, IsEnabled = !busy && installed.CanToggle, Margin = new Thickness(0, 0, 16, 0) };
            enabled.IsCheckedChanged += async (_, _) =>
            {
                if (launcherMods is null || busy)
                {
                    return;
                }
                ModOperationResult result = launcherMods.SetEnabled(installed.Package.Id, enabled.IsChecked == true);
                if (!result.Success)
                {
                    await LauncherDialogs.ShowAsync(this, "Could not change mod", result.Error ?? "The preference could not be saved.");
                }
                PollLauncherMods();
                RenderLauncherMods();
            };
            actions.Children.Add(enabled);
        }
        void AddAction(string label, Action action)
        {
            Button button = new() { Content = label, IsEnabled = !busy, Margin = new Thickness(0, 0, 8, 0) };
            button.Click += (_, _) =>
            {
                button.IsEnabled = false;
                action();
                launcherModActionStatus.IsVisible = true;
                launcherModActionStatus.Text = label + " requested.";
            };
            actions.Children.Add(button);
        }
        if (entry is not null && managed is not { Pending: true } && (installed is null || managed is not null && entry.CompareVersion(managed.Version) is > 0) && LauncherModDownloadAllowed(entry, snapshot) && job is not { CanPause: true } and not { CanResume: true } and not { CanInstall: true })
        {
            AddAction(installed is null ? "Download" : "Update", () => launcherModLibrary?.Queue(entry));
        }
        if (entry is not null && managed is { CanUninstall: true })
        {
            AddAction("Uninstall", () => launcherModLibrary?.Uninstall(entry));
        }
        if (job is not null)
        {
            if (job.CanDeleteDownload)
            {
                AddAction("Delete downloaded archive", () => launcherModLibrary?.DeleteDownload(job.Id));
            }
            if (job.CanPause)
            {
                AddAction("Pause", () => launcherModLibrary?.Pause(job.Id));
            }
            if (job.CanResume)
            {
                AddAction("Resume", () => launcherModLibrary?.Resume(job.Id));
            }
            if (job.CanInstall)
            {
                AddAction("Install", () => launcherModLibrary?.Install(job.Id));
            }
            if (job.CanRetry)
            {
                AddAction("Retry", () => launcherModLibrary?.Retry(job.Id));
            }
            if (job.CanOpenDownloadPage)
            {
                AddAction("Open download page", () => launcherModLibrary?.OpenDownloadPage(job.Id));
            }
            if (job.CanCancel)
            {
                AddAction("Cancel", () => launcherModLibrary?.Cancel(job.Id));
            }
        }
        body.Children.Add(actions);
        if (!string.IsNullOrWhiteSpace(job?.Details))
        {
            body.Children.Add(LauncherDescription(job.Details));
        }
        body.Children.Add(summary);
        if (entry is not null)
        {
            body.Children.Add(LauncherDescription($"Version {entry.Version} | {entry.Category}\nEdition: {entry.Edition} | Language: {entry.Language} | Size: {entry.PublishedSize}"));
            if (!string.IsNullOrWhiteSpace(entry.ReleaseNotes))
            {
                body.Children.Add(LauncherDescription(entry.ReleaseNotes));
            }
            if (!string.IsNullOrWhiteSpace(entry.Requirements))
            {
                body.Children.Add(LauncherDescription(entry.Requirements));
            }
        }
        return LauncherCard(entry?.Name ?? installed!.Package.Name, entry is null ? null : entry.Author, body);
    }

    private static void OpenLauncherModDownloadPage(Uri uri)
    {
        if (uri.Scheme is not ("https" or "http") || !string.IsNullOrEmpty(uri.UserInfo))
        {
            throw new InvalidOperationException("This download page address is unsupported.");
        }
        ProcessStartInfo start = OperatingSystem.IsWindows() ? new(uri.AbsoluteUri) { UseShellExecute = true } : new(OperatingSystem.IsMacOS() ? "open" : "xdg-open") { UseShellExecute = false, CreateNoWindow = true };
        if (!OperatingSystem.IsWindows())
        {
            start.ArgumentList.Add(uri.AbsoluteUri);
        }
        Process.Start(start)?.Dispose();
    }
}
