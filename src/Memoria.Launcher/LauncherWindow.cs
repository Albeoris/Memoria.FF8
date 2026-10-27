using System.Diagnostics;
using System.Net.Http;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Avalonia.Media;
using Memoria.FF8;
using Memoria.FF8.Platform;

namespace Memoria.Launcher;

internal sealed partial class LauncherWindow : Window
{
    private readonly ComboBox installations = new() { MinHeight = 32, IsEditable = false };
    private readonly ComboBox graphicsBackend = new() { MinHeight = 32, MinWidth = 180 };
    private readonly TextBlock graphicsStatus = new() { TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DimGray, Margin = new Thickness(0, 6, 0, 0) };
    private readonly Button tryAuto = new() { Content = "Try Auto", IsVisible = false, MinHeight = 30 };
    private GraphicsBackend requestedGraphicsBackend;
    private bool loadingGraphicsBackend;
    private bool graphicsPreferenceReadFailed;
    private readonly Button browse = new() { Content = "Browse…", MinWidth = 92, Margin = new Thickness(10, 0, 0, 0) };
    private readonly Button mediaSetup = new() { Content = "Set up FFmpeg", MinWidth = 130, MinHeight = 32 };
    private readonly Button play = new() { Content = "Play", MinWidth = 130, MinHeight = 38, IsDefault = true };
    private readonly CheckBox achievementsLocalOnly = new() { Content = "Keep achievements local (disable platform connection)", Margin = new Thickness(0, 12, 0, 0) };
    private readonly TextBlock mediaStatus = new() { Text = "Checking FFmpeg…", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 0) };
    private readonly ProgressBar mediaProgress = new() { Height = 8, Minimum = 0, Maximum = 100 };
    private readonly TextBlock mediaProgressText = new() { Margin = new Thickness(0, 6, 0, 0), Foreground = Brushes.DimGray };
    private readonly StackPanel mediaProgressPanel = new() { IsVisible = false, Margin = new Thickness(0, 10, 0, 0) };
    private readonly TextBox status = new() { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, BorderThickness = new Thickness(0), Background = Brushes.Transparent };
    private string mediaDirectory = MediaToolsInstallation.ResolveDirectory();
    private readonly Button cancel = new() { Content = "Cancel scan", IsVisible = false };
    private readonly CancellationTokenSource discoveryLifetime = new();
    private bool busy;
    private Process? runningGame;
    private GameUpdateSession? gameUpdateSession;
    private string? runningGameDirectory;
    private bool runningAchievementsLocalOnly;
    private readonly UpdateResume? updateResume;
    private bool updating;
    private bool closingForUpdate;

    public LauncherWindow(UpdateResume? updateResume = null)
    {
        this.updateResume = updateResume;
        Title = "Memoria — FINAL FANTASY VIII";
        Width = 740;
        Height = 690;
        MinWidth = 600;
        MinHeight = 560;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        FontFamily = FontFamily.Default;
        FontSize = 14;
        Background = new SolidColorBrush(Color.FromRgb(245, 247, 250));

        Grid layout = new() { Margin = new Thickness(28) };
        foreach (GridLength height in new[] { GridLength.Auto, GridLength.Auto, GridLength.Auto, GridLength.Auto, GridLength.Auto, new GridLength(1, GridUnitType.Star), GridLength.Auto })
        {
            layout.RowDefinitions.Add(new RowDefinition { Height = height });
        }
        Add(layout, new TextBlock { Text = "Memoria", FontSize = 32, FontWeight = FontWeight.SemiBold }, 0);
        Add(layout, new TextBlock { Text = "English Steam (2013) or Remastered · Remastered support is partial", Foreground = Brushes.DimGray, Margin = new Thickness(0, 4, 0, 24) }, 1);
        DockPanel selection = new() { LastChildFill = true, Margin = new Thickness(0, 0, 0, 20) };
        DockPanel.SetDock(browse, Dock.Right);
        selection.Children.Add(browse);
        selection.Children.Add(installations);
        Add(layout, selection, 2);
        DockPanel media = new();
        DockPanel.SetDock(mediaSetup, Dock.Right);
        media.Children.Add(mediaSetup);
        media.Children.Add(mediaStatus);
        StackPanel mediaSection = new() { Margin = new Thickness(0, 0, 0, 14) };
        mediaSection.Children.Add(media);
        mediaProgressPanel.Children.Add(mediaProgress);
        mediaProgressPanel.Children.Add(mediaProgressText);
        mediaSection.Children.Add(mediaProgressPanel);
        mediaSection.Children.Add(achievementsLocalOnly);
        StackPanel graphicsSelection = new() { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(0, 12, 0, 0) };
        graphicsSelection.Children.Add(new TextBlock { Text = "Graphics backend", VerticalAlignment = VerticalAlignment.Center });
        graphicsSelection.Children.Add(graphicsBackend);
        graphicsSelection.Children.Add(tryAuto);
        mediaSection.Children.Add(graphicsSelection);
        mediaSection.Children.Add(graphicsStatus);
        Add(layout, mediaSection, 3);
        Add(layout, CreateUpdatePanel(), 4);
        Add(layout, status, 5);
        play.HorizontalAlignment = HorizontalAlignment.Right;
        play.Margin = new Thickness(0, 16, 0, 0);
        Add(layout, play, 6);
        Content = layout;
        browse.Click += async (_, _) => await BrowseAsync();
        cancel.Click += (_, _) => discoveryLifetime.Cancel();
        mediaSection.Children.Add(cancel);
        installations.SelectionChanged += async (_, _) => await ValidateSelectionAsync();
        achievementsLocalOnly.IsCheckedChanged += (_, _) => SaveSelection();
        graphicsBackend.ItemsSource = GraphicsBackendSelection.Available(HostPlatforms.Current);
        graphicsBackend.SelectionChanged += async (_, _) => await SaveGraphicsBackendAsync();
        tryAuto.Click += (_, _) => graphicsBackend.SelectedItem = GraphicsBackend.Auto;
        mediaSetup.Click += async (_, _) => await RunSetupAsync();
        play.Click += async (_, _) => await PlayAsync();
        Opened += async (_, _) => await InitializeAsync();
        Closing += OnClosing;
    }

    private static void Add(Grid grid, Control control, int row)
    {
        Grid.SetRow(control, row);
        grid.Children.Add(control);
    }

    private async Task InitializeAsync()
    {
        SetBusy(true);
        try
        {
            cancel.IsVisible = true;
            List<string> warnings = [];
            LauncherSettings settings = LauncherSettings.Load(LauncherSettings.DefaultPath);
            await LoadGraphicsBackendAsync();
            string? previous = settings.LastGameDirectory;
            achievementsLocalOnly.IsChecked = settings.AchievementsLocalOnly;
            IReadOnlyList<GameInstallation> found = await Task.Run(() => GameInstallationDiscovery.Find(warnings.Add, discoveryLifetime.Token), discoveryLifetime.Token);
            installations.ItemsSource = GameInstallationSelection.Order(previous, found);
            if (installations.Items.Count > 0)
            {
                installations.SelectedIndex = 0;
            }
            status.Text = warnings.Count > 0 ? string.Join(Environment.NewLine, warnings) : "Choose your installed game folder, then press Play. Browse to select a folder manually.";
            await RefreshMediaStatusAsync();
        }
        catch (OperationCanceledException)
        {
            status.Text = "Discovery cancelled. Browse to select your game folder.";
        }
        catch (Exception exception)
        {
            await ShowErrorAsync("Game discovery failed. You can still select a folder with Browse.", exception);
        }
        finally
        {
            cancel.IsVisible = false;
            SetBusy(false);
        }
        if (!OperatingSystem.IsWindows())
        {
            status.Text = LauncherCapabilities.GameStatus;
        }
        if (updateResume is not null)
        {
            await PlayAsync(updateResume);
            return;
        }
        await CheckUpdatesAsync();
    }

    private async Task LoadGraphicsBackendAsync()
    {
        loadingGraphicsBackend = true;
        try
        {
            graphicsPreferenceReadFailed = false;
            requestedGraphicsBackend = await Task.Run(() => GraphicsBackendPreferences.Read(GraphicsBackendPreferences.DefaultPath));
            graphicsBackend.SelectedItem = GraphicsBackendSelection.Available(HostPlatforms.Current).Contains(requestedGraphicsBackend) ? requestedGraphicsBackend : null;
            UpdateGraphicsStatus();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidOperationException)
        {
            graphicsPreferenceReadFailed = true;
            graphicsStatus.Text = "Could not read graphics preference: " + exception.Message + " Try Auto to save a new preference.";
            tryAuto.IsVisible = true;
        }
        finally
        {
            loadingGraphicsBackend = false;
        }
    }

    private async Task SaveGraphicsBackendAsync()
    {
        if (loadingGraphicsBackend || graphicsBackend.SelectedItem is not GraphicsBackend selected)
        {
            return;
        }
        graphicsPreferenceReadFailed = false;
        requestedGraphicsBackend = selected;
        UpdateGraphicsStatus();
        SetBusy(true);
        try
        {
            await Task.Run(() => GraphicsBackendPreferences.Write(GraphicsBackendPreferences.DefaultPath, selected));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidDataException)
        {
            graphicsStatus.Text = "Could not save graphics preference: " + exception.Message;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void UpdateGraphicsStatus()
    {
        string? unavailable = GraphicsBackendSelection.UnavailableReason(requestedGraphicsBackend, HostPlatforms.Current);
        graphicsStatus.Text = unavailable is null ? $"Requested: {requestedGraphicsBackend}. Applies on the next start. Actual backend is reported after graphics initialization." : $"Requested: {requestedGraphicsBackend}. {unavailable}";
        tryAuto.IsVisible = unavailable is not null;
    }

    private async Task BrowseAsync()
    {
        try
        {
            IReadOnlyList<IStorageFolder> folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Select the installed FINAL FANTASY VIII folder", AllowMultiple = false });
            if (folders.Count == 0)
            {
                return;
            }
            using IStorageFolder chosen = folders[0];
            string folder = chosen.TryGetLocalPath() ?? throw new IOException("Select a local game folder.");
            List<GameInstallation> items = installations.Items.Cast<GameInstallation>().ToList();
            GameInstallation? existing = items.FirstOrDefault(item => LauncherCapabilities.PathComparer.Equals(item.Directory, folder));
            if (existing is null)
            {
                existing = new GameInstallation(folder, "Selected folder");
                items.Add(existing);
            }
            installations.ItemsSource = items;
            installations.SelectedItem = existing;
            await ValidateSelectionAsync();
        }
        catch (Exception exception)
        {
            await ShowErrorAsync("Could not select the game folder.", exception);
        }
    }

    private async Task ValidateSelectionAsync()
    {
        if (installations.SelectedItem is not GameInstallation selected)
        {
            return;
        }
        string? error = await Task.Run(() => GameInstallationSelection.ValidateForEngine(selected.Directory));
        if (!Equals(installations.SelectedItem, selected))
        {
            return;
        }
        status.Text = error ?? (OperatingSystem.IsWindows() ? "Game files found. Ready to play. Offline play is available without checking for updates." : "Game files found. " + LauncherCapabilities.GameStatus);
        SaveSelection();
    }

    private void SaveSelection()
    {
        if (installations.SelectedItem is not GameInstallation selected)
        {
            return;
        }
        try
        {
            new LauncherSettings(selected.Directory, achievementsLocalOnly.IsChecked == true).Save(LauncherSettings.DefaultPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            status.Text = "Could not save launcher settings: " + exception.Message;
        }
    }

    private async Task RunSetupAsync()
    {
        SetBusy(true);
        try
        {
            await EnsureMediaAsync();
        }
        catch (Exception exception)
        {
            await ShowErrorAsync("FFmpeg setup failed.", exception);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task<bool> EnsureMediaAsync()
    {
        IProgress<string> discoveryReport = new Progress<string>(message => status.Text = message);
        mediaDirectory = await MediaToolsInstallation.ResolveDirectoryAsync(report: discoveryReport.Report, cancellationToken: updateLifetime.Token);
        if (await MediaToolsInstallation.ValidateAsync(mediaDirectory, discoveryReport.Report, updateLifetime.Token))
        {
            mediaStatus.Text = "FFmpeg is ready for video and audio playback.";
            return true;
        }
        // Only the configured or managed destination may receive an installation, never a PATH discovery result.
        mediaDirectory = MediaToolsInstallation.ResolveDirectory();
        using HttpClient client = new() { Timeout = Timeout.InfiniteTimeSpan };
        FfmpegDownloadOptions options = FfmpegDownloadOptions.FromEnvironment();
        MediaToolsInstallation installation = new(client, options);
        try
        {
            if (!OperatingSystem.IsMacOS() && !await MediaToolsInstallation.ValidateAsync(mediaDirectory) && !await LauncherDialogs.ConfirmAsync(this, "Memoria — FFmpeg setup", $"Download and verify {options.ArchiveName} for video and replacement audio playback?\n\nSource: {options.ReleaseUrl}\nInstall to: {mediaDirectory}\n\nFFmpeg is a separate third-party LGPL dependency. License notices are kept with the tools. Cancel to stay in the launcher."))
            {
                return false;
            }
            bool ready = await installation.EnsureAsync(mediaDirectory, _ => true, message => status.Text = message, UpdateMediaProgress);
            await RefreshMediaStatusAsync();
            if (ready)
            {
                status.Text = "FFmpeg tools are installed and validated.";
            }
            return ready;
        }
        finally
        {
            mediaProgressPanel.IsVisible = false;
            mediaProgress.IsIndeterminate = false;
        }
    }

    private void UpdateMediaProgress(MediaToolsProgress progress)
    {
        mediaStatus.Text = "Setting up FFmpeg…";
        mediaProgressPanel.IsVisible = true;
        mediaProgress.IsIndeterminate = progress.Stage != MediaToolsStage.Complete && (progress.Stage != MediaToolsStage.Downloading || progress.TotalBytes is not > 0);
        mediaProgress.Value = progress.Stage == MediaToolsStage.Complete ? 100 : progress.TotalBytes is > 0 ? Math.Clamp(100d * progress.BytesReceived / progress.TotalBytes.Value, 0, 100) : 0;
        mediaProgressText.Text = progress.Stage switch
        {
            MediaToolsStage.FetchingChecksums => "Fetching FFmpeg checksums…",
            MediaToolsStage.Downloading when progress.TotalBytes is > 0 => $"Downloading FFmpeg — {mediaProgress.Value:F0}% · {progress.BytesReceived / 1048576d:F1} / {progress.TotalBytes.Value / 1048576d:F1} MiB",
            MediaToolsStage.Downloading => $"Downloading FFmpeg — {progress.BytesReceived / 1048576d:F1} MiB received",
            MediaToolsStage.Verifying => "Verifying FFmpeg checksum…",
            MediaToolsStage.Extracting => "Extracting FFmpeg…",
            MediaToolsStage.Validating => "Checking FFmpeg executables…",
            MediaToolsStage.Complete => "FFmpeg is installed and ready.",
            _ => throw new ArgumentOutOfRangeException(nameof(progress))
        };
        status.Text = string.Empty;
    }

    private async Task RefreshMediaStatusAsync()
    {
        IProgress<string> report = new Progress<string>(message => status.Text = message);
        mediaDirectory = await MediaToolsInstallation.ResolveDirectoryAsync(report: report.Report, cancellationToken: updateLifetime.Token);
        List<string> diagnostics = [];
        bool ready = await MediaToolsInstallation.ValidateAsync(mediaDirectory, diagnostics.Add, updateLifetime.Token);
        mediaStatus.Text = ready ? "FFmpeg is ready for video and audio playback." : "FFmpeg is unavailable. Use setup or select an LGPL tool pair.";
        if (!ready && diagnostics.Count > 0)
        {
            status.Text = string.Join(Environment.NewLine, diagnostics);
        }
    }

    private async Task PlayAsync(UpdateResume? resume = null)
    {
        if (!LauncherCapabilities.CanLaunchGame)
        {
            status.Text = LauncherCapabilities.GameStatus;
            return;
        }
        GameInstallation? selected = resume is null ? installations.SelectedItem as GameInstallation : new GameInstallation(resume.GameDirectory, "Update resume");
        if (selected is null)
        {
            status.Text = "Select an installation, or use Browse to choose your game folder.";
            return;
        }
        if (await Task.Run(() => GameInstallationSelection.ValidateForEngine(selected.Directory)) is string validationError)
        {
            status.Text = validationError;
            return;
        }
        await LoadGraphicsBackendAsync();
        if (graphicsPreferenceReadFailed)
        {
            return;
        }
        if (GraphicsBackendSelection.UnavailableReason(requestedGraphicsBackend, HostPlatforms.Current) is string unavailable)
        {
            graphicsStatus.Text = unavailable;
            tryAuto.IsVisible = true;
            return;
        }
        SetBusy(true);
        try
        {
            bool localOnly = resume?.AchievementsLocalOnly ?? achievementsLocalOnly.IsChecked == true;
            achievementsLocalOnly.IsChecked = localOnly;
            runningAchievementsLocalOnly = localOnly;
            ProcessStartInfo start = GameInstallationSelection.CreateStartInfo(AppContext.BaseDirectory, selected.Directory, localOnly, graphicsBackend: requestedGraphicsBackend);
            if (!File.Exists(start.FileName))
            {
                throw new FileNotFoundException("The engine is missing. Extract the complete Memoria archive, keeping its Data folder beside Memoria.Launcher.exe.", start.FileName);
            }
            if (!await EnsureMediaAsync())
            {
                return;
            }
            start = GameInstallationSelection.CreateStartInfo(AppContext.BaseDirectory, selected.Directory, localOnly, mediaDirectory, requestedGraphicsBackend);
            status.Text = "Starting the game…";
            GameUpdateSession control = new();
            start.ArgumentList.Add("--update-control-dir");
            start.ArgumentList.Add(control.ControlDirectory);
            if (resume is not null)
            {
                start.ArgumentList.Add("--state");
                start.ArgumentList.Add(resume.SavePath);
            }
            using TextWriter? console = GameConsole.Open();
            using GameProcessDiagnostics diagnostics = new(console: console);
            using Process process = Process.Start(start) ?? throw new InvalidOperationException("The game process could not be started.");
            runningGame = process;
            runningGameDirectory = selected.Directory;
            gameUpdateSession = control;
            SetBusy(true);
            Task capture = diagnostics.CaptureAsync(process);
            bool remembered = false;
            while (!process.HasExited)
            {
                process.Refresh();
                if (!remembered && diagnostics.GraphicsReady is string ready)
                {
                    graphicsStatus.Text = ready;
                    Remember(selected.Directory);
                    remembered = true;
                    WindowState = WindowState.Minimized;
                    if (resume is not null)
                    {
                        if (UpdateResume.TryDelete(AppContext.BaseDirectory) is string warning)
                        {
                            status.Text = warning;
                        }
                    }
                }
                await Task.Delay(200);
            }
            await process.WaitForExitAsync();
            await capture;
            string completion = diagnostics.Complete(process.ExitCode);
            if (!updating)
            {
                WindowState = WindowState.Normal;
                Activate();
            }
            if (process.ExitCode == 0)
            {
                if (!remembered && diagnostics.GraphicsReady is string ready)
                {
                    graphicsStatus.Text = ready;
                    Remember(selected.Directory);
                }
                if (diagnostics.GraphicsReady is null)
                {
                    tryAuto.IsVisible = requestedGraphicsBackend != GraphicsBackend.Auto;
                    status.Text = "The host exited without confirming graphics initialization." + Environment.NewLine + completion;
                }
                else
                {
                    status.Text = completion;
                }
            }
            else
            {
                tryAuto.IsVisible = diagnostics.GraphicsReady is null && requestedGraphicsBackend != GraphicsBackend.Auto;
                status.Text = completion + (resume is null ? string.Empty : Environment.NewLine + "Automatic update save retained at: " + resume.SavePath);
                await LauncherDialogs.ShowAsync(this, "Memoria", status.Text);
            }
        }
        catch (Exception exception)
        {
            WindowState = WindowState.Normal;
            tryAuto.IsVisible = requestedGraphicsBackend != GraphicsBackend.Auto;
            await ShowErrorAsync("Could not start the game.", exception);
        }
        finally
        {
            runningGame = null;
            runningGameDirectory = null;
            gameUpdateSession = null;
            SetBusy(false);
        }
    }

    private void Remember(string directory)
    {
        try
        {
            new LauncherSettings(directory, achievementsLocalOnly.IsChecked == true).Save(LauncherSettings.DefaultPath);
            installations.ItemsSource = GameInstallationSelection.Order(directory, installations.Items.Cast<GameInstallation>());
            installations.SelectedIndex = 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            status.Text = $"The game started, but its folder could not be remembered: {exception.Message}";
        }
    }

    private async Task ShowErrorAsync(string message, Exception exception)
    {
        status.Text = $"{message}{Environment.NewLine}{exception.Message}";
        await LauncherDialogs.ShowAsync(this, "Memoria", status.Text);
    }

    private void SetBusy(bool value)
    {
        busy = value || updating || runningGame is not null;
        installations.IsEnabled = !busy;
        browse.IsEnabled = !busy;
        achievementsLocalOnly.IsEnabled = !busy;
        graphicsBackend.IsEnabled = !busy;
        tryAuto.IsEnabled = !busy;
        mediaSetup.IsEnabled = !busy;
        play.IsEnabled = !busy && LauncherCapabilities.CanLaunchGame;
        releaseUpdate.IsEnabled = (!busy || runningGame is not null) && !updating && !checkingUpdates && LauncherCapabilities.CanUpdate;
        nightlyUpdate.IsEnabled = (!busy || runningGame is not null) && !updating && !checkingUpdates && LauncherCapabilities.CanUpdate;
    }

    private void OnClosing(object? sender, WindowClosingEventArgs args)
    {
        if (busy && !closingForUpdate)
        {
            args.Cancel = true;
            status.Text = "Wait for the current operation to finish, or close the game before closing the launcher.";
        }
        else
        {
            updateLifetime.Cancel();
            discoveryLifetime.Cancel();
        }
    }
}
