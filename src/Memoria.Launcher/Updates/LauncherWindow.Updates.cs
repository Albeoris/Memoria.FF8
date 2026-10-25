using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Memoria.Launcher;

internal sealed partial class LauncherWindow
{
    private readonly Button releaseUpdate = new() { Content = "Latest release", MinWidth = 180, MinHeight = 32, Margin = new Thickness(0, 0, 10, 0) };
    private readonly Button nightlyUpdate = new() { Content = "Nightly", MinWidth = 180, MinHeight = 32 };
    private readonly TextBlock updateStatus = new() { Text = "Checking for updates…", TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DimGray, Margin = new Thickness(0, 6, 0, 0) };
    private readonly ProgressBar updateProgress = new() { Height = 8, Minimum = 0, Maximum = 100, Visibility = Visibility.Collapsed, Margin = new Thickness(0, 8, 0, 0) };
    private readonly CancellationTokenSource updateLifetime = new();
    private bool checkingUpdates;

    private StackPanel CreateUpdatePanel()
    {
        StackPanel panel = new() { Margin = new Thickness(0, 0, 0, 14) };
        WrapPanel buttons = new();
        buttons.Children.Add(releaseUpdate);
        buttons.Children.Add(nightlyUpdate);
        panel.Children.Add(buttons);
        panel.Children.Add(updateStatus);
        panel.Children.Add(updateProgress);
        releaseUpdate.Click += async (_, _) => await InstallUpdateAsync(UpdateChannel.Release);
        nightlyUpdate.Click += async (_, _) => await InstallUpdateAsync(UpdateChannel.Nightly);
        return panel;
    }

    private static HttpClient CreateUpdateClient() => new() { Timeout = Timeout.InfiniteTimeSpan };

    private async Task CheckUpdatesAsync()
    {
        checkingUpdates = true;
        releaseUpdate.IsEnabled = false;
        nightlyUpdate.IsEnabled = false;
        try
        {
            using HttpClient http = CreateUpdateClient();
            using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(updateLifetime.Token);
            timeout.CancelAfter(TimeSpan.FromSeconds(30));
            ReleaseClient client = new(http);
            Task<string> release = DescribeChannelAsync(client, UpdateChannel.Release, timeout.Token);
            Task<string> nightly = DescribeChannelAsync(client, UpdateChannel.Nightly, timeout.Token);
            string[] descriptions = await Task.WhenAll(release, nightly);
            PackageManifest? installed = PackageManifest.LoadOptional(AppContext.BaseDirectory);
            string current = installed is null ? "Development build" : $"Installed: {installed.Version} ({installed.BuildId[..Math.Min(8, installed.BuildId.Length)]})";
            updateStatus.Text = current + Environment.NewLine + string.Join(Environment.NewLine, descriptions);
        }
        catch (Exception exception) when (!updateLifetime.IsCancellationRequested)
        {
            updateStatus.Text = "Could not check updates: " + exception.Message + " Click a channel to retry.";
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            checkingUpdates = false;
            SetBusy(busy);
        }
    }

    private async Task<string> DescribeChannelAsync(ReleaseClient client, UpdateChannel channel, CancellationToken cancellationToken)
    {
        try
        {
            UpdateRelease? release = await client.GetAsync(channel, cancellationToken);
            string name = channel == UpdateChannel.Release ? "Release" : "Nightly";
            if (release is null)
            {
                return name + ": no build published.";
            }
            PackageManifest? installed = PackageManifest.LoadOptional(AppContext.BaseDirectory);
            bool current = installed is not null && release.BuildId == installed.BuildId;
            return $"{name}: {release.Name} · {release.UpdatedAt.ToLocalTime():g}" + (current ? " · installed" : " · available");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return $"{channel}: {exception.Message} Click to retry.";
        }
    }

    private async Task InstallUpdateAsync(UpdateChannel channel)
    {
        if (updating)
        {
            return;
        }
        updating = true;
        SetBusy(true);
        try
        {
            using HttpClient http = CreateUpdateClient();
            ReleaseClient client = new(http);
            UpdateRelease? release;
            using (CancellationTokenSource metadataTimeout = new(TimeSpan.FromSeconds(30)))
            {
                updateStatus.Text = "Checking the selected channel…";
                release = await client.GetAsync(channel, metadataTimeout.Token);
            }
            if (release is null)
            {
                updateStatus.Text = "No build is published on this channel yet.";
                return;
            }
            string root = Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory);
            PackageManifest installed = PackageManifest.Read(root);
            if (release.BuildId == installed.BuildId)
            {
                updateStatus.Text = "This build is already installed.";
                return;
            }
            string prompt = $"Download {release.Name} ({release.Size / 1048576d:F1} MiB)?\n\nCurrent build: {installed.Version}\nYou can keep playing during the download. You will be asked before restarting to install. Switching channels may install an older build. Mods, saves and settings are preserved.";
            if (MessageBox.Show(this, prompt, "Memoria — install update", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes)
            {
                return;
            }
            updateProgress.Visibility = Visibility.Visible;
            updateProgress.Value = 0;
            updateStatus.Text = "Downloading and verifying the update…";
            using CancellationTokenSource downloadTimeout = new(TimeSpan.FromMinutes(20));
            string archive = await UpdatePackageCache.GetArchiveAsync(root, client, release, new Progress<double>(value =>
            {
                updateProgress.Value = value * 100;
                updateStatus.Text = $"Downloading the update — {value:P0}";
            }), downloadTimeout.Token);
            updateProgress.IsIndeterminate = true;
            updateStatus.Text = "Validating the distribution and preparing installation…";
            string work = await Task.Run(() => UpdateInstaller.Prepare(root));
            await Task.Run(() =>
            {
                string staged = Path.Combine(work, "new");
                PackageManifest.Extract(archive, staged);
                PackageManifest incoming = PackageManifest.Read(staged);
                if (release.BuildId is not null && incoming.BuildId != release.BuildId)
                {
                    throw new InvalidDataException("The downloaded build identity does not match its release metadata. Check for updates again.");
                }
                UpdateInstaller.CreateJournal(root, incoming);
            });
            updateProgress.Visibility = Visibility.Collapsed;
            bool gameRunning = runningGame is { HasExited: false };
            string restartPrompt = gameRunning
                ? "The update is ready. Restart the game to update now?\n\nMemoria will create a separate automatic update save, install the update, then start the game and restore that save. Choose No to keep playing; the downloaded package will be kept."
                : "The update is ready. Restart Memoria to install it now?\n\nChoose No to keep the downloaded package for later.";
            if (MessageBox.Show(this, restartPrompt, "Memoria — restart for update", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes)
            {
                updateStatus.Text = "Update downloaded and kept. Select this channel again to install without downloading it again.";
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
            updateStatus.Text = "Starting the installer. Memoria will restart automatically…";
            await UpdateInstaller.StartHelperAsync(root);
            closingForUpdate = true;
            Close();
        }
        catch (Exception exception)
        {
            updateStatus.Text = "Update failed: " + exception.Message;
            ShowError("Could not install the update.", exception);
        }
        finally
        {
            updateProgress.Visibility = Visibility.Collapsed;
            updateProgress.IsIndeterminate = false;
            updating = false;
            SetBusy(false);
        }
    }
}
