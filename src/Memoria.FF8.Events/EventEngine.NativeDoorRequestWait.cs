using Albeoris.Games.FF8.Jsm;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Events;

/// <summary>Executed first-call request373 state while the caller retains both original DWORD operands.</summary>
public sealed record NativeDoorRequestWaitState([property: JsonRequired] int Version, [property: JsonRequired] int Operation, [property: JsonRequired] int TargetNativeObjectId, [property: JsonRequired] int TargetObjectId, [property: JsonRequired] int RawPriority, [property: JsonRequired] int RawScriptId, [property: JsonRequired] long RequestSequence, [property: JsonRequired] long DefaultSequence);

public sealed partial class EventEngine
{
    private string? UnsupportedNativeDoorRequestWaitIssue(Fiber caller, int targetObjectId)
    {
        if (caller.Initialization || caller.ObjectId == targetObjectId)
        {
            return "Native door wait request requires a finalized cross-entity slot transfer; PC and operands preserved";
        }
        return UnsupportedNativeDoorSlotIssue(targetObjectId);
    }

    private string? UnsupportedPendingNativeDoorRequestWait(Fiber caller, JsmOperation operation)
    {
        try
        {
            ValidateNativeDoorRequestWaitOwner(caller, fibers, (uint)caller.NativeDoorRequestWait!.TargetObjectId < actors.Count ? actors[caller.NativeDoorRequestWait.TargetObjectId].NativeDoorScheduler : null);
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
            NativeDoorSchedulerState scheduler = RequireNativeDoorScheduler(targetObjectId);
            caller.NativeDoorRequestWait = new NativeDoorRequestWaitState(2, caller.Position - 1, targetNativeObjectId, targetObjectId, rawPriority, rawScript, requested.Sequence, scheduler.DefaultSequence);
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
        NativeDoorSchedulerState scheduler = RequireNativeDoorScheduler(owner.TargetObjectId);
        if (scheduler.DefaultSequence != owner.DefaultSequence)
        {
            throw new NotSupportedException("Native door wait lost its original52C960 current-slot owner");
        }
        int current = scheduler.CurrentSlot;
        return program!.Operations[owner.Operation].Opcode == Jsm.Opcode.REQSW ? current == owner.RawPriority : current < owner.RawPriority;
    }

    private void ValidateNativeDoorRequestWaitOwner(Fiber caller, IReadOnlyList<Fiber> source, NativeDoorSchedulerState? scheduler)
    {
        NativeDoorRequestWaitState owner = caller.NativeDoorRequestWait ?? throw new InvalidDataException("Saved native door wait lacks its executed first-call receipt");
        var operands = caller.Stack.Capture();
        if (operands.Length < 2 || !operands[0].IsExact || !operands[1].IsExact)
        {
            throw new InvalidDataException("Saved native door wait requires its two exact original DWORD operands");
        }
        if (NativeRuntimeProfile is not { IsVerified: true } || owner.Version != 2 || caller.Initialization || caller.Complete || !caller.Script.ContainsOperation(owner.Operation) || program!.Operations[owner.Operation].Opcode is not (Jsm.Opcode.REQSW or Jsm.Opcode.REQEW) || program.Operations[owner.Operation].Parameter != owner.TargetNativeObjectId || (uint)owner.TargetNativeObjectId >= program.NativeObjects.Count || program.NativeObjects[owner.TargetNativeObjectId] != owner.TargetObjectId || !program.Objects[owner.TargetObjectId].IsDoor || caller.ObjectId == owner.TargetObjectId || caller.Stack.Count < 2 || caller.Stack.Peek() != owner.RawScriptId || caller.Stack.Skip(1).First() != owner.RawPriority || unchecked((byte)owner.RawPriority) > 7)
        {
            throw new InvalidDataException("Saved native door wait lost its request opcode, full DWORD operands or target slot");
        }
        Fiber? requested = source.SingleOrDefault(fiber => fiber.Sequence == owner.RequestSequence);
        if (scheduler is not { Version: 1, CurrentSlot: <= 7 } || scheduler.DefaultSequence != owner.DefaultSequence || requested is null || requested.ObjectId != owner.TargetObjectId || requested.Initialization || requested.NativeActorSlot is not null || requested.NativeDoorSlot != unchecked((byte)owner.RawPriority) || requested.Priority != 7 - unchecked((byte)owner.RawPriority) || requested.Script.Id != unchecked((ushort)owner.RawScriptId) || requested.Sequence <= caller.Sequence || requested.Sequence <= scheduler.DefaultSequence)
        {
            throw new InvalidDataException("Saved native door wait lost its issued request or original52C960 current-slot authority");
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
                    throw new InvalidDataException("Saved door request wait requires its executed native current-slot contract; the snapshot is missing required native state");
                }
                continue;
            }
            ValidateNativeDoorRequestWaitOwner(caller, restored, (uint)caller.NativeDoorRequestWait.TargetObjectId < saved.Actors.Length ? saved.Actors[caller.NativeDoorRequestWait.TargetObjectId].NativeDoorScheduler : null);
            NativeDoorRequestWaitState owner = caller.NativeDoorRequestWait!;
            if (saved.Scene.NativeDoorDriverVersion != 2 || caller.WaitReason != WaitReason.NativeDoorRequest || caller.Position != owner.Operation || caller.Request?.Sequence != owner.RequestSequence)
            {
                throw new InvalidDataException("Saved native door wait lost its held PC, request reference or physical-slot scheduler");
            }
        }
    }
}
