using System.Globalization;
using Memoria.FF8.Mods;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    private string? modPreviewMediaTools;
    private bool modPreviewMediaAudioEnabled = true;
    private PcmAudioOutput? modPreviewMediaAudioOutput;
    private CatalogMediaComparison? modPreviewMedia;
    private Task<CatalogMediaComparison>? modPreviewMediaOpening;
    private Task<CatalogMediaPlayback>? modPreviewMediaSeeking;
    private bool resumePreviewAfterSeek;

    internal bool IsModMediaPreviewOpen => IsOpen && ScreenId == "mods-preview" && (modPreviewMediaOpening is not null || modPreviewMedia?.Playback.DurationSeconds > 0);

    internal void PauseModMediaForFocus()
    {
        resumePreviewAfterSeek = false;
        if (modPreviewMedia?.Playback is { IsPlaying: true } playback)
        {
            playback.SetPlaying(false);
            _view = null;
        }
    }

    private Task<CatalogMediaComparison> OpenModPreviewMediaAsync(ModPreviewDocument document, ModPreviewEntry entry, ModPreviewResourcesProfile profile, CancellationToken token)
    {
        return CatalogMediaComparison.OpenAsync(document, entry, profile, modPreviewMediaTools, token, modPreviewMediaAudioEnabled, modPreviewMediaAudioOutput);
    }

    private void PollModMedia()
    {
        if (modPreviewMediaOpening is { IsCompleted: true } opening)
        {
            modPreviewMediaOpening = null;
            if (opening.IsCompletedSuccessfully)
            {
                modPreviewMedia = opening.Result;
                CatalogMediaPlayback opened = modPreviewMedia.Playback;
                modPreviewResult = Describe(opened);
                modPreviewMessage = "";
            }
            else
            {
                modPreviewMessage = opening.Exception?.GetBaseException().Message ?? "Media preview was cancelled.";
            }
            _view = null;
        }

        if (modPreviewMediaSeeking is { IsCompleted: true } seeking && modPreviewMedia is { } media)
        {
            modPreviewMediaSeeking = null;
            if (seeking.IsCompletedSuccessfully)
            {
                media.ReplacePlayback(seeking.Result);
                modPreviewResult = Describe(media.Playback);
                modPreviewMessage = "";
                SetModMediaPlaying(media.Playback, resumePreviewAfterSeek);
            }
            else
            {
                modPreviewMessage = seeking.Exception?.GetBaseException().Message ?? "Seeking was cancelled.";
            }
            _view = null;
        }

        if (modPreviewMedia?.Playback is { } playback)
        {
            bool wasPlaying = playback.IsPlaying;
            string? beforeError = playback.Before.Error;
            string? afterError = playback.After.Error;
            try
            {
                playback.Update();
            }
            catch (Exception error) when (error is IOException or InvalidDataException or InvalidOperationException)
            {
                modPreviewMessage = error.Message;
                EndModMediaPreview();
                _view = null;
                return;
            }
            if (wasPlaying != playback.IsPlaying)
            {
                _view = null;
            }
            if (beforeError != playback.Before.Error || afterError != playback.After.Error)
            {
                modPreviewResult = Describe(playback);
                _view = null;
            }
        }
    }

    private static ModPreviewComparison Describe(CatalogMediaPlayback playback)
    {
        return new(CatalogMediaComparison.Describe(playback.Before, "Original media could not be matched."), CatalogMediaComparison.Describe(playback.After, "Package media is unavailable."));
    }

    private void ToggleModMediaPlayback()
    {
        if (modPreviewMedia?.Playback is not { } playback || modPreviewMediaSeeking is not null || playback.DurationSeconds <= 0)
        {
            return;
        }
        if (!playback.IsPlaying && playback.PositionSeconds >= playback.DurationSeconds)
        {
            SeekModMedia(0, resume: true);
            return;
        }
        SetModMediaPlaying(playback, !playback.IsPlaying);
        _view = null;
    }

    private void ToggleModMediaSound()
    {
        if (modPreviewMedia?.Playback is not { } playback)
        {
            return;
        }
        playback.ListenAfter = !playback.ListenAfter;
        _view = null;
    }

    private void SetModMediaPlaying(CatalogMediaPlayback playback, bool playing)
    {
        try
        {
            playback.SetPlaying(playing);
        }
        catch (Exception error) when (error is IOException or InvalidOperationException)
        {
            modPreviewMessage = error.Message;
        }
    }

    private void SeekModMedia(double seconds, bool? resume = null)
    {
        if (modPreviewMedia?.Playback is not { } playback || modPreviewMediaSeeking is not null || playback.DurationSeconds <= 0)
        {
            return;
        }
        resumePreviewAfterSeek = resume ?? playback.IsPlaying;
        playback.SetPlaying(false);
        modPreviewMediaSeeking = playback.SeekAsync(Math.Clamp(seconds, 0, playback.DurationSeconds), modPreviewCancellation!.Token);
        _view = null;
    }

    private void EndModMediaPreview()
    {
        Task<CatalogMediaComparison>? opening = modPreviewMediaOpening;
        Task<CatalogMediaPlayback>? seeking = modPreviewMediaSeeking;
        CatalogMediaComparison? media = modPreviewMedia;
        modPreviewMediaOpening = null;
        modPreviewMediaSeeking = null;
        modPreviewMedia = null;
        resumePreviewAfterSeek = false;
        if (opening is not null)
        {
            _ = ReleaseOpeningAsync(opening);
        }
        if (media is not null)
        {
            ObserveAbandonedPreview(media.ReleaseAsync(seeking));
        }
    }

    private static async Task ReleaseOpeningAsync(Task<CatalogMediaComparison> opening)
    {
        try
        {
            CatalogMediaComparison abandoned = await opening.ConfigureAwait(false);
            await abandoned.ReleaseAsync().ConfigureAwait(false);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            // The selection was abandoned. A later selection owns its own error state.
        }
    }

    private CatalogMediaSide? ModMediaSide(int slot)
    {
        return slot == 0 ? modPreviewMedia?.Playback.Before : modPreviewMedia?.Playback.After;
    }

    private sealed class PreviewMediaTimeline(MenuController menus) : UiElement
    {
        private UiRect track;

        public override void Arrange(UiRect bounds)
        {
            Bounds = bounds;
            float timeWidth = Math.Min(158 * menus.TextScale, bounds.Width * 0.42f);
            track = new(bounds.X + 8, bounds.Y + bounds.Height * 0.45f, Math.Max(0, bounds.Width - timeWidth - 16), Math.Max(2, bounds.Height * 0.14f));
        }

        public override void Pointer(float x, float y)
        {
            if (menus.modPreviewMedia?.Playback is { } playback && track.Width > 0)
            {
                double ratio = Math.Clamp((x - track.X) / track.Width, 0, 1);
                menus.SeekModMedia(playback.DurationSeconds * ratio);
            }
        }

        public override bool Key(UiKey key)
        {
            if (menus.modPreviewMedia?.Playback is not { } playback)
            {
                return false;
            }
            int seconds = key switch
            {
                UiKey.Left => -5,
                UiKey.Right => 5,
                UiKey.PageUp => -30,
                UiKey.PageDown => 30,
                _ => 0
            };
            if (seconds == 0)
            {
                return false;
            }
            menus.SeekModMedia(playback.PositionSeconds + seconds);
            return true;
        }

        protected override void DrawSelf(IUiCanvas canvas, UiTheme theme)
        {
            if (menus.modPreviewMedia?.Playback is not { } playback)
            {
                return;
            }
            if (IsFocused)
            {
                theme.Selection(canvas, Bounds);
            }
            canvas.Fill(track, theme.Muted);
            float ratio = playback.DurationSeconds > 0 ? (float)Math.Clamp(playback.PositionSeconds / playback.DurationSeconds, 0, 1) : 0;
            canvas.Fill(new(track.X, track.Y, track.Width * ratio, track.Height), theme.Foreground);
            string status = menus.modPreviewMediaSeeking is null ? $"{Format(playback.PositionSeconds)} / {Format(playback.DurationSeconds)}" : "Seeking…";
            canvas.Text(status, 15 * menus.TextScale, new(track.Right + 8, Bounds.Y, Math.Max(0, Bounds.Right - track.Right - 8), Bounds.Height), theme.Foreground, UiAlignment.Start, UiTextFlow.SingleLine);
        }

        private static string Format(double seconds)
        {
            TimeSpan time = TimeSpan.FromSeconds(Math.Max(0, seconds));
            return time.TotalHours >= 1
                ? FormattableString.Invariant($"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}")
                : time.ToString(@"mm\:ss", CultureInfo.InvariantCulture);
        }
    }
}
