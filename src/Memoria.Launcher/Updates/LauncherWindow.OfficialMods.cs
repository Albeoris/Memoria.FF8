using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Memoria.FF8.Mods;
using Memoria.FF8.Platform;

namespace Memoria.Launcher;

internal sealed partial class LauncherWindow
{
    private readonly TextBlock officialModStatus = new() { Text = "Checking official mods…", TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DimGray };
    private readonly Button officialModDetails = new() { Content = "Official mod details", IsEnabled = false, Margin = new Thickness(0, 0, 10, 0) };
    private readonly Button manageOfficialMods = new() { Content = "Manage mods in game", IsEnabled = false, Margin = new Thickness(0, 0, 10, 0) };
    private readonly Button refreshOfficialMods = new() { Content = "Refresh mods", IsEnabled = false };
    private OfficialModNotifications? officialModNotifications;
    private CancellationTokenSource? officialModCheck;
    private long lastManualModRefresh;
    private bool officialModChecksStarted;

    private StackPanel CreateOfficialModsPanel()
    {
        StackPanel panel = new() { Spacing = 6, Margin = new Thickness(0, 12, 0, 0) };
        WrapPanel actions = new();
        actions.Children.Add(officialModDetails);
        actions.Children.Add(manageOfficialMods);
        actions.Children.Add(refreshOfficialMods);
        panel.Children.Add(officialModStatus);
        panel.Children.Add(actions);
        officialModDetails.Click += async (_, _) => await ShowOfficialModDetailsAsync();
        manageOfficialMods.Click += async (_, _) => await ManageOfficialModsAsync();
        refreshOfficialMods.Click += async (_, _) => await CheckOfficialModsAsync(force: true);
        return panel;
    }

    private Task RecheckOfficialModSelectionAsync() => officialModChecksStarted ? CheckOfficialModsAsync() : Task.CompletedTask;

    private async Task CheckOfficialModsAsync(bool force = false)
    {
        if (force)
        {
            long now = Stopwatch.GetTimestamp();
            if (lastManualModRefresh != 0 && Stopwatch.GetElapsedTime(lastManualModRefresh, now) < TimeSpan.FromSeconds(20))
            {
                return;
            }
            lastManualModRefresh = now;
        }
        officialModCheck?.Cancel();
        if (installations.SelectedItem is not GameInstallation selected)
        {
            officialModNotifications = null;
            officialModDetails.IsEnabled = false;
            refreshOfficialMods.IsEnabled = false;
            officialModStatus.Text = "Choose an installation to check official mods.";
            return;
        }
        string language = SelectedResourceLanguage ?? "en";
        string packageDirectory = AppContext.BaseDirectory;
        officialModNotifications = null;
        officialModDetails.IsEnabled = false;
        refreshOfficialMods.IsEnabled = false;
        officialModStatus.Text = "Checking official mods…";
        using CancellationTokenSource check = CancellationTokenSource.CreateLinkedTokenSource(updateLifetime.Token);
        officialModCheck = check;
        try
        {
            OfficialModNotifications result = await Task.Run(async () =>
            {
                OfficialCatalogSnapshot catalog = await OfficialModCatalog.RefreshAsync(OfficialModCatalog.DefaultCacheDirectory, force: force, cancellationToken: check.Token);
                string edition = GameInstallationSelection.ResolveEdition(selected.Directory).ToString();
                string modsRoot = ApplicationInstallation.ResolveModsDirectory(packageDirectory, HostPlatforms.Current, ApplicationPaths.Current.Configuration);
                string profile = OfficialModNotifications.StateDirectory(ApplicationPaths.Current.Configuration, modsRoot, selected.Directory, edition, language);
                string? engine = EngineBuildMetadata.ReadInstalledCompatibilityVersion(MacBundleUpdate.NormalizeRoot(packageDirectory));
                IReadOnlyDictionary<string, ModPackageInstallResult> installed = ModPackageInstaller.ReadOfficialInventory(modsRoot);
                return OfficialModNotifications.Create(catalog, profile, engine, edition, language, installed);
            }, check.Token);
            if (!check.IsCancellationRequested)
            {
                officialModNotifications = result;
                officialModStatus.Text = result.Summary;
                officialModDetails.IsEnabled = result.Notices.Count > 0;
            }
        }
        catch (OperationCanceledException) when (check.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            if (!check.IsCancellationRequested)
            {
                officialModStatus.Text = "Could not check official mods. " + exception.Message;
            }
        }
        finally
        {
            if (ReferenceEquals(officialModCheck, check))
            {
                officialModCheck = null;
                refreshOfficialMods.IsEnabled = !updateLifetime.IsCancellationRequested && installations.SelectedItem is GameInstallation;
            }
        }
    }

    private async Task ShowOfficialModDetailsAsync()
    {
        if (officialModNotifications is not { } shown)
        {
            return;
        }
        string details = string.Join("\n\n", shown.Notices.Select(notice => notice.Describe()));
        if (shown.Error is string error)
        {
            details = "Saved information · " + error + "\n\n" + details;
        }
        await LauncherDialogs.ShowAsync(this, "Memoria — official mods", details);
        if (shown.Notices.All(notice => notice is { AcknowledgementKey: null } or { Reviewed: true }))
        {
            return;
        }
        try
        {
            OfficialModNotifications acknowledged = await Task.Run(shown.Acknowledge);
            if (ReferenceEquals(officialModNotifications, shown))
            {
                officialModNotifications = acknowledged;
                officialModStatus.Text = acknowledged.Summary;
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            officialModStatus.Text = "Could not mark official mod changes as reviewed. " + exception.Message;
        }
    }

    private async Task ManageOfficialModsAsync()
    {
        if (runningGame is { HasExited: false } || await Task.Run(IsAnotherGameRunning))
        {
            await ShowRunningGameModInstructionsAsync();
            return;
        }
        await PlayAsync(openModManager: true);
    }

    private static bool IsAnotherGameRunning()
    {
        Process[] processes = Process.GetProcessesByName("Memoria.FF8");
        try
        {
            return processes.Length > 0;
        }
        finally
        {
            foreach (Process process in processes)
            {
                process.Dispose();
            }
        }
    }

    private Task ShowRunningGameModInstructionsAsync()
    {
        return LauncherDialogs.ShowAsync(this, "Memoria — manage mods", "Memoria is already running. Open Mods from the title screen or from the in-game menu to manage official mods. For another installation, close the running game first.");
    }
}
