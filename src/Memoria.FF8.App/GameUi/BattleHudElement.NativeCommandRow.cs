using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

internal sealed partial class BattleHudElement
{
    private bool DrawNativeLimitCommandRow(IUiCanvas canvas, UiTheme theme, int row, float y, float width, float fontSize, bool unavailable)
    {
        if (controller.SelectingAction || controller.SelectingTarget || row != 0 || !controller.NativeLimitAvailable || controller.LimitCommandReveal <= 0)
        {
            return false;
        }
        float ordinaryWidth = width * (1 - controller.LimitCommandReveal);
        if (ordinaryWidth < width)
        {
            canvas.PushClip(Rect(60 + ordinaryWidth, y, width - ordinaryWidth, 32));
            try
            {
                ChoiceText(canvas, theme, controller.CommandLabel(Memoria.FF8.Runtime.BattleAbility.LimitBreak), 60 + ordinaryWidth, y, width, fontSize, unavailable);
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
                ChoiceText(canvas, theme, controller.OrdinaryFirstCommandLabel, 60, y, width, fontSize, unavailable);
            }
            finally
            {
                canvas.PopClip();
            }
        }
        return true;
    }
}
