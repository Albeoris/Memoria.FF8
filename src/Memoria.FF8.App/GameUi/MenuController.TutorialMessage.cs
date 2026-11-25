using Memoria.FF8.Ui;
using Memoria.FF8.Resources.Localization;
using Microsoft.Xna.Framework;

namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    private string? _tutorialMessage;
    private float _tutorialMessageX;
    private float _tutorialMessageY;

    internal void ShowTutorialMessage(string? text, int centerX, int centerY)
    {
        LocalizationTextBinding? before = _tutorialMessage is null ? null : LocalizationTextTrace.Find(_tutorialMessage);
        LocalizationTextBinding? after = text is null ? null : LocalizationTextTrace.Find(text);
        bool sameBinding = before?.Key == after?.Key && before?.ResourceFingerprint == after?.ResourceFingerprint && before?.Template == after?.Template && before?.PackageId == after?.PackageId;
        if (_tutorialMessage == text && sameBinding && _tutorialMessageX == centerX / 384f && _tutorialMessageY == centerY / 256f)
        {
            return;
        }
        _tutorialMessage = text;
        _tutorialMessageX = centerX / 384f;
        _tutorialMessageY = centerY / 256f;
        _view = null;
    }

    private UiTextPopup? BuildTutorialMessage()
    {
        if (_tutorialMessage is null)
        {
            return null;
        }
        DialogueDocument? document = _tutorialMessage.Contains("{Key ", StringComparison.Ordinal) ? DialogueTextFormatter.Parse(_tutorialMessage) : null;
        UiTextPopup popup = document is not null && document.Pages.SelectMany(page => page.Lines).SelectMany(line => line.Runs).Any(run => run.Icon.HasValue)
            ? new NativeTutorialIconPopup(document)
            : new UiTextPopup();
        popup.Id = "native-tutorial-message";
        popup.Text = _tutorialMessage;
        popup.FontSize = 25 * TextScale;
        popup.CenterX = _tutorialMessageX;
        popup.CenterY = _tutorialMessageY;
        return popup;
    }

    private sealed class NativeTutorialIconPopup(DialogueDocument document) : UiTextPopup
    {
        protected override UiSize MeasureContent(IUiCanvas canvas, float width)
        {
            (DialogueVisualLine[] lines, float lineHeight) = Layout(canvas, width);
            return new(lines.Max(line => line.Width), lines.Length * lineHeight);
        }

        protected override void DrawContent(IUiCanvas canvas, UiTheme theme, UiRect content)
        {
            (DialogueVisualLine[] lines, float lineHeight) = Layout(canvas, content.Width);
            float y = content.Y;
            foreach (DialogueVisualLine line in lines)
            {
                foreach (DialoguePlacedRun placed in line.Runs)
                {
                    DialogueRun run = placed.Run;
                    float x = content.X + placed.X;
                    UiRect bounds = new(x, y, Math.Max(0, content.Right - x), lineHeight);
                    if (run.Icon is int icon)
                    {
                        if (canvas is MonoGameUiCanvas monoGame)
                        {
                            monoGame.DrawDialogueIcon(icon, new Vector2(x, y), lineHeight, run.Color, new Rectangle((int)content.X, (int)content.Y, (int)content.Width, (int)content.Height));
                        }
                        else
                        {
                            canvas.Image(new UiImageSource(UiImageKind.Icon, icon), new UiRect(x, y, lineHeight, lineHeight));
                        }
                    }
                    else if (run.IsButton)
                    {
                        ButtonPrompt.DrawCanvas(canvas, run.Text, FontSize, bounds, 1, null);
                    }
                    else
                    {
                        canvas.Text(run.Text, FontSize, bounds, theme.Foreground);
                    }
                }
                y += lineHeight;
            }
        }

        private (DialogueVisualLine[] Lines, float LineHeight) Layout(IUiCanvas canvas, float width)
        {
            float lineHeight = Math.Max(1, canvas.MeasureText("Mg", FontSize, float.PositiveInfinity).Height);
            float Measure(string value) => value.Length == 0 ? 0 : canvas.MeasureText(value, FontSize, float.PositiveInfinity).Width;
            float IconWidth(int icon) => canvas is MonoGameUiCanvas monoGame ? monoGame.DialogueIconAdvance(icon, lineHeight) : lineHeight;
            DialogueVisualLine[] lines = document.Pages.SelectMany(page => page.Lines).SelectMany(line => DialogueLayout.Wrap(line, width, Measure, false, lineHeight, IconWidth)).ToArray();
            return (lines, lineHeight);
        }
    }
}
