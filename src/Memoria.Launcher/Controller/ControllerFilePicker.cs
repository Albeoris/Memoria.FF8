using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Memoria.Launcher.Controller;

namespace Memoria.Launcher;

internal sealed class ControllerFilePicker : Window
{
    private enum PickerMode
    {
        Folder,
        OpenFile,
        SaveFile
    }

    private sealed record Entry(string Path, string Name, bool IsDirectory)
    {
        public override string ToString() => IsDirectory ? Name + PathSeparator : Name;

        private string PathSeparator => System.IO.Path.EndsInDirectorySeparator(Path) ? string.Empty : System.IO.Path.DirectorySeparatorChar.ToString();
    }

    private readonly TextBlock location = new() { Text = "File system", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) };
    private readonly ListBox entries = new() { SelectionMode = SelectionMode.Single };
    private readonly TextBox fileName = new() { Watermark = "File name", Margin = new Thickness(0, 8, 0, 0) };
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };
    private readonly Button up = new() { Content = "Up", IsEnabled = false };
    private readonly Button open = new() { Content = "Open", IsDefault = true, IsEnabled = false };
    private readonly Button accept = new() { IsEnabled = false };
    private readonly PickerMode mode;
    private string? currentDirectory;
    private bool loading;
    private bool closed;

    private ControllerFilePicker(string title, string? suggestedDirectory, PickerMode mode, string? suggestedFileName = null)
    {
        this.mode = mode;
        Title = title;
        Width = 560;
        Height = 420;
        MinWidth = 380;
        MinHeight = 280;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        fileName.Text = suggestedFileName;
        fileName.IsVisible = mode == PickerMode.SaveFile;
        accept.Content = mode == PickerMode.SaveFile ? "Save" : "Use this folder";
        accept.IsVisible = mode != PickerMode.OpenFile;

        Button cancel = new() { Content = "Cancel", IsCancel = true };
        StackPanel buttons = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8, Margin = new Thickness(0, 12, 0, 0) };
        buttons.Children.Add(up);
        buttons.Children.Add(open);
        buttons.Children.Add(accept);
        buttons.Children.Add(cancel);

        Grid layout = new() { RowDefinitions = new RowDefinitions("Auto,*,Auto,Auto,Auto"), Margin = new Thickness(20) };
        Grid.SetRow(entries, 1);
        Grid.SetRow(fileName, 2);
        Grid.SetRow(status, 3);
        Grid.SetRow(buttons, 4);
        layout.Children.Add(location);
        layout.Children.Add(entries);
        layout.Children.Add(fileName);
        layout.Children.Add(status);
        layout.Children.Add(buttons);
        Content = layout;

        up.Click += async (_, _) => await NavigateAsync(currentDirectory is null ? null : Directory.GetParent(currentDirectory)?.FullName);
        open.Click += async (_, _) => await OpenSelectedEntryAsync();
        accept.Click += async (_, _) => await AcceptAsync();
        cancel.Click += (_, _) => Close(null);
        entries.SelectionChanged += (_, _) => open.IsEnabled = entries.SelectedItem is Entry;
        entries.DoubleTapped += async (_, _) => await OpenSelectedEntryAsync();
        ControllerNavigation.SetListCommitAction(entries, async () => await OpenSelectedEntryAsync());
        Opened += async (_, _) =>
        {
            entries.Focus();
            await NavigateAsync(suggestedDirectory, initial: true);
        };
        Closed += (_, _) => closed = true;
    }

    public static Task<string?> PickFolderAsync(Window owner, string title, string? suggestedDirectory) => new ControllerFilePicker(title, suggestedDirectory, PickerMode.Folder).ShowDialog<string?>(owner);

    public static Task<string?> OpenFileAsync(Window owner, string title, string? suggestedDirectory) => new ControllerFilePicker(title, suggestedDirectory, PickerMode.OpenFile).ShowDialog<string?>(owner);

    public static Task<string?> SaveFileAsync(Window owner, string title, string? suggestedDirectory, string suggestedFileName) => new ControllerFilePicker(title, suggestedDirectory, PickerMode.SaveFile, suggestedFileName).ShowDialog<string?>(owner);

    private async Task OpenSelectedEntryAsync()
    {
        if (loading || entries.SelectedItem is not Entry entry)
        {
            return;
        }

        if (entry.IsDirectory)
        {
            await NavigateAsync(entry.Path);
        }
        else
        {
            Close(entry.Path);
        }
    }

    private async Task AcceptAsync()
    {
        if (loading || currentDirectory is null)
        {
            return;
        }

        if (mode == PickerMode.Folder)
        {
            Close(currentDirectory);
            return;
        }

        string name = fileName.Text?.Trim() ?? string.Empty;
        if (name.Length == 0 || name.EndsWith('.') || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            status.Text = "Enter a valid file name.";
            return;
        }

        string path = Path.Combine(currentDirectory, Path.HasExtension(name) ? name : name + ".json");
        if (Directory.Exists(path))
        {
            status.Text = "A folder already has that name.";
            return;
        }

        if (!File.Exists(path) || await LauncherDialogs.ConfirmAsync(this, "Replace profile file?", $"Replace the existing file {Path.GetFileName(path)}?"))
        {
            Close(path);
        }
    }

    private async Task NavigateAsync(string? directory, bool initial = false)
    {
        if (loading)
        {
            return;
        }

        loading = true;
        status.Text = "Reading location…";
        try
        {
            bool includeFiles = mode == PickerMode.OpenFile;
            (string? path, Entry[] listing) = await Task.Run(() => initial ? ReadInitialEntries(directory, includeFiles) : ReadEntries(directory, includeFiles));
            if (closed)
            {
                return;
            }

            currentDirectory = path;
            location.Text = path ?? "File system";
            entries.ItemsSource = listing;
            entries.SelectedIndex = listing.Length == 0 ? -1 : 0;
            up.IsEnabled = path is not null;
            accept.IsEnabled = path is not null;
            status.Text = listing.Length == 0 ? includeFiles ? "No files or folders here." : "No folders to open." : string.Empty;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            if (!closed)
            {
                status.Text = $"Could not read location. {exception.Message}";
            }
        }
        finally
        {
            loading = false;
        }
    }

    private static (string? Path, Entry[] Entries) ReadInitialEntries(string? suggestedDirectory, bool includeFiles)
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string? path = Directory.Exists(suggestedDirectory) ? suggestedDirectory : Directory.Exists(home) ? home : null;
        if (path is not null)
        {
            try
            {
                return ReadEntries(path, includeFiles);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return ReadEntries(null, includeFiles);
            }
        }

        return ReadEntries(null, includeFiles);
    }

    private static (string? Path, Entry[] Entries) ReadEntries(string? path, bool includeFiles)
    {
        if (path is null)
        {
            return (null, Directory.GetLogicalDrives().Order(StringComparer.OrdinalIgnoreCase).Select(root => new Entry(root, root, IsDirectory: true)).ToArray());
        }

        path = Path.GetFullPath(path);
        IEnumerable<Entry> listing = Directory.EnumerateDirectories(path).Select(child => new Entry(child, Path.GetFileName(child), IsDirectory: true));
        if (includeFiles)
        {
            listing = listing.Concat(Directory.EnumerateFiles(path).Select(child => new Entry(child, Path.GetFileName(child), IsDirectory: false)));
        }
        return (path, listing.OrderByDescending(entry => entry.IsDirectory).ThenBy(entry => entry.Name, StringComparer.CurrentCultureIgnoreCase).ToArray());
    }
}
