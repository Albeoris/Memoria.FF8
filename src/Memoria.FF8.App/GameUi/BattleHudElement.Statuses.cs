using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

internal sealed partial class BattleHudElement
{
    internal readonly record struct StatusBadge(string Name, int? Icon = null);

    [Flags]
    internal enum AtbVisualStatus
    {
        None = 0,
        Haste = 1,
        Slow = 2,
        Stop = 4
    }

    private readonly record struct StatusLabel(bool Saved, uint Mask, string Name, int? Icon = null);

    private static readonly StatusLabel[] StatusLabels =
    [
        new(true, 1, "KO", SavedStatusArtwork.Icon(1)),
        new(true, 2, "Poison", SavedStatusArtwork.Icon(2)),
        new(true, 4, "Petrify", SavedStatusArtwork.Icon(4)),
        new(true, 8, "Blind", SavedStatusArtwork.Icon(8)),
        new(true, 16, "Silence", SavedStatusArtwork.Icon(16)),
        new(true, 32, "Berserk", SavedStatusArtwork.Icon(32)),
        new(true, 64, "Zombie", SavedStatusArtwork.Icon(64)),
        new(false, 1, "Sleep", 279),
        new(false, 2, "Haste"),
        new(false, 4, "Slow", 280),
        new(false, 8, "Stop", 281),
        new(false, 16, "Regen"),
        new(false, 32, "Protect"),
        new(false, 64, "Shell"),
        new(false, 128, "Reflect"),
        new(false, 256, "Aura"),
        new(false, 512, "Curse", 282),
        new(false, 1024, "Doom"),
        new(false, 2048, "Invincible"),
        new(false, 4096, "Petrifying"),
        new(false, 8192, "Float"),
        new(false, 16384, "Confuse", 283),
        new(false, 32768, "Drain", 284),
        new(false, 131072, "Double"),
        new(false, 262144, "Triple"),
        new(false, 16777216, "Vit 0"),
        new(false, 33554432, "Angel Wing")
    ];

    private string StatusName(StatusLabel label)
    {
        return StatusStatRules.TryGetStatusId(label.Saved, label.Mask, out NativeStatusId status) ? controller.SystemText(BattleStatusLocalization.SemanticId(status), label.Name) : label.Name;
    }

    private void DrawStatuses(IUiCanvas canvas, UiTheme theme, BattleStatusPresentation statuses, float x, float y, float width, float? textScale = null)
    {
        StatusBadge[] active = StatusLabels.Where(status => ((status.Saved ? statuses.Saved : statuses.Battle) & status.Mask) != 0).Select(status => new StatusBadge(StatusName(status), status.Icon)).ToArray();
        DrawStatusBadges(canvas, theme, Layout, textScale ?? controller.TextScale, controller.BattlePresentationSeconds, active, x, y, width);
    }

    internal static void DrawStatusBadges(IUiCanvas canvas, UiTheme theme, BattleHudLayout layout, float textScale, double seconds, StatusBadge[] active, float x, float y, float width)
    {
        float scale = layout.Scale;
        if (active.Length == 0)
        {
            return;
        }
        float fontSize = 11 * textScale;
        float height = Math.Max(12, fontSize);
        float totalWidth = active.Sum(status => canvas.MeasureText(status.Name, fontSize * scale, float.PositiveInfinity).Width / scale + (status.Icon.HasValue ? 14 : 0) + 9);
        // Narrow target columns cycle complete labels instead of hiding an active status in clipped text.
        if (totalWidth > width)
        {
            int index = (int)(seconds / 2) % active.Length;
            active = [active[index]];
        }
        canvas.PushClip(layout.Rect(x, y, width, height));
        try
        {
            foreach (StatusBadge status in active)
            {
                if (status.Icon is int icon)
                {
                    canvas.Image(new UiImageSource(UiImageKind.Icon, icon), layout.Rect(x, y, 12, 12));
                    x += 14;
                }
                float textWidth = canvas.MeasureText(status.Name, fontSize * scale, float.PositiveInfinity).Width / scale;
                canvas.Text(status.Name, fontSize * scale, layout.Rect(x, y, textWidth, height), theme.Foreground, UiAlignment.Start, UiTextFlow.SingleLine);
                x += textWidth + 9;
            }
        }
        finally
        {
            canvas.PopClip();
        }
    }

    private static (UiColor Left, UiColor Right) AtbStatusColors(AtbVisualStatus statuses, bool ready)
    {
        //4B14C0 selects Haste, Slow, Stop, then the ordinary B8790C/B8792C row.
        return statuses.HasFlag(AtbVisualStatus.Haste) ? (new(240, 5, 58), new(150, 0, 110))
            : statuses.HasFlag(AtbVisualStatus.Slow) ? (new(94, 46, 251), new(70, 0, 140))
            : statuses.HasFlag(AtbVisualStatus.Stop) ? (new(154, 154, 154), new(154, 154, 154))
            : ready ? (new(255, 255, 0), new(255, 255, 0)) : (new(254, 149, 14), new(179, 40, 1));
    }
}
