using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

internal sealed partial class BattleHudElement
{
    private void DrawNativeLimitCommandRow(IUiCanvas canvas, UiTheme theme, string limitLabel, string ordinaryLabel, float y, float width, float fontSize, bool unavailable)
    {
        float ordinaryWidth = width * (1 - controller.LimitCommandReveal);
        if (ordinaryWidth < width)
        {
            canvas.PushClip(Rect(60 + ordinaryWidth, y, width - ordinaryWidth, 32));
            try
            {
                ChoiceText(canvas, theme, limitLabel, 60 + ordinaryWidth, y, width, fontSize, unavailable);
            }
            finally
            {
                canvas.PopClip();
            }
        }
        if (ordinaryWidth > 0)
        {
            canvas.PushClip(Rect(60, y, ordinaryWidth, 32));
            try
            {
                ChoiceText(canvas, theme, ordinaryLabel, 60, y, width, fontSize, unavailable);
            }
            finally
            {
                canvas.PopClip();
            }
        }
    }
}
