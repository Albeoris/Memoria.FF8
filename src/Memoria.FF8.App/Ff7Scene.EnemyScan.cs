using Memoria.FF8.Rendering;
using Memoria.FF8.GameUi;
using Memoria.FF8.Ui;

namespace Memoria.FF8;

internal sealed partial class Ff7Scene
{
    private CrossoverEnemyScan? displayedEnemyScan;
    private CrossoverEnemyScan? dismissedEnemyScan;

    private bool UpdateEnemyScan()
    {
        CrossoverEnemyScan? latest = mixedCombat?.Guests.Select(guest => guest.LastScan).OfType<CrossoverEnemyScan>().OrderByDescending(scan => scan.Tick).FirstOrDefault();
        if (latest is null)
        {
            displayedEnemyScan = null;
            dismissedEnemyScan = null;
            return false;
        }
        if (displayedEnemyScan is null && latest != dismissedEnemyScan)
        {
            displayedEnemyScan = latest;
            sharedInput.SuppressHeldUiInput();
            return true;
        }
        if (displayedEnemyScan is not null && (semanticInput.Confirm || semanticInput.Cancel))
        {
            dismissedEnemyScan = displayedEnemyScan;
            displayedEnemyScan = null;
            return true;
        }
        return displayedEnemyScan is not null;
    }

    private void DrawEnemyScan(IUiCanvas canvas, UiTheme theme, BattleHudLayout layout, float textScale)
    {
        if (displayedEnemyScan is not { } scan)
        {
            return;
        }
        float scale = layout.Scale;
        float size = 22 * scale * textScale;
        string statistics = $"Level {scan.Level}     HP {scan.HitPoints}/{scan.MaximumHitPoints}     MP {scan.MagicPoints}/{scan.MaximumMagicPoints}\n{scan.CombatStatistics}";
        float statsHeight = canvas.MeasureText(statistics, size, 704 * scale).Height / scale;
        float descriptionHeight = canvas.MeasureText(scan.Description, size, 704 * scale).Height / scale;
        float height = statsHeight + descriptionHeight + 122;
        float top = (672 - height) / 2;
        canvas.Fill(layout.Bounds, new(0, 0, 0, 160));
        theme.Panel(canvas, layout.Rect(112, top, 736, height), scan.Name, UiTheme.DefaultCaptionFontSize * scale * textScale);
        canvas.Text(statistics, size, layout.Rect(128, top + 42, 704, statsHeight), theme.Foreground, UiAlignment.Start, UiTextFlow.Wrap);
        canvas.Text(scan.Description, size, layout.Rect(128, top + 58 + statsHeight, 704, descriptionHeight), theme.Foreground, UiAlignment.Start, UiTextFlow.Wrap);
        canvas.Text("Confirm or Cancel to return", size, layout.Rect(128, top + height - 38, 704, 28), theme.Foreground);
    }
}
