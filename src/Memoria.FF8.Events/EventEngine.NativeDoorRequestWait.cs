using Albeoris.Games.FF8.Jsm;

namespace Memoria.FF8.Events;

/// <summary>Executed first-call request373 state while the caller retains both original DWORD operands.</summary>
public sealed record NativeDoorRequestWaitState(int Version, int Operation, int TargetNativeObjectId, int TargetObjectId, int RawPriority, int RawScriptId, long RequestSequence, long DefaultSequence);

public sealed partial class EventEngine
{
    private Fiber? FindNativeDoorWaitDefault(int objectId, IEnumerable<Fiber> source)
    {
        return source.SingleOrDefault(fiber => fiber.ObjectId == objectId && !fiber.Complete && fiber.NativeDoorSlot is null && fiber.NativeActorSlot is null && fiber.Priority == 7 && NativeDoorScriptLocal(objectId, fiber.Script.Id) == 1 && fiber.NativeResultFrameEntered && fiber.WaitReason == WaitReason.NativeHalt && fiber.Position >= fiber.Script.Start && fiber.Position < fiber.Script.End && program!.Operations[fiber.Position].Opcode == Jsm.Opcode.HALT);
    }

    private string? UnsupportedNativeDoorRequestWaitIssue(Fiber caller, int targetObjectId)
    {
        if (caller.Initialization || caller.ObjectId == targetObjectId)
        {
            return "Native door wait request requires a finalized cross-entity slot transfer; PC and operands preserved";
        }
        if (FindNativeDoorWaitDefault(targetObjectId, fibers) is null)
        {
            return "Native door wait request requires its genuine occupied default0 HALT owner; empty-slot current state is unsupported";
        }
        return null;
    }

    private string? UnsupportedPendingNativeDoorRequestWait(Fiber caller, JsmOperation operation)
    {
        try
        {
            ValidateNativeDoorRequestWaitOwner(caller, fibers);
            if (caller.NativeDoorRequestWait!.Operation != caller.Position || operation.Opcode is not (Jsm.Opcode.REQSW or Jsm.Opcode.REQEW))
            {
                return "Native door wait lost its held original request instruction";
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or InvalidDataException or NotSupportedException)
        {
            return exception.Message;
        }
        return null;
    }

    private int[] ExecuteNativeDoorRequestWait(Fiber caller, int targetNativeObjectId)
    {
        if (caller.NativeDoorRequestWait is null)
        {
            int rawScript = caller.Stack.Peek();
            int rawPriority = caller.Stack.Skip(1).First();
            int targetObjectId = program!.NativeObjects[targetNativeObjectId];
            Fiber? requested = Request(caller, targetNativeObjectId, rawScript, rawPriority, Jsm.Opcode.REQ);
            if (requested is null)
            {
                // Null or initially occupied target consumes both operands without a wait owner.
                return Pop(caller, 2);
            }
            Fiber floor = FindNativeDoorWaitDefault(targetObjectId, fibers)!;
            caller.NativeDoorRequestWait = new NativeDoorRequestWaitState(1, caller.Position - 1, targetNativeObjectId, targetObjectId, rawPriority, rawScript, requested.Sequence, floor.Sequence);
            caller.Request = requested;
        }
        if (NativeDoorRequestWaitReady(caller))
        {
            caller.NativeDoorRequestWait = null;
            caller.Request = null;
            //51CED0/51D060 pop only when their full-DWORD comparison returns3.
            return Pop(caller, 2);
        }
        caller.Position--;
        caller.WaitReason = WaitReason.NativeDoorRequest;
        return [];
    }

    private bool NativeDoorRequestWaitReady(Fiber caller)
    {
        NativeDoorRequestWaitState owner = caller.NativeDoorRequestWait ?? throw new NotSupportedException("Native door wait lacks its executed first-call owner");
        Fiber? floor = FindNativeDoorWaitDefault(owner.TargetObjectId, fibers);
        if (floor?.Sequence != owner.DefaultSequence)
        {
            throw new NotSupportedException("Native door wait lost its occupied default0 HALT floor; current slot cannot be inferred");
        }
        // With an occupied nonretiring slot0, raw higher-slot selection and RET8
        // resumption make the highest occupied slot the actual BYTE372 owner.
        int current = fibers.Where(fiber => fiber.ObjectId == owner.TargetObjectId && !fiber.Complete).Select(NativeActorRequestSlot).Max();
        return program!.Operations[owner.Operation].Opcode == Jsm.Opcode.REQSW ? current == owner.RawPriority : current < owner.RawPriority;
    }

    private void ValidateNativeDoorRequestWaitOwner(Fiber caller, IReadOnlyList<Fiber> source)
    {
        NativeDoorRequestWaitState owner = caller.NativeDoorRequestWait ?? throw new InvalidDataException("Saved native door wait lacks its executed first-call receipt");
        var operands = caller.Stack.Capture();
        if (operands.Length < 2 || !operands[0].IsExact || !operands[1].IsExact)
        {
            throw new InvalidDataException("Saved native door wait requires its two exact original DWORD operands");
        }
        if (NativeRuntimeProfile is not { IsVerified: true } || owner.Version != 1 || caller.Initialization || caller.Complete || owner.Operation < caller.Script.Start || owner.Operation >= caller.Script.End || program!.Operations[owner.Operation].Opcode is not (Jsm.Opcode.REQSW or Jsm.Opcode.REQEW) || program.Operations[owner.Operation].Parameter != owner.TargetNativeObjectId || (uint)owner.TargetNativeObjectId >= program.NativeObjects.Count || program.NativeObjects[owner.TargetNativeObjectId] != owner.TargetObjectId || !program.Objects[owner.TargetObjectId].IsDoor || caller.ObjectId == owner.TargetObjectId || caller.Stack.Count < 2 || caller.Stack.Peek() != owner.RawScriptId || caller.Stack.Skip(1).First() != owner.RawPriority || unchecked((byte)owner.RawPriority) > 7)
        {
            throw new InvalidDataException("Saved native door wait lost its request opcode, full DWORD operands or target slot");
        }
        Fiber? floor = FindNativeDoorWaitDefault(owner.TargetObjectId, source);
        Fiber? requested = source.SingleOrDefault(fiber => fiber.Sequence == owner.RequestSequence);
        if (floor is null || floor.Sequence != owner.DefaultSequence || requested is null || requested.ObjectId != owner.TargetObjectId || requested.Initialization || requested.NativeActorSlot is not null || requested.NativeDoorSlot != unchecked((byte)owner.RawPriority) || requested.Priority != 7 - unchecked((byte)owner.RawPriority) || requested.Script.Id != unchecked((ushort)owner.RawScriptId) || requested.Sequence <= caller.Sequence || requested.Sequence <= floor.Sequence)
        {
            throw new InvalidDataException("Saved native door wait lost its issued request or occupied default0 HALT authority");
        }
    }

    private void ValidateNativeDoorRequestWaitCheckpoint(Checkpoint saved, IReadOnlyList<Fiber> restored)
    {
        foreach (Fiber caller in restored)
        {
            if (caller.NativeDoorRequestWait is null)
            {
                if (caller.WaitReason == WaitReason.NativeDoorRequest || caller.WaitReason is WaitReason.RequestStart or WaitReason.RequestEnd && caller.Request is Fiber legacy && program!.Objects[legacy.ObjectId].IsDoor)
                {
                    throw new InvalidDataException("Saved door request wait requires its executed native current-slot contract; recreate the authentic journal");
                }
                continue;
            }
            ValidateNativeDoorRequestWaitOwner(caller, restored);
            NativeDoorRequestWaitState owner = caller.NativeDoorRequestWait!;
            if (saved.Scene.NativeDoorDriverVersion != 2 || caller.WaitReason != WaitReason.NativeDoorRequest || caller.Position != owner.Operation || caller.Request?.Sequence != owner.RequestSequence)
            {
                throw new InvalidDataException("Saved native door wait lost its held PC, request reference or physical-slot scheduler");
            }
        }
    }
}
