using Memoria.FF7.Runtime;
using Memoria.FF8.GameUi;
using Memoria.FF8.Ui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8;

internal sealed partial class Ff7Scene
{
    private const int BattlePolicyDebugCommand = 4;
    private const int RandomEncountersDebugCommand = 5;
    private const int DebugCommandCount = 6;
    private UiView? debugView;
    private bool debugPointerPressed;

    internal CrossoverMappingMenu CreateMappingMenu(string ff8Directory, KeyboardState keys, MouseState mouse)
    {
        return new CrossoverMappingMenu(device, installation, ff8Directory, envelope.Mappings, keys, mouse, dialogueText!, dialogueCanvas!, SharedTheme, SessionGame.Ff8);
    }

    private UiTheme SharedTheme()
    {
        UiPreferences preferences = PresentationPreferences;
        return dialogueTheme?.Invoke() ?? (preferences.MenuPresentation == MenuPresentation.Vector ? new VectorUiTheme { FontFamily = preferences.UiFontFamily } : new UiTheme { FontFamily = preferences.UiFontFamily });
    }

    private void DrawSharedDebugMenu(Rectangle bounds)
    {
        if (dialogueCanvas is null)
        {
            throw new InvalidOperationException("FFVII debug menus require the shared presentation context.");
        }
        UiPreferences preferences = PresentationPreferences;
        float scale = preferences.TextScale / 100f * preferences.UiFontSize / 24f;
        UiAnchorPanel root = new();
        UiPanel panel = new() { Caption = "Debug menu", Anchors = new UiAnchors(0.06f, 0.12f, 0.94f, 0.88f), Padding = 18, CaptionHeight = 40 };
        UiAnchorPanel content = new();
        UiPanel choices = new() { Anchors = new UiAnchors(0, 0, 0.64f, 1), Padding = 8 };
        UiPanel preview = new() { Caption = "Selection", Anchors = new UiAnchors(0.66f, 0, 1, 1), Padding = 14, CaptionHeight = 32 };
        UiList commands = new()
        {
            Items = ["Save", "Switch world: FFVIII", "Dialogue: " + (UseNativeDialogue ? "Original FFVII" : "Common"), "Auto-play: " + (autoPlay?.Running == true && !autoPlay.Paused ? "Pause" : "Start"), "Battle policy: " + (Session?.Progress.FullCombatEnabled == true ? "Full combat" : "Instant victory"), "Random encounters: " + (Session?.Progress.RandomEncountersEnabled == false ? "Off" : "On")],
            EnabledItems = [CanSave, CanSave && envelope.Ff8Payload is { Length: > 0 }, true, !IsTitle, !IsTitle, !IsTitle],
            SelectedIndex = debugSelection,
            SelectionChanged = index => debugSelection = index,
            Confirmed = index =>
            {
                debugSelection = index;
                ActivateDebugSelection();
            },
            FontSize = 24 * scale,
            RowHeight = 52 * scale
        };
        choices.Children.Add(commands);
        content.Children.Add(choices);
        string details = noticeWindow.Text ?? "World switching and saving use the original game's available boundaries.";
        preview.Children.Add(new UiLabel { Text = details, FontSize = 18 * scale });
        content.Children.Add(preview);
        panel.Children.Add(content);
        root.Children.Add(panel);
        UiView view = new(root) { DesignWidth = 960, DesignHeight = 720, ViewportMode = preferences.MenuPresentation == MenuPresentation.Vector ? UiViewportMode.Expand : UiViewportMode.Fit };
        view.Resize(bounds.Width, bounds.Height);
        view.Focus(commands);
        view.Draw(dialogueCanvas, SharedTheme());
        debugView = view;
    }

    private void UpdateDebugPointer()
    {
        if (debugView is null)
        {
            return;
        }
        MouseState mouse = Mouse.GetState();
        bool pressed = mouse.LeftButton == ButtonState.Pressed;
        bool clicked = pressed && !debugPointerPressed;
        debugPointerPressed = pressed;
        if (clicked)
        {
            debugView.Resize(device.Viewport.Width, device.Viewport.Height);
            debugView.Pointer(mouse.X, mouse.Y, true);
        }
    }
}
