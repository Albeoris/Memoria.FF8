using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;
using Microsoft.Xna.Framework;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private readonly Dictionary<int, (string Key, UiView View)> dialogueUiViews = [];
    private UiView? movieHintUi;
    private string? movieHintBindings;
    private UiView? timerUi;
    private UiLabel? timerUiText;

    private void DrawDialogueUi(Rectangle bounds, PixelBounds viewport, PixelBounds panelBounds, int channel, string displayed, float measuredHeight)
    {
        float scale = viewport.Width / 320f;
        // Script-owned positions become viewport anchors; templates never assign pixel coordinates.
        var anchors = new UiAnchors((panelBounds.X - viewport.X) / (float)viewport.Width, (panelBounds.Y - viewport.Y) / (float)viewport.Height, (panelBounds.X + panelBounds.Width - viewport.X) / (float)viewport.Width, (panelBounds.Y + panelBounds.Height - viewport.Y) / (float)viewport.Height);
        string key = string.Join('|', displayed, anchors, measuredHeight / scale);
        if (!dialogueUiViews.TryGetValue(channel, out var cached) || cached.Key != key)
        {
            var root = new UiAnchorPanel();
            var panel = new UiPanel { Anchors = anchors, CaptionHeight = 0, Padding = new UiThickness(DialoguePresentation.Padding) };
            var content = new UiGrid { Columns = 1 };
            content.Children.Add(new UiLabel { Text = displayed, FontSize = 8, Height = measuredHeight / scale, VerticalAlignment = UiAlignment.Center });
            panel.Children.Add(content);
            root.Children.Add(panel);
            cached = (key, new UiView(root) { DesignWidth = 320, DesignHeight = 240 });
            dialogueUiViews[channel] = cached;
        }
        cached.View.Resize(bounds.Width, bounds.Height);
        cached.View.Draw(uiCanvas!, new UiTheme { FontSize = 8 });
    }

    private void DrawMovieHintUi(Rectangle bounds)
    {
        if (movieHintUi is null || movieHintBindings != keyboard.BindingHint(ControlAction.SkipMovie))
        {
            var root = new UiAnchorPanel();
            movieHintBindings = keyboard.BindingHint(ControlAction.SkipMovie);
            root.Children.Add(new UiLabel { Anchors = new UiAnchors(0.025f, 0.94f, 0.975f, 0.99f), Text = movieHintBindings + ": skip", FontSize = 16, Muted = true });
            movieHintUi = CreateWindowView(root);
        }
        DrawWindowView(movieHintUi, bounds);
    }

    private void DrawTimerUi(Rectangle bounds, NativeExamTimerState timer)
    {
        if (timerUi is null)
        {
            var root = new UiAnchorPanel();
            timerUiText = new UiLabel { FontSize = 10, HorizontalAlignment = UiAlignment.Start, VerticalAlignment = UiAlignment.Start };
            root.Children.Add(timerUiText);
            timerUi = new UiView(root) { DesignWidth = 320, DesignHeight = 240 };
        }
        string displayed = timer.FieldDisplayText();
        var anchors = new UiAnchors(timer.DisplayX / 320f, timer.DisplayY / 240f, timer.DisplayX / 320f + 0.5f, timer.DisplayY / 240f + 0.1f);
        if (timerUiText!.Text != displayed || timerUiText.Anchors != anchors)
        {
            timerUiText.Text = displayed;
            timerUiText.Anchors = anchors;
            timerUi.InvalidateLayout();
        }
        timerUi.Resize(bounds.Width, bounds.Height);
        timerUi.Draw(uiCanvas!, new UiTheme { FontSize = 10 });
    }
}
