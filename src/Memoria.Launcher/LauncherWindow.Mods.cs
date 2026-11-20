using System.Diagnostics;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Memoria.FF8;
using Memoria.FF8.Mods;
using Memoria.FF8.Platform;
using Memoria.Launcher.Controller;

namespace Memoria.Launcher;

internal sealed partial class LauncherWindow
{
    private readonly ListBox launcherModRows = new() { MinWidth = 180, Background = LauncherSurface, BorderBrush = LauncherBorder, BorderThickness = new Thickness(1) };
    private readonly StackPanel launcherModDetails = new() { Spacing = 8 };
    private readonly ScrollViewer launcherModDetailScroll = new() { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    private readonly Button launcherModClearFilters = LauncherModButton("Clear filters");
    private readonly Button launcherModPrevious = LauncherModButton("← Previous");
    private readonly Button launcherModNext = LauncherModButton("Next →");
    private readonly Button launcherModExpand = LauncherModButton("Expand details");
    private readonly Button launcherModIssues = LauncherModButton("View issues");
    private readonly Button launcherModComplete = LauncherModButton("Download missing components");
    private readonly Button launcherModRefresh = LauncherModButton("Refresh");
    private string? launcherModInitializationError;
    private (LauncherModRow Row, ModLibraryInstallation? Managed, bool Busy, bool TargetSupported, bool OfficialSupported, string? Language)? launcherModRenderedCard;
    private string launcherModIssueText = "";
    private string launcherModDetailText = "";
    private RemoteModEntry[] launcherModMissing = [];
    private readonly TextBlock launcherModActionStatus = new() { Foreground = LauncherMuted, TextWrapping = TextWrapping.Wrap, IsVisible = false };
    private readonly Dictionary<string, ListBoxItem> launcherModItems = new(StringComparer.Ordinal);
    private LauncherModRow[] launcherModVisible = [];
    private string? launcherModSelectedKey;
    private readonly string?[] launcherModTabSelections = new string?[3];
    private bool launcherModChangingRows;
    private const string LauncherMemoriaGroupKey = "group:Memoria";
    private bool launcherMemoriaExpanded;
    private Bitmap? launcherModCrest;
    private LauncherModRow[] launcherMemoriaComponents = [];


    internal sealed record LauncherModRow(string Key, string Name, string Description, string Category, ModPackageView? Installed, RemoteModEntry? Entry)
    {
        public ModLibraryJob? Job { get; init; }
        public bool IsMemoria => Entry?.Official is not null || Installed is not null && ModRuntime.SettingsGroupId(Installed.Package) == ModRuntime.MemoriaPackageId;
    }

    internal sealed record LauncherModCategory(string Name, int Count)
    {
        public override string ToString() => $"{Name} ({Count})";
    }
    private static readonly string[] LauncherModFilters = ["All mods", "Check required", "Installed", "Downloads", "Needs attention", "Official mods"];
    private readonly TextBox launcherModSearch = new() { Watermark = "Search mods…", MinWidth = 100, MinHeight = 32 };
    private readonly ComboBox launcherModCategory = new() { ItemsSource = new[] { new LauncherModCategory("All categories", 0) }, SelectedIndex = 0, MinWidth = 128, MaxWidth = 170, MinHeight = 32 };
    private readonly ComboBox launcherModFilter = new() { ItemsSource = LauncherModFilters, SelectedIndex = 0, MinWidth = 120, MinHeight = 32 };
    private readonly TextBlock launcherModSummary = new() { Foreground = LauncherMuted, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center, FontSize = 13 };
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

    private static Button LauncherModButton(string label)
    {
        return new Button { Content = label, MinHeight = 30, Padding = new Thickness(9, 4) };
    }

    private Control CreateModsPage()
    {
        Grid page = new() { RowDefinitions = new RowDefinitions("Auto,*"), RowSpacing = 8 };
        StackPanel header = new() { Spacing = 6 };
        Grid navigation = new() { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 8 };
        launcherModTabs.FontSize = 14;
        launcherModTabs.MinHeight = 0;
        launcherModTabs.Height = 34;
        launcherModTabs.ItemsSource = new[] { "Installed", "Catalog", "Downloads" }.Select(title => new TabItem { Header = title, FontSize = 14, MinHeight = 0, Height = 34, Padding = new Thickness(8, 4) }).ToArray();
        launcherModTabs.SelectedIndex = 0;
        launcherModTabs.SelectionChanged += (_, _) =>
        {
            if (!launcherModChangingFilter && launcherModTabs.SelectedIndex >= 0)
            {
                launcherModSelectedKey = launcherModTabSelections[launcherModTabs.SelectedIndex];
                launcherModDetailScroll.Offset = default;
                RenderLauncherMods();
            }
        };
        navigation.Children.Add(launcherModTabs);
        launcherModRefresh.Click += async (_, _) => await CheckOfficialModsAsync(force: true);
        Grid.SetColumn(launcherModRefresh, 1);
        navigation.Children.Add(launcherModRefresh);
        header.Children.Add(navigation);
        header.Children.Add(CreateLauncherModLocationControl());

        Grid filters = new() { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto,Auto"), ColumnSpacing = 8 };
        filters.Children.Add(launcherModSearch);
        Grid.SetColumn(launcherModCategory, 1);
        filters.Children.Add(launcherModCategory);
        Grid.SetColumn(launcherModFilter, 2);
        filters.Children.Add(launcherModFilter);
        Grid.SetColumn(launcherModClearFilters, 3);
        filters.Children.Add(launcherModClearFilters);
        launcherModSearch.TextChanged += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(launcherModSearch.Text))
            {
                launcherMemoriaExpanded = true;
            }
            LauncherModFilterChanged();
        };
        launcherModCategory.SelectionChanged += (_, _) => LauncherModFilterChanged();
        launcherModFilter.SelectionChanged += (_, _) => LauncherModFilterChanged();
        launcherModClearFilters.Click += (_, _) => ClearLauncherModFilters();
        AutomationProperties.SetName(launcherModSearch, "Search mods");
        AutomationProperties.SetName(launcherModCategory, "Mod category");
        AutomationProperties.SetName(launcherModFilter, "Mod status");
        header.Children.Add(filters);

        Grid status = new() { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), ColumnSpacing = 8 };
        status.Children.Add(launcherModSummary);
        Grid.SetColumn(launcherModComplete, 1);
        status.Children.Add(launcherModComplete);
        Grid.SetColumn(launcherModIssues, 2);
        status.Children.Add(launcherModIssues);
        launcherModComplete.Click += (_, _) =>
        {
            if (launcherModLibrary is { } library)
            {
                QueueMissingLauncherMods(launcherModMissing, library.View);
            }
        };
        launcherModIssues.Click += async (_, _) => await ShowLauncherModDetailsAsync("Mod library issues", launcherModIssueText);
        header.Children.Add(status);
        header.Children.Add(launcherModActionStatus);
        page.Children.Add(header);

        Grid collection = new() { ColumnDefinitions = new ColumnDefinitions("2*,10,3*"), RowDefinitions = new RowDefinitions("*,Auto"), RowSpacing = 6 };
        collection.ColumnDefinitions[0].MinWidth = 180;
        collection.ColumnDefinitions[2].MinWidth = 220;
        ScrollViewer.SetHorizontalScrollBarVisibility(launcherModRows, ScrollBarVisibility.Disabled);
        collection.Children.Add(launcherModRows);
        Border divider = new() { Width = 1, Background = LauncherBorder, HorizontalAlignment = HorizontalAlignment.Center, IsHitTestVisible = false };
        Grid.SetColumn(divider, 1);
        Grid.SetRowSpan(divider, 2);
        collection.Children.Add(divider);
        GridSplitter splitter = new() { ResizeDirection = GridResizeDirection.Columns, HorizontalAlignment = HorizontalAlignment.Stretch, Background = Brushes.Transparent };
        Grid.SetColumn(splitter, 1);
        Grid.SetRowSpan(splitter, 2);
        AutomationProperties.SetName(splitter, "Resize mod list and details");
        ToolTip.SetTip(splitter, "Resize list and details");
        collection.Children.Add(splitter);

        launcherModDetailScroll.Content = launcherModDetails;
        Grid.SetColumn(launcherModDetailScroll, 2);
        collection.Children.Add(launcherModDetailScroll);
        StackPanel rowNavigation = new() { Orientation = Orientation.Horizontal, Spacing = 6 };
        rowNavigation.Children.Add(launcherModPrevious);
        rowNavigation.Children.Add(launcherModNext);
        Grid.SetRow(rowNavigation, 1);
        collection.Children.Add(rowNavigation);
        Grid.SetColumn(launcherModExpand, 2);
        Grid.SetRow(launcherModExpand, 1);
        launcherModExpand.HorizontalAlignment = HorizontalAlignment.Left;
        collection.Children.Add(launcherModExpand);
        launcherModPrevious.Click += (_, _) => MoveLauncherModSelection(-1);
        launcherModNext.Click += (_, _) => MoveLauncherModSelection(1);
        launcherModExpand.Click += async (_, _) => await ShowLauncherModDetailsAsync("Mod details", launcherModDetailText);
        Grid.SetRow(collection, 1);
        page.Children.Add(collection);

        launcherModRows.SelectionChanged += (_, _) =>
        {
            if (!launcherModChangingRows && launcherModRows.SelectedItem is ListBoxItem { Tag: string key })
            {
                launcherModSelectedKey = key;
                if (launcherModTabs.SelectedIndex >= 0)
                {
                    launcherModTabSelections[launcherModTabs.SelectedIndex] = key;
                }
                launcherModDetailScroll.Offset = default;
                RenderLauncherModDetails();
            }
        };
        launcherModRows.AddHandler(KeyDownEvent, (_, input) =>
        {
            if (input.Key == Key.Enter && launcherModRows.SelectedIndex >= 0)
            {
                if (launcherModSelectedKey == LauncherMemoriaGroupKey)
                {
                    ToggleLauncherMemoriaGroup();
                }
                else
                {
                    launcherModExpand.Focus();
                }
                input.Handled = true;
            }
        }, RoutingStrategies.Tunnel);
        ControllerNavigation.SetListCommitAction(launcherModRows, () =>
        {
            if (launcherModSelectedKey == LauncherMemoriaGroupKey)
            {
                ToggleLauncherMemoriaGroup();
            }
            else if (launcherModExpand.IsEnabled)
            {
                launcherModExpand.Focus(NavigationMethod.Directional);
            }
        });
        page.AddHandler(KeyDownEvent, (_, input) =>
        {
            if (input.Key == Key.F && input.KeyModifiers == KeyModifiers.Control)
            {
                launcherModSearch.Focus();
                launcherModSearch.SelectAll();
                input.Handled = true;
            }
            else if (input.Key == Key.Escape && launcherModSearch.IsKeyboardFocusWithin && !string.IsNullOrEmpty(launcherModSearch.Text))
            {
                launcherModSearch.Text = "";
                input.Handled = true;
            }
        }, RoutingStrategies.Tunnel);
        launcherModTimer.Tick += (_, _) => PollLauncherMods();
        return page;
    }

    private void LauncherModFilterChanged()
    {
        if (!launcherModChangingFilter)
        {
            RenderLauncherMods();
        }
    }

    private void ClearLauncherModFilters()
    {
        launcherModChangingFilter = true;
        launcherModSearch.Text = "";
        launcherModCategory.SelectedIndex = 0;
        launcherModFilter.SelectedIndex = 0;
        launcherModChangingFilter = false;
        launcherModSearch.Focus();
        RenderLauncherMods();
    }

    private void MoveLauncherModSelection(int direction)
    {
        int index = launcherModRows.SelectedIndex + direction;
        if (index >= 0 && index < launcherModRows.ItemCount)
        {
            launcherModRows.SelectedIndex = index;
            launcherModRows.ScrollIntoView(index);
        }
    }

    private void ToggleLauncherMemoriaGroup()
    {
        launcherMemoriaExpanded = !launcherMemoriaExpanded;
        launcherModSelectedKey = LauncherMemoriaGroupKey;
        launcherModDetailScroll.Offset = default;
        RenderLauncherMods();
    }

    private async Task ShowLauncherModDetailsAsync(string title, string detail)
    {
        Window dialog = new() { Title = title, Width = 720, Height = 440, MinWidth = 360, MinHeight = 240, WindowStartupLocation = WindowStartupLocation.CenterOwner, RequestedThemeVariant = ActualThemeVariant };
        Grid layout = new() { Margin = new Thickness(18), RowDefinitions = new RowDefinitions("*,Auto"), RowSpacing = 12 };
        ScrollViewer scroll = new() { Content = new SelectableTextBlock { Text = detail, TextWrapping = TextWrapping.Wrap }, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Focusable = true };
        layout.Children.Add(scroll);
        StackPanel actions = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8 };
        Button maximize = LauncherModButton("Maximize");
        maximize.Click += (_, _) =>
        {
            dialog.WindowState = dialog.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            maximize.Content = dialog.WindowState == WindowState.Maximized ? "Restore size" : "Maximize";
        };
        Button close = LauncherModButton("Back");
        close.IsCancel = true;
        close.Click += (_, _) => dialog.Close();
        actions.Children.Add(maximize);
        actions.Children.Add(close);
        Grid.SetRow(actions, 1);
        layout.Children.Add(actions);
        dialog.Content = layout;
        dialog.Opened += (_, _) => scroll.Focus();
        await dialog.ShowDialog(this);
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
            return;
        }
        var edition = GameInstallationSelection.ResolveEdition(selected.Directory);
        ApplicationPaths paths = ApplicationPaths.Current;
        ModDirectories locations;
        try
        {
            locations = ApplicationInstallation.ResolveModDirectories(AppContext.BaseDirectory, selected.Directory, paths, LauncherSettings.Load(paths.LauncherSettings));
        }
        catch (ArgumentException exception)
        {
            await StopLauncherModLibraryAsync();
            launcherModInitializationError = exception.Message;
            RenderLauncherMods();
            return;
        }
        string root = locations.Primary;
        string targetIdentity = ModLibraryProfile.Identity(locations.LegacyDefault, selected.Directory, edition.ToString(), language);
        string libraryIdentity = string.Join('\0', locations.Discovery.Prepend(root).Prepend(targetIdentity));
        if (libraryIdentity == launcherModIdentity && launcherModLibrary is not null)
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
            ModRuntime.ImportLocationPreferences(locations);
            string profile = $"{edition}/{language}/{targetIdentity}";
            bool junction = ModCompatibility.IsCompatible(edition, ModFormat.Junction) && language.Equals("en", StringComparison.OrdinalIgnoreCase);
            ModPackageInstaller installer = ModLibraryProfile.CreateInstaller(root, selected.Directory, edition.ToString(), language, junction, locations.Preferences);
            ModPackageInstaller[] previousInstallers = locations.Discovery
                .Where(directory => !ApplicationInstallation.ModPathComparer.Equals(directory, root) && Directory.Exists(ModPackageInstaller.StateDirectoryFor(directory)))
                .Select(directory => ModLibraryProfile.CreateInstaller(directory, selected.Directory, edition.ToString(), language, junction, locations.Preferences)).ToArray();
            ModPackageInstaller[] installers = previousInstallers.Prepend(installer).OrderBy(item => item.ModsRoot, StringComparer.OrdinalIgnoreCase).ThenBy(item => item.ModsRoot, StringComparer.Ordinal).ToArray();
            // Apply managed transactions only at the same exclusive boundary used by game startup.
            await Task.Run(() => RecoverLauncherModInstallations(installers, root));
            if (initialization != launcherModInitialization || busy || runningGame is not null || !Equals(installations.SelectedItem, selected))
            {
                return;
            }
            ModRuntime mods = new(ApplicationInstallation.DataDirectory, format => ModCompatibility.IsCompatible(edition, format), watch: false, edition: edition.ToString(), language: language, modLocations: locations);
            launcherMods = mods;
            mods.PreparePending(true);
            mods.CommitPending();
            string? officialDownloads = ModLibraryProfile.OfficialDownloadsDirectory(locations.LegacyDefault, paths);
            launcherModLibrary = new ModLibraryService(Path.Combine(paths.Cache, "mod-library", targetIdentity), installer, launcherMods, profile, junction, openDownloadPage: OpenLauncherModDownloadPage, officialCacheDirectory: OfficialModCatalog.DefaultCacheDirectory, officialSupported: true, applyPendingChanges: cancellationToken => ApplyLauncherModChangesAsync(installers, mods, cancellationToken), previousInstallers: previousInstallers, officialDownloadsDirectory: officialDownloads, refreshCatalogOnStartup: true);
            launcherModIdentity = libraryIdentity;
            launcherModTimer.Start();
            PollLauncherMods();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException or InvalidOperationException)
        {
            launcherModInitializationError = exception.Message;
            RenderLauncherMods();
        }
    }

    private static bool RecoverLauncherModInstallations(IEnumerable<ModPackageInstaller> installers, string primaryRoot)
    {
        bool applied = true;
        List<ModDirectoryResourceLease> leases = [];
        List<ModPackageInstaller> writable = [];
        try
        {
            foreach (ModPackageInstaller installer in installers)
            {
                ModDirectoryResourceLease lease;
                try
                {
                    lease = ModDirectoryResourceLease.Acquire(installer.ModsRoot, true);
                }
                catch (Exception exception) when (!ApplicationInstallation.ModPathComparer.Equals(installer.ModsRoot, primaryRoot)
                    && exception is (IOException or UnauthorizedAccessException or InvalidDataException))
                {
                    applied = false;
                    continue;
                }
                leases.Add(lease);
                if (lease.IsWriter)
                {
                    writable.Add(installer);
                }
                else
                {
                    applied = false;
                }
            }
            foreach (ModPackageInstaller installer in writable)
            {
                installer.RecoverRelocations(writable);
                installer.RecoverAndApply();
            }
            return applied;
        }
        finally
        {
            foreach (ModDirectoryResourceLease lease in leases)
            {
                lease.Dispose();
            }
        }
    }

    private async Task<bool> ApplyLauncherModChangesAsync(IReadOnlyList<ModPackageInstaller> installers, ModRuntime mods, CancellationToken cancellationToken)
    {
        bool available = await Dispatcher.UIThread.InvokeAsync(() => !cancellationToken.IsCancellationRequested && !busy && runningGame is null && ReferenceEquals(launcherMods, mods));
        if (!available)
        {
            return false;
        }
        // Launch and shutdown await the library worker before disposing this runtime or starting a game.
        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            bool applied = RecoverLauncherModInstallations(installers, mods.PrimaryModsDirectory);
            mods.Refresh();
            mods.PreparePending(true);
            mods.CommitPending();
            return applied;
        }, cancellationToken);
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
        launcherModInitializationError = null;
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
        UpdateLauncherModLocationControl();
        launcherModClearFilters.IsEnabled = !string.IsNullOrEmpty(launcherModSearch.Text) || launcherModCategory.SelectedIndex > 0 || launcherModFilter.SelectedIndex > 0;
        if (launcherMods is null || launcherModLibrary is null)
        {
            launcherModRows.ItemsSource = null;
            launcherModItems.Clear();
            launcherModVisible = [];
            launcherModMissing = [];
            launcherModComplete.IsVisible = false;
            launcherModRefresh.IsEnabled = !busy && runningGame is null && installations.SelectedItem is not null;
            SetLauncherModIssues(new[] { launcherModInitializationError }.OfType<string>());
            launcherModSummary.Foreground = launcherModInitializationError is null ? LauncherMuted : LauncherProblem;
            launcherModSummary.Text = launcherModInitializationError is not null ? "Could not open the mod library." : runningGame is not null ? "Use Mods in the game menu while the game is running." : EffectiveResourceLanguage is null && installations.SelectedItem is not null ? "Choose an installation with supported game language resources." : "Choose an installation to open your mod collection.";
            RenderLauncherModDetails();
            return;
        }
        ModLibrarySnapshot snapshot = launcherModLibrary.View;
        string category = (launcherModCategory.SelectedItem as LauncherModCategory)?.Name ?? "All categories";
        string search = launcherModSearch.Text?.Trim() ?? "";
        bool downloads = launcherModTabs.SelectedIndex == 2;
        launcherModRefresh.IsEnabled = !busy && !snapshot.Catalog.IsRefreshing;
        IReadOnlyList<ModPackageView> packages = LauncherInstalledModViews();
        string[] target = snapshot.TargetProfile.Split('/');
        RemoteModEntry[] entries = LauncherKnownModEntries(snapshot).Where(entry => entry.Official is null || OfficialModCatalog.TargetMatches(entry.Official.Target, target[0], target[1])).ToArray();
        LauncherModRow[] rows = launcherModTabs.SelectedIndex switch
        {
            0 => packages.Where(package => !package.Package.IsSettingsGroup).Select(package =>
            {
                RemoteModEntry? entry = entries.FirstOrDefault(entry => FindLauncherPackage(entry, [package]) is not null);
                return new LauncherModRow("installed:" + package.Package.Id, package.Package.Name, entry?.Description ?? package.Package.Descriptor?.DescriptionForLanguage(SelectedResourceLanguage) ?? "", entry?.Category ?? package.Package.Format.ToString(), package, entry) { Job = LauncherEntryJob(entry, snapshot) };
            }).ToArray(),
            2 => snapshot.Jobs.OrderBy(LauncherJobGroup).ThenBy(job => job.Entry.Name, StringComparer.OrdinalIgnoreCase).ThenBy(job => job.Id).Select(job =>
                new LauncherModRow("job:" + job.Id, job.Entry.Name, job.Entry.Description, job.Entry.Category, FindLauncherPackage(job.Entry, packages), job.Entry) { Job = job }).ToArray(),
            _ => entries.OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase).ThenBy(entry => entry.Id).Select(entry =>
                new LauncherModRow(entry.SourceId + ":" + entry.Id, entry.Name, entry.Description, entry.Category, FindLauncherPackage(entry, packages), entry) { Job = LauncherEntryJob(entry, snapshot) }).ToArray()
        };
        LauncherModCategory[] categories = CreateLauncherModCategories(rows.Select(row => row.Category));
        if (!categories.SequenceEqual(launcherModCategory.Items.Cast<LauncherModCategory>()))
        {
            launcherModChangingFilter = true;
            launcherModCategory.ItemsSource = categories;
            launcherModCategory.SelectedItem = categories.FirstOrDefault(item => item.Name.Equals(category, StringComparison.OrdinalIgnoreCase)) ?? categories[0];
            launcherModChangingFilter = false;
        }
        category = ((LauncherModCategory)launcherModCategory.SelectedItem!).Name;
        LauncherModRow[] filtered = rows.Where(row => MatchesLauncherModFilter(row.Name, row.Description + " " + row.Entry?.Author + " " + row.Category + (row.IsMemoria ? " Memoria" : ""), row.Category, search, category) && MatchesLauncherModStatus(row, snapshot, launcherModFilter.SelectedIndex)).ToArray();
        launcherMemoriaComponents = downloads ? [] : filtered.Where(row => row.IsMemoria).ToArray();
        bool expanded = launcherMemoriaExpanded;
        launcherModVisible = downloads ? filtered : GroupLauncherModRows(filtered, expanded);

        int activeDownloads = snapshot.Jobs.Count(job => job.Phase is not ("Installed" or "Cancelled"));
        launcherModSummary.Foreground = snapshot.Error is null && snapshot.Catalog.Error is null ? LauncherMuted : LauncherProblem;
        launcherModSummary.Text = snapshot.Catalog.IsRefreshing ? "Refreshing catalog…" : downloads ? $"{filtered.Length} matching downloads" : $"{filtered.Length} matching mods · {activeDownloads} pending downloads";
        launcherModMissing = entries.Where(entry => IsLauncherDownloadComponent(entry) && FindLauncherPackage(entry, packages) is null).ToArray();
        int eligible = launcherModMissing.Count(entry => LauncherModDownloadAllowed(entry, snapshot));
        launcherModComplete.IsVisible = !downloads && eligible > 0;
        launcherModComplete.IsEnabled = !busy && eligible > 0;
        launcherModComplete.Content = $"Download missing components ({eligible})";
        IEnumerable<string> unavailable = launcherModMissing.Select(entry => (entry.Name, Reason: LauncherModDownloadBlocker(entry, snapshot))).Where(item => item.Reason is not null).Select(item => item.Name + "\n" + item.Reason);
        SetLauncherModIssues(new[] { snapshot.Error, snapshot.Catalog.Error }.OfType<string>().Concat(unavailable));

        string[] keys = launcherModVisible.Select(row => row.Key).ToArray();
        bool changed = !launcherModItems.Keys.SequenceEqual(keys);
        bool restoreListFocus = changed && launcherModRows.IsKeyboardFocusWithin;
        launcherModChangingRows = true;
        if (changed)
        {
            // The virtualizing panel detaches existing containers when the source is replaced.
            // Give the new source its own containers so they can be realized and anchored.
            ListBoxItem[] items = keys.Select(key => new ListBoxItem { Tag = key, HorizontalContentAlignment = HorizontalAlignment.Stretch }).ToArray();
            launcherModItems.Clear();
            foreach (ListBoxItem item in items)
            {
                launcherModItems.Add((string)item.Tag!, item);
            }
            launcherModRows.ItemsSource = items;
        }
        foreach (LauncherModRow row in launcherModVisible)
        {
            ListBoxItem item = launcherModItems[row.Key];
            if (row.Key == LauncherMemoriaGroupKey)
            {
                item.Padding = new Thickness(6, 3);
                UpdateLauncherMemoriaHeader(item, expanded);
                continue;
            }
            item.Padding = new Thickness(row.IsMemoria && !downloads ? 26 : 8, 4, 8, 4);
            if (item.Content is not StackPanel label)
            {
                label = new StackPanel { Spacing = 2 };
                label.Children.Add(new TextBlock { TextWrapping = TextWrapping.Wrap, FontWeight = FontWeight.SemiBold });
                label.Children.Add(new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 12 });
                item.Content = label;
            }
            ((TextBlock)label.Children[0]).Text = row.Name;
            TextBlock status = (TextBlock)label.Children[1];
            ModLibraryInstallation? managed = LauncherManagedMod(row.Entry, snapshot);
            string state = LauncherModStatusText(row.Installed, row.Entry, row.Job, managed, snapshot);
            status.Text = string.IsNullOrWhiteSpace(row.Category) ? state : row.Category + " · " + state;
            bool hasProblem = row.Job?.Error is not null || row.Installed?.NeedsAttention == true || managed?.Error is not null || row.Entry?.CompatibilityError is not null;
            status.Foreground = hasProblem ? LauncherProblem : row.Job?.VersionWarning is not null ? LauncherWarning : LauncherMuted;
        }
        string? previousSelection = launcherModSelectedKey;
        launcherModSelectedKey = SelectLauncherModKey(keys, launcherModSelectedKey);
        if (launcherModTabs.SelectedIndex >= 0)
        {
            launcherModTabSelections[launcherModTabs.SelectedIndex] = launcherModSelectedKey;
        }
        if (previousSelection != launcherModSelectedKey)
        {
            launcherModDetailScroll.Offset = default;
        }
        launcherModRows.SelectedItem = launcherModSelectedKey is null ? null : launcherModItems[launcherModSelectedKey];
        if ((changed || previousSelection != launcherModSelectedKey) && launcherModRows.SelectedIndex >= 0)
        {
            launcherModRows.ScrollIntoView(launcherModRows.SelectedIndex);
        }
        launcherModChangingRows = false;
        RenderLauncherModDetails();
        if (restoreListFocus && launcherModRows.SelectedItem is ListBoxItem selectedItem)
        {
            selectedItem.Focus();
        }
    }

    private void SetLauncherModIssues(IEnumerable<string> issues)
    {
        launcherModIssueText = string.Join("\n\n", issues.Where(issue => !string.IsNullOrWhiteSpace(issue)).Distinct());
        launcherModIssues.IsVisible = launcherModIssueText.Length > 0;
    }

    private static int LauncherJobGroup(ModLibraryJob job)
    {
        return job.Phase is "Installed" or "Cancelled" ? 3 : job.Error is not null || job.CanInstall || job.CanResume || job.CanRetry ? 0 : job.Phase == "Queued" ? 2 : 1;
    }

    private static ModLibraryJob? LauncherEntryJob(RemoteModEntry? entry, ModLibrarySnapshot snapshot)
    {
        return entry is null ? null : snapshot.Jobs.LastOrDefault(job => job.Entry.Id == entry.Id && job.Entry.SourceId == entry.SourceId && (job.Entry.Version == entry.Version || entry.CompareVersion(job.Entry.Version) == 0) && job.Phase is not ("Installed" or "Cancelled"));
    }

    private static ModLibraryInstallation? LauncherManagedMod(RemoteModEntry? entry, ModLibrarySnapshot snapshot)
    {
        return entry is null ? null : snapshot.Installations.FirstOrDefault(item => item.Id == entry.Id && item.SourceId == entry.SourceId);
    }

    private static bool MatchesLauncherModStatus(LauncherModRow row, ModLibrarySnapshot snapshot, int filter)
    {
        ModLibraryInstallation? managed = LauncherManagedMod(row.Entry, snapshot);
        return filter switch
        {
            1 => row.Entry is not null && row.Job is null && row.Installed is null && managed is null && LauncherModDownloadAllowed(row.Entry, snapshot),
            2 => row.Installed is not null || managed is not null,
            3 => row.Job is not null,
            4 => row.Job is { Error: not null } or { VersionWarning: not null } || row.Installed?.NeedsAttention == true || managed?.Error is not null || row.Entry?.CompatibilityError is not null,
            5 => row.IsMemoria,
            _ => true
        };
    }

    internal static LauncherModRow[] GroupLauncherModRows(IReadOnlyList<LauncherModRow> rows, bool expanded)
    {
        LauncherModRow[] components = rows.Where(row => row.IsMemoria).ToArray();
        if (components.Length == 0)
        {
            return rows.ToArray();
        }
        LauncherModRow group = new(LauncherMemoriaGroupKey, "Memoria", "Official Memoria components", "Memoria", null, null);
        return new[] { group }.Concat(expanded ? components : []).Concat(rows.Where(row => !row.IsMemoria)).ToArray();
    }

    private void UpdateLauncherMemoriaHeader(ListBoxItem item, bool expanded)
    {
        if (item.Content is not Grid header)
        {
            header = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*"), ColumnSpacing = 8 };
            Button toggle = LauncherModButton("");
            toggle.Background = Brushes.Transparent;
            toggle.VerticalAlignment = VerticalAlignment.Center;
            toggle.Click += (_, _) => ToggleLauncherMemoriaGroup();
            header.Children.Add(toggle);
            if (launcherModCrest is null)
            {
                using Stream? stream = typeof(LauncherWindow).Assembly.GetManifestResourceStream("Memoria.Launcher.Crest.png");
                if (stream is not null)
                {
                    launcherModCrest = new Bitmap(stream);
                    launcherArtwork.Add(launcherModCrest);
                }
            }
            Image crest = new() { Source = launcherModCrest, Width = 30, Height = 32, Stretch = Stretch.Uniform };
            Grid.SetColumn(crest, 1);
            header.Children.Add(crest);
            StackPanel title = new() { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
            title.Children.Add(new TextBlock { Text = "Memoria", FontWeight = FontWeight.SemiBold });
            title.Children.Add(new TextBlock { Foreground = LauncherMuted, FontSize = 12 });
            Grid.SetColumn(title, 2);
            header.Children.Add(title);
            item.Content = header;
        }
        Button button = (Button)header.Children[0];
        button.Content = expanded ? "\u25BE" : "\u25B8";
        AutomationProperties.SetName(button, expanded ? "Collapse Memoria components" : "Expand Memoria components");
        ((TextBlock)((StackPanel)header.Children[2]).Children[1]).Text = $"{launcherMemoriaComponents.Length} components";
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
        string progress = job.TotalBytes is > 0 ? $" | {job.ReceivedBytes * 100 / job.TotalBytes}%" : job.ReceivedBytes > 0 ? $" | {job.ReceivedBytes / (1024 * 1024)} MB" : "";
        return job.Phase + progress + (job.VersionWarning is not null ? " · Version mismatch" : "");
    }

    private static string LauncherModStatusText(ModPackageView? installed, RemoteModEntry? entry, ModLibraryJob? job, ModLibraryInstallation? managed, ModLibrarySnapshot snapshot)
    {
        if (managed is { PendingRemoval: true })
        {
            return "Waiting to uninstall";
        }
        if (managed is { Pending: true })
        {
            return "Waiting to apply";
        }
        if (job is not null)
        {
            return LauncherModJobStatus(job);
        }
        if (managed is { Available: false })
        {
            return "Repair required";
        }
        if (entry is not null && managed is not null && entry.CompareVersion(managed.Version) is > 0)
        {
            return "Update available";
        }
        if (installed is not null)
        {
            return installed.StatusText;
        }
        return entry is not null && LauncherModDownloadAllowed(entry, snapshot) ? "Check required" : "Unavailable";
    }

    private void RenderLauncherModDetails()
    {
        launcherModPrevious.IsEnabled = launcherModRows.SelectedIndex > 0;
        launcherModNext.IsEnabled = launcherModRows.SelectedIndex >= 0 && launcherModRows.SelectedIndex + 1 < launcherModRows.ItemCount;
        LauncherModRow? selected = launcherModVisible.FirstOrDefault(row => row.Key == launcherModSelectedKey);
        ModLibrarySnapshot? snapshot = launcherModLibrary?.View;
        var previousCard = launcherModRenderedCard;
        launcherModRenderedCard = selected is not null && selected.Key != LauncherMemoriaGroupKey && snapshot is not null
            ? (selected, LauncherManagedMod(selected.Entry, snapshot), busy, snapshot.TargetSupported, snapshot.OfficialSupported, SelectedResourceLanguage)
            : null;
        if (launcherModRenderedCard.HasValue && launcherModRenderedCard.Equals(previousCard))
        {
            return;
        }
        ContentControl? focused = FocusManager?.GetFocusedElement() as ContentControl;
        bool restoreFocus = focused is not null && selected?.Key == previousCard?.Row.Key && focused.GetLogicalAncestors().Contains(launcherModDetails);
        Vector offset = launcherModDetailScroll.Offset;
        launcherModDetails.Children.Clear();
        launcherModDetailText = "";
        if (selected is not null && snapshot is not null)
        {
            if (selected.Key == LauncherMemoriaGroupKey)
            {
                launcherModDetails.Children.Add(new TextBlock { Text = "Memoria", FontSize = 20, FontWeight = FontWeight.SemiBold });
                launcherModDetailText = "Memoria\n\n" + string.Join("\n", launcherMemoriaComponents.Select(component =>
                    component.Name + " · " + (component.Job is { } job ? LauncherModJobStatus(job) : component.Installed?.StatusText ?? component.Category)));
                foreach (LauncherModRow component in launcherMemoriaComponents)
                {
                    Button open = LauncherModButton(component.Name);
                    open.HorizontalAlignment = HorizontalAlignment.Stretch;
                    open.HorizontalContentAlignment = HorizontalAlignment.Left;
                    open.Click += (_, _) =>
                    {
                        launcherMemoriaExpanded = true;
                        launcherModSelectedKey = component.Key;
                        launcherModDetailScroll.Offset = default;
                        RenderLauncherMods();
                    };
                    launcherModDetails.Children.Add(open);
                }
            }
            else
            {
                launcherModDetails.Children.Add(CreateLauncherModCard(selected.Installed, selected.Entry, selected.Job, snapshot, out launcherModDetailText));
            }
        }
        else if (launcherModLibrary is not null)
        {
            launcherModDetails.Children.Add(LauncherDescription(launcherModTabs.SelectedIndex == 2 ? "No downloads match your filters." : "No mods match your filters."));
        }
        launcherModExpand.IsEnabled = launcherModDetailText.Length > 0;
        launcherModDetailScroll.Offset = offset;
        if (restoreFocus)
        {
            var replacement = launcherModDetails.GetLogicalDescendants().OfType<ContentControl>().FirstOrDefault(control => control.GetType() == focused!.GetType() && Equals(control.Content, focused.Content) && control.IsEnabled);
            (replacement ?? launcherModExpand).Focus();
        }
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
            launcherModChangingFilter = true;
            launcherModSearch.Text = "";
            launcherModCategory.SelectedIndex = 0;
            launcherModFilter.SelectedIndex = 0;
            launcherModTabs.SelectedIndex = 1;
            launcherModChangingFilter = false;
            launcherMemoriaExpanded = allowed[0].Official is not null || launcherMemoriaExpanded;
            launcherModSelectedKey = allowed[0].SourceId + ":" + allowed[0].Id;
        }
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

    private Control CreateLauncherModCard(ModPackageView? installed, RemoteModEntry? entry, ModLibraryJob? job, ModLibrarySnapshot snapshot, out string expandedDetails)
    {
        StackPanel body = new() { Spacing = 8 };
        string description = entry?.Description ?? installed?.Package.Descriptor?.DescriptionForLanguage(SelectedResourceLanguage) ?? $"{installed?.Package.Format} mod";
        string title = entry?.Name ?? installed!.Package.Name;
        ModLibraryInstallation? managed = LauncherManagedMod(entry, snapshot);
        string state = LauncherModStatusText(installed, entry, job, managed, snapshot);
        TextBlock badge = LauncherDescription(state);
        badge.Foreground = installed?.Package.RequestedEnabled == true && installed.NeedsAttention == false && job is null ? LauncherSuccess : LauncherMuted;
        body.Children.Add(badge);
        if (job is { CanPause: true })
        {
            body.Children.Add(new ProgressBar { Minimum = 0, Maximum = 100, Value = job.TotalBytes is > 0 ? job.ReceivedBytes * 100d / job.TotalBytes.Value : 0, IsIndeterminate = job.TotalBytes is null or <= 0, Height = 6 });
        }
        List<string> details = [title];
        if (!string.IsNullOrWhiteSpace(entry?.Author))
        {
            details.Add(entry.Author);
        }
        details.Add(state);
        if (job?.VersionWarning is { } versionWarning)
        {
            TextBlock warning = LauncherDescription(versionWarning);
            warning.Foreground = LauncherWarning;
            body.Children.Add(warning);
            details.Add(versionWarning);
        }
        string? blocker = entry is null || installed is not null ? null : LauncherModDownloadBlocker(entry, snapshot);
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
            Button button = LauncherModButton(label);
            button.IsEnabled = !busy;
            button.Margin = new Thickness(0, 0, 6, 4);
            button.Click += (_, _) =>
            {
                button.IsEnabled = false;
                action();
                launcherModActionStatus.IsVisible = true;
                launcherModActionStatus.Text = label + " requested.";
            };
            actions.Children.Add(button);
        }
        if (entry is not null && job is null && managed is not { Pending: true } and not { PendingRemoval: true } && LauncherModDownloadAllowed(entry, snapshot))
        {
            string? label = managed is { Available: false } ? "Download & check repair" : installed is null ? "Download & check" : managed is not null && entry.CompareVersion(managed.Version) is > 0 ? "Download update & check" : null;
            if (label is not null)
            {
                AddAction(label, () => launcherModLibrary?.Queue(entry));
            }
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
                bool allowVersionMismatch = job.VersionWarning is not null;
                AddAction(allowVersionMismatch ? "Install anyway" : "Install", () => launcherModLibrary?.Install(job.Id, allowVersionMismatch));
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
        if (CreateLauncherModPreview(installed, job) is { } preview)
        {
            actions.Children.Add(preview);
        }
        if (actions.Children.Count > 0)
        {
            body.Children.Add(actions);
        }
        foreach (string problem in new[] { job?.Error, managed?.Error, installed?.Problem, blocker }.OfType<string>().Where(problem => !string.IsNullOrWhiteSpace(problem)).Distinct())
        {
            TextBlock error = LauncherDescription(problem);
            error.Foreground = problem == blocker && entry?.Official is { Delivery: "engine" } ? LauncherMuted : LauncherProblem;
            body.Children.Add(error);
            details.Add(problem);
        }
        AddDetail(null, job?.Details);
        AddDetail(null, description);
        if (entry is not null)
        {
            AddDetail(null, $"Version {entry.Version} · {entry.Category}\nEdition: {entry.Edition} · Language: {entry.Language}\nSource: {entry.SourceId}");
            AddDetail("Published size", entry.PublishedSize);
            AddDetail("Requirements", entry.Requirements);
            AddDetail("Release notes", entry.ReleaseNotes);
            AddDetail("Unsupported catalog metadata", entry.UnsupportedMetadata);
        }
        expandedDetails = string.Join("\n\n", details);
        return LauncherCard(title, string.IsNullOrWhiteSpace(entry?.Author) ? null : entry.Author, body);

        void AddDetail(string? heading, string? content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return;
            }
            string text = heading is null ? content : heading + "\n" + content;
            body.Children.Add(LauncherDescription(text));
            details.Add(text);
        }
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
