using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private void SelectNativeActorRequest(Fiber requested)
    {
        if (!IsNativeActorRequest(requested.ObjectId))
        {
            return;
        }
        Fiber? previous = fibers.Where(item => item.ObjectId == requested.ObjectId && !item.Complete && item != requested).OrderByDescending(NativeActorRequestSlot).ThenBy(item => item.Sequence).FirstOrDefault();
        if (previous is not null && NativeActorRequestSlot(requested) > NativeActorRequestSlot(previous))
        {
            SetNativeDoorLiveResults(requested, ReadNativeDoorLiveResults(previous));
            if (!previous.NativeResultFrameEntered && !previous.NativeDoorDefaultFrame)
            {
                previous.Results.Clear();
            }
        }
    }

    private void EnterNativeActorResultFrame(Fiber fiber)
    {
        if (fiber.Initialization || !IsNativeActorRequest(fiber.ObjectId))
        {
            return;
        }
        if (SelectScheduledFiber(fiber.ObjectId) != fiber || fiber.NativeDoorDefaultFrame || fiber.NativeResultFrameEntered || fiber.NativeDoorSavedResults is not null)
        {
            throw new NotSupportedException("Native actor LBL requires its selected unentered request context.");
        }
        // The existing serialized bank names the same physical 51C570 frame
        // for actors, areas and modules as for doors; no second bank is added.
        fiber.NativeDoorSavedResults = ReadNativeDoorLiveResults(fiber);
        fiber.Results.Clear();
    }

    private string? UnsupportedNativeActorReturn(Fiber fiber, JsmOperation operation)
    {
        if (operation.Opcode != Jsm.Opcode.RET || fiber.Initialization || !IsNativeActorRequest(fiber.ObjectId))
        {
            return null;
        }
        if (fiber.NativeDoorDefaultFrame || !fiber.NativeResultFrameEntered || fiber.NativeDoorSavedResults is not { Length: 8 })
        {
            return "Native actor RET requires its actual requested LBL frame; default frame aliases are unsupported";
        }
        if (operation.Parameter != 8)
        {
            Fiber? target = fibers.SingleOrDefault(item => item.ObjectId == fiber.ObjectId && !item.Complete && item != fiber && NativeActorRequestSlot(item) == operation.Parameter);
            if (operation.Parameter is < 0 or > 7 || target is null || operation.Parameter >= NativeActorRequestSlot(fiber) || !target.NativeResultFrameEntered && !target.NativeDoorDefaultFrame)
            {
                return "Native explicit RET requires an occupied lower entered slot and its retained operand depth";
            }
            if (actors[fiber.ObjectId].NativeMovementRequest is { RestoreSuppressedByFlush: false })
            {
                return "Native explicit RET with a movement backup requires its cancellation and motion restoration contract";
            }
            return null;
        }
        if (!fibers.Any(item => item.ObjectId == fiber.ObjectId && !item.Complete && item != fiber && NativeActorRequestSlot(item) < NativeActorRequestSlot(fiber)))
        {
            return "Native actor RET without a lower occupied slot requires its unrepresented sticky current-PC and consumed-frame memory aliases";
        }
        return null;
    }

    private void CompleteNativeActorResultFrame(Fiber fiber)
    {
        if (fiber.Initialization || !IsNativeActorRequest(fiber.ObjectId))
        {
            return;
        }
        int returnSlot = program!.Operations[fiber.Position - 1].Parameter;
        if (returnSlot != 8)
        {
            Fiber target = fibers.Single(item => item.ObjectId == fiber.ObjectId && !item.Complete && item != fiber && NativeActorRequestSlot(item) == returnSlot);
            //51C5C0 first consumes the current frame, then rewinds depth388 to
            // savedDepth380[target]+8 and pops a second bank. Those eight words
            // belong to the first entered frame above the target, not the current
            // frame when intermediate slots exist. Retained target operands stay.
            Fiber firstFrame = fibers.Where(item => item.ObjectId == fiber.ObjectId && !item.Complete && item.NativeResultFrameEntered && NativeActorRequestSlot(item) > returnSlot).OrderBy(NativeActorRequestSlot).First();
            SetNativeDoorLiveResults(target, firstFrame.NativeDoorSavedResults!.Reverse().ToArray());
            foreach (Fiber canceled in fibers.Where(item => item.ObjectId == fiber.ObjectId && !item.Complete && NativeActorRequestSlot(item) > returnSlot && NativeActorRequestSlot(item) <= NativeActorRequestSlot(fiber)))
            {
                canceled.Complete = true;
            }
            return;
        }
        Fiber lower = fibers.Where(item => item.ObjectId == fiber.ObjectId && !item.Complete && item != fiber && NativeActorRequestSlot(item) < NativeActorRequestSlot(fiber)).OrderByDescending(NativeActorRequestSlot).First();
        NativeScalarValue[] bank = fiber.NativeDoorSavedResults ?? throw new NotSupportedException("Native actor RET lost its actual LBL saved-result bank.");
        SetNativeDoorLiveResults(lower, bank.Reverse().ToArray());
    }

    private bool IsSavedNativeResultCurrent(Checkpoint saved, FiberCheckpoint fiber)
    {
        if (saved.Actors[fiber.ObjectId].NativeDoorScheduler?.CurrentSequence == fiber.Sequence)
        {
            return true;
        }
        if (program!.Objects[fiber.ObjectId].IsDoor)
        {
            return false;
        }
        int Slot(FiberCheckpoint item) => item.NativeActorSlot ?? (item.Priority == 7 && program.Objects[item.ObjectId].Scripts.Count > 1 && item.ScriptId == program.Objects[item.ObjectId].Scripts[1].Id ? 0 : item.Priority);
        return saved.Fibers.Where(item => item.ObjectId == fiber.ObjectId && !item.Complete && !item.Initialization).OrderByDescending(Slot).ThenBy(item => item.Sequence).FirstOrDefault()?.Sequence == fiber.Sequence;
    }
}
