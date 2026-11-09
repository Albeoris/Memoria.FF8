using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7Session
{
    private readonly Dictionary<(long Generation, int Text), IReadOnlyList<FieldTextToken>> dialogueTokens = [];

    private void AdvanceDialogues()
    {
        foreach (Ff7Dialogue dialogue in Host.Process.Dialogues.Where(item => !item.Completed).ToArray())
        {
            if ((uint)dialogue.Text >= Field.Scripts.Texts.Count)
            {
                throw new InvalidDataException("FFVII dialogue continuation references an absent native text.");
            }
            var identity = (FieldGeneration, dialogue.Text);
            if (!dialogueTokens.TryGetValue(identity, out IReadOnlyList<FieldTextToken>? tokens))
            {
                tokens = FieldText.Read(Field.Scripts.Texts[dialogue.Text].Span);
                dialogueTokens.Add(identity, tokens);
            }
            Ff7Dialogue next = Ff7DialogueCursor.AdvanceTick(dialogue, tokens);
            Host.UpdateDialogue(next.RequestId, next.Page, next.Selected, next.TokenPosition, next.WaitTicks, next.AwaitingInput);
        }
    }
}
