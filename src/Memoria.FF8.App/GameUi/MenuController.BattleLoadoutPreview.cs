using System.Diagnostics;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    private const int LoadoutPreviewImageSlot = 90;
    private Func<BattleLoadoutDraft, int, CancellationToken, Task<BattleLoadoutModelPreview>>? prepareBattleLoadoutPreview;
    private readonly Dictionary<(int Character, int Weapon), BattleLoadoutModelPreview> loadoutPreviewCache = [];
    private (BattleLoadoutDraft Draft, int Character, int Weapon)? loadoutPreviewSelection;
    private CancellationTokenSource? loadoutPreviewCancellation;
    private Task<BattleLoadoutModelPreview>? loadoutPreviewPreparation;
    private BattleLoadoutModelPreview? loadoutPreview;
    private string? loadoutPreviewError;
    private long loadoutPreviewClock;

    private void DrawBattleLoadoutPreview(IUiCanvas canvas, UiTheme theme, UiRect bounds, float scale, BattleLoadoutDraft draft, int character)
    {
        if (bounds.IsEmpty || prepareBattleLoadoutPreview is null)
        {
            return;
        }
        var selection = (Draft: draft, Character: character, Weapon: draft.ResolveWeapon(character));
        if (loadoutPreviewSelection != selection)
        {
            if (!ReferenceEquals(loadoutPreviewSelection?.Draft, draft))
            {
                loadoutPreviewCache.Clear();
            }
            CancelBattleLoadoutPreview();
            loadoutPreviewSelection = selection;
            loadoutPreviewClock = Stopwatch.GetTimestamp();
            if (loadoutPreviewCache.TryGetValue((character, selection.Weapon), out BattleLoadoutModelPreview? cached))
            {
                loadoutPreview = cached;
            }
            else
            {
                loadoutPreviewCancellation = new();
                try
                {
                    loadoutPreviewPreparation = prepareBattleLoadoutPreview(draft, character, loadoutPreviewCancellation.Token);
                }
                catch (Exception exception) when (exception is IOException or InvalidDataException or NotSupportedException or InvalidOperationException or ArgumentException)
                {
                    loadoutPreviewError = exception.Message;
                }
            }
        }
        if (loadoutPreviewPreparation is { IsCompleted: true } prepared)
        {
            loadoutPreviewPreparation = null;
            if (prepared.IsCompletedSuccessfully)
            {
                loadoutPreview = prepared.Result;
                if (loadoutPreviewCache.Count == 12)
                {
                    loadoutPreviewCache.Remove(loadoutPreviewCache.Keys.First());
                }
                loadoutPreviewCache[(character, selection.Weapon)] = loadoutPreview;
                loadoutPreviewClock = Stopwatch.GetTimestamp();
            }
            else
            {
                loadoutPreviewError = prepared.Exception?.GetBaseException().Message ?? LoadoutText("preview-interrupted", "Model preview interrupted.");
            }
        }
        float seconds = (float)Stopwatch.GetElapsedTime(loadoutPreviewClock).TotalSeconds;
        if (loadoutPreview is { } model)
        {
            canvas.RasterImage(model.Render(seconds), bounds, LoadoutPreviewImageSlot, model.CurrentFrame);
        }
        else if (loadoutPreviewError is { } error)
        {
            canvas.AlignedText(error, 12 * scale, bounds, theme.Muted, UiAlignment.Center);
        }
        else
        {
            DrawBattleLoadoutPreviewSpinner(canvas, theme.Foreground, bounds, scale, seconds);
        }
    }

    private static void DrawBattleLoadoutPreviewSpinner(IUiCanvas canvas, UiColor color, UiRect bounds, float scale, float seconds)
    {
        const int spokes = 10;
        int active = (int)(seconds * 14) % spokes;
        float x = bounds.X + bounds.Width / 2;
        float y = bounds.Y + bounds.Height / 2;
        float radius = 13 * scale;
        for (int index = 0; index < spokes; index++)
        {
            float angle = index * MathF.Tau / spokes;
            float dx = MathF.Cos(angle) * radius;
            float dy = MathF.Sin(angle) * radius;
            int age = (active - index + spokes) % spokes;
            canvas.Line(x + dx * 0.6f, y + dy * 0.6f, x + dx, y + dy, color with { A = (byte)(255 - age * 23) }, 2 * scale);
        }
    }

    private void ReleaseBattleLoadoutPreview()
    {
        CancelBattleLoadoutPreview();
        loadoutPreviewCache.Clear();
    }

    private void CancelBattleLoadoutPreview()
    {
        loadoutPreviewCancellation?.Cancel();
        loadoutPreviewCancellation?.Dispose();
        loadoutPreviewCancellation = null;
        if (loadoutPreviewPreparation is { } pending)
        {
            _ = pending.ContinueWith(static task => task.Exception, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }
        loadoutPreviewPreparation = null;
        loadoutPreviewSelection = null;
        loadoutPreview = null;
        loadoutPreviewError = null;
    }
}
