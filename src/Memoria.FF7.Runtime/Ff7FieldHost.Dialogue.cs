using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7FieldHost
{
    public void RestoreDialoguePresentation()
    {
        bool inferred = false;
        foreach (Ff7Dialogue dialogue in Process.Dialogues.Where(item => !item.Completed && item.Presentation is null).ToArray())
        {
            IReadOnlyList<FieldTextToken> tokens = FieldText.Read(field.Scripts.Texts[dialogue.Text].Span);
            Ff7Window window = Process.Windows.Single(item => item.Id == dialogue.Window);
            UpdateDialogue(Ff7DialogueCursor.RestorePresentation(dialogue, tokens, window));
            inferred = true;
        }
        if (inferred)
        {
            Console.Error.WriteLine("FFVII compatibility: legacy message animation was inferred from saved text, page and wait progress; script PCs and completed choices were preserved.");
        }
    }

    public void UpdateDialogue(Ff7Dialogue updated)
    {
        ArgumentNullException.ThrowIfNull(updated);
        Ff7Dialogue original = Process.Dialogues.Single(item => item.RequestId == updated.RequestId);
        if (original.Completed || original.Window != updated.Window || original.Text != updated.Text || original.FirstChoice != updated.FirstChoice || original.LastChoice != updated.LastChoice)
        {
            throw new InvalidDataException("FFVII dialogue update changed its native request ownership.");
        }
        IReadOnlyList<FieldTextToken> tokens = FieldText.Read(field.Scripts.Texts[updated.Text].Span);
        Ff7DialogueCursor.Validate(updated, tokens);
        if (updated.Selected is < 0 or > 255 || (updated.Completed || updated.Presentation?.NativeState == 7) && updated.FirstChoice is int first && updated.LastChoice is int last && (updated.Selected < first || updated.Selected > last))
        {
            throw new InvalidDataException("FFVII dialogue selection is outside its native question bounds.");
        }
        Process = Process with { Dialogues = Process.Dialogues.Select(item => item.RequestId == updated.RequestId ? updated : item).ToArray() };
    }

    public void UpdateDialogue(long requestId, int page, int selected, int tokenPosition, int waitTicks, bool awaitingInput)
    {
        Ff7Dialogue dialogue = Process.Dialogues.Single(item => item.RequestId == requestId);
        IReadOnlyList<FieldTextToken> tokens = FieldText.Read(field.Scripts.Texts[dialogue.Text].Span);
        int pages = tokens.Count(token => token.Kind == FieldTextTokenKind.NewPage) + 1;
        if (dialogue.Completed || page < 0 || page >= pages || tokenPosition < 0 || tokenPosition > tokens.Count || waitTicks is < 0 or > 65535 || awaitingInput && waitTicks > 0 || dialogue.FirstChoice is int first && dialogue.LastChoice is int last && (selected < first || selected > last))
        {
            throw new InvalidDataException("FFVII dialogue presentation continuation is invalid.");
        }
        Process = Process with { Dialogues = Process.Dialogues.Select(item => item.RequestId == requestId ? item with { Page = page, Selected = selected, TokenPosition = tokenPosition, WaitTicks = waitTicks, AwaitingInput = awaitingInput } : item).ToArray() };
    }
}
