using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Memoria.FF8;
using Memoria.FF8.Mods;
using Memoria.FF8.Platform;

namespace Memoria.Launcher.Tests;

[Collection("Incident dialog UI")]
public sealed class LauncherModPreviewMediaTests(LauncherHeadlessFixture fixture)
{
    [Theory]
    [InlineData("MEMORIA_FF8_PREVIEW_VIDEO_FIXTURE", "disc00_03h.avi", ".avi", true)]
    [InlineData("MEMORIA_FF8_PREVIEW_MUSIC_FIXTURE", "alien.ogg", ".sgt", false)]
    public async Task InstalledAndArchivedMediaPlayAndSeekInOneLauncherComparison(string fixtureVariable, string file, string originalExtension, bool video)
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? archive = Environment.GetEnvironmentVariable(fixtureVariable);
        string tools = MediaToolsInstallation.ResolveDirectory();
        Assert.SkipUnless(Directory.Exists(game) && File.Exists(archive) &&
            File.Exists(MediaToolPaths.ResolveExecutable(tools, "ffmpeg")) && File.Exists(MediaToolPaths.ResolveExecutable(tools, "ffprobe")),
            $"Configure the original game, media tools and {fixtureVariable} to run this installed comparison.");
        using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(45));
        CancellationToken token = deadline.Token;
        string root = Path.Combine(Path.GetTempPath(), "launcher-preview-media-" + Guid.NewGuid().ToString("N"));
        try
        {
            using ModRuntime mods = new(root, watch: false);
            ModPackageInstaller installer = new(Path.Combine(root, "state"), Path.Combine(root, "Mods"), "Classic/en/test");
            using ModLibraryService library = new(Path.Combine(root, "cache"), installer, mods, "Classic/en/test", false);
            await library.StopAsync();
            ModPackage package = new("Junction/fixture", "Media fixture", ModFormat.Junction, null, false, ModEffectiveStatus.Disabled, archive!);
            ModPreviewDocument document = await library.OpenPreviewAsync(null, package, token);
            ModPreviewEntry item = Assert.Single(document.Entries, entry => entry.IsMedia && entry.Name.Equals(file, StringComparison.OrdinalIgnoreCase));
            await fixture.Session.Dispatch(async () =>
            {
                Application.Current!.Styles.Add(new FluentTheme());
                CatalogMediaComparison media = await CatalogMediaComparison.OpenAsync(document, item, new(game!), tools, token, audioEnabled: false);
                LauncherCatalogPreviewPlayer player = new(media);
                Grid content = new() { RowDefinitions = new RowDefinitions("*,Auto"), ColumnDefinitions = new ColumnDefinitions("*,*") };
                Grid.SetColumn(player.After, 1);
                Grid.SetRow(player.Controls, 1);
                Grid.SetColumnSpan(player.Controls, 2);
                content.Children.Add(player.Before);
                content.Children.Add(player.After);
                content.Children.Add(player.Controls);
                Window window = new() { Content = content, Width = 1280, Height = 720 };
                try
                {
                    window.Show();
                    Dispatcher.UIThread.RunJobs();
                    Assert.Null(media.Playback.Before.Error);
                    Assert.Null(media.Playback.After.Error);
                    Assert.EndsWith(originalExtension, media.Playback.Before.Name, StringComparison.OrdinalIgnoreCase);
                    Assert.True(media.Playback.Before.HasAudio);
                    Assert.True(media.Playback.After.HasAudio);
                    Assert.Equal(video, media.Playback.Before.Image is not null);
                    Assert.Equal(video, media.Playback.After.Image is not null);

                    CheckPane(player.Before, media.Playback.Before, playing: false);
                    CheckPane(player.After, media.Playback.After, playing: false);

                    Slider slider = Assert.Single(player.Controls.GetVisualDescendants().OfType<Slider>());
                    Assert.True(slider.Bounds.Width > 400);
                    Assert.True(slider.Maximum > 12);
                    Button play = FindButton("Play");
                    Button sound = FindButton("Sound · Before");
                    play.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    while (media.Playback.PositionSeconds < 0.05)
                    {
                        await Task.Delay(15, token);
                    }
                    double position = media.Playback.PositionSeconds;
                    sound.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Assert.True(media.Playback.ListenAfter);
                    Assert.Equal("Sound · After", sound.Content);
                    Assert.True(media.Playback.PositionSeconds >= position);
                    Assert.Equal("Pause", play.Content);
                    CheckPane(player.Before, media.Playback.Before, playing: true);
                    CheckPane(player.After, media.Playback.After, playing: true);

                    CatalogMediaPlayback previous = media.Playback;
                    double target = slider.Maximum * 0.7;
                    slider.Value = target;
                    while (ReferenceEquals(media.Playback, previous))
                    {
                        await Task.Delay(15, token);
                    }
                    Assert.True(media.Playback.ListenAfter);
                    Assert.True(media.Playback.IsPlaying);
                    Assert.InRange(media.Playback.PositionSeconds, target - 0.01, target + 1);
                    Assert.Equal(video, media.Playback.After.Image is not null);
                    play.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    CheckPane(player.Before, media.Playback.Before, playing: false);
                    CheckPane(player.After, media.Playback.After, playing: false);
                }
                finally
                {
                    window.Close();
                    await player.StopAsync();
                }

                void CheckPane(Control pane, CatalogMediaSide side, bool playing)
                {
                    Dispatcher.UIThread.RunJobs();
                    TextBlock[] texts = pane.GetVisualDescendants().OfType<TextBlock>().ToArray();
                    Assert.Contains(texts, text => text.Text?.Contains("Audio", StringComparison.Ordinal) == true &&
                        text.Text.Contains("Hz", StringComparison.Ordinal) && text.Text.Contains("channel", StringComparison.Ordinal));
                    Assert.Equal(video, texts.Any(text => text.Text?.Contains(" × ", StringComparison.Ordinal) == true));
                    TextBlock fps = Assert.Single(texts, text => text.Name == "VideoFrameRate");
                    Border badge = Assert.IsType<Border>(fps.Parent);
                    Assert.Equal(playing && video, badge.IsVisible);
                    if (playing && video)
                    {
                        Assert.Equal(side.PlayingVideoRate(media.Playback.PositionSeconds), fps.Text);
                        Grid surface = Assert.IsType<Grid>(badge.Parent);
                        Assert.True(badge.Bounds.Left > surface.Bounds.Width / 2);
                        Assert.True(badge.Bounds.Top < surface.Bounds.Height / 2);
                    }
                }

                Button FindButton(string label) => Assert.Single(player.Controls.GetVisualDescendants().OfType<Button>(), button => Equals(button.Content, label));
            }, token);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
