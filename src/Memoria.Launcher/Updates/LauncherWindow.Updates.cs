using System.Net.Http;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Memoria.FF8.Platform;

namespace Memoria.Launcher;

internal sealed partial class LauncherWindow
{
    private readonly Button releaseUpdate = new() { Content = "Latest release", MinWidth = 180, MinHeight = 32, Margin = new Thickness(0, 0, 10, 0) };
    private readonly CheckBox selectOtherRelease = new() { Content = "Choose another release", Margin = new Thickness(0, 8, 0, 0) };
    private readonly ComboBox releaseChoices = new() { MinWidth = 260, MaxWidth = 520, IsVisible = false, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch, Margin = new Thickness(0, 6, 0, 0) };
    private bool releaseCatalogLoaded;
    private readonly TextBlock updateStatus = new() { Text = "Checking for updates…", TextWrapping = TextWrapping.Wrap, Foreground = LauncherMuted, Margin = new Thickness(0, 6, 0, 0) };
    private readonly ProgressBar updateProgress = new() { Height = 8, Minimum = 0, Maximum = 100, IsVisible = false, Margin = new Thickness(0, 8, 0, 0) };
    private readonly CancellationTokenSource updateLifetime = new();
    private readonly Button cancelUpdate = new() { Content = "Cancel download", IsVisible = false, Margin = new Thickness(10, 0, 0, 0) };
    private CancellationTokenSource? updateDownload;
    private bool checkingUpdates;

    private StackPanel CreateUpdatePanel()
    {
        StackPanel panel = new() { Margin = new Thickness(0, 0, 0, 14) };
        WrapPanel buttons = new();
        buttons.Children.Add(releaseUpdate);
        cancelUpdate.Click += (_, _) => updateDownload?.Cancel();
        panel.Children.Add(buttons);
        panel.Children.Add(selectOtherRelease);
        panel.Children.Add(releaseChoices);
        panel.Children.Add(updateStatus);
        releaseUpdate.Click += async (_, _) => await InstallUpdateAsync();
        selectOtherRelease.IsCheckedChanged += async (_, _) =>
        {
            releaseChoices.IsVisible = selectOtherRelease.IsChecked == true;
            RefreshReleaseSelection();
            if (selectOtherRelease.IsChecked == true && !releaseCatalogLoaded)
            {
                await LoadReleaseCatalogAsync();
            }
        };
        releaseChoices.SelectionChanged += (_, _) => RefreshReleaseSelection();
        return panel;
    }

    private static HttpClient CreateUpdateClient() => new() { Timeout = Timeout.InfiniteTimeSpan };

    private async Task CheckUpdatesAsync()
    {
        if (!LauncherCapabilities.CanUpdate)
        {
            updateStatus.Text = LauncherCapabilities.UpdateStatus;
            return;
        }
        updateStatus.Foreground = LauncherMuted;
        checkingUpdates = true;
        releaseUpdate.IsEnabled = false;
        selectOtherRelease.IsEnabled = false;
        releaseChoices.IsEnabled = false;
        try
        {
            using HttpClient http = CreateUpdateClient();
            using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(updateLifetime.Token);
            timeout.CancelAfter(TimeSpan.FromSeconds(30));
            ReleaseClient client = new(http);
            Task<PackageManifest?> inventory = Task.Run(() => PackageManifest.LoadOptional(MacBundleUpdate.NormalizeRoot(AppContext.BaseDirectory)), timeout.Token);
            UpdateRelease? release = await client.GetAsync(UpdateChannel.Release, timeout.Token);
            PackageManifest? installed = await inventory;
            string current = installed is null ? "Development build" : $"Installed: {installed.Version} ({installed.BuildId[..Math.Min(8, installed.BuildId.Length)]})";
            string description = release is null ? "Latest release: no build published." : $"Latest release: {release.Tag} | {release.UpdatedAt.ToLocalTime():g}" + (installed is not null && release.BuildId == installed.BuildId ? " | installed" : " | available");
            updateStatus.Text = current + Environment.NewLine + description;
            updateStatus.Foreground = release is not null && (installed is null || release.BuildId != installed.BuildId) ? LauncherSuccess : LauncherMuted;
        }
        catch (Exception exception) when (!updateLifetime.IsCancellationRequested)
        {
            updateStatus.Foreground = LauncherProblem;
            updateStatus.Text = "Could not check updates: " + exception.Message + " Offline: installed games remain playable. Click Latest release to retry.";
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            checkingUpdates = false;
            SetBusy(busy);
            RefreshReleaseSelection();
        }
    }

    private void RefreshReleaseSelection()
    {
        bool chooseTag = selectOtherRelease.IsChecked == true;
        ReleaseCatalogEntry? selected = releaseChoices.SelectedItem as ReleaseCatalogEntry;
        releaseUpdate.Content = chooseTag && selected is not null ? $"Download {selected.Tag}" : "Latest release";
        releaseUpdate.IsEnabled = (!busy || runningGame is not null) && !updating && !checkingUpdates && LauncherCapabilities.CanUpdate && (!chooseTag || selected is not null);
    }

    private async Task LoadReleaseCatalogAsync()
    {
        updateStatus.Foreground = LauncherMuted;
        checkingUpdates = true;
        SetBusy(busy);
        try
        {
            updateStatus.Text = "Loading available releases...";
            using HttpClient http = CreateUpdateClient();
            using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(updateLifetime.Token);
            timeout.CancelAfter(TimeSpan.FromSeconds(30));
            IReadOnlyList<ReleaseCatalogEntry> releases = await new ReleaseClient(http).GetCatalogAsync(timeout.Token);
            releaseChoices.ItemsSource = releases;
            releaseChoices.SelectedIndex = releases.Count > 0 ? 0 : -1;
            releaseCatalogLoaded = true;
            updateStatus.Foreground = releases.Count > 0 ? LauncherSuccess : LauncherMuted;
            updateStatus.Text = releases.Count > 0 ? "Choose a release tag. Releases are ordered by their last update time." : "No releases have been published.";
        }
        catch (Exception exception) when (!updateLifetime.IsCancellationRequested)
        {
            updateStatus.Foreground = LauncherProblem;
            updateStatus.Text = "Could not load releases: " + exception.Message + " Turn the release selector off and on to retry.";
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            checkingUpdates = false;
            SetBusy(busy);
            RefreshReleaseSelection();
        }
    }

    private async Task InstallUpdateAsync()
    {
        string? selectedTag = selectOtherRelease.IsChecked == true ? (releaseChoices.SelectedItem as ReleaseCatalogEntry)?.Tag : null;
        if (selectOtherRelease.IsChecked == true && selectedTag is null)
        {
            return;
        }
        if (updating || !LauncherCapabilities.CanUpdate)
        {
            return;
        }
        updateStatus.Foreground = LauncherMuted;
        updating = true;
        SetBusy(true);
        try
        {
            using HttpClient http = CreateUpdateClient();
            ReleaseClient client = new(http);
            UpdateRelease? release;
            using (CancellationTokenSource metadataTimeout = CancellationTokenSource.CreateLinkedTokenSource(updateLifetime.Token))
            {
                metadataTimeout.CancelAfter(TimeSpan.FromSeconds(30));
                updateStatus.Text = "Checking the selected release…";
                release = selectedTag is null ? await client.GetAsync(UpdateChannel.Release, metadataTimeout.Token) : await client.GetByTagAsync(selectedTag, metadataTimeout.Token);
            }
            if (release is null)
            {
                updateStatus.Text = "The selected release is no longer available.";
                return;
            }
            string root = MacBundleUpdate.NormalizeRoot(AppContext.BaseDirectory);
            PackageManifest installed = await Task.Run(() => PackageManifest.Read(root));
            if (release.BuildId == installed.BuildId)
            {
                updateStatus.Text = "This build is already installed.";
                return;
            }
            string prompt = $"Download {release.Name} ({release.Size / 1048576d:F1} MiB)?\n\nCurrent build: {installed.Version}\nYou can keep playing during the download. You will be asked before restarting to install. Selecting another release may install an older build. Mods, saves and settings are preserved.";
            if (!await LauncherDialogs.ConfirmAsync(this, "Memoria — install update", prompt))
            {
                return;
            }
            updateProgress.IsVisible = true;
            updateProgress.Value = 0;
            updateStatus.Text = "Downloading and verifying the update…";
            using CancellationTokenSource downloadTimeout = CancellationTokenSource.CreateLinkedTokenSource(updateLifetime.Token);
            downloadTimeout.CancelAfter(TimeSpan.FromMinutes(20));
            updateDownload = downloadTimeout;
            cancelUpdate.IsVisible = true;
            (UpdateRelease Release, string Archive)? downloaded = await UpdatePackageCache.GetArchiveWithNightlyRefreshAsync(root, client, release, latest => LauncherDialogs.ConfirmAsync(this, "Memoria — nightly changed", $"A new nightly was published during download. Download {latest.Name} ({latest.Size / 1048576d:F1} MiB)?"), new Progress<double>(value =>
            {
                updateProgress.Value = value * 100;
                updateStatus.Text = $"Downloading the update — {value:P0}";
            }), downloadTimeout.Token);
            if (downloaded is not { } package)
            {
                updateStatus.Text = "Download cancelled. The installed game remains available.";
                return;
            }
            release = package.Release;
            cancelUpdate.IsVisible = false;
            updateDownload = null;
            updateProgress.IsIndeterminate = true;
            updateStatus.Text = "Validating the distribution and preparing installation…";
            string work = await Task.Run(() => UpdateInstaller.Prepare(root));
            await Task.Run(() =>
            {
                string staged = MacBundleUpdate.StagingPath(root);
                PackageManifest.Extract(package.Archive, staged, installed.RuntimeIdentifier);
                PackageManifest incoming = PackageManifest.Read(staged);
                if (release.BuildId is not null && incoming.BuildId != release.BuildId)
                {
                    throw new InvalidDataException("The downloaded build identity does not match its release metadata. Check for updates again.");
                }
                if (release.EngineCompatibilityVersion is string expected && EngineBuildMetadata.ReadInstalledCompatibilityVersion(staged) != expected)
                {
                    throw new InvalidDataException("The downloaded engine compatibility version does not match the update feed.");
                }
                UpdateInstaller.CreateJournal(root, incoming);
            });
            updateProgress.IsVisible = false;
            bool gameRunning = runningGame is { HasExited: false };
            string restartPrompt = gameRunning
                ? "The update is ready. Restart the game to update now?\n\nMemoria will create a separate automatic update save, install the update, then start the game and restore that save. Choose No to keep playing; the downloaded package will be kept."
                : "The update is ready. Restart Memoria to install it now?\n\nChoose No to keep the downloaded package for later.";
            if (!await LauncherDialogs.ConfirmAsync(this, "Memoria — restart for update", restartPrompt))
            {
                updateStatus.Text = "Update downloaded and kept. Select this release again to install without downloading it again.";
                return;
            }
            if (runningGame is { HasExited: false } activeProcess)
            {
                using System.Diagnostics.Process process = System.Diagnostics.Process.GetProcessById(activeProcess.Id);
                GameUpdateSession control = gameUpdateSession ?? throw new InvalidOperationException("The running game has no update connection.");
                string gameDirectory = runningGameDirectory ?? throw new InvalidOperationException("The running game directory is unknown.");
                try
                {
                    updateStatus.Text = "Asking the game to save before updating…";
                    await control.SaveAsync(() => process.HasExited);
                    new UpdateResume(gameDirectory, control.SavePath, runningAchievementsLocalOnly).Write(root);
                    updateStatus.Text = "Save confirmed. Closing the game…";
                    await control.RequestAsync("quit", () => process.HasExited);
                    using CancellationTokenSource exitTimeout = new(TimeSpan.FromSeconds(30));
                    await process.WaitForExitAsync(exitTimeout.Token);
                }
                catch (Exception exception)
                {
                    if (!process.HasExited)
                    {
                        try
                        {
                            await control.RequestAsync("cancel-update", () => process.HasExited, TimeSpan.FromSeconds(3));
                        }
                        catch (Exception cancelException)
                        {
                            status.Text = "Could not resume the game after the failed update request: " + cancelException.Message;
                        }
                    }
                    if (UpdateResume.TryDelete(root) is string warning)
                    {
                        status.Text = warning;
                    }
                    throw new InvalidOperationException("Automatic update stopped: " + exception.Message + "\nPlease finish the game yourself, close it, and install the update. The downloaded package has been kept.", exception);
                }
            }
            updateStatus.Foreground = LauncherSuccess;
            updateStatus.Text = "Starting the installer. Memoria will restart automatically…";
            await UpdateInstaller.StartHelperAsync(root);
            closingForUpdate = true;
            Close();
        }
        catch (OperationCanceledException)
        {
            updateStatus.Text = "Download cancelled. The installed game remains available for offline play.";
        }
        catch (Exception exception)
        {
            updateStatus.Foreground = LauncherProblem;
            updateStatus.Text = "Update failed: " + exception.Message;
            await ShowErrorAsync("Could not install the update.", exception);
        }
        finally
        {
            updateProgress.IsVisible = false;
            updateProgress.IsIndeterminate = false;
            cancelUpdate.IsVisible = false;
            updateDownload = null;
            updating = false;
            SetBusy(false);
            RefreshReleaseSelection();
        }
    }
}
