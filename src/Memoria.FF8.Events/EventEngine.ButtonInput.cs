using System.Text.Json.Serialization;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    public NativeFieldButtonInput? ButtonInput => services.FieldButtonInput;

    private void QueueConfirmEdge()
    {
        QueueButtonEdge(0x40);
    }

    private void QueueButtonEdge(ushort mappedMask)
    {
        NativeInputConfiguration configuration = services.InputConfiguration ?? throw new NotSupportedException("Logical input requires the actual native input configuration; no raw alias is inferred.");
        ushort raw = configuration.RawForMappedPulse(mappedMask);
        NativeFieldButtonInput current = services.FieldButtonInput ?? throw new NotSupportedException("Native input requires its explicit current sampler process.");
        services.SetFieldButtonInput(current with { PendingPulse = (ushort)(current.PendingPulse | raw) });
    }

    private void SetFieldButtons(int value)
    {
        if (value is < 0 or > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Field input requires an unsigned native raw WORD.");
        }
        NativeFieldButtonInput current = services.FieldButtonInput ?? throw new NotSupportedException("Native input requires its explicit current sampler process.");
        services.SetFieldButtonInput(current with { PhysicalHeld = (ushort)value });
    }

    private void SampleFieldButtons()
    {
        //49E9C0 computes raw rising edges;4767B0 samples unsigned49ED30/49EDE0 then remaps each independently.
        if (services.FieldButtonInput is not NativeFieldButtonInput current)
        {
            return;
        }
        ushort rawHeld = (ushort)(current.PhysicalHeld | current.PendingPulse);
        ushort rawPressed = (ushort)(rawHeld & ~current.RawHeld);
        ushort? held = services.InputConfiguration?.Map(rawHeld);
        ushort? pressed = services.InputConfiguration?.Map(rawPressed);
        services.SetFieldButtonInput(new NativeFieldButtonInput(3, rawHeld, rawPressed, Frame, FieldId, current.PhysicalHeld, 0, held, pressed));
    }

    private string? UnsupportedButtonQuery(Fiber fiber, JsmOperation operation)
    {
        if (operation.Opcode is not (Jsm.Opcode.KEYON or Jsm.Opcode.KEYSCAN or Jsm.Opcode.KEYSCAN2))
        {
            return null;
        }
        if (fiber.Stack.Count == 0 || ButtonInput is null)
        {
            return $"{operation.Opcode} requires its original DWORD mask and an ordinary sampled native input word";
        }
        ButtonInput.ValidateSample(FieldId, Frame, services.InputConfiguration);
        if (operation.Opcode != Jsm.Opcode.KEYSCAN2 && ButtonInput.Held is null)
        {
            return "Mapped button query requires the actual retained native input configuration; raw input cannot replace it";
        }
        return null;
    }
}
