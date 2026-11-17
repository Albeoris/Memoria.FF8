using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Memoria.FF7.Resources;
using Memoria.FF8.Platform;

namespace Memoria.Launcher;

internal sealed partial class LauncherWindow
{
    private readonly ComboBox ff7Installations = new() { MinHeight = 32, HorizontalAlignment = HorizontalAlignment.Stretch, PlaceholderText = "Select an installed FINAL FANTASY VII folder" };
    private readonly TextBlock ff7Status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly Button selectFf7 = new() { Content = "Browse...", MinWidth = 100 };
    private readonly Button clearFf7 = new() { Content = "Clear", MinWidth = 100 };
    private readonly Button startFf7 = new() { Content = "Start FFVII debug", MinHeight = 38, MinWidth = 180 };
    private string? selectedFf7GameDirectory;
    private bool ff7ResourcesAvailable;
    private bool updatingFf7Installations;

    private Control CreateFf7Selection()
    {
        StackPanel panel = new() { Spacing = 18 };
        panel.Children.Add(CreateGameHero("FINAL FANTASY VII", "Experimental field gameplay", "A NEW WORLD TO EXPLORE"));
        StackPanel resources = new() { Spacing = 12 };
        resources.Children.Add(ff7Installations);
        WrapPanel actions = new();
        selectFf7.Margin = new Thickness(0, 0, 8, 0);
        actions.Children.Add(selectFf7);
        actions.Children.Add(clearFf7);
        resources.Children.Add(actions);
        resources.Children.Add(ff7Status);
        panel.Children.Add(LauncherCard("Game installation", "Use the current Steam release (app 3837340). Steam 2013 (app 39140) is unsupported.", resources));
        panel.Children.Add(LauncherDescription("The shared engine also needs your FINAL FANTASY VIII installation. Select it using the game selector above before starting FFVII."));
        panel.Children.Add(LauncherDescription("The debug host supports the opening field route and Memoria crossover saves. Original FFVII saves and full-game compatibility are not supported. Play FFVII opens its original title; choose New Game or Continue there."));
        panel.Children.Add(LauncherDescription("Use the in-game Debug Menu World switch to change worlds at an allowed save boundary."));
        ff7Installations.SelectionChanged += (_, _) =>
        {
            if (updatingFf7Installations)
            {
                return;
            }
            SetFf7GameDirectory((ff7Installations.SelectedItem as GameInstallation)?.Directory);
            if (!busy)
            {
                SaveFf7Selection();
            }
        };
        selectFf7.Click += async (_, _) => await BrowseFf7Async();
        clearFf7.Click += (_, _) =>
        {
            ff7Installations.SelectedIndex = -1;
        };
        startFf7.Click += async (_, _) =>
        {
            if (installations.SelectedItem is not GameInstallation)
            {
                ff7Status.Text = "Choose FINAL FANTASY VIII above and select its installation before starting the shared engine.";
                status.Text = ff7Status.Text;
                return;
            }
            await PlayAsync(ff7Debug: true);
        };
        SetFf7GameDirectory(null);
        return panel;
    }

    private void PopulateFf7Installations(string? previous, IEnumerable<GameInstallation> found)
    {
        GameInstallation[] discovered = found.ToArray();
        IReadOnlyList<GameInstallation> choices = GameInstallationSelection.Order(previous, discovered);
        updatingFf7Installations = true;
        try
        {
            ff7Installations.ItemsSource = choices.Select(choice => discovered.FirstOrDefault(candidate => LauncherCapabilities.PathComparer.Equals(GameInstallationSelection.Normalize(candidate.Directory), choice.Directory)) ?? choice).ToArray();
            ff7Installations.SelectedIndex = choices.Count > 0 ? 0 : -1;
        }
        finally
        {
            updatingFf7Installations = false;
        }
        SetFf7GameDirectory((ff7Installations.SelectedItem as GameInstallation)?.Directory);
    }

    private void SetFf7GameDirectory(string? directory)
    {
        selectedFf7GameDirectory = string.IsNullOrWhiteSpace(directory) ? null : directory;
        ff7ResourcesAvailable = false;
        UpdateFf7Controls();
        if (selectedFf7GameDirectory is null)
        {
            ff7Status.Text = "No FINAL FANTASY VII resource installation configured.";
            return;
        }
        try
        {
            Ff7Installation installation = Ff7Installation.Discover(selectedFf7GameDirectory);
            ff7ResourcesAvailable = true;
            UpdateFf7Controls();
            ff7Status.Text = $"Resource layout found: {installation.Layout}. Ready to start the experimental FFVII title.";
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
        {
            ff7Status.Text = "Configured resources are unavailable: " + exception.Message;
        }
    }

    private void UpdateFf7Controls()
    {
        ff7Installations.IsEnabled = !busy;
        selectFf7.IsEnabled = !busy;
        clearFf7.IsEnabled = !busy && selectedFf7GameDirectory is not null;
        startFf7.IsEnabled = !busy && !ResourceLanguageDiscoveryPending && SelectedResourceLanguage is not null && ff7ResourcesAvailable && LauncherCapabilities.CanLaunchGame;
    }

    private void SaveFf7Selection()
    {
        try
        {
            LauncherSettings settings = LauncherSettings.Load(LauncherSettings.DefaultPath);
            (settings with { Ff7GameDirectory = selectedFf7GameDirectory }).Save(LauncherSettings.DefaultPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            ff7Status.Text = "Could not save FINAL FANTASY VII configuration: " + exception.Message;
        }
    }

    private async Task BrowseFf7Async()
    {
        try
        {
            string? directory = await PickFolderAsync("Select installed FINAL FANTASY VII resources", selectedFf7GameDirectory, "Select a local installed game folder.");
            if (directory is null)
            {
                return;
            }
            Ff7Installation.Discover(directory);
            PopulateFf7Installations(directory, ff7Installations.Items.Cast<GameInstallation>().ToArray());
            SaveFf7Selection();
        }
        catch (Exception exception)
        {
            await ShowErrorAsync("Could not select FINAL FANTASY VII resources.", exception);
        }
    }
}
