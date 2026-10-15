using Albeoris.Games.FF8.Jsm;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private bool IsAllocatedPhysicalRequestActor(int objectId) => NativeRuntimeProfile is { IsVerified: true } && program!.NativeObjects.Take(program.NativeActorCount).Contains(objectId);

    private string? UnsupportedNativeRequestHeaderWrite(Fiber fiber, JsmOperation operation)
    {
        if (NativeRuntimeProfile is { IsVerified: true })
        {
            if (IsNativeDoorRequest(fiber.ObjectId) && operation.Opcode == Jsm.Opcode.RET && operation.Parameter != 8)
            {
                return "Native door RET slot cancellation requires its original explicit-slot contract; PC and operands preserved";
            }
            if (operation.Opcode == Jsm.Opcode.LBL && (fiber.NativeResultFrameEntered || fiber.Position != fiber.Script.Start))
            {
                return "Native repeated LBL requires its additional saved-result frame; PC and operands preserved";
            }
            if (operation.Opcode != Jsm.Opcode.LBL && !fiber.NativeResultFrameEntered)
            {
                return "Native opcode requires its executed LBL saved-result frame owner; PC and operands preserved";
            }
            if (operation.Opcode is Jsm.Opcode.PSHI_L or Jsm.Opcode.POPI_L && (uint)operation.Parameter >= 8)
            {
                return "Native result access outside DWORD320..348 requires its unrepresented physical memory alias; PC and operands preserved";
            }
            int additionalWords = operation.Opcode == Jsm.Opcode.LBL ? 8 : operation.Opcode is Jsm.Opcode.PSHN_L or Jsm.Opcode.PSHAC or Jsm.Opcode.PSHM_B or Jsm.Opcode.PSHSM_B or Jsm.Opcode.PSHM_W or Jsm.Opcode.PSHM_L or Jsm.Opcode.PSHSM_W or Jsm.Opcode.PSHI_L ? 1 : 0;
            // Native LBL51C570 saves eight results; unchecked pushes51C990 and POPI51CC70 can alias the actor header.
            // Refuse the unrepresented write rather than imposing a purported native clamp (NG-002).
            if (additionalWords > 0)
            {
                int retainedWords = fibers.Where(item => item.ObjectId == fiber.ObjectId && !item.Complete && item.NativeResultFrameEntered).Sum(item => item.Stack.Count + 8);
                if (retainedWords + additionalWords > 80)
                {
                    return "Native operand write outside DWORD0..316 requires its unrepresented result/header memory alias; PC and operands preserved";
                }
            }
        }
        if (!IsAllocatedPhysicalRequestActor(fiber.ObjectId))
        {
            return null;
        }
        if (operation.Opcode is Jsm.Opcode.SETLINE or Jsm.Opcode.BGDRAW or Jsm.Opcode.BGOFF or Jsm.Opcode.BGANIME or Jsm.Opcode.RBGANIME or Jsm.Opcode.RBGANIMELOOP)
        {
            return $"Native {operation.Opcode} on a physical actor requires its overlapping WORD398 header write; PC and operands preserved";
        }
        return null;
    }

    private string? UnsupportedRequestSlot(int objectId, int rawPriority, Jsm.Opcode opcode, out int priority, out bool occupied)
    {
        bool native = NativeRuntimeProfile is { IsVerified: true };
        priority = native ? unchecked((byte)rawPriority) : rawPriority;
        occupied = false;
        if (native && program!.Objects[objectId].IsDoor && !IsNativeDoorRequest(objectId))
        {
            return "Native door request requires its completed physical-slot scheduler owner; PC and operands preserved";
        }
        if ((uint)priority < 8)
        {
            occupied = RequestPriorityOccupied(objectId, priority);
            if (native && IsNativeDoorRequest(objectId) && !occupied && opcode is Jsm.Opcode.REQSW or Jsm.Opcode.REQEW)
            {
                return $"Native door {opcode} requires its actual current-slot/raw DWORD wait comparison; PC and operands preserved";
            }
            if (native && !occupied && opcode is Jsm.Opcode.REQSW or Jsm.Opcode.REQEW && rawPriority != priority)
            {
                return $"Native {opcode} scheduling with a non-byte priority requires its original raw DWORD wait comparison";
            }
            return null;
        }
        // NG-002: original51CD60/51CED0/51D060 index356+2*lowBYTE without a slot mask.
        // Priority21 aliases allocated physical WORD398
        if (native && priority == 21 && IsAllocatedPhysicalRequestActor(objectId))
        {
            ushort? word = actors[objectId].NativeActorWord398;
            if (word is null)
            {
                return "Native REQ priority21 requires the actual allocated physical WORD398 owner; PC and operands preserved";
            }
            if (word == ushort.MaxValue)
            {
                return "Native REQ priority21 free physical WORD398 requires unimplemented overlapping request writes; PC and operands preserved";
            }
            occupied = true;
            return null;
        }
        return $"Native {opcode} priority{priority} requires its original out-of-slot physical record owner; PC and operands preserved";
    }
}
