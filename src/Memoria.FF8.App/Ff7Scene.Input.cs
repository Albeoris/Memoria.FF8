using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Memoria.FF8;

internal sealed partial class Ff7Scene
{
    private readonly Dictionary<(long Generation, int Text), IReadOnlyList<FieldTextToken>> dialogueTokens = new();
    private ushort heldKeys;
    private ushort pendingPressedKeys;
    private int moveHorizontal;
    private int moveVertical;
    private bool runHeld;

    private void UpdateSessionInput()
    {
        Ff7Session session = Session!;
        ushort held = Ff7ControlButtons.FromShared(sharedInput.HeldFieldButtons);
        ushort pressed = (ushort)(held & ~heldKeys);
        heldKeys = held;
        pendingPressedKeys |= pressed;
        moveHorizontal = 0;
        moveVertical = 0;
        Ff7Dialogue? dialogue = session.Host.Process.Dialogues.LastOrDefault(item => !item.Completed && session.Host.CanConfirmDialogue(item.RequestId));
        if (dialogue is not null)
        {
            if (dialogue.ChoiceReady && CanSelectCommonDialogueChoice(dialogue) && dialogue.FirstChoice is int first && dialogue.LastChoice is int last)
            {
                int selected = Math.Clamp(dialogue.Selected, first, last);
                if (semanticInput.MenuVertical < 0)
                {
                    selected = selected <= first ? last : selected - 1;
                }
                if (semanticInput.MenuVertical > 0)
                {
                    selected = selected >= last ? first : selected + 1;
                }
                session.Host.UpdateDialogue(dialogue with { Selected = selected });
                dialogue = session.Host.Process.Dialogues.Single(item => item.RequestId == dialogue.RequestId);
            }
            if (semanticInput.Confirm)
            {
                if (AdvanceCommonDialogue(dialogue))
                {
                    pendingPressedKeys &= unchecked((ushort)~0x20);
                    return;
                }
                if (!dialogue.AwaitingInput)
                {
                    return;
                }
                Ff7Dialogue confirmed = Ff7DialogueCursor.Confirm(dialogue, Tokens(dialogue));
                session.Host.UpdateDialogue(confirmed);
            }
            return;
        }
        if (semanticInput.Confirm)
        {
            session.TryTalk();
        }
        moveHorizontal = Math.Sign(semanticInput.Horizontal);
        moveVertical = Math.Sign(semanticInput.Vertical);
        runHeld = Ff7ControlButtons.Run(sharedInput.HeldFieldButtons);
    }

    private void ApplyLogicalInput()
    {
        Session!.SetInput(heldKeys, pendingPressedKeys);
        pendingPressedKeys = 0;
        Session.MovePlayer(moveHorizontal, moveVertical, runHeld);
    }

    private IReadOnlyList<FieldTextToken> Tokens(Ff7Dialogue dialogue)
    {
        var identity = (Session!.FieldGeneration, dialogue.Text);
        if (!dialogueTokens.TryGetValue(identity, out IReadOnlyList<FieldTextToken>? tokens))
        {
            tokens = FieldText.Read(Session.Field.Scripts.Texts[dialogue.Text].Span);
            dialogueTokens.Add(identity, tokens);
        }
        Ff7DialogueCursor.Validate(dialogue, tokens);
        return tokens;
    }

    private void DrawDialogues(SpriteBatch batch, Rectangle bounds)
    {
        if (Session is null)
        {
            return;
        }
        foreach (Ff7Dialogue dialogue in Session.Host.Process.Dialogues.Where(item => !item.Completed))
        {
            Ff7Window window = Session.Host.Process.Windows.Single(item => item.Id == dialogue.Window);
            if (window.Style is not (0 or 1 or 2))
            {
                throw new NotSupportedException($"FFVII dialogue window style {window.Style} is unsupported.");
            }
            if (DrawSpecialWindow(batch, dialogue, window, bounds))
            {
                continue;
            }
            if (!UseNativeDialogue)
            {
                DrawCommonDialogue(batch, dialogue, window, bounds);
                continue;
            }
            Memoria.FF8.Ui.UiRect animated = Ff7DialoguePresentationPolicy.NativePanel(dialogue, window, bounds);
            Rectangle clip = Ff7DialoguePresentationPolicy.Clip(animated, bounds);
            if (clip.Width == 0 || clip.Height == 0)
            {
                continue;
            }
            if (window.Style == 0)
            {
                DrawWindow(batch, animated, Math.Max(1, bounds.Width / 320));
            }
            if (dialogue.TextVisible)
            {
                dialogueCanvas!.PushClip(new(clip.X, clip.Y, clip.Width, clip.Height));
                try
                {
                    DrawDialogueText(batch, dialogue, window, bounds, clip);
                }
                finally
                {
                    dialogueCanvas.PopClip();
                }
            }
        }
    }

    private void DrawDialogueText(SpriteBatch batch, Ff7Dialogue dialogue, Ff7Window window, Rectangle bounds, Rectangle clip)
    {
        IReadOnlyList<FieldTextToken> tokens = Tokens(dialogue);
        int start = 0;
        int page = 0;
        for (int i = 0; i < tokens.Count && page < dialogue.Page; i++)
        {
            if (tokens[i].Kind is FieldTextTokenKind.NewPage or FieldTextTokenKind.Scroll)
            {
                page++;
                start = i + 1;
            }
        }
        Vector2 scale = new(bounds.Width / 320f, bounds.Height / 240f);
        Vector2 origin = new(bounds.X + (window.X + 8) * scale.X, bounds.Y + (window.Y + 6) * scale.Y);
        Vector2 position = origin;
        int color = 7;
        int row = 0;
        bool fixedSpacing = false;
        int limit = Math.Min(dialogue.TokenPosition + ((dialogue.Presentation?.ExpansionOffset ?? 0) > 0 ? 1 : 0), tokens.Count);
        for (int i = start; i < limit; i++)
        {
            FieldTextToken token = tokens[i];
            switch (token.Kind)
            {
                case FieldTextTokenKind.Glyph:
                    if (token.Code < 224)
                    {
                        position.X += font.DrawGlyph(batch, token.Code, position, scale, color, fixedSpacing, clip);
                    }
                    else
                    {
                        DrawTokenExpansion(token, i);
                    }
                    break;
                case FieldTextTokenKind.NewLine:
                    row++;
                    position.X = origin.X;
                    position.Y += Ff7FontResources.LineHeight * scale.Y;
                    break;
                case FieldTextTokenKind.NewPage:
                case FieldTextTokenKind.Scroll:
                    return;
                case FieldTextTokenKind.ChoiceTab:
                    if (dialogue.ChoiceReady && row == dialogue.Selected)
                    {
                        font.DrawPlain(batch, ">", position, scale, color, clip);
                    }
                    position.X += 10 * 3 * scale.X;
                    break;
                case FieldTextTokenKind.Tab:
                    position.X += 4 * 3 * scale.X;
                    break;
                case FieldTextTokenKind.Color:
                    color = token.Argument;
                    break;
                case FieldTextTokenKind.CharacterName:
                    DrawTokenExpansion(token, i);
                    break;
                case FieldTextTokenKind.ControllerButton:
                    string label = ButtonLabel(token);
                    float lineHeight = Ff7FontResources.LineHeight * scale.Y;
                    string family = dialoguePreferences!().DialogueFontFamily;
                    float size = lineHeight * 0.8f;
                    foreach (DialogueRun run in ButtonPromptText.BindingRuns(label, Color.White))
                    {
                        if (run.IsButton)
                        {
                            ButtonPrompt.Draw(batch, pixel, dialogueText!, run.Text, size, position, lineHeight, 1, family, clip, dialogueCanvas);
                            position.X += ButtonPrompt.Measure(run.Text, value => dialogueText!.MeasureWrapped(value, size, float.PositiveInfinity, family).X, lineHeight);
                        }
                        else
                        {
                            DrawExpansion(run.Text);
                        }
                    }
                    break;
                case FieldTextTokenKind.PartyName:
                    DrawTokenExpansion(token, i);
                    break;
                case FieldTextTokenKind.FixedSpacing:
                    fixedSpacing = !fixedSpacing;
                    break;
                case FieldTextTokenKind.Wait:
                case FieldTextTokenKind.Pause:
                    break;
                default:
                    throw new NotSupportedException($"FFVII dialogue token {token.Kind} requires its native layout or variable binding.");
            }
        }

        void DrawExpansion(string text)
        {
            font.DrawPlain(batch, text, position, scale, color, clip);
            position.X += font.MeasurePlain(text) * scale.X;
        }

        void DrawTokenExpansion(FieldTextToken token, int index)
        {
            string text = Session!.ExpandDialogueToken(token);
            if (index == dialogue.TokenPosition)
            {
                int remaining = dialogue.Presentation?.ExpansionOffset ?? 0;
                text = DialoguePager.VisiblePrefix(text, ref remaining);
            }
            DrawExpansion(text);
        }
    }

}
