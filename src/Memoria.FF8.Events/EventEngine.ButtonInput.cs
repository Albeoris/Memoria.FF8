using Albeoris.Games.FF8.Jsm;

namespace Memoria.FF8.Events;

/// <summary>One ordinary input edge queued for the next field callback, plus its last sampled native word.</summary>
public sealed record NativeFieldButtonInput(int Version, ushort PendingPressed, ushort Pressed, long SampledFrame)
{
    public void Validate(long frame)
    {
        if (Version != 1 || (PendingPressed & ~0x50) != 0 || (Pressed & ~0x50) != 0 || SampledFrame < 0 || SampledFrame > frame)
        {
            throw new InvalidDataException("Native field button input does not match its supported ordinary confirm/cancel edges or sampled frame.");
        }
    }
}

public sealed partial class EventEngine
{
    private void QueueConfirmEdge()
    {
        QueueButtonEdge(0x40);
    }

    private void QueueButtonEdge(ushort mask)
    {
        NativeFieldButtonInput current = Scene.ButtonInput ?? new NativeFieldButtonInput(1, 0, 0, Frame);
        Scene.ButtonInput = current with { PendingPressed = (ushort)(current.PendingPressed | mask) };
    }

    private void SampleFieldButtons()
    {
        // Native49E9C0 constructs rising edges;4767B0 samples that word before dialogue and scripts.
        ushort pressed = Scene.ButtonInput?.PendingPressed ?? 0;
        Scene.ButtonInput = new NativeFieldButtonInput(1, 0, pressed, Frame);
    }

    private string? UnsupportedKeyOn(Fiber fiber, JsmOperation operation)
    {
        if (operation.Opcode != Jsm.Opcode.KEYON)
        {
            return null;
        }
        if (fiber.Stack.Count == 0 || Scene.ButtonInput is null)
        {
            return "KEYON requires its original DWORD mask and an ordinary sampled field input word";
        }
        Scene.ButtonInput.Validate(Frame);
        return null;
    }
}
