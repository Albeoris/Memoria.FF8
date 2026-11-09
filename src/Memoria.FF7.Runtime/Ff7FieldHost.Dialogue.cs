using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7FieldHost
{
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
