using Memoria.FF7.Events;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7Session
{
    public Func<byte, byte>? DialogueMessageSpeed { get; set; }

    public string ExpandDialogueToken(FieldTextToken token)
    {
        int[] scriptParty = Enumerable.Range(9, 3).Select(address => (int)Machine.Variables.ReadByte(FieldBank.Persistent1Byte, (byte)address)).ToArray();
        return Ff7DialogueCursor.ExpandToken(token, Progress, scriptParty);
    }
    private readonly Dictionary<(long Generation, int Text), IReadOnlyList<FieldTextToken>> dialogueTokens = [];

    private Ff7Dialogue AdvanceDialogue(Ff7Dialogue dialogue, Ff7Window window)
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
        byte speed = DialogueMessageSpeed?.Invoke(Progress.NativeMessageSpeed) ?? Progress.NativeMessageSpeed;
        return Ff7DialogueCursor.AdvanceTick(dialogue, tokens, window, speed, (Machine.Input.Held & 0x20) != 0, expandToken: ExpandDialogueToken);
    }
}
