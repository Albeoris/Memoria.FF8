using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

internal sealed partial class BattleHudElement
{
    private readonly record struct StatusLabel(bool Saved, uint Mask, string Name, int? Icon = null);

    private static readonly StatusLabel[] StatusLabels =
    [
        new(true, 1, "KO", 272),
        new(true, 2, "Poison", 273),
        new(true, 4, "Petrify", 274),
        new(true, 8, "Blind", 275),
        new(true, 16, "Silence", 276),
        new(true, 32, "Berserk", 277),
        new(true, 64, "Zombie", 278),
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
        new(false, 4194304, "Vit 0"),
        new(false, 33554432, "Angel Wing")
    ];

    private void DrawStatuses(IUiCanvas canvas, UiTheme theme, BattleStatusPresentation statuses, float x, float y, float width)
    {
        StatusLabel[] active = StatusLabels.Where(status => ((status.Saved ? statuses.Saved : statuses.Battle) & status.Mask) != 0).ToArray();
        if (active.Length == 0)
        {
            return;
        }
        float fontSize = 11 * controller.TextScale;
        float height = Math.Max(12, fontSize);
        float totalWidth = active.Sum(status => canvas.MeasureText(status.Name, fontSize * Scale, float.PositiveInfinity).Width / Scale + (status.Icon.HasValue ? 14 : 0) + 9);
        // Narrow target columns cycle complete labels instead of hiding an active status in clipped text.
        if (totalWidth > width)
        {
            int index = (int)(controller.BattlePresentationSeconds / 2) % active.Length;
            active = [active[index]];
        }
        canvas.PushClip(Rect(x, y, width, height));
        try
        {
            foreach (StatusLabel status in active)
            {
                if (status.Icon is int icon)
                {
                    canvas.Image(new UiImageSource(UiImageKind.Icon, icon), Rect(x, y, 12, 12));
                    x += 14;
                }
                float textWidth = canvas.MeasureText(status.Name, fontSize * Scale, float.PositiveInfinity).Width / Scale;
                canvas.Text(status.Name, fontSize * Scale, Rect(x, y, textWidth, height), theme.Foreground, UiAlignment.Start, UiTextFlow.SingleLine);
                x += textWidth + 9;
            }
        }
        finally
        {
            canvas.PopClip();
        }
    }

    private static (UiColor Left, UiColor Right) AtbStatusColors(uint statuses, bool ready)
    {
        //4B14C0 selects Haste, Slow, Stop, then the ordinary B8790C/B8792C row.
        return (statuses & 2) != 0 ? (new(240, 5, 58), new(150, 0, 110))
            : (statuses & 4) != 0 ? (new(94, 46, 251), new(70, 0, 140))
            : (statuses & 8) != 0 ? (new(154, 154, 154), new(154, 154, 154))
            : ready ? (new(255, 255, 0), new(255, 255, 0)) : (new(254, 149, 14), new(179, 40, 1));
    }
}
