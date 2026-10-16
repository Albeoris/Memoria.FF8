using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private NativeFieldBarsState PrepareNativeBars(Fiber fiber, Jsm.Opcode opcode)
    {
        int count = opcode == Jsm.Opcode.DISPBAR ? 7 : opcode == Jsm.Opcode.SETBAR ? 2 : 1;
        if (!services.CanStoreFieldBars || services.FieldBars is not { } bars || fiber.Stack.Count < count)
        {
            throw new NotSupportedException("Native bar command requires its global mirror/UI owner and all original DWORD operands.");
        }
        bars.Validate();
        int[] values = fiber.Stack.Take(count).Reverse().ToArray();
        return opcode switch
        {
            Jsm.Opcode.DISPBAR => bars.Display(values[0], values[1], values[2], values[3], values[4], values[5], values[6]),
            Jsm.Opcode.SETBAR => bars.SetValue(values[0], values[1]),
            Jsm.Opcode.KILLBAR => bars.Hide(values[0]),
            _ => throw new ArgumentOutOfRangeException(nameof(opcode))
        };
    }

    private string? UnsupportedNativeBars(Fiber fiber, Jsm.Opcode opcode)
    {
        if (opcode is not (Jsm.Opcode.DISPBAR or Jsm.Opcode.SETBAR or Jsm.Opcode.KILLBAR))
        {
            return null;
        }
        try
        {
            _ = PrepareNativeBars(fiber, opcode);
            return null;
        }
        catch (Exception exception) when (exception is NotSupportedException or InvalidDataException or ArithmeticException)
        {
            return exception.Message;
        }
    }

    private void StepNativeBars(bool movieUi)
    {
        if (services.FieldBars is not { } bars)
        {
            return;
        }
        NativeFieldBarsState next = bars.Advance(movieUi ? 4 : 2);
        if (next != bars)
        {
            services.SetFieldBars(next);
        }
    }
}
