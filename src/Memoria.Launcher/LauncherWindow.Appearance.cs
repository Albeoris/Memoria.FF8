using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Memoria.FF8.Platform;

namespace Memoria.Launcher;

internal sealed partial class LauncherWindow
{
    private readonly ComboBox launcherAppearance = new() { MinHeight = 36, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch };

    private string SelectedLauncherAppearance => launcherAppearance.SelectedItem as string ?? "System";

    private Control CreateAppearanceSelection()
    {
        launcherAppearance.ItemsSource = new[] { "System", "Dark", "Light" };
        ToolTip.SetTip(launcherAppearance, "System follows your operating system's appearance. Changes apply immediately.");
        return launcherAppearance;
    }

    private void InitializeLauncherAppearance()
    {
        if (Application.Current is { } application)
        {
            application.RequestedThemeVariant = ThemeVariant.Default;
        }
        string preference = LauncherSettings.Load(LauncherSettings.DefaultPath).Appearance;
        launcherAppearance.SelectedItem = preference is "Dark" or "Light" ? preference : "System";
        ActualThemeVariantChanged += (_, _) => ApplyLauncherPalette();
        ApplyLauncherAppearance();
        launcherAppearance.SelectionChanged += (_, _) =>
        {
            ApplyLauncherAppearance();
            SaveSelection(includeWithoutInstallation: true);
        };
    }

    private void ApplyLauncherAppearance()
    {
        RequestedThemeVariant = SelectedLauncherAppearance switch
        {
            "Dark" => ThemeVariant.Dark,
            "Light" => ThemeVariant.Light,
            _ => ThemeVariant.Default
        };
        ApplyLauncherPalette();
    }

    private void ApplyLauncherPalette()
    {
        bool dark = ActualThemeVariant == ThemeVariant.Dark;
        LauncherBackground.Color = Color.Parse(dark ? "#0C141E" : "#EEF3F8");
        LauncherSurface.Color = Color.Parse(dark ? "#152638" : "#FFFFFF");
        LauncherBorder.Color = Color.Parse(dark ? "#2B4258" : "#CDDAE6");
        LauncherText.Color = Color.Parse(dark ? "#ECF2F8" : "#172B40");
        LauncherMuted.Color = Color.Parse(dark ? "#A8B9CB" : "#4D647B");
        LauncherAccent.Color = Color.Parse(dark ? "#214D78" : "#245B8F");
        LauncherSuccess.Color = Color.Parse(dark ? "#74D5A5" : "#17633F");
        LauncherProblem.Color = Color.Parse(dark ? "#EF8EA8" : "#A12548");
        foreach (var hero in gameHeroes)
        {
            if (hero.Frame.Background is LinearGradientBrush gradient)
            {
                gradient.GradientStops = new GradientStops
                {
                    new GradientStop(Color.Parse(dark ? "#263744" : "#E2ECF5"), 0),
                    new GradientStop(Color.Parse(dark ? "#1A2631" : "#F3F7FB"), 0.65),
                    new GradientStop(Color.Parse(dark ? "#203F60" : "#CEDFEF"), 1)
                };
            }
        }
    }
}
