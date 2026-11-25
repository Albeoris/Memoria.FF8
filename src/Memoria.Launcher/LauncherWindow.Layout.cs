using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;

namespace Memoria.Launcher;

internal sealed partial class LauncherWindow
{
    private static readonly SolidColorBrush LauncherBackground = new(Color.Parse("#0C141E"));
    private static readonly SolidColorBrush LauncherSurface = new(Color.Parse("#152638"));
    private static readonly SolidColorBrush LauncherBorder = new(Color.Parse("#2B4258"));
    private static readonly SolidColorBrush LauncherText = new(Color.Parse("#ECF2F8"));
    private static readonly SolidColorBrush LauncherMuted = new(Color.Parse("#A8B9CB"));
    private static readonly SolidColorBrush LauncherAccent = new(Color.Parse("#214D78"));
    private static readonly SolidColorBrush LauncherSuccess = new(Color.Parse("#74D5A5"));
    private static readonly SolidColorBrush LauncherWarning = new(Color.Parse("#F3A654"));
    private static readonly SolidColorBrush LauncherProblem = new(Color.Parse("#EF8EA8"));
    private readonly Grid launcherHeader = new() { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), RowDefinitions = new RowDefinitions("Auto,Auto"), Margin = new Thickness(24, 14), ColumnSpacing = 12 };
    private readonly ComboBox selectedGame = new() { MinHeight = 36, Width = 270 };
    private readonly TabControl launcherPages = new()
    {
        TabStripPlacement = Dock.Left,
        ItemsPanel = new FuncTemplate<Panel?>(() => new StackPanel { Orientation = Orientation.Vertical })
    };
    private readonly List<Bitmap> launcherArtwork = [];
    private readonly List<(Border Frame, Grid Layout, TextBlock Heading, Image? Crest)> gameHeroes = [];
    private Control? ff8Home;
    private Control? ff7Home;
    private readonly TextBlock installationProblem = new() { Foreground = LauncherProblem, TextWrapping = TextWrapping.Wrap, IsVisible = false };

    internal static Border IncidentCard(Control content)
    {
        return new Border { Background = LauncherSurface, BorderBrush = LauncherBorder, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Child = content };
    }

    internal static void StyleIncidentDialog(Window dialog)
    {
        dialog.FontSize = 14;
        dialog.Background = LauncherBackground;
        dialog.Foreground = LauncherText;
        dialog.Resources["SystemControlHighlightAccentBrush"] = LauncherAccent;
        dialog.Styles.Add(new Style(selector => selector.OfType<Button>())
        {
            Setters =
            {
                new Setter(BackgroundProperty, LauncherAccent),
                new Setter(ForegroundProperty, Brushes.White),
                new Setter(BorderBrushProperty, LauncherBorder),
                new Setter(CornerRadiusProperty, new CornerRadius(7))
            }
        });
    }

    private void CreateLauncherLayout()
    {
        Width = 960;
        Height = 640;
        MinWidth = 720;
        MinHeight = 480;
        FontSize = 14;
        Background = LauncherBackground;
        Foreground = LauncherText;
        RequestedThemeVariant = ThemeVariant.Dark;
        Resources["TabItemHeaderSelectedPipeFill"] = LauncherAccent;
        Resources["SystemControlHighlightAccentBrush"] = LauncherAccent;
        Resources["SystemControlBackgroundAccentBrush"] = LauncherAccent;
        Resources["SystemControlForegroundAccentBrush"] = LauncherAccent;

        Styles.Add(new Style(selector => selector.OfType<Button>())
        {
            Setters =
            {
                new Setter(BackgroundProperty, LauncherAccent),
                new Setter(ForegroundProperty, Brushes.White),
                new Setter(BorderBrushProperty, LauncherBorder),
                new Setter(CornerRadiusProperty, new CornerRadius(7))
            }
        });
        Styles.Add(new Style(selector => selector.OfType<TabItem>().Class("launcher-nav"))
        {
            Setters =
            {
                new Setter(FontSizeProperty, 14d),
                new Setter(ForegroundProperty, LauncherMuted),
                new Setter(PaddingProperty, new Thickness(16, 15)),
                new Setter(MarginProperty, new Thickness(0, 2)),
                new Setter(HorizontalContentAlignmentProperty, HorizontalAlignment.Left)
            }
        });
        Styles.Add(new Style(selector => selector.OfType<TabItem>().Class("launcher-nav").Class(":selected"))
        {
            Setters =
            {
                new Setter(ForegroundProperty, LauncherText),
                new Setter(BackgroundProperty, LauncherSurface)
            }
        });

        selectedGame.ItemsSource = new[] { "FINAL FANTASY VIII", "FINAL FANTASY VII · Experimental" };
        selectedGame.SelectedIndex = 0;
        ToolTip.SetTip(selectedGame, "Choose the game to launch. Both resource installations are kept.");

        StackPanel brand = new() { Orientation = Orientation.Horizontal, Spacing = 9, VerticalAlignment = VerticalAlignment.Center };
        using (Stream? crestStream = typeof(LauncherWindow).Assembly.GetManifestResourceStream("Memoria.Launcher.Crest.png"))
        {
            if (crestStream is not null)
            {
                Bitmap crest = new(crestStream);
                launcherArtwork.Add(crest);
                brand.Children.Add(new Image { Source = crest, Width = 30, Height = 38, Stretch = Stretch.Uniform });
            }
        }
        brand.Children.Add(new TextBlock { Text = "MEMORIA", FontSize = 17, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        brand.Children.Add(new TextBlock { Text = "ENGINE", FontSize = 11, Foreground = LauncherMuted, VerticalAlignment = VerticalAlignment.Center });
        launcherHeader.Children.Add(brand);
        Grid.SetColumn(selectedGame, 1);
        launcherHeader.Children.Add(selectedGame);
        Control launchActions = CreateHeaderLaunchActions();
        Grid.SetColumn(launchActions, 2);
        launcherHeader.Children.Add(launchActions);

        ff8Home = CreateFf8Home();
        ff7Home = CreateFf7Selection();
        StackPanel home = new() { Spacing = 18 };
        home.Children.Add(ff8Home);
        home.Children.Add(ff7Home);
        launcherPages.ItemsSource = new[]
        {
            CreateLauncherTab("Play", home),
            CreateLauncherTab("Mods", CreateModsPage(), scroll: false),
            CreateLauncherTab("Settings", CreateSettingsPage()),
            CreateLauncherTab("Updates", CreateUpdatesPage()),
            CreateLauncherTab("Tools", CreateToolsPage())
        };
        launcherPages.Margin = new Thickness(12, 8, 0, 0);

        Grid shell = new() { RowDefinitions = new RowDefinitions("Auto,*") };
        Add(shell, new Border { Background = LauncherBackground, BorderBrush = LauncherBorder, BorderThickness = new Thickness(0, 0, 0, 1), Child = launcherHeader }, 0);
        Add(shell, launcherPages, 1);
        Content = shell;

        selectedGame.SelectionChanged += (_, _) => UpdateSelectedGamePresentation();
        UpdateSelectedGamePresentation();
        SizeChanged += (_, _) => UpdateNavigationWidth();
        Opened += (_, _) => FitLauncherToScreen();
        Closed += (_, _) =>
        {
            foreach (Bitmap artwork in launcherArtwork)
            {
                artwork.Dispose();
            }
        };
        UpdateNavigationWidth();
    }

    private static TabItem CreateLauncherTab(string name, Control content, bool scroll = true)
    {
        TabItem tab = new()
        {
            Header = name,
            Width = 142,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
            Content = scroll
                ? new ScrollViewer
                {
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    Content = new Border { Padding = new Thickness(20, 12, 24, 16), Child = content }
                }
                : new Border { Padding = new Thickness(20, 12, 24, 16), Child = content }
        };
        tab.Classes.Add("launcher-nav");
        return tab;
    }

    private Control CreateFf8Home()
    {
        StackPanel content = new() { Spacing = 20 };
        content.Children.Add(CreateGameHero("FINAL FANTASY VIII", "Your world. Your memories.", "THE JOURNEY CONTINUES"));

        installations.HorizontalAlignment = HorizontalAlignment.Stretch;
        installations.PlaceholderText = "Choose your installed game";
        browse.Margin = new Thickness(10, 0, 0, 0);
        Grid selection = new() { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        selection.Children.Add(installations);
        Grid.SetColumn(browse, 1);
        selection.Children.Add(browse);
        StackPanel installationContent = new() { Spacing = 8 };
        installationContent.Children.Add(selection);
        installationContent.Children.Add(installationProblem);
        installationContent.Children.Add(cancel);
        content.Children.Add(LauncherCard("Game installation", "English Steam (2013) or Remastered. Remastered support is partial.", installationContent));

        return content;
    }

    private Border CreateGameHero(string title, string subtitle, string eyebrow)
    {
        Grid hero = new() { ColumnDefinitions = new ColumnDefinitions("*,Auto"), MinHeight = 158 };
        StackPanel words = new() { Spacing = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
        words.Children.Add(new TextBlock { Text = eyebrow, Foreground = LauncherMuted, FontSize = 11, TextWrapping = TextWrapping.Wrap });
        TextBlock heading = new() { Text = title, FontFamily = new FontFamily("Georgia"), FontSize = 32, TextWrapping = TextWrapping.Wrap, LineHeight = 39 };
        words.Children.Add(heading);
        words.Children.Add(LauncherDescription(subtitle));
        hero.Children.Add(words);
        Image? image = null;
        using Stream? stream = typeof(LauncherWindow).Assembly.GetManifestResourceStream("Memoria.Launcher.Crest.png");
        if (stream is not null)
        {
            Bitmap crest = new(stream);
            launcherArtwork.Add(crest);
            image = new Image { Source = crest, Width = 122, Height = 148, Opacity = 0.72, Stretch = Stretch.Uniform };
            Grid.SetColumn(image, 1);
            hero.Children.Add(image);
        }
        Border frame = new()
        {
            Padding = new Thickness(24, 16),
            CornerRadius = new CornerRadius(12),
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops = new GradientStops
                {
                    new GradientStop(Color.Parse("#263744"), 0),
                    new GradientStop(Color.Parse("#1A2631"), 0.65),
                    new GradientStop(Color.Parse("#203F60"), 1)
                }
            },
            Child = hero
        };
        gameHeroes.Add((frame, hero, heading, image));
        return frame;
    }

    private Control CreateSettingsPage()
    {
        StackPanel page = LauncherPage("Settings", "Changes are saved automatically and apply on the next game start.");
        StackPanel graphics = new() { Spacing = 8 };
        graphicsBackend.HorizontalAlignment = HorizontalAlignment.Stretch;
        graphics.Children.Add(graphicsBackend);
        graphics.Children.Add(graphicsStatus);
        graphics.Children.Add(tryAuto);
        page.Children.Add(LauncherCard("Graphics backend", "Choose the renderer for the shared engine.", graphics));

        page.Children.Add(LauncherCard("Language", "Choose an original edition or a translation for the selected installation.", gameLanguage));


        achievementsLocalOnly.Margin = new Thickness(0);
        achievementsLocalOnly.Content = "Keep achievements local";
        closeAfterLaunch.Content = "Close launcher after starting the game";
        StackPanel preferences = new() { Spacing = 10 };
        preferences.Children.Add(achievementsLocalOnly);
        preferences.Children.Add(LauncherDescription("Local achievements disable the Steam or GOG connection."));
        preferences.Children.Add(closeAfterLaunch);
        page.Children.Add(LauncherCard("Launcher", null, preferences));
        page.Children.Add(LauncherCard("Appearance", "Follow your system or choose a launcher theme.", CreateAppearanceSelection()));

        return page;
    }

    private Control CreateToolsPage()
    {
        StackPanel page = LauncherPage("Tools", "Manage engine resources and mod authoring tools.");
        page.Children.Add(LauncherCard("Model resources", null, CreateModelSourceSelection()));

        StackPanel media = new() { Spacing = 12 };
        mediaStatus.TextWrapping = TextWrapping.Wrap;
        mediaStatus.Margin = new Thickness(0);
        media.Children.Add(mediaStatus);
        mediaSetup.HorizontalAlignment = HorizontalAlignment.Left;
        media.Children.Add(mediaSetup);
        mediaProgressPanel.Margin = new Thickness(0);
        mediaProgressPanel.Children.Add(mediaProgress);
        mediaProgressText.TextWrapping = TextWrapping.Wrap;
        mediaProgressPanel.Children.Add(mediaProgressText);
        mediaProgressPanel.Children.Add(cancelMediaSetup);
        media.Children.Add(mediaProgressPanel);
        page.Children.Add(LauncherCard("Video and audio", "FFmpeg is used for movie and replacement audio playback.", media));

        Button encodingEditor = new() { Content = "Open Encoding Editor…", HorizontalAlignment = HorizontalAlignment.Left };
        encodingEditor.Click += (_, _) => new EncodingProfiles.EncodingEditorWindow().Show(this);
        page.Children.Add(LauncherCard("Mod authoring", "Edit font encodings and language resources.", encodingEditor));
        return page;
    }

    private Control CreateUpdatesPage()
    {
        StackPanel page = LauncherPage("Updates", "Keep the engine up to date. Installed games remain playable offline.");
        page.Children.Add(LauncherCard("Memoria Engine", "Download the latest release, or choose another published tag. The package is verified before installation.", CreateUpdatePanel()));
        page.Children.Add(LauncherDescription("Other tags are ordered by update time, newest first. Installing another tag may downgrade the engine; your mods, saves and settings are preserved."));
        return page;
    }

    private Control CreateHeaderLaunchActions()
    {
        Grid actions = new() { ColumnDefinitions = new ColumnDefinitions("Auto,Auto"), ColumnSpacing = 12, VerticalAlignment = VerticalAlignment.Center };
        StackPanel updateContent = new() { Spacing = 2 };
        updateContent.Children.Add(headerUpdateTitle);
        updateContent.Children.Add(headerUpdateDate);
        headerUpdate.Content = updateContent;
        headerUpdate.Click += (_, _) => launcherPages.SelectedIndex = 3;
        actions.Children.Add(headerUpdate);
        foreach (Button button in new[] { play, startFf7 })
        {
            button.IsDefault = false;
            button.Content = "Play";
            button.MinWidth = 112;
            button.MinHeight = 40;
            button.Padding = new Thickness(20, 8);
            button.HorizontalContentAlignment = HorizontalAlignment.Center;
            button.FontWeight = FontWeight.SemiBold;
            Grid.SetColumn(button, 1);
            actions.Children.Add(button);
        }
        return actions;
    }

    private void UpdateSelectedGamePresentation()
    {
        bool ff7 = selectedGame.SelectedIndex == 1;
        if (ff8Home is not null && ff7Home is not null)
        {
            ff8Home.IsVisible = !ff7;
            ff7Home.IsVisible = ff7;
        }
        play.IsVisible = !ff7;
        startFf7.IsVisible = ff7;
        Title = ff7 ? "Memoria — FINAL FANTASY VII (experimental)" : "Memoria — FINAL FANTASY VIII";
    }

    private void UpdateNavigationWidth()
    {
        bool compact = Bounds.Width > 0 && Bounds.Width < 840 || Bounds.Height > 0 && Bounds.Height < 580;
        bool narrowHeader = Bounds.Width > 0 && Bounds.Width < 840;
        Grid.SetRow(selectedGame, narrowHeader ? 1 : 0);
        Grid.SetColumn(selectedGame, narrowHeader ? 0 : 1);
        Grid.SetColumnSpan(selectedGame, narrowHeader ? 3 : 1);
        selectedGame.HorizontalAlignment = HorizontalAlignment.Right;
        launcherHeader.RowSpacing = narrowHeader ? 8 : 0;
        foreach (TabItem tab in launcherPages.Items.OfType<TabItem>())
        {
            tab.Width = Bounds.Width > 0 && Bounds.Width < 840 ? 108 : 142;
            // Keep navigation in one column even on a short working area.
            bool shortWindow = Bounds.Height > 0 && Bounds.Height < 500;
            tab.MinHeight = 0;
            tab.Height = shortWindow ? 32 : double.NaN;
            tab.Padding = shortWindow ? new Thickness(12, 6) : new Thickness(16, 15);
        }
        foreach (var hero in gameHeroes)
        {
            hero.Frame.Padding = compact ? new Thickness(20, 12) : new Thickness(24, 16);
            hero.Layout.MinHeight = compact ? 108 : 158;
            hero.Heading.FontSize = compact ? 28 : 32;
            hero.Heading.LineHeight = compact ? 34 : 39;
            if (hero.Crest is not null)
            {
                hero.Crest.Width = compact ? 96 : 122;
                hero.Crest.Height = compact ? 108 : 148;
            }
        }
    }

    private void FitLauncherToScreen()
    {
        if (Screens.ScreenFromWindow(this) is not { } screen)
        {
            return;
        }
        // WorkingArea is in physical pixels; Avalonia window sizes are logical pixels.
        // Reserve room for native decorations and the desktop taskbar at every DPI.
        double availableWidth = Math.Max(320, screen.WorkingArea.Width / screen.Scaling - 32);
        double availableHeight = Math.Max(240, screen.WorkingArea.Height / screen.Scaling - 64);
        MinWidth = Math.Min(720, availableWidth);
        MinHeight = Math.Min(480, availableHeight);
        Width = Math.Min(960, availableWidth);
        Height = Math.Min(640, availableHeight);
        Position = new PixelPoint(
            screen.WorkingArea.X + (int)Math.Max(0, (screen.WorkingArea.Width - Width * screen.Scaling) / 2),
            screen.WorkingArea.Y + (int)Math.Max(0, (screen.WorkingArea.Height - (Height + 32) * screen.Scaling) / 2));
        UpdateNavigationWidth();
    }

    private static StackPanel LauncherPage(string title, string description)
    {
        StackPanel page = new() { Spacing = 18 };
        page.Children.Add(new TextBlock { Text = title, FontSize = 28, FontWeight = FontWeight.SemiBold });
        page.Children.Add(LauncherDescription(description));
        return page;
    }

    private static Border LauncherCard(string title, string? description, Control content)
    {
        StackPanel panel = new() { Spacing = 10 };
        panel.Children.Add(new TextBlock { Text = title, FontSize = 15, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap });
        if (!string.IsNullOrWhiteSpace(description))
        {
            panel.Children.Add(LauncherDescription(description));
        }
        panel.Children.Add(content);
        return new Border { Background = LauncherSurface, BorderBrush = LauncherBorder, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(9), Padding = new Thickness(18), Child = panel };
    }

    private static TextBlock LauncherDescription(string text) => new() { Text = text, Foreground = LauncherMuted, TextWrapping = TextWrapping.Wrap, FontSize = 13 };
}
