using Memoria.FF7.Events;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7Session
{
    public Func<byte, byte>? DialogueMessageSpeed { get; set; }
    public Func<int, string, string>? PresentationNameResolver { get; set; }

    public string ExpandDialogueToken(FieldTextToken token)
    {
        int[] scriptParty = Enumerable.Range(9, 3).Select(address => (int)(World?.ReadPersistentByte(256 + address) ?? Machine.Variables.ReadByte(FieldBank.Persistent1Byte, (byte)address))).ToArray();
        return Ff7DialogueCursor.ExpandToken(token, Progress, scriptParty, PresentationNameResolver);
    }

    public string ExpandDialogueToken(Ff7Dialogue dialogue, FieldTextToken token)
    {
        if (!Ff7DialogueCursor.IsNumeric(token))
        {
            return ExpandDialogueToken(token);
        }
        if (World is not null)
        {
            throw new NotSupportedException("FFVII world numeric messages require their native parameter publication contract.");
        }
        IReadOnlyList<FieldTextToken> tokens = FieldText.Read(Field.Scripts.Texts[dialogue.Text].Span);
        int slot = Ff7DialogueCursor.NumericParameterIndex(tokens, token);
        if (dialogue.Numbers?.Get(slot) is string cached)
        {
            return cached;
        }
        Ff7Window window = Host.Process.Windows.Single(item => item.Id == dialogue.Window);
        Ff7WindowParameter parameter = (window.Parameters ?? new()).Get(slot);
        ushort value = Machine.Variables.ReadWindowParameter(parameter.Bank, parameter.Address);
        return Ff7DialogueCursor.FormatNumber(token.Kind, value);
    }

    private void ConfigureWorldPresentation()
    {
        if (World is not null)
        {
            World.ConfigureMovementSpeed(worldMovementSpeedPercent);
            World.NativeMessageSpeed = DialogueMessageSpeed?.Invoke(Progress.NativeMessageSpeed) ?? Progress.NativeMessageSpeed;
            World.ExpandDialogueToken = ExpandDialogueToken;
            World.AudioRequested = Host.SubmitNativeAudio;
            World.ResetSoundPitchesRequested = Host.ResetNativeSoundPitches;
        }
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
        return Ff7DialogueCursor.AdvanceTick(dialogue, tokens, window, speed, (Machine.Input.Held & 0x20) != 0, expandToken: token => ExpandDialogueToken(dialogue, token));
    }
}
