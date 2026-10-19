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
        if (previous is not null)
        {
            SaveNativePhysicalDepth(previous);
        }
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
        if (actors[fiber.ObjectId].NativeCardChallenge)
        {
            fiber.Results[0] = 1;
        }
    }

    private string? UnsupportedNativeActorReturn(Fiber fiber, JsmOperation operation)
    {
        if (operation.Opcode == Jsm.Opcode.RET && (operation.Parameter == 9 || fiber.NativeGlobalJumps.Count != 0))
        {
            return UnsupportedNativeGlobalReturn(fiber, operation);
        }
        if (operation.Opcode != Jsm.Opcode.RET || fiber.Initialization || !IsNativeActorRequest(fiber.ObjectId))
        {
            return null;
        }
        if (fiber.NativeDoorDefaultFrame || !fiber.NativeResultFrameEntered || fiber.NativeDoorSavedResults is not { Length: 8 })
        {
            return "Native actor RET requires its actual requested LBL frame; default frame aliases are unsupported";
        }
        try
        {
            _ = ReadNativeReturnBank(fiber);
            if (operation.Parameter == 8 && HasNativeOperandRecord(fiber.ObjectId))
            {
                Fiber? lower = fibers.Where(item => item.ObjectId == fiber.ObjectId && !item.Complete && item != fiber && NativeActorRequestSlot(item) < NativeActorRequestSlot(fiber)).OrderByDescending(NativeActorRequestSlot).FirstOrDefault();
                if (lower is not null)
                {
                    _ = ReadNativeResumedOperands(lower, actors[fiber.ObjectId].NativeOperandMemory!.LiveDepth!.Value - 8);
                }
            }
        }
        catch (Exception exception) when (exception is NotSupportedException or InvalidDataException)
        {
            return exception.Message;
        }
        if (operation.Parameter != 8)
        {
            Fiber? target = fibers.SingleOrDefault(item => item.ObjectId == fiber.ObjectId && !item.Complete && item != fiber && NativeActorRequestSlot(item) == operation.Parameter);
            if (operation.Parameter is < 0 or > 7 || target is null || operation.Parameter >= NativeActorRequestSlot(fiber))
            {
                return "Native explicit RET requires an occupied lower entered slot and its retained operand depth";
            }
            if (HasNativeOperandRecord(fiber.ObjectId) || !target.NativeResultFrameEntered && !target.NativeDoorDefaultFrame)
            {
                try
                {
                    _ = ReadNativeExplicitReturnBank(target);
                }
                catch (Exception exception) when (exception is NotSupportedException or InvalidDataException)
                {
                    return exception.Message;
                }
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
        NativeScalarValue[] poppedBank = PopNativeReturnBank(fiber);
        // Native51C5C0 clears actor352 bit20 after restoring the result bank on every RET.
        actors[fiber.ObjectId].NativeCardChallenge = false;
        if (returnSlot != 8)
        {
            Fiber target = fibers.Single(item => item.ObjectId == fiber.ObjectId && !item.Complete && item != fiber && NativeActorRequestSlot(item) == returnSlot);
            //51C5C0 first consumes the current frame, then rewinds depth388 to
            // savedDepth380[target]+8 and pops a second bank. Those eight words
            // belong to the first entered frame above the target, not the current
            // frame when intermediate slots exist. Retained target operands stay.
            if (HasNativeOperandRecord(fiber.ObjectId) || !target.NativeResultFrameEntered && !target.NativeDoorDefaultFrame)
            {
                SetNativeDoorLiveResults(target, ReadNativeExplicitReturnBank(target));
            }
            else
            {
                Fiber firstFrame = fibers.Where(item => item.ObjectId == fiber.ObjectId && !item.Complete && item.NativeResultFrameEntered && NativeActorRequestSlot(item) > returnSlot).OrderBy(NativeActorRequestSlot).First();
                SetNativeDoorLiveResults(target, firstFrame.NativeDoorSavedResults!.Reverse().ToArray());
            }
            if (actors[fiber.ObjectId].NativeOperandMemory is { } memory)
            {
                byte? saved = memory.SavedDepths[returnSlot];
                actors[fiber.ObjectId].NativeOperandMemory = memory with { LiveDepth = saved is byte depth ? unchecked((sbyte)depth) : null };
            }
            foreach (Fiber canceled in fibers.Where(item => item.ObjectId == fiber.ObjectId && !item.Complete && NativeActorRequestSlot(item) > returnSlot && NativeActorRequestSlot(item) <= NativeActorRequestSlot(fiber)))
            {
                canceled.Complete = true;
            }
            return;
        }
        Fiber lower = fibers.Where(item => item.ObjectId == fiber.ObjectId && !item.Complete && item != fiber && NativeActorRequestSlot(item) < NativeActorRequestSlot(fiber)).OrderByDescending(NativeActorRequestSlot).First();
        SetNativeDoorLiveResults(lower, poppedBank);
        if (HasNativeOperandRecord(fiber.ObjectId))
        {
            RestoreNativeResumedOperandView(lower, actors[fiber.ObjectId].NativeOperandMemory!.LiveDepth!.Value);
        }
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
