using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private NativeFieldBarsState PrepareNativeBars(Fiber fiber, Jsm.Opcode opcode)
    {
        int count = opcode == Jsm.Opcode.BROKEN ? 8 : opcode == Jsm.Opcode.DISPBAR ? 7 : opcode == Jsm.Opcode.SETBAR ? 2 : 1;
        if (!services.CanStoreFieldBars || services.FieldBars is not { } bars || fiber.Stack.Count < count)
        {
            throw new NotSupportedException("Native bar command requires its global mirror/UI owner and all original DWORD operands.");
        }
        bars.Validate();
        int[] values = fiber.Stack.Take(count).Reverse().ToArray();
        if (opcode == Jsm.Opcode.KILLBAR && values[0] == 2)
        {
            if (!services.CanStoreNativeFieldBarAliasOwners || services.VibrationProcess is not NativeVibrationProcessState vibration)
            {
                throw new NotSupportedException("Native bar alias writes require the adjacent mirror WORD, vibration resource pointer and atomic owner publication.");
            }
            vibration.Validate();
            return bars.ClearAdjacentMirrorWord();
        }
        return opcode switch
        {
            Jsm.Opcode.DISPBAR => bars.Display(values[0], values[1], values[2], values[3], values[4], values[5], values[6]),
            Jsm.Opcode.BROKEN => bars.DisplayWidth(values[0], values[1], values[2], values[3], values[4], values[5], values[6], values[7]),
            Jsm.Opcode.SETBAR => bars.SetValue(values[0], values[1]),
            Jsm.Opcode.KILLBAR => bars.Hide(values[0]),
            _ => throw new ArgumentOutOfRangeException(nameof(opcode))
        };
    }

    private int[] ExecuteNativeBars(Fiber fiber, Jsm.Opcode opcode)
    {
        NativeFieldBarsState bars = PrepareNativeBars(fiber, opcode);
        int count = opcode == Jsm.Opcode.BROKEN ? 8 : opcode == Jsm.Opcode.DISPBAR ? 7 : opcode == Jsm.Opcode.SETBAR ? 2 : 1;
        try
        {
            if (opcode == Jsm.Opcode.KILLBAR && fiber.Stack.Peek() == 2)
            {
                NativeVibrationProcessState vibration = services.VibrationProcess!.AndResourcePointer(0xFFFFFF7Fu);
                services.SetNativeFieldBarAliasOwners(bars, vibration);
            }
            else
            {
                services.SetFieldBars(bars);
            }
        }
        catch (Exception exception) when (exception is NotSupportedException or InvalidDataException)
        {
            fiber.Position--;
            string message = $"Field {FieldId}, object {fiber.ObjectId}, script {fiber.Script.Id}, operation {fiber.Position}: native bar owner publication rejected; PC and operands preserved.";
            LastFault = new EventFault(FieldId, fiber.ObjectId, fiber.Script.Id, fiber.Position, opcode.ToString(), message);
            throw new EventScriptException(message, exception);
        }
        return Pop(fiber, count);
    }

    private string? UnsupportedNativeBars(Fiber fiber, Jsm.Opcode opcode)
    {
        if (opcode is not (Jsm.Opcode.DISPBAR or Jsm.Opcode.BROKEN or Jsm.Opcode.SETBAR or Jsm.Opcode.KILLBAR))
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
