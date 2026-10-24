using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

internal sealed partial class BattleHudElement
{
    private void DrawNativeDuel(IUiCanvas canvas, UiTheme theme, BattleNativeDuelState state)
    {
        // TODO(TD-009): Retain the procedural panel reveal until the native widget scale and timer blink modulation are owned.
        if (state.Phase == BattleNativeDuelPhase.Ready)
        {
            theme.Panel(canvas, Rect(36, 492, 726, 156));
            IReadOnlyList<BattleNativeDuelChoice> choices = controller.NativeDuelChoices;
            float fontSize = 24 * controller.TextScale;
            float longestName = choices.Select(choice => canvas.MeasureText(choice.Name, fontSize * Scale, 4096 * Scale).Width / Scale).DefaultIfEmpty(0).Max();
            float nameSize = fontSize * Math.Min(1, 324 / Math.Max(1, longestName));
            for (int row = 0; row < choices.Count; row++)
            {
                BattleNativeDuelChoice choice = choices[row];
                float y = 513 + row * 39;
                UiColor color = choice.Finisher ? new UiColor(255, 255, 123) : theme.Foreground;
                Text(canvas, theme, choice.Name, 51, y, 324, 36, nameSize / controller.TextScale, color: color);
                for (int button = 0; button < choice.Buttons.Length; button++)
                {
                    string prompt = ButtonPromptText.Mark(controller.NativeButtonBindingHint(choice.Buttons[button]));
                    float promptWidth = canvas.MeasureText(prompt, fontSize * Scale, 4096 * Scale).Width / Scale;
                    float promptSize = fontSize * Math.Min(1, 66 / Math.Max(1, promptWidth));
                    Text(canvas, theme, prompt, 393 + button * 72, y, 66, 36, promptSize / controller.TextScale, color: color);
                }
            }
        }
        int timer = Math.Max(0, (int)state.RemainingTimer);
        int seconds = timer / 60;
        int hundredths = 100 * (timer % 60) / 60;
        // Icon111 has a +4 X sprite offset; the digit sprites are 8x16 with zero offsets.
        canvas.Image(new UiImageSource(UiImageKind.Icon, 111), Rect(777, 525, 72, 24));
        canvas.ImageStretched(new UiImageSource(UiImageKind.Icon, 56 + seconds / 10), Rect(777, 558, 45, 96));
        canvas.ImageStretched(new UiImageSource(UiImageKind.Icon, 56 + seconds % 10), Rect(825, 558, 45, 96));
        canvas.Image(new UiImageSource(UiImageKind.Icon, 56 + hundredths / 10), Rect(876, 588, 24, 48));
        canvas.Image(new UiImageSource(UiImageKind.Icon, 56 + hundredths % 10), Rect(900, 588, 24, 48));
    }
}
