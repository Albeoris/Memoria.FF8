using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Memoria.FF8.Platform;

namespace Memoria.Launcher;

internal sealed partial class LauncherWindow
{
    private sealed record LauncherModLocationOption(ModInstallationLocation Location, string Label)
    {
        public override string ToString() => Label;
    }

    private static readonly LauncherModLocationOption[] LauncherModLocationOptions =
    [
        new(ModInstallationLocation.Memoria, "Memoria folder"),
        new(ModInstallationLocation.Game, "Game folder"),
        new(ModInstallationLocation.UserData, OperatingSystem.IsWindows() ? "AppData" : "User data"),
        new(ModInstallationLocation.Custom, "Custom folder")
    ];

    private readonly ComboBox launcherModLocation = new() { ItemsSource = LauncherModLocationOptions, MinWidth = 144, MinHeight = 32 };
    private readonly Button launcherModBrowseLocation = LauncherModButton("Browse…");
    private readonly TextBlock launcherModLocationPath = new() { Foreground = LauncherMuted, FontSize = 13, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
    private LauncherSettings launcherModLocationSettings = new();
    private bool changingLauncherModLocation;

    private Control CreateLauncherModLocationControl()
    {
        launcherModLocationSettings = LauncherSettings.Load(LauncherSettings.DefaultPath);
        ResetLauncherModLocationSelection();

        Grid row = new() { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*,Auto"), ColumnSpacing = 8 };
        row.Children.Add(new TextBlock { Text = "Install mods in", VerticalAlignment = VerticalAlignment.Center });
        Grid.SetColumn(launcherModLocation, 1);
        row.Children.Add(launcherModLocation);
        Grid.SetColumn(launcherModLocationPath, 2);
        row.Children.Add(launcherModLocationPath);
        Grid.SetColumn(launcherModBrowseLocation, 3);
        row.Children.Add(launcherModBrowseLocation);

        AutomationProperties.SetName(launcherModLocation, "Mod install location");
        AutomationProperties.SetName(launcherModBrowseLocation, "Browse for a custom mod install folder");
        launcherModLocation.SelectionChanged += async (_, _) =>
        {
            if (!changingLauncherModLocation && launcherModLocation.SelectedItem is LauncherModLocationOption option)
            {
                await ChangeLauncherModLocationAsync(option.Location);
            }
        };
        launcherModBrowseLocation.Click += async (_, _) => await ChangeLauncherModLocationAsync(ModInstallationLocation.Custom);
        return row;
    }

    private void ResetLauncherModLocationSelection()
    {
        ModInstallationLocation location = launcherModLocationSettings.ModLocation;
        if (location == ModInstallationLocation.Default)
        {
            location = OperatingSystem.IsMacOS() ? ModInstallationLocation.UserData : ModInstallationLocation.Memoria;
        }
        launcherModLocation.SelectedItem = LauncherModLocationOptions.FirstOrDefault(option => option.Location == location) ?? LauncherModLocationOptions[0];
    }

    private void UpdateLauncherModLocationControl()
    {
        bool enabled = !changingLauncherModLocation && !busy && runningGame is null;
        launcherModLocation.IsEnabled = enabled;
        launcherModBrowseLocation.IsEnabled = enabled;

        string? path = launcherModLocationSettings.ModLocation == ModInstallationLocation.Custom ? launcherModLocationSettings.CustomModsDirectory : null;
        if (installations.SelectedItem is GameInstallation selected)
        {
            try
            {
                path = ApplicationInstallation.ResolveModDirectories(AppContext.BaseDirectory, selected.Directory, ApplicationPaths.Current, launcherModLocationSettings).Primary;
            }
            catch (Exception exception) when (exception is ArgumentException or IOException)
            {
                // The mod library reports invalid paths alongside its other initialization errors.
            }
        }
        if (launcherModLocationPath.Text != path)
        {
            launcherModLocationPath.Text = path;
            ToolTip.SetTip(launcherModLocationPath, path);
        }
    }

    private async Task ChangeLauncherModLocationAsync(ModInstallationLocation location)
    {
        if (changingLauncherModLocation || busy || runningGame is not null)
        {
            return;
        }

        changingLauncherModLocation = true;
        launcherModLocation.IsEnabled = false;
        launcherModBrowseLocation.IsEnabled = false;
        bool changed = false;
        try
        {
            string? customDirectory = null;
            if (location == ModInstallationLocation.Custom)
            {
                customDirectory = await PickFolderAsync("Select a mod install folder", launcherModLocationSettings.CustomModsDirectory ?? launcherModLocationPath.Text, "Select a local folder for mods.");
                if (customDirectory is null || busy || runningGame is not null)
                {
                    return;
                }
            }

            string settingsPath = LauncherSettings.DefaultPath;
            LauncherSettings settings = LauncherSettings.Load(settingsPath);
            LauncherSettings updated = settings.WithModLocation(location, customDirectory);
            string gameDirectory = (installations.SelectedItem as GameInstallation)?.Directory ?? AppContext.BaseDirectory;
            ApplicationInstallation.ResolveModDirectories(AppContext.BaseDirectory, gameDirectory, ApplicationPaths.Current, updated);
            if (settings.ModLocation != updated.ModLocation || !ApplicationInstallation.ModPathComparer.Equals(settings.CustomModsDirectory, updated.CustomModsDirectory))
            {
                updated.Save(settingsPath);
                changed = true;
            }
            launcherModLocationSettings = updated;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or NotSupportedException)
        {
            launcherModActionStatus.Text = "Could not change the mod install location. " + exception.Message;
            launcherModActionStatus.IsVisible = true;
        }
        finally
        {
            ResetLauncherModLocationSelection();
            changingLauncherModLocation = false;
            UpdateLauncherModLocationControl();
        }

        if (changed)
        {
            Task recheck = CheckOfficialModsAsync();
            RenderLauncherMods();
            await recheck;
        }
    }
}
