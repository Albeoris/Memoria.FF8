using System.Globalization;
using System.Runtime.InteropServices;
using Albeoris.Games.FF8.Imaging;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Memoria.FF8;

namespace Memoria.Launcher;

internal sealed class LauncherCatalogPreviewPlayer
{
    private readonly CatalogMediaComparison comparison;
    private readonly CancellationTokenSource lifetime = new();
    private readonly DispatcherTimer display = new() { Interval = TimeSpan.FromMilliseconds(15) };
    private readonly DispatcherTimer scrub = new() { Interval = TimeSpan.FromMilliseconds(120) };
    private readonly Button play = new() { Content = "Play", MinWidth = 66 };
    private readonly Button sound = new() { MinWidth = 122 };
    private readonly Slider timeline = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock time = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap, IsVisible = false };
    private readonly Pane before = new("Before", "Original media could not be matched.");
    private readonly Pane after = new("After", "Package media is unavailable.");
    private Task<CatalogMediaPlayback>? seeking;
    private bool updatingTimeline;
    private double? requestedPosition;
    private bool resume;

    public LauncherCatalogPreviewPlayer(CatalogMediaComparison comparison)
    {
        this.comparison = comparison;

        Grid buttons = new() { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*,Auto"), ColumnSpacing = 10 };
        foreach (Control control in new Control[] { play, sound, timeline, time })
        {
            Grid.SetColumn(control, buttons.Children.Count);
            buttons.Children.Add(control);
        }
        StackPanel controls = new() { Spacing = 4 };
        controls.Children.Add(buttons);
        controls.Children.Add(status);
        Controls = controls;
        timeline.Minimum = 0;
        timeline.Maximum = Math.Max(1, Playback.DurationSeconds);
        timeline.SmallChange = 5;
        timeline.LargeChange = 30;
        timeline.ValueChanged += (_, _) =>
        {
            if (!updatingTimeline)
            {
                RequestSeek(timeline.Value, resumePlayback: seeking is not null || requestedPosition is not null ? resume : Playback.IsPlaying);
            }
        };
        play.Click += (_, _) =>
        {
            if (Playback.PositionSeconds >= Playback.DurationSeconds)
            {
                RequestSeek(0, resumePlayback: true);
            }
            else
            {
                TryPlay(!Playback.IsPlaying);
            }
            Refresh();
        };
        sound.Click += (_, _) =>
        {
            Playback.ListenAfter = !Playback.ListenAfter;
            Refresh();
        };
        scrub.Tick += (_, _) =>
        {
            scrub.Stop();
            StartSeek();
        };
        display.Tick += (_, _) => Update();
        Refresh();
        display.Start();
    }

    public Control Controls { get; }
    public Control Before => before.Control;
    public Control After => after.Control;
    private CatalogMediaPlayback Playback => comparison.Playback;

    private void RequestSeek(double seconds, bool resumePlayback)
    {
        resume = resumePlayback;
        requestedPosition = seconds;
        TryPlay(false);
        scrub.Stop();
        scrub.Start();
        Refresh();
    }

    private void StartSeek()
    {
        if (seeking is not null || requestedPosition is not double seconds)
        {
            return;
        }
        requestedPosition = null;
        SetStatus("");
        seeking = Playback.SeekAsync(Math.Clamp(seconds, 0, Playback.DurationSeconds), lifetime.Token);
    }

    private void Update()
    {
        if (seeking is { IsCompleted: true } completed)
        {
            seeking = null;
            if (completed.IsCompletedSuccessfully)
            {
                comparison.ReplacePlayback(completed.Result);
                if (requestedPosition is null)
                {
                    TryPlay(resume);
                }
            }
            else
            {
                SetStatus(completed.Exception?.GetBaseException().Message ?? "Seeking was cancelled.");
            }
            if (!scrub.IsEnabled)
            {
                StartSeek();
            }
        }
        try
        {
            Playback.Update();
        }
        catch (Exception error) when (error is IOException or InvalidDataException or InvalidOperationException)
        {
            TryPlay(false);
            SetStatus(error.Message);
        }
        Refresh();
    }

    private void TryPlay(bool playing)
    {
        try
        {
            Playback.SetPlaying(playing);
        }
        catch (Exception error) when (error is IOException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            SetStatus(error.Message);
        }
    }

    private void Refresh()
    {
        bool moving = seeking is not null || requestedPosition is not null;
        play.IsEnabled = Playback.DurationSeconds > 0 && !moving;
        play.Content = Playback.IsPlaying ? "Pause" : Playback.PositionSeconds >= Playback.DurationSeconds && Playback.DurationSeconds > 0 ? "Replay" : "Play";
        sound.IsVisible = Playback.Before.HasAudio || Playback.After.HasAudio;
        sound.Content = Playback.ListenAfter ? "Sound · After" : "Sound · Before";
        timeline.IsEnabled = Playback.DurationSeconds > 0;
        if (!moving)
        {
            updatingTimeline = true;
            timeline.Value = Playback.PositionSeconds;
            updatingTimeline = false;
        }
        time.Text = moving ? $"Seeking… {Format(timeline.Value)}" : $"{Format(Playback.PositionSeconds)} / {Format(Playback.DurationSeconds)}";
        before.Update(Playback.Before);
        after.Update(Playback.After);
    }

    private void SetStatus(string message)
    {
        status.Text = message;
        status.IsVisible = message.Length != 0;
    }

    private static string Format(double seconds)
    {
        TimeSpan position = TimeSpan.FromSeconds(seconds);
        return position.TotalHours < 1 ? position.ToString(@"mm\:ss", CultureInfo.InvariantCulture) : FormattableString.Invariant($"{(int)position.TotalHours}:{position.Minutes:00}:{position.Seconds:00}");
    }

    public async Task StopAsync()
    {
        display.Stop();
        scrub.Stop();
        lifetime.Cancel();
        before.Dispose();
        after.Dispose();
        try
        {
            await comparison.ReleaseAsync(seeking).ConfigureAwait(false);
        }
        finally
        {
            lifetime.Dispose();
        }
    }

    private sealed class Pane : IDisposable
    {
        private readonly string unavailable;
        private readonly TextBlock description = new() { TextWrapping = TextWrapping.Wrap };
        private readonly Image video = new() { Stretch = Stretch.Uniform };
        private CatalogMediaSide? side;
        private string? error;
        private long revision;
        private WriteableBitmap? bitmap;
        private byte[] row = [];

        public Pane(string label, string unavailable)
        {
            this.unavailable = unavailable;
            Grid pane = new() { RowDefinitions = new RowDefinitions("Auto,Auto,*"), RowSpacing = 8, Margin = new Thickness(10) };
            pane.Children.Add(new TextBlock { Text = label, FontSize = 19, FontWeight = FontWeight.SemiBold });
            Grid.SetRow(description, 1);
            Grid.SetRow(video, 2);
            pane.Children.Add(description);
            pane.Children.Add(video);
            Control = pane;
        }

        public Control Control { get; }

        public void Update(CatalogMediaSide input)
        {
            bool replaced = !ReferenceEquals(side, input);
            if (replaced || error != input.Error)
            {
                description.Text = CatalogMediaComparison.Describe(input, unavailable).Text;
                error = input.Error;
            }
            side = input;
            if (input.Image is not { } image)
            {
                video.IsVisible = false;
                return;
            }
            video.IsVisible = true;
            if (!replaced && revision == input.ImageRevision)
            {
                return;
            }
            revision = input.ImageRevision;
            PixelSize size = new(image.Width, image.Height);
            if (bitmap is null || bitmap.PixelSize != size)
            {
                Dispose();
                bitmap = new(size, new Vector(96, 96), PixelFormat.Rgba8888, AlphaFormat.Unpremul);
                row = new byte[checked(image.Width * 4)];
                video.Source = bitmap;
            }
            using ILockedFramebuffer target = bitmap.Lock();
            for (int y = 0; y < image.Height; y++)
            {
                MemoryMarshal.AsBytes(image.Pixels.AsSpan(y * image.Width, image.Width)).CopyTo(row);
                Marshal.Copy(row, 0, IntPtr.Add(target.Address, y * target.RowBytes), row.Length);
            }
            video.InvalidateVisual();
        }

        public void Dispose()
        {
            video.Source = null;
            bitmap?.Dispose();
            bitmap = null;
        }
    }
}
