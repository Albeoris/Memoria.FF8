using Memoria.FF7.Formats;
using Memoria.FF7.Runtime;
using Memoria.FF8.Events;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Memoria.FF8;

internal sealed partial class Ff7Scene
{
    private Func<ushort, string>? buttonPrompts;

    public void ConfigureButtonPrompts(Func<ushort, string> prompts)
    {
        buttonPrompts = prompts ?? throw new ArgumentNullException(nameof(prompts));
    }

    private string ButtonLabel(FieldTextToken token) => Ff7DialogueButtons.Label(token.Code, buttonPrompts ?? throw new InvalidOperationException("FFVII button prompts require the shared input profile."));

    private SystemTextRenderer? ownedDialogueText;
    private MonoGameUiCanvas? ownedDialogueCanvas;
    private readonly Dictionary<(long Generation, long Request), (int NativePage, string Key, IReadOnlyList<DialogueVisualPage> Pages, DialoguePager Pager)> commonDialoguePages = [];
    private Action<UiPreferences>? setDialoguePreferences;
    private Func<UiPreferences>? dialoguePreferences;
    private Func<UiTheme>? dialogueTheme;
    private SystemTextRenderer? dialogueText;
    private MonoGameUiCanvas? dialogueCanvas;

    public void ConfigureDialoguePresentation(Func<UiPreferences> preferences, SystemTextRenderer text, MonoGameUiCanvas canvas, Func<UiTheme>? theme = null, Action<UiPreferences>? setPreferences = null)
    {
        dialoguePreferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
        dialogueText = text ?? throw new ArgumentNullException(nameof(text));
        dialogueCanvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
        dialogueTheme = theme;
        setDialoguePreferences = setPreferences;
    }

    public void ConfigureSharedDialogueDefaults(SpriteBatch batch, string gameDirectory, string? settingsDirectory)
    {
        UiPreferences preferences = UiPreferences.Load(new GlobalSettingsPaths(isolatedDirectory: settingsDirectory).UiPreferencesPath);
        ownedDialogueText = new SystemTextRenderer(device);
        ownedDialogueCanvas = new MonoGameUiCanvas(device, batch, pixel, ownedDialogueText, gameDirectory);
        ConfigureDialoguePresentation(() => preferences, ownedDialogueText, ownedDialogueCanvas, setPreferences: updated =>
        {
            updated.Save(new GlobalSettingsPaths(isolatedDirectory: settingsDirectory).UiPreferencesPath);
            preferences = updated;
        });
    }

    private void DisposeDialoguePresentation()
    {
        ownedDialogueCanvas?.Dispose();
        ownedDialogueText?.Dispose();
    }

    private bool UseNativeDialogue => dialoguePreferences?.Invoke().TextFontMode == GameTextFontMode.Native;

    private bool AdvanceCommonDialogue(Ff7Dialogue dialogue)
    {
        if (UseNativeDialogue || !dialogue.TextVisible || dialogue.Presentation?.NativeState == 7)
        {
            return false;
        }
        if (!commonDialoguePages.TryGetValue((Session!.FieldGeneration, dialogue.RequestId), out var presentation) || presentation.NativePage != dialogue.Page)
        {
            return true;
        }
        Ff7CommonDialogueText content = Ff7CommonDialogueText.Create(dialogue, Tokens(dialogue), Session!.ExpandDialogueToken, ButtonLabel);
        int pageEnd = presentation.Pages.Take(presentation.Pager.PageIndex + 1).Sum(DialoguePager.CharacterCount);
        return content.VisibleCharacters >= pageEnd && presentation.Pager.Advance(presentation.Pages.Count);
    }

    private bool CanSelectCommonDialogueChoice(Ff7Dialogue dialogue)
    {
        return UseNativeDialogue || commonDialoguePages.TryGetValue((Session!.FieldGeneration, dialogue.RequestId), out var presentation) && presentation.NativePage == dialogue.Page && presentation.Pages[presentation.Pager.PageIndex].HasChoices;
    }

    private void ToggleDialoguePresentation()
    {
        if (dialoguePreferences is null || setDialoguePreferences is null)
        {
            throw new InvalidOperationException("FFVII presentation settings require the shared host preference writer.");
        }
        UiPreferences current = dialoguePreferences();
        bool native = current.TextFontMode != GameTextFontMode.Native;
        setDialoguePreferences(current with { TextFontMode = native ? GameTextFontMode.Native : GameTextFontMode.System, TextLayoutMode = native ? GameTextLayoutMode.OriginalCompatible : GameTextLayoutMode.Adaptive });
    }

    private void DrawCommonDialogue(SpriteBatch batch, Ff7Dialogue dialogue, Ff7Window window, Rectangle bounds)
    {
        UiPreferences preferences = dialoguePreferences?.Invoke() ?? throw new InvalidOperationException("FFVII dialogue requires the shared host presentation context.");
        IReadOnlyList<FieldTextToken> tokens = Tokens(dialogue);
        Ff7CommonDialogueText content = Ff7CommonDialogueText.Create(dialogue, tokens, Session!.ExpandDialogueToken, ButtonLabel);
        DialogueDocument document = content.Document;
        DialogueState state = new(dialogue.Window, dialogue.Text, document.Text, window.X, window.Y, dialogue.FirstChoice, dialogue.LastChoice, dialogue.Selected);
        DialogueWindow nativeWindow = new(dialogue.Window, window.X, window.Y, window.Width, window.Height);
        using var measurer = SystemTextLayout.CreateMeasurer(preferences.DialogueFontSize, preferences.DialogueFontFamily);
        IReadOnlyList<DialogueVisualPage> pages = preferences.TextLayoutMode == GameTextLayoutMode.OriginalCompatible
            ? DialogueLayout.CreateNative(document, state, nativeWindow, measurer.LineHeight, measurer.Measure)
            : DialogueLayout.Create(document, state, nativeWindow, measurer.LineHeight, measurer.Measure);
        var identity = (Session!.FieldGeneration, dialogue.RequestId);
        string key = document.Text + "\u0001" + preferences.DialogueFontFamily + ":" + preferences.DialogueFontSize + ":" + preferences.TextLayoutMode;
        if (!commonDialoguePages.TryGetValue(identity, out var presentation) || presentation.NativePage != dialogue.Page || presentation.Key != key)
        {
            DialoguePager pager = new();
            if (presentation.Pager is not null && presentation.NativePage == dialogue.Page)
            {
                pager.ReflowFrom(presentation.Pager, pages);
            }
            presentation = (dialogue.Page, key, pages, pager);
            commonDialoguePages[identity] = presentation;
        }
        DialogueVisualPage visual = presentation.Pages[presentation.Pager.PageIndex];
        float scale = bounds.Width / 320f;
        PixelBounds panel = DialoguePresentation.Bounds(state, nativeWindow, new PixelBounds(bounds.X, bounds.Y, bounds.Width, bounds.Height), visual.Width * scale, visual.Height * scale);
        UiRect animated = Ff7DialoguePresentationPolicy.AnimatedPanel(panel, dialogue, window);
        Rectangle clip = Ff7DialoguePresentationPolicy.Clip(animated, bounds);
        if (clip.Width == 0 || clip.Height == 0)
        {
            return;
        }
        if (window.Style == 0)
        {
            UiTheme theme = dialogueTheme?.Invoke() ?? (preferences.MenuPresentation == MenuPresentation.Vector
                ? new VectorUiTheme { FontFamily = preferences.UiFontFamily }
                : new UiTheme { FontFamily = preferences.UiFontFamily });
            theme.Panel(dialogueCanvas!, animated);
        }
        if (!dialogue.TextVisible)
        {
            return;
        }
        int remaining = Math.Max(0, content.VisibleCharacters - presentation.Pages.Take(presentation.Pager.PageIndex).Sum(DialoguePager.CharacterCount));
        float lineHeight = measurer.LineHeight * scale;
        Vector2 origin = new(panel.X + DialoguePresentation.Padding * scale, panel.Y + DialoguePresentation.Padding * scale);
        dialogueCanvas!.PushClip(new(clip.X, clip.Y, clip.Width, clip.Height));
        try
        {
            foreach (DialogueVisualLine line in visual.Lines)
            {
                if (dialogue.ChoiceReady && line.IsChoice && line.SourceLineIndex == dialogue.Selected)
                {
                    dialogueCanvas.MenuPointer(new UiRect(origin.X, origin.Y, DialogueLayout.ChoiceIndent(lineHeight) - DialogueLayout.ChoicePointerGap, lineHeight));
                }
                foreach (DialoguePlacedRun run in line.Runs)
                {
                    string visible = ButtonPrompt.VisibleText(run.Run, ref remaining);
                    if (visible.Length == 0)
                    {
                        continue;
                    }
                    Vector2 position = origin + new Vector2(run.X * scale, 0);
                    if (run.Run.IsButton)
                    {
                        ButtonPrompt.Draw(batch, pixel, dialogueText!, visible, preferences.DialogueFontSize * scale, position, lineHeight, 1, preferences.DialogueFontFamily, clip, dialogueCanvas);
                    }
                    else
                    {
                        dialogueText!.DrawLineClipped(batch, visible, preferences.DialogueFontSize * scale, position, run.Run.Color, clip, preferences.DialogueFontFamily);
                    }
                }
                origin.Y += lineHeight;
            }
        }
        finally
        {
            dialogueCanvas.PopClip();
        }
    }
}
