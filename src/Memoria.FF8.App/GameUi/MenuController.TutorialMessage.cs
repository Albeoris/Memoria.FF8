using Memoria.FF8.Ui;
using Memoria.FF8.Resources.Localization;

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

    private void AddTutorialMessage(UiAnchorPanel parent)
    {
        if (_tutorialMessage is null)
        {
            return;
        }
        parent.Children.Add(new UiTextPopup
        {
            Id = "native-tutorial-message",
            Text = _tutorialMessage,
            FontSize = 25 * TextScale,
            CenterX = _tutorialMessageX,
            CenterY = _tutorialMessageY,
            MinimumWidth = 300,
            MaximumWidth = 870,
            Padding = new UiThickness(24, 15)
        });
    }
}
