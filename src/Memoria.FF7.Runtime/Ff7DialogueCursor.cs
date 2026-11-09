using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

public static class Ff7DialogueCursor
{
    public static Ff7Dialogue AdvanceTick(Ff7Dialogue dialogue, IReadOnlyList<FieldTextToken> tokens)
    {
        Validate(dialogue, tokens);
        if (dialogue.Completed || dialogue.AwaitingInput)
        {
            return dialogue;
        }
        if (dialogue.WaitTicks > 0)
        {
            return dialogue with { WaitTicks = dialogue.WaitTicks - 1 };
        }
        int position = dialogue.TokenPosition;
        int wait = 0;
        bool awaiting = false;
        // TODO(TD-021): The explicit instant-text debug policy retains original controls while skipping glyph reveal timing.
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
        return dialogue with { Selected = selected, TokenPosition = position, WaitTicks = wait, AwaitingInput = awaiting || wait == 0 && position == tokens.Count };
    }

    public static Ff7Dialogue Confirm(Ff7Dialogue dialogue, IReadOnlyList<FieldTextToken> tokens)
    {
        Validate(dialogue, tokens);
        if (dialogue.Completed || !dialogue.AwaitingInput)
        {
            return dialogue;
        }
        if (dialogue.TokenPosition >= tokens.Count)
        {
            return dialogue with { Completed = true };
        }
        bool pageBreak = dialogue.TokenPosition > 0 && tokens[dialogue.TokenPosition - 1].Kind == FieldTextTokenKind.NewPage;
        return dialogue with { Page = dialogue.Page + (pageBreak ? 1 : 0), WaitTicks = 0, AwaitingInput = false };
    }

    public static void Validate(Ff7Dialogue dialogue, IReadOnlyList<FieldTextToken> tokens)
    {
        if (dialogue.TokenPosition < 0 || dialogue.TokenPosition > tokens.Count || dialogue.Page < 0 || dialogue.Page > tokens.Count(token => token.Kind == FieldTextTokenKind.NewPage) || dialogue.WaitTicks is < 0 or > 65535 || dialogue.AwaitingInput && dialogue.WaitTicks > 0)
        {
            throw new InvalidDataException("FFVII saved dialogue cursor, page or wait state exceeds its original text.");
        }
    }
}
