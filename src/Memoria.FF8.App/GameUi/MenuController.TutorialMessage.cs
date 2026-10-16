using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    private string? _tutorialMessage;
    private float _tutorialMessageX;
    private float _tutorialMessageY;

    internal void ShowTutorialMessage(string? text, int centerX, int centerY)
    {
        _tutorialMessage = text;
        _tutorialMessageX = centerX / 384f;
        _tutorialMessageY = centerY / 256f;
        _view = null;
    }

    private void AddTutorialMessage(UiAnchorPanel parent)
    {
        if (_tutorialMessage is null)
        {
            return;
        }
        float width = Math.Min(870, Math.Max(300, _tutorialMessage.Split('\n').Max(line => line.Length) * 14 + 48));
        float height = _tutorialMessage.Split('\n').Length * 34 + 30;
        float left = Math.Clamp(_tutorialMessageX - width / 1920f, 0, 1 - width / 960);
        float top = Math.Clamp(_tutorialMessageY - height / 1344f, 0, Math.Max(0, 1 - height / 672));
        UiPanel panel = Panel(null, Label(_tutorialMessage, 25));
        panel.Id = "native-tutorial-message";
        panel.Anchors = new UiAnchors(left, top, left + width / 960, top + height / 672);
        parent.Children.Add(panel);
    }
}
