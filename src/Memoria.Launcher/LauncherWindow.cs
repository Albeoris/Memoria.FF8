using System.ComponentModel;
using System.Diagnostics;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Memoria.FF8;
using Microsoft.Win32;

namespace Memoria.Launcher;

internal sealed partial class LauncherWindow : Window
{
    private readonly ComboBox installations = new() { MinHeight = 32, IsEditable = false };
    private readonly Button browse = new() { Content = "Browse…", MinWidth = 92, Margin = new Thickness(10, 0, 0, 0) };
    private readonly Button mediaSetup = new() { Content = "Set up FFmpeg", MinWidth = 130, MinHeight = 32 };
    private readonly Button play = new() { Content = "Play", MinWidth = 130, MinHeight = 38, IsDefault = true };
    private readonly TextBlock mediaStatus = new() { Text = "Checking FFmpeg…", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 0) };
    private readonly ProgressBar mediaProgress = new() { Height = 8, Minimum = 0, Maximum = 100 };
    private readonly TextBlock mediaProgressText = new() { Margin = new Thickness(0, 6, 0, 0), Foreground = Brushes.DimGray };
    private readonly StackPanel mediaProgressPanel = new() { Visibility = Visibility.Collapsed, Margin = new Thickness(0, 10, 0, 0) };
    private readonly TextBox status = new() { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, BorderThickness = new Thickness(0), Background = Brushes.Transparent };
    private readonly string mediaDirectory = MediaToolsInstallation.ResolveDirectory();
    private bool busy;

    public LauncherWindow()
    {
        Title = "Memoria — FINAL FANTASY VIII";
        Width = 740;
        Height = 600;
        MinWidth = 600;
        MinHeight = 560;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        FontFamily = new FontFamily("Segoe UI");
        FontSize = 14;
        Background = new SolidColorBrush(Color.FromRgb(245, 247, 250));

        Grid layout = new() { Margin = new Thickness(28) };
        foreach (GridLength height in new[] { GridLength.Auto, GridLength.Auto, GridLength.Auto, GridLength.Auto, GridLength.Auto, new GridLength(1, GridUnitType.Star), GridLength.Auto })
        {
            layout.RowDefinitions.Add(new RowDefinition { Height = height });
        }
        Add(layout, new TextBlock { Text = "Memoria", FontSize = 32, FontWeight = FontWeights.SemiBold }, 0);
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
        Add(layout, mediaSection, 3);
        Add(layout, CreateUpdatePanel(), 4);
        Add(layout, status, 5);
        play.HorizontalAlignment = HorizontalAlignment.Right;
        play.Margin = new Thickness(0, 16, 0, 0);
        Add(layout, play, 6);
        Content = layout;
        browse.Click += Browse;
        mediaSetup.Click += async (_, _) => await RunSetupAsync();
        play.Click += async (_, _) => await PlayAsync();
        Loaded += async (_, _) => await InitializeAsync();
        Closing += OnClosing;
    }

    private static void Add(Grid grid, UIElement control, int row)
    {
        Grid.SetRow(control, row);
        grid.Children.Add(control);
    }

    private async Task InitializeAsync()
    {
        SetBusy(true);
        try
        {
            List<string> warnings = [];
            string? previous = LauncherSettings.Load(LauncherSettings.DefaultPath).LastGameDirectory;
            IReadOnlyList<GameInstallation> found = await Task.Run(() => GameInstallationDiscovery.Find(warnings.Add));
            installations.ItemsSource = GameInstallationSelection.Order(previous, found);
            if (installations.Items.Count > 0)
            {
                installations.SelectedIndex = 0;
            }
            status.Text = warnings.Count > 0 ? string.Join(Environment.NewLine, warnings) : "Choose your installed game folder, then press Play. Browse to select a folder manually.";
            await RefreshMediaStatusAsync();
        }
        catch (Exception exception)
        {
            ShowError("Game discovery failed. You can still select a folder with Browse.", exception);
        }
        finally
        {
            SetBusy(false);
        }
        await CheckUpdatesAsync();
    }

    private void Browse(object sender, RoutedEventArgs args)
    {
        OpenFolderDialog dialog = new() { Title = "Select the installed FINAL FANTASY VIII folder", Multiselect = false };
        if (installations.SelectedItem is GameInstallation selected)
        {
            dialog.InitialDirectory = selected.Directory;
        }
        if (dialog.ShowDialog(this) == true)
        {
            List<GameInstallation> items = installations.Items.Cast<GameInstallation>().ToList();
            string folder = Path.GetFullPath(dialog.FolderName);
            GameInstallation? existing = items.FirstOrDefault(item => item.Directory.Equals(folder, StringComparison.OrdinalIgnoreCase));
            if (existing is null)
            {
                existing = new GameInstallation(folder, "Selected folder");
                items.Add(existing);
            }
            installations.ItemsSource = items;
            installations.SelectedItem = existing;
            status.Text = GameInstallationSelection.ValidateForEngine(folder) ?? "Game files found. Ready to play.";
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
            ShowError("FFmpeg setup failed.", exception);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task<bool> EnsureMediaAsync()
    {
        using HttpClient client = new() { Timeout = Timeout.InfiniteTimeSpan };
        MediaToolsInstallation installation = new(client, FfmpegDownloadOptions.FromEnvironment());
        try
        {
            bool ready = await installation.EnsureAsync(mediaDirectory, prompt => MessageBox.Show(this, prompt.Replace("You can continue without installing it.", "Cancel setup to stay in the launcher. FFmpeg must be ready before Play."), "Memoria — FFmpeg setup", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes, message => status.Text = message.Replace("Continuing without FFmpeg video playback (and replacement audio decoding).", "FFmpeg is not ready. Install the tools before pressing Play."), UpdateMediaProgress);
            await RefreshMediaStatusAsync();
            if (ready)
            {
                status.Text = "FFmpeg is ready. You can start the game.";
            }
            return ready;
        }
        finally
        {
            mediaProgressPanel.Visibility = Visibility.Collapsed;
            mediaProgress.IsIndeterminate = false;
        }
    }

    private void UpdateMediaProgress(MediaToolsProgress progress)
    {
        mediaStatus.Text = "Setting up FFmpeg…";
        mediaProgressPanel.Visibility = Visibility.Visible;
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
        bool ready = await MediaToolsInstallation.ValidateAsync(mediaDirectory);
        mediaStatus.Text = ready ? "FFmpeg is ready for video and audio playback." : "FFmpeg is not installed.";
    }

    private async Task PlayAsync()
    {
        if (installations.SelectedItem is not GameInstallation selected)
        {
            status.Text = "Select an installation, or use Browse to choose your game folder.";
            return;
        }
        if (GameInstallationSelection.ValidateForEngine(selected.Directory) is string validationError)
        {
            status.Text = validationError;
            return;
        }
        SetBusy(true);
        try
        {
            ProcessStartInfo start = GameInstallationSelection.CreateStartInfo(AppContext.BaseDirectory, selected.Directory);
            if (!File.Exists(start.FileName))
            {
                throw new FileNotFoundException("The engine is missing. Extract the complete Memoria archive, keeping its Data folder beside Memoria.Launcher.exe.", start.FileName);
            }
            if (!await EnsureMediaAsync())
            {
                return;
            }
            status.Text = "Starting the game…";
            using Process process = Process.Start(start) ?? throw new InvalidOperationException("The game process could not be started.");
            bool remembered = false;
            while (!process.HasExited)
            {
                process.Refresh();
                if (!remembered && process.MainWindowHandle != IntPtr.Zero)
                {
                    Remember(selected.Directory);
                    remembered = true;
                    WindowState = WindowState.Minimized;
                }
                await Task.Delay(200);
            }
            await process.WaitForExitAsync();
            WindowState = WindowState.Normal;
            Activate();
            if (process.ExitCode == 0)
            {
                if (!remembered)
                {
                    Remember(selected.Directory);
                }
                status.Text = "The game has closed. Ready to play again.";
            }
            else
            {
                status.Text = $"The game exited with code {process.ExitCode}.";
                MessageBox.Show(this, status.Text, "Memoria", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception exception)
        {
            WindowState = WindowState.Normal;
            ShowError("Could not start the game.", exception);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void Remember(string directory)
    {
        try
        {
            new LauncherSettings(directory).Save(LauncherSettings.DefaultPath);
            installations.ItemsSource = GameInstallationSelection.Order(directory, installations.Items.Cast<GameInstallation>());
            installations.SelectedIndex = 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            status.Text = $"The game started, but its folder could not be remembered: {exception.Message}";
        }
    }

    private void ShowError(string message, Exception exception)
    {
        status.Text = $"{message}{Environment.NewLine}{exception.Message}";
        MessageBox.Show(this, status.Text, "Memoria", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private void SetBusy(bool value)
    {
        busy = value;
        installations.IsEnabled = !value;
        browse.IsEnabled = !value;
        mediaSetup.IsEnabled = !value;
        play.IsEnabled = !value;
        releaseUpdate.IsEnabled = !value && !checkingUpdates;
        nightlyUpdate.IsEnabled = !value && !checkingUpdates;
    }

    private void OnClosing(object? sender, CancelEventArgs args)
    {
        if (busy)
        {
            args.Cancel = true;
            status.Text = "Wait for the current operation to finish, or close the game before closing the launcher.";
        }
        else
        {
            updateLifetime.Cancel();
        }
    }
}
