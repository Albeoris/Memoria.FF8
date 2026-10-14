using Albeoris.Games.FF8.Jsm;

namespace Memoria.FF8.Events;

/// <summary>Mapped physical held buttons, queued action pulses and the last sampled native edge word.</summary>
public sealed record NativeFieldButtonInput(int Version, ushort PendingPressed, ushort Pressed, long SampledFrame, ushort Held, ushort PhysicalHeld, ushort PendingPulse)
{
    public const ushort SupportedButtons = 0xF050;

    public void Validate(long frame)
    {
        if (Version != 2 || PendingPressed != PendingPulse || (Pressed & ~SupportedButtons) != 0 || (Pressed & ~Held) != 0 || (Held & ~SupportedButtons) != 0 || (PhysicalHeld & ~SupportedButtons) != 0 || (PendingPulse & ~0x50) != 0 || SampledFrame < 0 || SampledFrame != frame)
        {
            throw new InvalidDataException("Native field button input does not match its supported mapped buttons or sampled frame.");
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
        NativeFieldButtonInput current = Scene.ButtonInput ?? new NativeFieldButtonInput(2, 0, 0, Frame, 0, 0, 0);
        Scene.ButtonInput = current with { PendingPressed = (ushort)(current.PendingPressed | mask), PendingPulse = (ushort)(current.PendingPulse | mask) };
    }

    private void SetFieldButtons(int value)
    {
        if (value < 0 || (value & ~NativeFieldButtonInput.SupportedButtons) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Field input requires supported mapped held buttons.");
        }
        NativeFieldButtonInput current = Scene.ButtonInput ?? new NativeFieldButtonInput(2, 0, 0, Frame, 0, 0, 0);
        ushort held = (ushort)value;
        Scene.ButtonInput = current with { PhysicalHeld = held };
    }

    private void SampleFieldButtons()
    {
        // Native49E9C0 constructs rising edges;4767B0 samples that word before dialogue and scripts.
        ushort physicalHeld = Scene.ButtonInput?.PhysicalHeld ?? 0;
        ushort pressed = (ushort)((Scene.ButtonInput?.PendingPressed ?? 0) | (physicalHeld & ~(Scene.ButtonInput?.Held ?? 0)));
        ushort held = (ushort)(physicalHeld | (Scene.ButtonInput?.PendingPulse ?? 0));
        Scene.ButtonInput = new NativeFieldButtonInput(2, 0, pressed, Frame, held, physicalHeld, 0);
    }

    private string? UnsupportedButtonQuery(Fiber fiber, JsmOperation operation)
    {
        if (operation.Opcode is not Jsm.Opcode.KEYON and not Jsm.Opcode.KEYSCAN)
        {
            return null;
        }
        if (fiber.Stack.Count == 0 || Scene.ButtonInput is null)
        {
            return $"{operation.Opcode} requires its original DWORD mask and an ordinary sampled field input word";
        }
        Scene.ButtonInput.Validate(Frame);
        return null;
    }
}
