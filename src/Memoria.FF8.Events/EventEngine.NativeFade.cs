using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    public NativeFieldFadeState? FieldFade => services.FieldFade;

    private string? UnsupportedNativeFadeAccess(Fiber fiber, JsmOperation operation)
    {
        int count = operation.Opcode switch
        {
            Jsm.Opcode.DCOLADD or Jsm.Opcode.DCOLSUB => 3,
            Jsm.Opcode.TCOLADD or Jsm.Opcode.TCOLSUB => 4,
            Jsm.Opcode.FCOLADD or Jsm.Opcode.FCOLSUB => 7,
            Jsm.Opcode.FADEIN or Jsm.Opcode.FADEOUT or Jsm.Opcode.FADENONE or Jsm.Opcode.FADEBLACK or Jsm.Opcode.FADESYNC or Jsm.Opcode.COLSYNC or Jsm.Opcode.MAPFADEOFF or Jsm.Opcode.MAPFADEON => 0,
            _ => -1
        };
        int width = NativeMemoryWidth(operation.Opcode);
        bool memory = width != 0 && operation.Parameter < 240 && (long)operation.Parameter + width > 216;
        if (count < 0 && !memory)
        {
            return null;
        }
        if (FieldFade is null || !services.CanStoreFieldFade)
        {
            return "Native field fade requires its actual physical/saved WORD banks before mutation.";
        }
        try
        {
            FieldFade.Validate();
            if (EventMemory.Overlaps(216, 24))
            {
                return "Native saved fade words overlap generic event memory.";
            }
            if (memory)
            {
                _ = FieldFade.ReadSaved(operation.Parameter, width);
                if (operation.Opcode is Jsm.Opcode.POPM_B or Jsm.Opcode.POPM_W or Jsm.Opcode.POPM_L)
                {
                    if (fiber.Stack.Count == 0)
                    {
                        return "Native fade bank store requires its DWORD operand.";
                    }
                    _ = fiber.Stack.PeekValue().RequireExact();
                }
            }
            else if (fiber.Stack.Count < count)
            {
                return "Native field color command lacks its complete operand packet.";
            }
            return null;
        }
        catch (Exception exception) when (exception is InvalidDataException or NotSupportedException)
        {
            return exception.Message;
        }
    }

    private bool TryReadNativeFadeMemory(JsmOperation operation, out int value)
    {
        int width = NativeMemoryWidth(operation.Opcode);
        if (width != 0 && operation.Parameter >= 216 && (long)operation.Parameter + width <= 240)
        {
            value = (FieldFade ?? throw new NotSupportedException("Native fade bank is unknown.")).ReadSaved(operation.Parameter, width);
            return true;
        }
        value = 0;
        return false;
    }

    private bool TryWriteNativeFadeMemory(Fiber fiber, JsmOperation operation, out int[] operands)
    {
        int width = NativeMemoryWidth(operation.Opcode);
        if (width != 0 && operation.Parameter >= 216 && (long)operation.Parameter + width <= 240)
        {
            NativeFieldFadeState next = FieldFade!.WriteSaved(operation.Parameter, width, fiber.Stack.Peek());
            services.SetFieldFade(next);
            operands = Pop(fiber, 1);
            return true;
        }
        operands = [];
        return false;
    }

    private int[] ExecuteNativeFieldColor(Fiber fiber, Jsm.Opcode opcode)
    {
        int count = opcode is Jsm.Opcode.DCOLADD or Jsm.Opcode.DCOLSUB ? 3 : opcode is Jsm.Opcode.TCOLADD or Jsm.Opcode.TCOLSUB ? 4 : 7;
        int[] values = fiber.Stack.Take(count).Reverse().ToArray();
        bool subtract = opcode is Jsm.Opcode.DCOLSUB or Jsm.Opcode.TCOLSUB or Jsm.Opcode.FCOLSUB;
        NativeFieldFadeState current = FieldFade!;
        NativeFieldFadeState next = count switch
        {
            3 => current.IssueDirectColor(subtract, values[0], values[1], values[2]),
            4 => current.IssueTargetColor(subtract, values[0], values[1], values[2], values[3]),
            _ => current.IssueInterpolatedColor(subtract, values[0], values[1], values[2], values[3], values[4], values[5], values[6])
        };
        services.SetFieldFade(next);
        return Pop(fiber, count);
    }

    private void ExecuteNativeFadeSync(Fiber fiber, bool color)
    {
        NativeFieldFadeState current = FieldFade!;
        if (color ? current.IsColorComplete : current.IsFadeComplete)
        {
            if (color)
            {
                services.SetFieldFade(current.CompleteColorSync());
            }
            return;
        }
        fiber.Position--;
        fiber.WaitReason = color ? WaitReason.Color : WaitReason.Fade;
    }

    private static void ValidateNativeFadeCheckpoint(Checkpoint saved)
    {
        if (saved.FieldFade is null)
        {
            return;
        }
        saved.FieldFade.Validate();
        if (saved.EventMemory.Overlaps(216, 24))
        {
            throw new InvalidDataException("Saved field fade words cannot also exist in untyped or uncertain event memory.");
        }
    }
}
