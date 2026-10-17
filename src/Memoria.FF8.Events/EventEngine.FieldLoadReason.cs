using Albeoris.Games.FF8.Jsm;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private void ValidateFieldLoadReasonLoad(byte? reason)
    {
        if (reason is not null && HasFieldLoadReasonAlias(EventMemory))
        {
            throw new NotSupportedException("Native field-load BYTE87 allocation intersects retained generic memory; the current field is preserved.");
        }
    }

    private void InitializeFieldLoadReason(byte? reason)
    {
        Scene.FieldLoadReasonByte87 = reason;
    }

    private static bool HasFieldLoadReasonAlias(Memoria.FF8.Runtime.NativeEventMemory memory)
    {
        return memory.Cells.Any(pair => pair.Key <= 87 && (long)pair.Key + pair.Value.Width > 87);
    }

    private string? UnsupportedFieldLoadReasonAccess(Fiber fiber, JsmOperation operation)
    {
        int width = NativeMemoryWidth(operation.Opcode);
        if (width == 0 || operation.Parameter > 87 || (long)operation.Parameter + width <= 87 || NativeRuntimeProfile is not { IsVerified: true } && Scene.FieldLoadReasonByte87 is null)
        {
            return null;
        }
        if (operation.Parameter != 87 || operation.Opcode is not (Jsm.Opcode.PSHM_B or Jsm.Opcode.PSHSM_B or Jsm.Opcode.POPM_B))
        {
            return "Native field-load BYTE87 requires an exact BYTE access; overlapping widths preserve PC and operands";
        }
        if (HasFieldLoadReasonAlias(EventMemory))
        {
            return "Native field-load BYTE87 intersects retained generic memory; PC and operands preserved";
        }
        if (operation.Opcode == Jsm.Opcode.POPM_B)
        {
            return fiber.Stack.Count == 0 || !fiber.Stack.PeekValue().IsExact || fiber.Stack.PeekValue().ActorInfoOrigin is not null ? "Native field-load BYTE87 requires its exact original store operand; PC and operands preserved" : null;
        }
        return Scene.FieldLoadReasonByte87 is null ? "Native field-load BYTE87 requires an explicit field-allocation reason; PC and operands preserved" : null;
    }

    private void ValidateFieldLoadReasonCheckpoint(Checkpoint saved)
    {
        if (saved.Scene.FieldLoadReasonByte87 is not null && HasFieldLoadReasonAlias(saved.EventMemory))
        {
            throw new InvalidDataException("Saved field-load BYTE87 overlaps retained generic memory.");
        }
    }
}
