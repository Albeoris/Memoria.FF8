using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8;

internal sealed partial class Ff7Scene
{
    private readonly Dictionary<(long Generation, int Text), IReadOnlyList<FieldTextToken>> dialogueTokens = new();
    private ushort heldKeys;
    private ushort pendingPressedKeys;
    private int moveHorizontal;
    private int moveVertical;
    private bool runHeld;

    private void UpdateSessionInput(KeyboardState keys)
    {
        Ff7Session session = Session!;
        ushort held = NativeKeys(keys);
        ushort pressed = (ushort)(held & ~NativeKeys(previousKeys));
        heldKeys = held;
        pendingPressedKeys |= pressed;
        moveHorizontal = 0;
        moveVertical = 0;
        Ff7Dialogue? dialogue = session.Host.Process.Dialogues.LastOrDefault(item => !item.Completed);
        if (dialogue is not null)
        {
            if (dialogue.FirstChoice is int first && dialogue.LastChoice is int last)
            {
                int selected = Math.Clamp(dialogue.Selected, first, last);
                if (Pressed(keys, Keys.Up))
                {
                    selected = selected <= first ? last : selected - 1;
                }
                if (Pressed(keys, Keys.Down))
                {
                    selected = selected >= last ? first : selected + 1;
                }
                session.Host.UpdateDialogue(dialogue.RequestId, dialogue.Page, selected, dialogue.TokenPosition, dialogue.WaitTicks, dialogue.AwaitingInput);
                dialogue = session.Host.Process.Dialogues.Single(item => item.RequestId == dialogue.RequestId);
            }
            if ((Pressed(keys, Keys.Enter) || Pressed(keys, Keys.Z)) && dialogue.AwaitingInput)
            {
                if (AdvanceCommonDialogue(dialogue))
                {
                    return;
                }
                IReadOnlyList<FieldTextToken> tokens = Tokens(dialogue);
                if (dialogue.TokenPosition >= tokens.Count)
                {
                    session.Host.CompleteDialogue(dialogue.RequestId, dialogue.Selected);
                }
                else
                {
                    bool pageBreak = dialogue.TokenPosition > 0 && tokens[dialogue.TokenPosition - 1].Kind == FieldTextTokenKind.NewPage;
                    session.Host.UpdateDialogue(dialogue.RequestId, dialogue.Page + (pageBreak ? 1 : 0), dialogue.Selected, dialogue.TokenPosition, 0, false);
                }
            }
            return;
        }
        if (Pressed(keys, Keys.Enter) || Pressed(keys, Keys.Z))
        {
            session.TryTalk();
        }
        moveHorizontal = (keys.IsKeyDown(Keys.Right) ? 1 : 0) - (keys.IsKeyDown(Keys.Left) ? 1 : 0);
        moveVertical = (keys.IsKeyDown(Keys.Down) ? 1 : 0) - (keys.IsKeyDown(Keys.Up) ? 1 : 0);
        runHeld = keys.IsKeyDown(Keys.LeftShift) || keys.IsKeyDown(Keys.RightShift);
    }

    private void ApplyLogicalInput()
    {
        Session!.SetInput(heldKeys, pendingPressedKeys);
        pendingPressedKeys = 0;
        Session.MovePlayer(moveHorizontal, moveVertical, runHeld);
    }

    private void UpdateDialogueTick()
    {
        foreach (Ff7Dialogue dialogue in Session!.Host.Process.Dialogues.Where(item => !item.Completed).ToArray())
        {
            if (dialogue.AwaitingInput)
            {
                continue;
            }
            if (dialogue.WaitTicks > 0)
            {
                Session.Host.UpdateDialogue(dialogue.RequestId, dialogue.Page, dialogue.Selected, dialogue.TokenPosition, dialogue.WaitTicks - 1, false);
                continue;
            }
            IReadOnlyList<FieldTextToken> tokens = Tokens(dialogue);
            int position = dialogue.TokenPosition;
            int wait = 0;
            bool awaiting = false;
            // TODO(TD-021): The explicit instant-text debug policy skips native glyph reveal timing while retaining original controls and metrics.
            while (position < tokens.Count)
            {
                FieldTextToken token = tokens[position++];
                if (token.Kind == FieldTextTokenKind.Wait)
                {
                    wait = token.Argument;
                    if (wait > 0)
                    {
                        break;
                    }
                    continue;
                }
                if (token.Kind == FieldTextTokenKind.Scroll)
                {
                    throw new NotSupportedException("FFVII scrolling text requires its original scrolling continuation contract.");
                }
                if (token.Kind is FieldTextTokenKind.Pause or FieldTextTokenKind.NewPage)
                {
                    awaiting = true;
                    break;
                }
            }
            int selected = dialogue.FirstChoice is int first && dialogue.LastChoice is int last ? Math.Clamp(dialogue.Selected, first, last) : dialogue.Selected;
            Session.Host.UpdateDialogue(dialogue.RequestId, dialogue.Page, selected, position, wait, awaiting || wait == 0 && position == tokens.Count);
        }
    }

    private IReadOnlyList<FieldTextToken> Tokens(Ff7Dialogue dialogue)
    {
        var identity = (Session!.FieldGeneration, dialogue.Text);
        if (!dialogueTokens.TryGetValue(identity, out IReadOnlyList<FieldTextToken>? tokens))
        {
            tokens = FieldText.Read(Session.Field.Scripts.Texts[dialogue.Text].Span);
            dialogueTokens.Add(identity, tokens);
        }
        if (dialogue.TokenPosition > tokens.Count || dialogue.Page < 0 || dialogue.Page > tokens.Count(token => token.Kind == FieldTextTokenKind.NewPage) || dialogue.WaitTicks is < 0 or > 65535 || dialogue.AwaitingInput && dialogue.WaitTicks > 0)
        {
            throw new InvalidDataException("FFVII saved dialogue cursor, page or wait state exceeds its original text.");
        }
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
            if (!UseNativeDialogue)
            {
                DrawCommonDialogue(batch, dialogue, window, bounds);
                continue;
            }
            if (window.Style == 0)
            {
                DrawWindow(batch, new Rectangle(window.X, window.Y, window.Width, window.Height), bounds);
            }
            DrawDialogueText(batch, dialogue, window, bounds);
        }
    }

    private void DrawDialogueText(SpriteBatch batch, Ff7Dialogue dialogue, Ff7Window window, Rectangle bounds)
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
        Vector2 origin = new(bounds.X + (window.X + 8) * scale.X, bounds.Y + (window.Y + 8) * scale.Y);
        Vector2 position = origin;
        int color = 7;
        int row = 0;
        bool fixedSpacing = false;
        int limit = Math.Min(dialogue.TokenPosition, tokens.Count);
        for (int i = start; i < limit; i++)
        {
            FieldTextToken token = tokens[i];
            switch (token.Kind)
            {
                case FieldTextTokenKind.Glyph:
                    if (token.Code < 224)
                    {
                        position.X += font.DrawGlyph(batch, token.Code, position, scale, color, fixedSpacing);
                    }
                    else
                    {
                        DrawExpansion(token.Text);
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
                    if (dialogue.FirstChoice is not null && row == dialogue.Selected)
                    {
                        font.DrawPlain(batch, ">", position, scale, color);
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
                    DrawExpansion(Session!.Progress.Characters[token.Argument].Name);
                    break;
                case FieldTextTokenKind.PartyName:
                    int character = Session!.Progress.Party[token.Argument];
                    if (character is < 0 or >= 9)
                    {
                        throw new InvalidDataException("FFVII text references an empty party name.");
                    }
                    DrawExpansion(Session.Progress.Characters[character].Name);
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
            font.DrawPlain(batch, text, position, scale, color);
            position.X += font.MeasurePlain(text) * scale.X;
        }
    }

    private static ushort NativeKeys(KeyboardState keys)
    {
        ushort mask = 0;
        if (keys.IsKeyDown(Keys.Up))
        {
            mask |= 0x1000;
        }
        if (keys.IsKeyDown(Keys.Right))
        {
            mask |= 0x2000;
        }
        if (keys.IsKeyDown(Keys.Down))
        {
            mask |= 0x4000;
        }
        if (keys.IsKeyDown(Keys.Left))
        {
            mask |= 0x8000;
        }
        if (keys.IsKeyDown(Keys.Z) || keys.IsKeyDown(Keys.Enter))
        {
            mask |= 0x0020;
        }
        if (keys.IsKeyDown(Keys.X) || keys.IsKeyDown(Keys.Escape))
        {
            mask |= 0x0040;
        }
        if (keys.IsKeyDown(Keys.LeftShift) || keys.IsKeyDown(Keys.RightShift))
        {
            mask |= 0x0040;
        }
        return mask;
    }
}
