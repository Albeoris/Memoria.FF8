using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;

namespace Memoria.Launcher;

internal sealed partial class LauncherWindow
{
    private readonly TextBox modelSourcePath = new() { IsReadOnly = true, MinHeight = 32, Watermark = "Same as game" };
    private readonly Button browseModelSource = new() { Content = "Browse…", MinWidth = 90, Margin = new Thickness(8, 0, 0, 0) };
    private readonly Button resetModelSource = new() { Content = "Same as game", MinWidth = 110, Margin = new Thickness(8, 0, 0, 0) };
    private string? selectedModelSourceDirectory;

    private Control CreateModelSourceSelection()
    {
        StackPanel panel = new() { Spacing = 6, Margin = new Thickness(0, 12, 0, 0) };
        panel.Children.Add(new TextBlock { Text = "Model resources" });
        DockPanel selection = new() { LastChildFill = true };
        DockPanel.SetDock(resetModelSource, Dock.Right);
        DockPanel.SetDock(browseModelSource, Dock.Right);
        selection.Children.Add(resetModelSource);
        selection.Children.Add(browseModelSource);
        selection.Children.Add(modelSourcePath);
        panel.Children.Add(selection);
        panel.Children.Add(new TextBlock { Text = "Choose another installed edition for compatible models and their textures. Game scripts, language and rules stay with the game installation. Applies on the next start.", TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DimGray });
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
            IReadOnlyList<IStorageFolder> folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Select an installed FINAL FANTASY VIII model resource source", AllowMultiple = false });
            if (folders.Count == 0)
            {
                return;
            }
            using IStorageFolder chosen = folders[0];
            string directory = chosen.TryGetLocalPath() ?? throw new IOException("Select a local installed game folder.");
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
