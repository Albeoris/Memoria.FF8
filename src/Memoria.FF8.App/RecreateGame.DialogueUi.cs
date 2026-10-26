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
        DialogueState display = LocalizedDialogue(dialogue);
        if (display.Text.Contains("{Item ", StringComparison.Ordinal))
        {
            EnsureLiveMenuCatalog();
            display = display with { Text = DialogueTextFormatter.ExpandItemNames(display.Text, liveMenuCatalog!.ItemName) };
        }
        DialogueWindow? window = dialogue.X == 0 && dialogue.Y == 0 ? events!.Scene.DialogueWindows.GetValueOrDefault(dialogue.Channel) : null;
        window = LocalizedWindow(dialogue, window);
        string expanded = DialogueTextFormatter.ExpandButtonBindings(NativeMessageVariables.Expand(display.Text, events!.MessageVariables), keyboard.NativeButtonBindingPrompt);
        foreach (var name in events.Scene.Names.OrderBy(pair => pair.Key))
        {
            expanded += "\u0001" + name.Key + "=" + name.Value;
        }
        // Names, message variables and input bindings can change while a window retains its native identity.
        string key = expanded + "\u0002" + display.X + "," + display.Y + "," + display.FirstChoice + "," + display.LastChoice;
        var presentation = dialoguePages.Get(dialogue, window, uiPreferences.DialogueFontFamily, uiPreferences.DialogueFontSize, key, session.State.Scene == SceneKind.WorldMap, () =>
        {
            var document = DialogueTextFormatter.Parse(display.Text, events.Scene.Names, events.MessageVariables, keyboard.NativeButtonBindingPrompt);
            float size = uiPreferences.DialogueFontSize;
            string font = uiPreferences.DialogueFontFamily;
            using var measurer = SystemTextLayout.CreateMeasurer(size, font);
            return DialogueLayout.Create(document, display, window, measurer.LineHeight, measurer.Measure);
        }, retainPosition: true);
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
        SynchronizeFieldVoices();
    }

    private void SynchronizeFieldVoices()
    {
        if (fieldAudio is null || resourceHost is null)
        {
            return;
        }
        bool active = session.State.Scene == SceneKind.Field && field is not null;
        FieldVoiceDialogue[] voices = active ? VisibleDialogues.Select(dialogue =>
        {
            var presentation = GetDialoguePages(dialogue);
            DialogueVisualPage page = presentation.Pages[presentation.Pager.PageIndex];
            bool focused = dialogue.Channel == events?.CurrentDialogue?.Channel;
            int? choice = page.HasChoices && focused && presentation.Pager.IsPageRevealed(page) ? selectedChoice : null;
            return new FieldVoiceDialogue(dialogue, page.SourcePageIndex, choice);
        }).ToArray() : [];
        NativeScalarValue? story = active ? events?.EventMemory.Read(256, 2) : null;
        int? gameMoment = story is { IsExact: true } ? story.RequireExact() : null;
        fieldAudio.SynchronizeVoices(active ? field!.Name : null, active ? field : null, voices, resourceHost.Resources, resourceHost.VoiceResolver, gameMoment);
    }

    private bool CanVoiceAutoAdvance(DialogueState dialogue)
    {
        if (fieldAudio?.CanVoiceAutoAdvance(dialogue.Channel) != true)
        {
            return false;
        }
        var presentation = GetDialoguePages(dialogue);
        return presentation.Pager.IsPageRevealed(presentation.Pages[presentation.Pager.PageIndex]);
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
        DialogueState display = LocalizedDialogue(dialogue);
        float scale = viewport.Width / 320f;
        var presentation = GetDialoguePages(dialogue);
        var page = presentation.Pages[presentation.Pager.PageIndex];
        DialogueWindow? window = dialogue.X == 0 && dialogue.Y == 0 ? events!.Scene.DialogueWindows.GetValueOrDefault(dialogue.Channel) : null;
        window = LocalizedWindow(dialogue, window);
        PixelBounds panelBounds = DialoguePresentation.Bounds(display, window, viewport, page.Width * scale, page.Height * scale);
        DialogueChannelStyle style = events?.Scene.MessageChannelStyles.GetValueOrDefault(dialogue.Channel) ?? DialogueChannelStyle.Default;
        if (style.HasPanel)
        {
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
            // TODO(TD-003): Match the complete native UI material and device profile.
            cached.View.Draw(uiCanvas!, CreateUiTheme(8, style.BackgroundAlpha));
        }
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
            if (remaining > 0 && line.IsChoice && line.SourceLineIndex == LocalizedChoiceLine(dialogue, selectedChoice))
            {
                uiCanvas!.MenuPointer(new UiRect(position.X, position.Y, DialogueLayout.ChoiceIndent(lineHeight) - DialogueLayout.ChoicePointerGap, lineHeight));
            }
            foreach (var run in line.Runs)
            {
                float alpha = run.Run.Blink ? DialogueBlink.Opacity(seconds) : 1;
                string visibleText = ButtonPrompt.VisibleText(run.Run, ref remaining);
                Vector2 runPosition = position + new Vector2(run.X * scale, 0);
                if (run.Run.IsButton)
                {
                    ButtonPrompt.Draw(batch, pixel, text, visibleText, fontSize, runPosition, lineHeight, alpha, family, bounds, uiCanvas);
                }
                else
                {
                    text.DrawLine(batch, visibleText, fontSize, runPosition, run.Run.Color * alpha, family);
                }
            }
            position.Y += lineHeight;
        }
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
        timerUi.Draw(uiCanvas!, CreateUiTheme(10));
    }
}
