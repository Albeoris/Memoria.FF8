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
        return !UseNativeDialogue && commonDialoguePages.TryGetValue((Session!.FieldGeneration, dialogue.RequestId), out var presentation) && presentation.NativePage == dialogue.Page && presentation.Pager.Advance(presentation.Pages.Count);
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
        int page = 0;
        int row = 0;
        List<DialogueLine> lines = [];
        List<DialogueRun> runs = [];
        Color color = Color.White;
        void Add(string value)
        {
            if (value.Length > 0)
            {
                if (runs.LastOrDefault() is DialogueRun previous && previous.Color == color)
                {
                    runs[^1] = previous with { Text = previous.Text + value };
                }
                else
                {
                    runs.Add(new DialogueRun(value, color));
                }
            }
        }
        for (int index = 0; index < Math.Min(tokens.Count, dialogue.TokenPosition); index++)
        {
            FieldTextToken token = tokens[index];
            if (token.Kind == FieldTextTokenKind.NewPage)
            {
                page++;
                row = 0;
                continue;
            }
            if (page != dialogue.Page)
            {
                continue;
            }
            switch (token.Kind)
            {
                case FieldTextTokenKind.Glyph:
                    Add(token.Text);
                    break;
                case FieldTextTokenKind.NewLine:
                    lines.Add(new DialogueLine(runs.ToArray(), row++));
                    runs.Clear();
                    break;
                case FieldTextTokenKind.CharacterName:
                    Add(Session!.Progress.Characters[token.Argument].Name);
                    break;
                case FieldTextTokenKind.PartyName:
                    int character = Session!.Progress.Party[token.Argument];
                    if (character is < 0 or >= 9)
                    {
                        throw new InvalidDataException("FFVII text references an empty party name.");
                    }
                    Add(Session.Progress.Characters[character].Name);
                    break;
                case FieldTextTokenKind.Color:
                    color = token.Argument switch
                    {
                        0 => Color.Gray,
                        1 => Color.CornflowerBlue,
                        2 => Color.Red,
                        3 => Color.Violet,
                        4 => Color.LimeGreen,
                        5 => Color.Cyan,
                        6 => Color.Yellow,
                        7 => Color.White,
                        _ => throw new InvalidDataException("FFVII dialogue color exceeds its native palette.")
                    };
                    break;
                case FieldTextTokenKind.Tab:
                    Add("    ");
                    break;
                case FieldTextTokenKind.ChoiceTab:
                case FieldTextTokenKind.FixedSpacing:
                case FieldTextTokenKind.Wait:
                case FieldTextTokenKind.Pause:
                    break;
                default:
                    throw new NotSupportedException($"FFVII dialogue token {token.Kind} requires its native layout or variable binding.");
            }
        }
        lines.Add(new DialogueLine(runs.ToArray(), row));
        DialogueDocument document = new([new DialoguePage(lines)]);
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
        presentation.Pager.RevealCurrentPage(visual);
        float scale = bounds.Width / 320f;
        PixelBounds panel = DialoguePresentation.Bounds(state, nativeWindow, new PixelBounds(bounds.X, bounds.Y, bounds.Width, bounds.Height), visual.Width * scale, visual.Height * scale);
        if (window.Style == 0)
        {
            UiTheme theme = dialogueTheme?.Invoke() ?? (preferences.MenuPresentation == MenuPresentation.Vector
                ? new VectorUiTheme { FontFamily = preferences.UiFontFamily }
                : new UiTheme { FontFamily = preferences.UiFontFamily });
            theme.Panel(dialogueCanvas!, new UiRect(panel.X, panel.Y, panel.Width, panel.Height));
        }
        float lineHeight = measurer.LineHeight * scale;
        Vector2 origin = new(panel.X + DialoguePresentation.Padding * scale, panel.Y + DialoguePresentation.Padding * scale);
        foreach (DialogueVisualLine line in visual.Lines)
        {
            if (line.IsChoice && line.SourceLineIndex == dialogue.Selected)
            {
                dialogueCanvas!.MenuPointer(new UiRect(origin.X, origin.Y, DialogueLayout.ChoiceIndent(lineHeight) - DialogueLayout.ChoicePointerGap, lineHeight));
            }
            foreach (DialoguePlacedRun run in line.Runs)
            {
                dialogueText!.DrawLine(batch, run.Run.Text, preferences.DialogueFontSize * scale, origin + new Vector2(run.X * scale, 0), run.Run.Color, preferences.DialogueFontFamily);
            }
            origin.Y += lineHeight;
        }
    }
}
