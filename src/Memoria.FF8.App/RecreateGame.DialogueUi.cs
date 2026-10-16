using Memoria.FF8.Runtime;
using Memoria.FF8.Events;
using Memoria.FF8.Ui;
using Microsoft.Xna.Framework;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private readonly Dictionary<int, (string Key, UiView View)> dialogueUiViews = [];
    private UiView? timerUi;
    private UiLabel? timerUiText;
    private readonly DialoguePageCache dialoguePages = new();

    private (IReadOnlyList<DialogueVisualPage> Pages, DialoguePager Pager) GetDialoguePages(DialogueState dialogue)
    {
        DialogueWindow? window = dialogue.X == 0 && dialogue.Y == 0 ? events!.Scene.DialogueWindows.GetValueOrDefault(dialogue.Channel) : null;
        string expanded = NativeMessageVariables.Expand(dialogue.Text, events!.MessageVariables);
        foreach (var name in events.Scene.Names.OrderBy(pair => pair.Key))
        {
            expanded += "\u0001" + name.Key + "=" + name.Value;
        }
        // Names and message variables can change while a persistent window retains its native identity.
        string key = expanded + (dialogue.IsNamePrompt ? "\u0001" + enteredName : "");
        var presentation = dialoguePages.Get(dialogue, window, uiPreferences.DialogueFontFamily, uiPreferences.DialogueFontSize, key, session.State.Scene == SceneKind.WorldMap, () =>
        {
            var document = DialogueTextFormatter.Parse(dialogue.Text, events.Scene.Names, events.MessageVariables);
            if (dialogue.IsNamePrompt)
            {
                document = document.WithLiteralLine(enteredName + "_  (Enter: accept)");
            }
            float size = uiPreferences.DialogueFontSize;
            string font = uiPreferences.DialogueFontFamily;
            using var measurer = SystemTextLayout.CreateMeasurer(size, font);
            return DialogueLayout.Create(document, dialogue, window, measurer.LineHeight, measurer.Measure);
        }, retainPosition: dialogue.IsNamePrompt);
        if (dialogue.IsNamePrompt)
        {
            presentation.Pager.UpdateReveal(TimeSpan.Zero, presentation.Pages[presentation.Pager.PageIndex], uiPreferences.FieldMessageSpeed, instant: true);
        }
        return presentation;
    }

    private IEnumerable<DialogueState> VisibleDialogues
    {
        get
        {
            if (session.State.Scene == SceneKind.WorldMap)
            {
                return world?.CurrentDialogue is { } current ? new[] { current } : [];
            }
            return events is null ? [] : events.DialogueTemplates.OrderBy(dialogue => dialogue.Channel);
        }
    }

    private void UpdateDialoguePages(TimeSpan elapsed)
    {
        var visible = VisibleDialogues.ToArray();
        dialoguePages.RetainChannels(visible.Select(dialogue => dialogue.Channel));
        foreach (var dialogue in visible)
        {
            var presentation = GetDialoguePages(dialogue);
            presentation.Pager.UpdateReveal(elapsed, presentation.Pages[presentation.Pager.PageIndex], uiPreferences.FieldMessageSpeed, instant: dialogue.IsNamePrompt);
            if (!dialogue.CanConfirm)
            {
                presentation.Pager.UpdatePopup(elapsed, presentation.Pages, DialoguePager.CharactersPerSecond(uiPreferences.FieldMessageSpeed));
            }
        }
    }

    private bool AdvanceDialoguePages()
    {
        var channels = VisibleDialogues.Select(dialogue =>
        {
            var presentation = GetDialoguePages(dialogue);
            return (Dialogue: dialogue, presentation.Pager, presentation.Pages);
        }).ToArray();
        bool revealed = false;
        foreach (var channel in channels.Where(channel => channel.Dialogue.CanConfirm))
        {
            revealed |= channel.Pager.RevealCurrentPage(channel.Pages[channel.Pager.PageIndex]);
        }
        return revealed || DialoguePager.AdvanceConfirmable(channels.Select(channel => (channel.Dialogue, channel.Pager, channel.Pages.Count)));
    }

    private void DrawDialogueUi(Rectangle bounds, PixelBounds viewport, DialogueState dialogue, double seconds)
    {
        float scale = viewport.Width / 320f;
        var presentation = GetDialoguePages(dialogue);
        var page = presentation.Pages[presentation.Pager.PageIndex];
        DialogueWindow? window = dialogue.X == 0 && dialogue.Y == 0 ? events!.Scene.DialogueWindows.GetValueOrDefault(dialogue.Channel) : null;
        PixelBounds panelBounds = DialoguePresentation.Bounds(dialogue, window, viewport, page.Width * scale, page.Height * scale);
        // Script-owned positions become viewport anchors; templates never assign pixel coordinates.
        var anchors = new UiAnchors((panelBounds.X - viewport.X) / (float)viewport.Width, (panelBounds.Y - viewport.Y) / (float)viewport.Height, (panelBounds.X + panelBounds.Width - viewport.X) / (float)viewport.Width, (panelBounds.Y + panelBounds.Height - viewport.Y) / (float)viewport.Height);
        string key = anchors.ToString();
        if (!dialogueUiViews.TryGetValue(dialogue.Channel, out var cached) || cached.Key != key)
        {
            var root = new UiAnchorPanel();
            var panel = new UiPanel { Anchors = anchors, CaptionHeight = 0, Padding = new UiThickness(DialoguePresentation.Padding) };
            root.Children.Add(panel);
            cached = (key, new UiView(root) { DesignWidth = 320, DesignHeight = 240 });
            dialogueUiViews[dialogue.Channel] = cached;
        }
        cached.View.Resize(bounds.Width, bounds.Height);
        cached.View.Draw(uiCanvas!, new UiTheme { FontSize = 8 });
        float fontSize = uiPreferences.DialogueFontSize * scale;
        string family = uiPreferences.DialogueFontFamily;
        float lineHeight = SystemTextLayout.LineHeight(uiPreferences.DialogueFontSize, family) * scale;
        var position = new Vector2(panelBounds.X + DialoguePresentation.Padding * scale, panelBounds.Y + DialoguePresentation.Padding * scale);
        if (page.Speaker is string speaker)
        {
            text.DrawLine(batch, speaker, fontSize * page.SpeakerScale, position, new Color(148, 213, 255), family);
            position.Y += lineHeight * page.SpeakerScale + DialogueLayout.SpeakerGap * scale;
        }
        int remaining = presentation.Pager.VisibleCharacters;
        foreach (var line in page.Lines)
        {
            if (remaining > 0 && line.IsChoice && line.SourceLineIndex == selectedChoice)
            {
                uiCanvas!.MenuPointer(new UiRect(position.X, position.Y, DialogueLayout.ChoiceIndent(lineHeight) - DialogueLayout.ChoicePointerGap, lineHeight));
            }
            foreach (var run in line.Runs)
            {
                float alpha = run.Run.Blink ? DialogueBlink.Opacity(seconds) : 1;
                string visibleText = DialoguePager.VisiblePrefix(run.Run.Text, ref remaining);
                text.DrawLine(batch, visibleText, fontSize, position + new Vector2(run.X * scale, 0), run.Run.Color * alpha, family);
            }
            position.Y += lineHeight;
        }
    }

    private void DrawMovieHintUi(Rectangle bounds)
    {
        MovieSkipHint.Draw(uiCanvas!, new UiRect(bounds.X, bounds.Y, bounds.Width, bounds.Height), keyboard.CancelBindingHint, movieSkipGesture.Progress, uiPreferences.UiFontFamily);
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
