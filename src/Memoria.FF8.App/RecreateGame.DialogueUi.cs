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
            display = display with { Text = DialogueTextFormatter.ExpandItemNames(display.Text, liveMenuCatalog!.EncodedItemName) };
        }
        DialogueWindow? window = dialogue.X == 0 && dialogue.Y == 0 ? events!.Scene.DialogueWindows.GetValueOrDefault(dialogue.Channel) : null;
        window = LocalizedWindow(dialogue, window);
        string expanded = DialogueTextFormatter.ExpandButtonBindings(NativeMessageVariables.Expand(display.Text, events!.MessageVariables), keyboard.NativeButtonBindingPrompt);
        foreach (var name in events.Scene.Names.OrderBy(pair => pair.Key))
        {
            expanded += "\u0001" + name.Key + "=" + name.Value;
        }
        // Names, message variables and input bindings can change while a window retains its native identity.
        var terms = world!.TextTerms;
        string key = expanded + "\u0002" + display.X + "," + display.Y + "," + display.FirstChoice + "," + display.LastChoice + "\u0003" + terms.Generation + "\u0004" + uiPreferences.TextFontMode + ":" + uiPreferences.TextLayoutMode + ":" + nativeTextFont?.ProfileFor(NativeDialogueContext)?.Id + ":" + nativeTextFont?.Revision + "\u0005" + window?.Width + "," + window?.Height;
        IReadOnlyList<DialogueVisualPage> CreatePresentation()
        {
            var document = authoringDraftPreview is not null && IsEditingAuthoringOccurrence(dialogue) && authoringDraftPreview.Length > 0 ? ParseAuthoringPreview(authoringDraftPreview, OriginalTextLayout) : DialogueTextFormatter.Parse(display.Text, events.Scene.Names, events.MessageVariables, keyboard.NativeButtonBindingPrompt, terms, originalCompatible: OriginalTextLayout);
            float size = uiPreferences.DialogueFontSize;
            string font = uiPreferences.DialogueFontFamily;
            if (NativeGameFonts)
            {
                float nativeLineHeight = NativeDialogueLineHeight;
                float MeasureNative(string value) => nativeTextFont?.Measure(value, NativeDialogueContext) ?? 0;
                return OriginalTextLayout ? DialogueLayout.CreateNative(document, display, window, nativeLineHeight, MeasureNative) : DialogueLayout.Create(document, display, window, nativeLineHeight, MeasureNative);
            }
            using var measurer = SystemTextLayout.CreateMeasurer(size, font);
            return DialogueLayout.Create(document, display, window, measurer.LineHeight, measurer.Measure);
        }
        (IReadOnlyList<DialogueVisualPage> Pages, DialoguePager Pager) presentation;
        if (authoringDraftPreview is not null && IsEditingAuthoringOccurrence(dialogue))
        {
            if (authoringPreviewPagesKey != key || authoringPreviewPages is null)
            {
                authoringPreviewPages = CreatePresentation();
                authoringPreviewPagesKey = key;
                if (dialoguePages.ReadPager(dialogue.Channel) is { } baseline)
                {
                    authoringPreviewPager.ReflowFrom(baseline, authoringPreviewPages);
                }
                authoringPreviewPager.RevealCurrentPage(authoringPreviewPages[authoringPreviewPager.PageIndex]);
            }
            localizationWorkbench.SetPreviewPages(authoringPreviewPages.Count, authoringPreviewPages[authoringPreviewPager.PageIndex].Lines.Where(line => line.IsChoice).GroupBy(line => line.SourceLineIndex).Select(group => string.Concat(group.SelectMany(line => line.Runs).Select(run => run.Run.Text))).ToArray());
            presentation = (authoringPreviewPages, authoringPreviewPager);
        }
        else
        {
            presentation = dialoguePages.Get(dialogue, window, uiPreferences.DialogueFontFamily, uiPreferences.DialogueFontSize, key, session.State.Scene == SceneKind.WorldMap, CreatePresentation, retainPosition: true);
        }
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

    internal static int AuthoringPreviewChoiceLine(DialogueVisualPage page, int choice, int fallback)
    {
        int[] lines = page.Lines.Where(line => line.IsChoice).Select(line => line.SourceLineIndex).Distinct().ToArray();
        return lines.Length == 0 ? fallback : lines[Math.Clamp(choice, 0, lines.Length - 1)];
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
        PixelBounds nativeOrigin = panelBounds;
        if (localizationWorkbench.Enabled && nativeTextFont?.Ready == true)
        {
            DialogueDocument nativeDocument = DialogueTextFormatter.Parse(display.Text, events!.Scene.Names, events.MessageVariables, keyboard.NativeButtonBindingPrompt, world!.TextTerms, originalCompatible: true);
            DialogueVisualPage nativePage = DialogueLayout.CreateNative(nativeDocument, display, window, NativeDialogueLineHeight, value => nativeTextFont.Measure(value, NativeDialogueContext))[0];
            nativeOrigin = DialoguePresentation.Bounds(display, window, viewport, nativePage.Width * scale, nativePage.Height * scale);
        }
        ObserveAuthoringDialogue(dialogue, panelBounds, viewport, nativeOrigin);
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
        float fontSize = (NativeGameFonts ? NativeDialogueLineHeight : uiPreferences.DialogueFontSize) * scale;
        string family = uiPreferences.DialogueFontFamily;
        float lineHeight = (NativeGameFonts ? NativeDialogueLineHeight : SystemTextLayout.LineHeight(uiPreferences.DialogueFontSize, family)) * scale;
        Rectangle textClip = Rectangle.Intersect(bounds, new Rectangle(panelBounds.X, panelBounds.Y, panelBounds.Width, panelBounds.Height));
        var position = new Vector2(panelBounds.X + DialoguePresentation.Padding * scale, panelBounds.Y + DialoguePresentation.Padding * scale);
        if (page.Speaker is string speaker)
        {
            if (!DrawNativeDialogueText(speaker, position, scale * page.SpeakerScale, new Color(148, 213, 255), textClip))
            {
                text.DrawLine(batch, speaker, fontSize * page.SpeakerScale, position, new Color(148, 213, 255), family);
            }
            position.Y += lineHeight * page.SpeakerScale + DialogueLayout.SpeakerGap * scale;
        }
        int remaining = presentation.Pager.VisibleCharacters;
        int choiceLine = LocalizedChoiceLine(dialogue, selectedChoice);
        if (authoringDraftPreview is not null && IsEditingAuthoringOccurrence(dialogue) && localizationWorkbench.Session?.Mode == Localization.Authoring.LocalizationSessionMode.Preview)
        {
            choiceLine = AuthoringPreviewChoiceLine(page, localizationWorkbench.PreviewChoice, choiceLine);
        }
        foreach (var line in page.Lines)
        {
            if (remaining > 0 && line.IsChoice && line.SourceLineIndex == choiceLine)
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
                    if (!DrawNativeDialogueText(visibleText, runPosition, scale, run.Run.Color * alpha, textClip))
                    {
                        text.DrawLine(batch, visibleText, fontSize, runPosition, run.Run.Color * alpha, family);
                    }
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
