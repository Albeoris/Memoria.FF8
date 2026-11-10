using System.Buffers.Binary;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7WorldRuntime
{
    private Ff7Window? window;
    private Ff7Dialogue? dialogue;
    private short lastSelection;

    public byte NativeMessageSpeed { get; set; } = 128;
    public bool HeldConfirm { get; set; }
    public Func<FieldTextToken, string>? ExpandDialogueToken { get; set; }

    private static IReadOnlyList<ReadOnlyMemory<byte>> ReadTexts(byte[] data)
    {
        if (data.Length < 2)
        {
            throw new InvalidDataException("FFVII world text table is truncated.");
        }
        int count = BinaryPrimitives.ReadUInt16LittleEndian(data);
        if (count > (data.Length - 2) / 2)
        {
            throw new InvalidDataException("FFVII world text count exceeds its table.");
        }
        ReadOnlyMemory<byte>[] texts = new ReadOnlyMemory<byte>[count];
        for (int i = 0; i < count; i++)
        {
            int offset = BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(2 + i * 2));
            if (offset < 2 + count * 2 || offset >= data.Length)
            {
                throw new InvalidDataException("FFVII world text offset is outside its string data.");
            }
            int length = Array.IndexOf(data, (byte)255, offset) - offset;
            if (length < 0)
            {
                throw new InvalidDataException("FFVII world text is not terminated.");
            }
            texts[i] = data.AsMemory(offset, length + 1);
        }
        return texts;
    }

    private WorldHostReply? ExecuteDialogue(WorldHostRequest request)
    {
        int[] values = request.Command.Arguments;
        switch (request.Command.Operation)
        {
            case WorldOperation.Window:
                window = (window ?? new(0, 0, 0, 8, 8)) with { X = unchecked((short)values[0]), Y = unchecked((short)values[1]), Width = unchecked((short)values[2]), Height = unchecked((short)values[3]) };
                return new(true);
            case WorldOperation.WindowAttributes:
                if (window is null)
                {
                    throw new InvalidDataException("FFVII world dialogue attributes require a configured native window.");
                }
                window = window with { Style = values[0], Flags = values[1] };
                return new(true);
            case WorldOperation.Text:
            case WorldOperation.Choice:
                if (dialogue is not { Completed: false })
                {
                    if (window is null || (uint)values[0] >= Texts.Count)
                    {
                        throw new InvalidDataException("FFVII world dialogue requires a valid window and text index.");
                    }
                    int? first = values.Length == 3 ? unchecked((short)values[1]) : null;
                    int? last = values.Length == 3 ? unchecked((short)values[2]) : null;
                    dialogue = new(request.Id, 0, values[0], first, last, first ?? 0, Presentation: new(0, 0, 0));
                    AdvanceWorldDialogue();
                }
                return new(true);
            case WorldOperation.CloseWindow:
                if (dialogue is { Completed: false, Presentation: { NativeState: not 7 } presentation })
                {
                    dialogue = dialogue with { AwaitingInput = false, Presentation = presentation with { NativeState = 7, ConfirmPending = false } };
                }
                return new(dialogue is not { Completed: false });
            case WorldOperation.WaitWindow:
                return new(dialogue is not { Completed: false });
            default:
                return null;
        }
    }

    private void AdvanceWorldDialogue()
    {
        if (dialogue is not { Completed: false })
        {
            return;
        }
        dialogue = Ff7DialogueCursor.AdvanceTick(dialogue, FieldText.Read(Texts[dialogue.Text].Span), window, NativeMessageSpeed, HeldConfirm, expandToken: ExpandDialogueToken);
        if (dialogue.Completed)
        {
            lastSelection = unchecked((short)dialogue.Selected);
        }
    }

    public bool CanConfirmDialogue(long requestId)
    {
        return dialogue is { Completed: false } && dialogue.RequestId == requestId && (dialogue.Presentation?.NativeState is 13 or 14 || (window!.Flags & 1) == 0);
    }

    public void CompleteDialogue(long requestId, int selected)
    {
        if (!CanConfirmDialogue(requestId) || !dialogue!.AwaitingInput)
        {
            return;
        }
        Ff7Dialogue updated = dialogue;
        if (updated.FirstChoice is int)
        {
            updated = Ff7DialogueCursor.Select(updated, selected);
        }
        dialogue = Ff7DialogueCursor.Confirm(updated, FieldText.Read(Texts[updated.Text].Span));
    }

    private void RestoreDialogue(Ff7WorldProcess state)
    {
        if (state.Dialogue is Ff7Dialogue saved)
        {
            if (state.Window is null || (uint)saved.Text >= Texts.Count || saved.Window != 0 || saved.RequestId <= 0 || saved.RequestId >= state.Vm.NextRequestId)
            {
                throw new InvalidDataException("FFVII world dialogue snapshot has an invalid window, text or request owner.");
            }
            Ff7DialogueCursor.Validate(saved, FieldText.Read(Texts[saved.Text].Span));
        }
        window = state.Window;
        dialogue = state.Dialogue;
        lastSelection = state.LastSelection;
    }
}
