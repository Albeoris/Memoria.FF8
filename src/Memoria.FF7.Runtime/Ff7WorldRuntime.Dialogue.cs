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
                int width = unchecked((short)values[2]);
                int height = unchecked((short)values[3]);
                int x = Math.Max(8, (int)unchecked((short)values[0]));
                int y = Math.Max(8, (int)unchecked((short)values[1]));
                if (x + width > 312)
                {
                    x = 312 - width;
                }
                if (y + height > 224)
                {
                    y = 224 - height;
                }
                window = (window ?? new(0, 0, 0, 8, 8)) with { X = x, Y = y, Width = width, Height = height };
                return new(true);
            case WorldOperation.WindowAttributes:
                if (window is null)
                {
                    throw new InvalidDataException("FFVII world dialogue attributes require a configured native window.");
                }
                window = window with { Style = unchecked((sbyte)values[0]), Flags = unchecked((short)values[1]) };
                return new(true);
            case WorldOperation.Text:
            case WorldOperation.Choice:
                if (dialogue is not { Completed: false })
                {
                    int text = unchecked((byte)values[0]);
                    if (window is null || (uint)text >= Texts.Count)
                    {
                        throw new InvalidDataException("FFVII world dialogue requires a valid window and text index.");
                    }
                    int? first = values.Length == 3 ? unchecked((byte)values[1]) : null;
                    int? last = values.Length == 3 ? unchecked((byte)values[2]) : null;
                    dialogue = new(request.Id, 0, text, first, last, lastSelection, Presentation: new(0, 0, 0));
                    AdvanceWorldDialogue();
                }
                return new(true);
            case WorldOperation.CloseWindow:
                if (dialogue is { Completed: false, Presentation: { NativeState: 2 or 4 or 6 or 8 or 11 or 13 } presentation })
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

    public void UpdateDialogue(Ff7Dialogue updated)
    {
        ArgumentNullException.ThrowIfNull(updated);
        EnsureHealthy();
        if (dialogue is null || dialogue.Completed || dialogue.RequestId != updated.RequestId || dialogue.Window != updated.Window || dialogue.Text != updated.Text || dialogue.FirstChoice != updated.FirstChoice || dialogue.LastChoice != updated.LastChoice)
        {
            throw new InvalidDataException("FFVII world dialogue update changed its native request ownership.");
        }
        Ff7DialogueCursor.Validate(updated, FieldText.Read(Texts[updated.Text].Span));
        if (updated.Selected is < 0 or > 255 || (updated.Completed || updated.Presentation?.NativeState == 7) && updated.FirstChoice is int first && updated.LastChoice is int last && (updated.Selected < first || updated.Selected > last))
        {
            throw new InvalidDataException("FFVII world dialogue selection is outside its native question bounds.");
        }
        dialogue = updated;
        if (updated.Completed)
        {
            lastSelection = unchecked((short)updated.Selected);
        }
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
        UpdateDialogue(Ff7DialogueCursor.Confirm(updated, FieldText.Read(Texts[updated.Text].Span)));
    }

    private void RestoreDialogue(Ff7WorldProcess state)
    {
        window = state.Window;
        dialogue = state.Dialogue;
        lastSelection = state.LastSelection;
    }

    private void ValidateDialogue(Ff7WorldProcess state)
    {
        if (state.Dialogue is Ff7Dialogue saved)
        {
            if (state.Window is null || (uint)saved.Text >= Texts.Count || saved.Window != 0 || saved.RequestId <= 0 || saved.RequestId >= state.Vm.NextRequestId)
            {
                throw new InvalidDataException("FFVII world dialogue snapshot has an invalid window, text or request owner.");
            }
            Ff7DialogueCursor.Validate(saved, FieldText.Read(Texts[saved.Text].Span));
        }
    }
}
