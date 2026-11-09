using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Memoria.FF7.Resources;

namespace Memoria.Launcher;

internal sealed partial class LauncherWindow
{
    private readonly TextBox ff7Path = new() { IsReadOnly = true, MinHeight = 32, Watermark = "Optional installed FINAL FANTASY VII folder" };
    private readonly TextBlock ff7Status = new() { TextWrapping = TextWrapping.Wrap };
    private string? selectedFf7GameDirectory;

    private Control CreateFf7Selection()
    {
        StackPanel panel = new() { Margin = new Thickness(28), Spacing = 14 };
        panel.Children.Add(new TextBlock { Text = "FINAL FANTASY VII", FontSize = 28, FontWeight = FontWeight.SemiBold });
        panel.Children.Add(new TextBlock { Text = "Experimental resource configuration", Foreground = Brushes.DimGray });
        panel.Children.Add(new TextBlock { Text = "Keep FINAL FANTASY VIII selected in its own tab and optionally configure a separate FINAL FANTASY VII installation here. Applies on the next start.", TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(ff7Path);
        Button select = new() { Content = "Browse...", MinWidth = 100 };
        Button clear = new() { Content = "Clear", MinWidth = 100 };
        StackPanel actions = new() { Orientation = Orientation.Horizontal, Spacing = 10 };
        actions.Children.Add(select);
        actions.Children.Add(clear);
        panel.Children.Add(actions);
        panel.Children.Add(ff7Status);
        panel.Children.Add(new TextBlock { Text = "FINAL FANTASY VII gameplay is unavailable in this experimental engine. Configuring resources does not start a title screen or a new game. Field execution, presentation, battle transitions and saves still require integration.", TextWrapping = TextWrapping.Wrap });
        select.Click += async (_, _) => await BrowseFf7Async();
        clear.Click += (_, _) =>
        {
            SetFf7GameDirectory(null);
            SaveFf7Selection();
        };
        SetFf7GameDirectory(null);
        return panel;
    }

    private void SetFf7GameDirectory(string? directory)
    {
        selectedFf7GameDirectory = string.IsNullOrWhiteSpace(directory) ? null : directory;
        ff7Path.Text = selectedFf7GameDirectory ?? string.Empty;
        if (selectedFf7GameDirectory is null)
        {
            ff7Status.Text = "No FINAL FANTASY VII resource installation configured.";
            return;
        }
        try
        {
            Ff7Installation installation = Ff7Installation.Discover(selectedFf7GameDirectory);
            ff7Status.Text = $"Resource layout found: {installation.Layout}. Gameplay remains unavailable; this does not certify release or save compatibility.";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            ff7Status.Text = "Configured resources are unavailable: " + exception.Message;
        }
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
            IReadOnlyList<IStorageFolder> folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Select installed FINAL FANTASY VII resources", AllowMultiple = false });
            if (folders.Count == 0)
            {
                return;
            }
            using IStorageFolder chosen = folders[0];
            string directory = chosen.TryGetLocalPath() ?? throw new IOException("Select a local installed game folder.");
            Ff7Installation.Discover(directory);
            SetFf7GameDirectory(directory);
            SaveFf7Selection();
        }
        catch (Exception exception)
        {
            await ShowErrorAsync("Could not select FINAL FANTASY VII resources.", exception);
        }
    }
}
