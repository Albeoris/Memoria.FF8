using System.Text;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

public static class Ff7DialogueCursor
{
    public static string ExpandToken(FieldTextToken token, Ff7Progress progress, IReadOnlyList<int> scriptParty)
    {
        if (token.Kind == FieldTextTokenKind.Glyph)
        {
            return token.Text;
        }
        int character = token.Kind switch
        {
            FieldTextTokenKind.CharacterName => token.Argument,
            FieldTextTokenKind.PartyName when (uint)token.Argument < scriptParty.Count => scriptParty[token.Argument],
            _ => throw new NotSupportedException($"FFVII token {token.Kind} has no admitted native text expansion.")
        };
        string name = progress.Characters.SingleOrDefault(item => item.Id == character)?.Name ?? throw new InvalidDataException("FFVII text references an absent native character name.");
        return string.Concat(name.EnumerateRunes().Take(9));
    }

    public static Ff7DialogueBounds WindowBounds(Ff7Dialogue dialogue, Ff7Window window)
    {
        int width = Math.Max(0, dialogue.Presentation?.AnimatedWidth ?? window.Width);
        int height = Math.Max(0, dialogue.Presentation?.AnimatedHeight ?? window.Height);
        return new(window.X + (window.Width - width) / 2f, window.Y + (window.Height - height) / 2f, width, height);
    }

    public static Ff7Dialogue RestorePresentation(Ff7Dialogue dialogue, IReadOnlyList<FieldTextToken> tokens, Ff7Window window)
    {
        Validate(dialogue, tokens);
        return dialogue.Completed || dialogue.Presentation is not null ? dialogue : dialogue with { Presentation = LegacyPresentation(dialogue, tokens, window.Width, window.Height) };
    }

    public static Ff7Dialogue Select(Ff7Dialogue dialogue, int selected)
    {
        if (dialogue.FirstChoice is not int first || dialogue.LastChoice is not int last || selected < first || selected > last)
        {
            throw new ArgumentOutOfRangeException(nameof(selected), "The selected reply is outside this native question's choices.");
        }
        return dialogue with { Selected = selected };
    }

    public static Ff7Dialogue AdvanceTick(Ff7Dialogue dialogue, IReadOnlyList<FieldTextToken> tokens, Ff7Window? window = null, byte messageSpeed = 128, bool heldConfirm = false, bool pressedConfirm = false, Func<FieldTextToken, string>? expandToken = null)
    {
        Validate(dialogue, tokens);
        if (dialogue.Completed)
        {
            return dialogue;
        }
        window ??= new(dialogue.Window, 0, 0, 304, 72);
        if (window.Width is < 8 or > short.MaxValue || window.Height is < 8 or > short.MaxValue)
        {
            throw new NotSupportedException("FFVII message animation requires supported positive native window dimensions.");
        }
        Ff7DialoguePresentation presentation = dialogue.Presentation ?? LegacyPresentation(dialogue, tokens, window.Width, window.Height);
        if (presentation.NativeState >= 2 && presentation.AnimatedWidth == 0 && presentation.AnimatedHeight == 0)
        {
            presentation = presentation with { AnimatedWidth = window.Width, AnimatedHeight = window.Height };
        }
        bool confirm = pressedConfirm || presentation.ConfirmPending;
        presentation = presentation with { ConfirmPending = false };
        dialogue = dialogue with { Presentation = presentation };
        switch (presentation.NativeState)
        {
            case 0:
                return dialogue with { Presentation = new(1, Math.Max(8, window.Width / 4), Math.Max(8, window.Height / 4)), AwaitingInput = false };
            case 1:
                int width = Math.Clamp(unchecked((short)(presentation.AnimatedWidth + window.Width / 4)), 8, window.Width);
                int height = Math.Clamp(unchecked((short)(presentation.AnimatedHeight + window.Height / 4)), 8, window.Height);
                return dialogue with { Presentation = presentation with { NativeState = width == window.Width && height == window.Height ? 2 : 1, AnimatedWidth = width, AnimatedHeight = height }, AwaitingInput = false };
            case 2:
                return Reveal(dialogue, tokens, window, messageSpeed, heldConfirm, expandToken);
            case 3:
                return dialogue.WaitTicks == 0
                    ? dialogue with { Presentation = presentation with { NativeState = 2 } }
                    : dialogue with { WaitTicks = dialogue.WaitTicks - 1 };
            case 6:
                if (!confirm || (window.Flags & 1) != 0)
                {
                    return dialogue;
                }
                return Close(dialogue with { AwaitingInput = false, Presentation = presentation with { NativeState = 7 } }, window, acknowledgeCompletion: false);
            case 5:
            case 7:
                return Close(dialogue, window, acknowledgeCompletion: true);
            case 13:
                return confirm ? dialogue with { AwaitingInput = false, Presentation = presentation with { NativeState = 2 } } : dialogue;
            case 14:
                return confirm ? dialogue with { Page = dialogue.Page + 1, AwaitingInput = false, Presentation = presentation with { NativeState = 2, FastCounter = 1, Row = 0 } } : dialogue;
            default:
                throw new NotSupportedException($"FFVII message phase {presentation.NativeState} requires its native continuation contract.");
        }
    }

    private static Ff7Dialogue Reveal(Ff7Dialogue dialogue, IReadOnlyList<FieldTextToken> tokens, Ff7Window window, byte messageSpeed, bool heldConfirm, Func<FieldTextToken, string>? expandToken)
    {
        Ff7DialoguePresentation presentation = dialogue.Presentation!;
        int interval;
        int increment;
        if ((window.Flags & 2) != 0)
        {
            increment = 256;
            interval = 1;
        }
        else
        {
            int fast = heldConfirm ? Math.Min(128, presentation.FastCounter + 1) : Math.Max(1, presentation.FastCounter - 1);
            presentation = presentation with { FastCounter = (short)fast };
            increment = messageSpeed < 128 ? ((128 - messageSpeed) >> 5) + 2 : 2;
            interval = messageSpeed < 128 ? 1 : ((messageSpeed - 128) >> 5) + 1;
            increment += (fast >> 4) * interval;
        }
        short budget = unchecked((short)(presentation.RevealBudget + increment));
        int position = dialogue.TokenPosition;
        int expansion = presentation.ExpansionOffset;
        int row = presentation.Row;
        while (budget > interval)
        {
            if (position == tokens.Count)
            {
                return dialogue with { TokenPosition = position, AwaitingInput = true, Presentation = presentation with { NativeState = 6, RevealBudget = 0, ExpansionOffset = 0, Row = row } };
            }
            FieldTextToken token = tokens[position];
            switch (token.Kind)
            {
                case FieldTextTokenKind.Glyph when token.Code is 0xE2 or 0xE3 or 0xE4:
                case FieldTextTokenKind.Tab:
                case FieldTextTokenKind.ChoiceTab:
                case FieldTextTokenKind.Color:
                case FieldTextTokenKind.FixedSpacing:
                    position++;
                    break;
                case FieldTextTokenKind.Glyph:
                case FieldTextTokenKind.ControllerButton:
                    position++;
                    budget = unchecked((short)(budget - interval));
                    break;
                case FieldTextTokenKind.CharacterName:
                case FieldTextTokenKind.PartyName:
                    string text = expandToken?.Invoke(token) ?? throw new NotSupportedException("FFVII name reveal requires its native character and script-party bindings.");
                    int length = text.EnumerateRunes().Count();
                    if (expansion >= length)
                    {
                        expansion = 0;
                        position++;
                    }
                    else
                    {
                        expansion++;
                        budget = unchecked((short)(budget - interval));
                    }
                    break;
                case FieldTextTokenKind.NewLine:
                    if (row == (window.Height - 9) / 16 - 1)
                    {
                        throw new NotSupportedException("FFVII message row overflow requires its native scrolling continuation contract.");
                    }
                    row++;
                    position++;
                    break;
                case FieldTextTokenKind.Wait:
                    return dialogue with { TokenPosition = position + 1, WaitTicks = token.Argument, Presentation = presentation with { NativeState = 3, RevealBudget = budget, ExpansionOffset = expansion, Row = row } };
                case FieldTextTokenKind.Pause:
                case FieldTextTokenKind.NewPage:
                    return dialogue with { TokenPosition = position + 1, AwaitingInput = true, Presentation = presentation with { NativeState = token.Kind == FieldTextTokenKind.Pause ? 13 : 14, RevealBudget = 0, FastCounter = 1, ExpansionOffset = expansion, Row = row } };
                default:
                    throw new NotSupportedException($"FFVII dialogue token {token.Kind} requires its native reveal continuation contract.");
            }
        }
        return dialogue with { TokenPosition = position, Presentation = presentation with { RevealBudget = budget, ExpansionOffset = expansion, Row = row } };
    }

    private static Ff7Dialogue Close(Ff7Dialogue dialogue, Ff7Window window, bool acknowledgeCompletion)
    {
        Ff7DialoguePresentation presentation = dialogue.Presentation!;
        int width = presentation.AnimatedWidth < 8 ? 8 : unchecked((short)(presentation.AnimatedWidth - window.Width / 4));
        int height = presentation.AnimatedHeight < 8 ? 8 : unchecked((short)(presentation.AnimatedHeight - window.Height / 4));
        bool complete = width <= 8 && height <= 8;
        if (complete && !acknowledgeCompletion)
        {
            throw new NotSupportedException("FFVII message dimensions complete closing during the initial confirmation callback.");
        }
        return dialogue with { Completed = complete, Presentation = presentation with { NativeState = complete ? 0 : 7, AnimatedWidth = width, AnimatedHeight = height } };
    }

    private static Ff7DialoguePresentation LegacyPresentation(Ff7Dialogue dialogue, IReadOnlyList<FieldTextToken> tokens, int width, int height)
    {
        int state = dialogue.WaitTicks > 0 ? 3 : 2;
        if (dialogue.AwaitingInput)
        {
            state = dialogue.TokenPosition > 0 && tokens[dialogue.TokenPosition - 1].Kind == FieldTextTokenKind.NewPage ? 14
                : dialogue.TokenPosition > 0 && tokens[dialogue.TokenPosition - 1].Kind == FieldTextTokenKind.Pause ? 13 : 6;
        }
        int row = 0;
        foreach (FieldTextToken token in tokens.Take(dialogue.TokenPosition))
        {
            if (token.Kind == FieldTextTokenKind.NewPage)
            {
                row = 0;
            }
            else if (token.Kind == FieldTextTokenKind.NewLine)
            {
                row++;
            }
        }
        return new(state, width, height, Row: row);
    }

    public static Ff7Dialogue Confirm(Ff7Dialogue dialogue, IReadOnlyList<FieldTextToken> tokens)
    {
        Validate(dialogue, tokens);
        if (dialogue.Completed || !dialogue.AwaitingInput)
        {
            return dialogue;
        }
        Ff7DialoguePresentation presentation = dialogue.Presentation ?? LegacyPresentation(dialogue, tokens, 0, 0);
        return dialogue with { Presentation = presentation with { ConfirmPending = true } };
    }

    public static void Validate(Ff7Dialogue dialogue, IReadOnlyList<FieldTextToken> tokens)
    {
        if (dialogue.TokenPosition < 0 || dialogue.TokenPosition > tokens.Count || dialogue.Page < 0 || dialogue.Page > tokens.Count(token => token.Kind == FieldTextTokenKind.NewPage) || dialogue.WaitTicks is < 0 or > 65535 || dialogue.AwaitingInput && dialogue.WaitTicks > 0)
        {
            throw new InvalidDataException("FFVII saved dialogue cursor, page or wait state exceeds its original text.");
        }
        dialogue.Presentation?.Validate();
    }
}
