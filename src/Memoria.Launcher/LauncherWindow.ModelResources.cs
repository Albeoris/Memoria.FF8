using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Memoria.Launcher;

internal sealed partial class LauncherWindow
{
    private readonly TextBox modelSourcePath = new() { IsReadOnly = true, MinHeight = 32, Watermark = "Same as game" };
    private readonly Button browseModelSource = new() { Content = "Browse…", MinWidth = 90, Margin = new Thickness(8, 0, 0, 0) };
    private readonly Button resetModelSource = new() { Content = "Same as game", MinWidth = 110, Margin = new Thickness(8, 0, 0, 0) };
    private string? selectedModelSourceDirectory;

    private Control CreateModelSourceSelection()
    {
        StackPanel panel = new() { Spacing = 10 };
        panel.Children.Add(modelSourcePath);
        WrapPanel actions = new();
        browseModelSource.Margin = new Thickness(0, 0, 8, 0);
        resetModelSource.Margin = new Thickness(0);
        actions.Children.Add(browseModelSource);
        actions.Children.Add(resetModelSource);
        panel.Children.Add(actions);
        panel.Children.Add(new TextBlock { Text = "Choose another installed edition for compatible models and their textures. Game scripts, language and rules stay with the game installation. Applies on the next start.", TextWrapping = TextWrapping.Wrap, Foreground = LauncherMuted });
        browseModelSource.Click += async (_, _) => await BrowseModelSourceAsync();
        resetModelSource.Click += (_, _) =>
        {
            SetModelSourceDirectory(null);
            SaveSelection();
        };
        return panel;
    }

    private void SetModelSourceDirectory(string? directory)
    {
        selectedModelSourceDirectory = string.IsNullOrWhiteSpace(directory) ? null : directory;
        modelSourcePath.Text = selectedModelSourceDirectory ?? string.Empty;
        ToolTip.SetTip(modelSourcePath, selectedModelSourceDirectory ?? "Use model resources from the selected game installation.");
    }

    private async Task BrowseModelSourceAsync()
    {
        try
        {
            string? directory = await PickFolderAsync("Select an installed FINAL FANTASY VIII model resource source", selectedModelSourceDirectory ?? (installations.SelectedItem as GameInstallation)?.Directory, "Select a local installed game folder.");
            if (directory is null)
            {
                return;
            }
            if (GameInstallationSelection.ValidateForEngine(directory) is string error)
            {
                throw new InvalidDataException(error);
            }
            SetModelSourceDirectory(directory);
            SaveSelection();
        }
        catch (Exception exception)
        {
            await ShowErrorAsync("Could not select model resources.", exception);
        }
    }
}
