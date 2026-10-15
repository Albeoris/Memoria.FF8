using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Events;

/// <summary>Original door BYTE372 and its selected WORD374 context, initialized by52C960.</summary>
public sealed record NativeDoorSchedulerState([property: JsonRequired] int Version, [property: JsonRequired] byte CurrentSlot, [property: JsonRequired] long CurrentSequence, [property: JsonRequired] long DefaultSequence, [property: JsonRequired] long InitializationSequence, [property: JsonRequired] int? RetiredOperation);

public sealed partial class EventEngine
{
    private void InitializeNativeDoorDefault(Fiber fiber)
    {
        Fiber initialization = fibers.Single(item => item.ObjectId == fiber.ObjectId && item.Initialization && item.Complete && item.Script.Id == program!.Objects[fiber.ObjectId].Scripts[0].Id);
        if (fiber.Script.Start >= fiber.Script.End || program!.Operations[fiber.Script.Start].Opcode != Jsm.Opcode.LBL)
        {
            throw new NotSupportedException("Native door default reset requires its original entry label");
        }
        //52C960 resets depth388 toFF, skips the default LBL and leaves the
        // live result bank produced by52C650 intact. No saved-result frame exists.
        fiber.Position = fiber.Script.Start + 1;
        fiber.NativeDoorDefaultFrame = true;
        foreach (var result in initialization.Results)
        {
            fiber.Results.Add(result.Key, result.Value);
        }
        actors[fiber.ObjectId].NativeDoorScheduler = new NativeDoorSchedulerState(1, 0, fiber.Sequence, fiber.Sequence, initialization.Sequence, null);
    }

    private NativeDoorSchedulerState RequireNativeDoorScheduler(int objectId)
    {
        return actors[objectId].NativeDoorScheduler is { Version: 1, CurrentSlot: <= 7 } scheduler ? scheduler : throw new NotSupportedException("Native door request requires its executed current372/default reset owner");
    }

    private string? UnsupportedNativeDoorSlotIssue(int objectId)
    {
        NativeDoorSchedulerState? scheduler = actors[objectId].NativeDoorScheduler;
        if (scheduler is not { Version: 1, CurrentSlot: <= 7 })
        {
            return "Native door request requires its executed52C960 current-slot owner; PC and operands preserved";
        }
        return scheduler.RetiredOperation is not null ? "Native door request after an empty-bank RET requires its consumed-frame memory aliases; PC and operands preserved" : null;
    }

    private void SelectNativeDoorRequest(Fiber requested)
    {
        NativeDoorSchedulerState scheduler = RequireNativeDoorScheduler(requested.ObjectId);
        byte slot = requested.NativeDoorSlot ?? throw new NotSupportedException("Native door request lost its physical slot");
        if (scheduler.RetiredOperation is not null)
        {
            throw new NotSupportedException("Native door selection after an empty-bank RET requires its consumed-frame memory aliases");
        }
        if (slot > scheduler.CurrentSlot)
        {
            Fiber previous = fibers.Single(item => item.Sequence == scheduler.CurrentSequence);
            SetNativeDoorLiveResults(requested, ReadNativeDoorLiveResults(previous));
            if (!previous.NativeResultFrameEntered && !previous.NativeDoorDefaultFrame)
            {
                previous.Results.Clear();
            }
            actors[requested.ObjectId].NativeDoorScheduler = scheduler with { CurrentSlot = slot, CurrentSequence = requested.Sequence };
        }
    }

    private Fiber SelectNativeDoorScheduledFiber(int objectId)
    {
        NativeDoorSchedulerState scheduler = RequireNativeDoorScheduler(objectId);
        Fiber selected = fibers.Single(item => item.Sequence == scheduler.CurrentSequence && item.ObjectId == objectId);
        if (scheduler.RetiredOperation is not null || selected.Complete)
        {
            // RET8 returns4 and leaves372/374 sticky when there is no lower slot.
            // Another fetch would pop an already consumed saved-result frame.
            throw new NotSupportedException("Native door dispatch after an empty-bank RET requires its consumed saved-result memory aliases");
        }
        if (NativeActorRequestSlot(selected) != scheduler.CurrentSlot)
        {
            throw new NotSupportedException("Native door current372 differs from its selected instruction owner");
        }
        return selected;
    }

    private void CompleteNativeDoorRequest(Fiber fiber, int returnSlot)
    {
        if (!IsNativeDoorRequest(fiber.ObjectId))
        {
            return;
        }
        NativeDoorSchedulerState scheduler = RequireNativeDoorScheduler(fiber.ObjectId);
        if (returnSlot != 8 || scheduler.CurrentSequence != fiber.Sequence || !fiber.NativeResultFrameEntered || fiber.NativeDoorDefaultFrame)
        {
            throw new NotSupportedException("Native door RET requires its selected requested context and saved-result frame");
        }
        Fiber? lower = fibers.Where(item => item.ObjectId == fiber.ObjectId && !item.Complete && item != fiber && NativeActorRequestSlot(item) < scheduler.CurrentSlot).OrderByDescending(NativeActorRequestSlot).FirstOrDefault();
        NativeScalarValue[] saved = fiber.NativeDoorSavedResults is { Length: 8 } bank ? bank : throw new NotSupportedException("Native door RET lost its executed LBL saved-result bank");
        //51C570 pushes R0..R7 ascending;51C5C0 writes R0..R7 from the
        // descending stack top, so the actual restored bank is reversed.
        SetNativeDoorLiveResults(lower ?? fiber, saved.Reverse().ToArray());
        actors[fiber.ObjectId].NativeDoorScheduler = lower is null ? scheduler with { RetiredOperation = fiber.Position - 1 } : scheduler with { CurrentSlot = (byte)NativeActorRequestSlot(lower), CurrentSequence = lower.Sequence, RetiredOperation = null };
    }

    private static NativeScalarValue[] ReadNativeDoorLiveResults(Fiber fiber)
    {
        return Enumerable.Range(0, 8).Select(index => fiber.Results.GetValueOrDefault(index) ?? NativeScalarValue.Exact(0)).ToArray();
    }

    private static void SetNativeDoorLiveResults(Fiber fiber, NativeScalarValue[] bank)
    {
        fiber.Results.Clear();
        for (int index = 0; index < 8; index++)
        {
            fiber.Results.Add(index, bank[index]);
        }
    }

    private void EnterNativeDoorResultFrame(Fiber fiber)
    {
        NativeDoorSchedulerState scheduler = RequireNativeDoorScheduler(fiber.ObjectId);
        if (scheduler.CurrentSequence != fiber.Sequence || fiber.NativeDoorDefaultFrame || fiber.NativeResultFrameEntered || fiber.NativeDoorSavedResults is not null)
        {
            throw new NotSupportedException("Native door LBL requires its selected unentered request context");
        }
        fiber.NativeDoorSavedResults = ReadNativeDoorLiveResults(fiber);
        fiber.Results.Clear();
    }

    private void ValidateNativeDoorSchedulers(Checkpoint saved, IReadOnlyList<Fiber> restored)
    {
        foreach (ActorState actor in saved.Actors)
        {
            if (saved.Scene.NativeDoorDriverVersion != 2 || !program!.Objects[actor.ObjectId].IsDoor)
            {
                if (actor.NativeDoorScheduler is not null)
                {
                    throw new InvalidDataException("Saved native door scheduler has no finalized original door allocation");
                }
                continue;
            }
            NativeDoorSchedulerState scheduler = actor.NativeDoorScheduler ?? throw new InvalidDataException("Saved door lacks its executed current372/default reset; recreate the authentic journal");
            Fiber? initialization = restored.SingleOrDefault(fiber => fiber.Sequence == scheduler.InitializationSequence);
            Fiber? root = restored.SingleOrDefault(fiber => fiber.Sequence == scheduler.DefaultSequence);
            Fiber? current = restored.SingleOrDefault(fiber => fiber.Sequence == scheduler.CurrentSequence);
            if (scheduler.Version != 1 || scheduler.CurrentSlot > 7 || initialization is null || !initialization.Initialization || !initialization.Complete || initialization.ObjectId != actor.ObjectId || initialization.Script.Id != program.Objects[actor.ObjectId].Scripts[0].Id || root is null || root.Initialization || root.Complete || root.ObjectId != actor.ObjectId || root.Sequence <= initialization.Sequence || !root.NativeDoorDefaultFrame || root.NativeResultFrameEntered || root.NativeDoorSlot is not null || root.NativeActorSlot is not null || root.Priority != 7 || root.Script.Id != program.Objects[actor.ObjectId].Scripts[1].Id || root.Position <= root.Script.Start || root.Position >= root.Script.End || current is null || current.ObjectId != actor.ObjectId || current.Initialization || NativeActorRequestSlot(current) != scheduler.CurrentSlot || current.Sequence < root.Sequence)
            {
                throw new InvalidDataException("Saved native door scheduler lost its initialization, default entry or selected physical slot");
            }
            if (scheduler.RetiredOperation is int retired)
            {
                if (!current.Complete || !current.NativeResultFrameEntered || current.NativeDoorDefaultFrame || retired != current.Position - 1 || retired < current.Script.Start || retired >= current.Script.End || program.Operations[retired].Opcode != Jsm.Opcode.RET || program.Operations[retired].Parameter != 8 || restored.Any(fiber => fiber.ObjectId == actor.ObjectId && !fiber.Complete))
                {
                    throw new InvalidDataException("Saved native door sticky current slot lacks its exact empty-bank RET owner");
                }
            }
            else if (current.Complete || root.Complete || restored.Any(fiber => fiber.ObjectId == actor.ObjectId && !fiber.Complete && NativeActorRequestSlot(fiber) > scheduler.CurrentSlot))
            {
                throw new InvalidDataException("Saved native door current372 is not its selected occupied request context");
            }
        }
        foreach (Fiber fiber in restored.Where(fiber => !fiber.Initialization && program!.Objects[fiber.ObjectId].IsDoor))
        {
            if (fiber.NativeDoorDefaultFrame)
            {
                if (fiber.NativeDoorSavedResults is not null)
                {
                    throw new InvalidDataException("Saved native default owns no LBL saved-result frame");
                }
            }
            else if (fiber.NativeResultFrameEntered ? fiber.NativeDoorSavedResults is not { Length: 8 } : fiber.NativeDoorSavedResults is not null || fiber.Results.Count != 0 && saved.Actors[fiber.ObjectId].NativeDoorScheduler?.CurrentSequence != fiber.Sequence)
            {
                throw new InvalidDataException("Saved door request lost its actual LBL bank or unentered current live-result ownership");
            }
        }
        if (restored.Any(fiber => fiber.NativeDoorSavedResults is not null && (fiber.Initialization || !program!.Objects[fiber.ObjectId].IsDoor || !fiber.NativeResultFrameEntered || fiber.NativeDoorDefaultFrame)))
        {
            throw new InvalidDataException("Saved door LBL bank belongs to an unproved context");
        }
        if (restored.Any(fiber => fiber.NativeDoorDefaultFrame && (!program!.Objects[fiber.ObjectId].IsDoor || saved.Actors[fiber.ObjectId].NativeDoorScheduler?.DefaultSequence != fiber.Sequence || fiber.Initialization || fiber.NativeResultFrameEntered)))
        {
            throw new InvalidDataException("Saved default-without-frame marker lacks its original52C960 door reset");
        }
    }
}
