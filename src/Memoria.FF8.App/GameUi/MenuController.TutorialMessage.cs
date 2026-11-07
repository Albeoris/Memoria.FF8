using Memoria.FF8.Ui;
using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    private string? _tutorialMessage;
    private float _tutorialMessageX;
    private float _tutorialMessageY;
    internal bool IsJunctionTutorial { get; init; }

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

    private UiTextPopup? BuildTutorialMessage(bool inDashboard = false)
    {
        if (_tutorialMessage is null)
        {
            return null;
        }
        return new UiTextPopup
        {
            Id = "native-tutorial-message",
            Text = _tutorialMessage,
            FontSize = (inDashboard ? 20 : 25) * TextScale,
            CenterX = inDashboard ? 0.5f : _tutorialMessageX,
            CenterY = inDashboard ? 0.5f : _tutorialMessageY,
            MinimumWidth = 300,
            MaximumWidth = inDashboard ? 1000 : 870,
            Padding = new UiThickness(24, inDashboard ? 10 : 15)
        };
    }
}
