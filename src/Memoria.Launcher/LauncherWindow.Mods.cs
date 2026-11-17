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
    private readonly StackPanel launcherModRows = new() { Spacing = 12 };
    private readonly TextBox launcherModSearch = new() { Watermark = "Search mods…", MinWidth = 220 };
    private readonly ComboBox launcherModCategory = new() { MinWidth = 160, ItemsSource = new[] { "All categories" }, SelectedIndex = 0 };
    private readonly TextBlock launcherModSummary = new() { Foreground = LauncherMuted, TextWrapping = TextWrapping.Wrap };
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
        StackPanel page = LauncherPage("Mods", "Build your collection for the selected FINAL FANTASY VIII installation.");
        launcherModTabs.ItemsSource = new[] { new TabItem { Header = "Installed" }, new TabItem { Header = "Catalog" } };
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
        page.Children.Add(launcherModTabs);
        page.Children.Add(filters);
        page.Children.Add(CreateOfficialModsPanel());
        page.Children.Add(launcherModSummary);
        page.Children.Add(launcherModRows);
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
        string language = SelectedResourceLanguage ?? "en";
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
        launcherModRows.Children.Clear();
        if (launcherMods is null || launcherModLibrary is null)
        {
            launcherModSummary.Text = runningGame is not null ? "Close the running game to manage mods here, or use Mods in the game menu." : "Choose an installation to open your mod collection.";
            return;
        }
        ModLibrarySnapshot snapshot = launcherModLibrary.View;
        string category = launcherModCategory.SelectedItem as string ?? "All categories";
        string[] categories = new[] { "All categories" }.Concat(snapshot.Catalog.Entries.Select(entry => entry.Category).Concat(LauncherInstalledModViews().Select(view => view.Package.Format.ToString())).Where(value => value.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value)).ToArray();
        if (!(launcherModCategory.ItemsSource as IEnumerable<string> ?? []).SequenceEqual(categories))
        {
            launcherModChangingFilter = true;
            launcherModCategory.ItemsSource = categories;
            launcherModCategory.SelectedItem = categories.Contains(category) ? category : "All categories";
            launcherModChangingFilter = false;
            category = launcherModCategory.SelectedItem as string ?? "All categories";
        }
        string search = launcherModSearch.Text?.Trim() ?? "";
        bool installedTab = launcherModTabs.SelectedIndex == 0;
        IReadOnlyList<ModPackageView> packages = LauncherInstalledModViews();
        RemoteModEntry[] entries = snapshot.Catalog.Entries.Where(entry => entry.Official is null || OfficialModCatalog.TargetMatches(entry.Official.Target, snapshot.TargetProfile.Split('/')[0], snapshot.TargetProfile.Split('/')[1])).ToArray();
        int total = entries.Count(entry => entry.Official is not null);
        int available = entries.Count(entry => entry.Official is not null && FindLauncherPackage(entry, packages) is not null);
        launcherModSummary.Foreground = snapshot.Error is null && snapshot.Catalog.Error is null ? LauncherMuted : LauncherProblem;
        launcherModSummary.Text = snapshot.Error ?? snapshot.Catalog.Error ?? (snapshot.Catalog.IsRefreshing ? "Refreshing catalog…" : $"{packages.Count(package => !package.Package.IsSettingsGroup)} installed mods · {entries.Length} catalog entries");
        if (total > 0 && available < total)
        {
            StackPanel completion = new() { Spacing = 8 };
            completion.Children.Add(LauncherDescription($"{available} of {total} official components installed. Download individual components below or complete your collection."));
            Button complete = new() { Content = "Download missing components", HorizontalAlignment = HorizontalAlignment.Left, IsEnabled = !busy };
            complete.Click += (_, _) =>
            {
                foreach (RemoteModEntry entry in entries.Where(entry => entry.Official is not null && FindLauncherPackage(entry, packages) is null && LauncherModDownloadAllowed(entry, snapshot)))
                {
                    launcherModLibrary?.Queue(entry);
                }
            };
            completion.Children.Add(complete);
            launcherModRows.Children.Add(LauncherCard("Memoria components", null, completion));
        }
        foreach (ModPackageView package in installedTab ? packages.Where(package => !package.Package.IsSettingsGroup) : [])
        {
            RemoteModEntry? entry = entries.FirstOrDefault(entry => FindLauncherPackage(entry, [package]) is not null);
            if (MatchesLauncherModFilter(package.Package.Name, entry?.Description ?? package.Package.Descriptor?.DescriptionForLanguage(SelectedResourceLanguage) ?? "", entry?.Category ?? package.Package.Format.ToString(), search, category))
            {
                launcherModRows.Children.Add(CreateLauncherModCard(package, entry, snapshot));
            }
        }
        if (!installedTab)
        {
            foreach (RemoteModEntry entry in entries.Where(entry => MatchesLauncherModFilter(entry.Name, entry.Description, entry.Category, search, category)))
            {
                launcherModRows.Children.Add(CreateLauncherModCard(FindLauncherPackage(entry, packages), entry, snapshot));
            }
        }
        if (launcherModRows.Children.Count == 0)
        {
            launcherModRows.Children.Add(LauncherDescription(installedTab ? "No installed mods match your filters." : "No catalog entries match your filters."));
        }
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

    private static bool LauncherModDownloadAllowed(RemoteModEntry entry, ModLibrarySnapshot snapshot)
    {
        return !entry.IsStale && entry.CompatibilityError is null && entry.Links.Count > 0 && (entry.Official is null ? snapshot.TargetSupported : snapshot.OfficialSupported);
    }

    private Control CreateLauncherModCard(ModPackageView? installed, RemoteModEntry? entry, ModLibrarySnapshot snapshot)
    {
        StackPanel body = new() { Spacing = 8 };
        string description = entry?.Description ?? installed?.Package.Descriptor?.DescriptionForLanguage(SelectedResourceLanguage) ?? $"{installed?.Package.Format} mod";
        TextBlock summary = LauncherDescription(description);
        summary.MaxLines = 3;
        summary.TextTrimming = TextTrimming.WordEllipsis;
        body.Children.Add(summary);
        ModLibraryInstallation? managed = entry is null ? null : snapshot.Installations.FirstOrDefault(item => item.Id == entry.Id && item.SourceId == entry.SourceId);
        ModLibraryJob? job = entry is null ? null : snapshot.Jobs.FirstOrDefault(item => item.Entry.Id == entry.Id && item.Entry.SourceId == entry.SourceId);
        string state = managed is { Pending: true } ? "Ready for next game start" : installed?.StatusText ?? "Not installed";
        if (job is not null)
        {
            state = job.Phase + (job.TotalBytes is > 0 ? $" · {job.ReceivedBytes * 100 / job.TotalBytes}%" : job.ReceivedBytes > 0 ? $" · {job.ReceivedBytes / (1024 * 1024)} MB" : "");
        }
        TextBlock badge = LauncherDescription(state);
        badge.Foreground = installed?.Package.RequestedEnabled == true && installed.NeedsAttention == false && job is null ? LauncherSuccess : LauncherMuted;
        body.Children.Add(badge);
        string? problem = job?.Error ?? entry?.CompatibilityError ?? managed?.Error ?? installed?.Problem;
        if (!string.IsNullOrEmpty(problem))
        {
            TextBlock error = LauncherDescription(problem);
            error.Foreground = LauncherProblem;
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
        Button details = new() { Content = "Details", Margin = new Thickness(0, 0, 8, 0) };
        details.Click += async (_, _) => await LauncherDialogs.ShowAsync(this, entry?.Name ?? installed!.Package.Name, description + "\n\n" + (entry is null ? installed?.StatusText : $"Version {entry.Version}\nBy {entry.Author}\nCategory: {entry.Category}\nEdition: {entry.Edition}\nLanguage: {entry.Language}\nSize: {entry.PublishedSize}\n\n{entry.ReleaseNotes}\n\n{entry.Requirements}") + (problem is null ? "" : "\n\n" + problem));
        actions.Children.Add(details);
        void AddAction(string label, Action action)
        {
            Button button = new() { Content = label, IsEnabled = !busy, Margin = new Thickness(0, 0, 8, 0) };
            button.Click += (_, _) => action();
            actions.Children.Add(button);
        }
        if (entry is not null && managed is not { Pending: true } && (installed is null || managed is not null && entry.CompareVersion(managed.Version) is > 0) && LauncherModDownloadAllowed(entry, snapshot) && job is not { CanPause: true } and not { CanResume: true } and not { CanInstall: true })
        {
            AddAction(installed is null ? "Download" : "Update", () => launcherModLibrary?.Queue(entry));
        }
        if (job is not null)
        {
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
