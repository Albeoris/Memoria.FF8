using System.Numerics;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private const byte NativeDoorEntry = 1;
    private const byte NativeDoorFacing = 2;
    private const byte NativeDoorCrossing = 4;
    private const byte NativeDoorExit = 8;
    private const byte NativeDoorAction = 16;
    private void InitializeNativeDoorDriverAfterInitialization()
    {
        if (program is null || !program.Objects.Any(obj => obj.IsDoor) || Scene.NativeFieldInitialization is not { Stage: NativeFieldInitializationStage.Completed } proof || !proof.IsFrom(NativeRuntimeProfile, FieldId, programHash))
        {
            return;
        }
        if (Scene.NativeActorLayoutVersion != 1 || program.Objects.Where(obj => obj.IsDoor).Any(obj => !ValidNativeDoorState(actors[obj.Index], Frame)))
        {
            throw new NotSupportedException($"Fresh field{FieldId} door initialization does not match its executed original SETLINE owners.");
        }
        Scene.NativeDoorDriverVersion = 2;
        Scene.NativeDoorInitializationFrame = Frame;
        Scene.NativeDoorPendingEvents = [];
        foreach (ActorState line in actors.Where(actor => program.Objects[actor.ObjectId].IsDoor))
        {
            // Original52BC00 allocates zeroed door412/413 before SETLINE.
            line.NativeLineFacingDirection = 0;
            line.NativeLineFacingContact = false;
        }
    }

    private bool ValidNativeDoorState(ActorState line, long frame)
    {
        if (program is null || (uint)line.ObjectId >= program.Objects.Count || !program.Objects[line.ObjectId].IsDoor)
        {
            return false;
        }
        if (line.LineOperation == -1 && line.NativeSetLineReceipt is null && line.LineStart is null && line.LineEnd is null)
        {
            return !line.LineEnabled && !line.LineContact && !line.NativeLineCrossingPending && line.NativeLineContactVersion == 0;
        }
        return ValidNativeSetLine(line, frame);
    }

    private IReadOnlyList<NativeLineContactChange> PrepareNativeDoorMovement(ActorState player, FieldFixedPosition proposal, int direction)
    {
        ValidatePendingNativeDoorEvents();
        FieldFixedPosition raw = ReadNativePartyPlayerPosition(player);
        Vector3 current = new(raw.X >> 12, raw.Y >> 12, raw.Z >> 12);
        Vector3 proposed = new(proposal.X >> 12, proposal.Y >> 12, raw.Z >> 12);
        int radius = (ushort)player.PushRadius;
        if ((long)radius * radius > int.MaxValue)
        {
            throw new NotSupportedException($"Native field{FieldId} door radius exceeds the bounded signed distance comparison; original movement preserved.");
        }
        List<NativeLineContactChange> changes = [];
        foreach (ActorState line in actors.Where(line => program!.Objects[line.ObjectId].IsDoor && line.LineEnabled))
        {
            if (!ValidNativeDoorState(line, Frame) || line.NativeLineContactVersion == 1)
            {
                throw new NotSupportedException($"Native field{FieldId} door state lacks its fresh original endpoints and event schema; original movement preserved.");
            }
            int distance = NativeInfDistance(line.LineStart!.Value, line.LineEnd!.Value, proposed, out Vector3 closest);
            if (distance < -1)
            {
                throw new NotSupportedException($"Native field{FieldId} door distance overflow remains unsupported; original movement preserved.");
            }
            bool contact = distance != -1 && distance < radius * radius;
            bool facing = contact && (current.X == closest.X && current.Y == closest.Y || InfFacingContact(current, closest, direction));
            bool crossing = contact && NativeInfCrosses(line.LineStart.Value, line.LineEnd.Value, current, proposed);
            byte events = 0;
            if (contact && !line.LineContact)
            {
                events |= NativeDoorEntry;
            }
            if (facing)
            {
                events |= NativeDoorFacing;
            }
            if (crossing)
            {
                events |= NativeDoorCrossing;
            }
            if (!contact && line.LineContact)
            {
                events |= NativeDoorExit;
            }
            ValidateNativeDoorAdmission(line.ObjectId, (byte)(events | Scene.NativeDoorPendingEvents!.GetValueOrDefault(line.ObjectId)), contact);
            byte? facingDirection = !contact || current.X == closest.X && current.Y == closest.Y ? line.NativeLineFacingDirection : (byte)NativePartyDirection((int)current.X, (int)current.Y, (int)closest.X, (int)closest.Y);
            changes.Add(new NativeLineContactChange(line.ObjectId, contact, crossing, new Vector3(proposal.X / 4096f, proposal.Y / 4096f, proposal.Z / 4096f), events, facingDirection, facing));
        }
        return changes;
    }

    private void CommitNativeDoorMovement(IReadOnlyList<NativeLineContactChange> changes)
    {
        foreach (NativeLineContactChange change in changes)
        {
            ActorState line = actors[change.ObjectId];
            line.NativeLineContactVersion = 2;
            line.LineContact = change.Contact;
            line.NativeLineFacingDirection = change.FacingDirection;
            line.NativeLineFacingContact = change.FacingContact;
            byte pending = (byte)(Scene.NativeDoorPendingEvents!.GetValueOrDefault(line.ObjectId) | change.PendingEvents);
            if (pending != 0)
            {
                Scene.NativeDoorPendingEvents![line.ObjectId] = pending;
            }
            line.NativeLineCrossingPending = (pending & NativeDoorCrossing) != 0;
            if (change.Crossing)
            {
                LineCrossing crossing = new(FieldId, line.ObjectId, program!.Objects[line.ObjectId].Scripts[4].Id, line.LineOperation, Frame, change.Position, activeActionIndex ?? actionLog.Count);
                lineCrossings.Add(crossing);
                LineCrossed?.Invoke(crossing);
            }
        }
    }

    private void ValidatePendingNativeDoorEvents()
    {
        if (Scene.NativeDoorPendingEvents is null || Scene.NativeFieldInitialization is { } proof && (proof.Stage != NativeFieldInitializationStage.Completed || !proof.IsFrom(NativeRuntimeProfile, FieldId, programHash)))
        {
            throw new NotSupportedException("Native door V2 requires its original source initialization and preserved pending events; original state preserved.");
        }
        foreach (ActorState line in actors.Where(line => program!.Objects[line.ObjectId].IsDoor))
        {
            ValidateNativeDoorAdmission(line.ObjectId, Scene.NativeDoorPendingEvents.GetValueOrDefault(line.ObjectId), line.LineContact);
        }
    }

    private void ValidateNativeDoorAdmission(int objectId, byte events, bool contact)
    {
        ActorState line = actors[objectId];
        if (!ValidNativeDoorState(line, Frame) || (events & ~31) != 0)
        {
            throw new NotSupportedException("Native door V2 pending event lacks its original owner or byte flags; original state preserved.");
        }
        Fiber[] active = fibers.Where(fiber => fiber.ObjectId == objectId && !fiber.Complete).ToArray();
        if (active.GroupBy(fiber => fiber.Priority).Any(slot => slot.Count() > 1))
        {
            throw new NotSupportedException("Native door request slots cannot contain multiple unfinished owners; original state preserved.");
        }
        foreach (Fiber fiber in active)
        {
            int local = NativeDoorLocalFromManagedPriority(fiber.Priority);
            bool knownDefault = local == 1 && fiber.NativeDoorSlot is null;
            bool knownRequest = local is >= 2 and <= 7 && fiber.NativeDoorSlot == 7 - fiber.Priority;
            if (fiber.Initialization || !knownDefault && !knownRequest || (uint)local >= program!.Objects[objectId].Scripts.Count || fiber.Script.Id != program.Objects[objectId].Scripts[local].Id)
            {
                throw new NotSupportedException("Native door V2 cannot suspend an external raw-priority request or unknown operand stack; original state preserved.");
            }
        }
        int[] requested = NativeDoorRequestedLocals(events, contact).ToArray();
        if (requested.Any(local => local >= program!.Objects[objectId].Scripts.Count))
        {
            throw new NotSupportedException("Reached native door event has no declared original local script; original state preserved.");
        }
        int[] meaningful = active.Select(fiber => NativeDoorLocalFromManagedPriority(fiber.Priority)).Where(local => !EmptyNativeLineScript(line, local)).Concat(requested.Where(local => !EmptyNativeLineScript(line, local))).Distinct().ToArray();
        if (meaningful.Length > 1)
        {
            ValidateNativeDoorSuspension(line, active, requested);
        }
    }

    private void ValidateNativeDoorSuspension(ActorState line, IReadOnlyList<Fiber> active, IReadOnlyList<int> requested)
    {
        int[] locals = active.Select(fiber => NativeDoorLocalFromManagedPriority(fiber.Priority)).Concat(requested).Distinct().ToArray();
        foreach (int local in locals)
        {
            EventScript script = program!.Objects[line.ObjectId].Scripts[local];
            if (program.Operations.Skip(script.Start).Take(script.End - script.Start).Any(operation => operation.Opcode == Albeoris.Games.FF8.Jsm.Jsm.Opcode.RET && operation.Parameter != 8))
            {
                throw new NotSupportedException("Native door competition requires proved RET8 slot resumption; explicit RET slot cancellation preserves the original requests.");
            }
        }
        int newlyRequested = requested.Distinct().Count(local => !active.Any(fiber => NativeDoorLocalFromManagedPriority(fiber.Priority) == local));
        // The native entity has eighty DWORD stack entries before its result bank.
        // Each started LBL saves eight results separately from ordinary operands.
        int stackWords = active.Sum(fiber => fiber.Stack.Count + 8) + newlyRequested * 8;
        if (stackWords > 80 || active.Any(fiber => fiber.Results.Keys.Any(register => register is < 0 or > 7)))
        {
            throw new NotSupportedException("Native door suspension exceeds the original operand/result stack or its eight result registers; original state preserved.");
        }
        // Native529FF0 saves PC374/depth388 for the current slot before selecting
        // the higher direct slot. Native51C570 LBL saves and clears its eight
        // result registers;51C5C0 RET8 restores the next occupied lower slot.
        // Distinct managed fibers already retain those PC, operand and result
        // banks, including unfinished expressions across the sixteen-op budget.
    }

    private void ValidateNativeDoorSuspensionCheckpoint(Checkpoint saved, IReadOnlyList<Fiber> restored)
    {
        if (saved.Scene.NativeDoorDriverVersion != 2)
        {
            return;
        }
        foreach (ActorState line in saved.Actors.Where(line => program!.Objects[line.ObjectId].IsDoor))
        {
            Fiber[] active = restored.Where(fiber => fiber.ObjectId == line.ObjectId && !fiber.Complete).ToArray();
            if (active.GroupBy(fiber => fiber.Priority).Any(slot => slot.Count() > 1))
            {
                throw new InvalidDataException("Saved native door request slot has multiple unfinished owners.");
            }
            int[] requested = NativeDoorRequestedLocals(saved.Scene.NativeDoorPendingEvents!.GetValueOrDefault(line.ObjectId), line.LineContact).ToArray();
            if (requested.Any(local => local >= program!.Objects[line.ObjectId].Scripts.Count))
            {
                throw new InvalidDataException("Saved native door request has no original local script.");
            }
            int[] meaningful = active.Select(fiber => NativeDoorLocalFromManagedPriority(fiber.Priority)).Concat(requested).Distinct().Where(local => !EmptyNativeLineScript(line, local)).ToArray();
            if (meaningful.Length > 1)
            {
                ValidateNativeDoorSuspension(line, active, requested);
            }
        }
    }

    private static IEnumerable<int> NativeDoorRequestedLocals(byte events, bool contact)
    {
        if ((events & NativeDoorAction) != 0)
        {
            yield return 2;
        }
        if ((events & NativeDoorFacing) != 0)
        {
            yield return 3;
        }
        if ((events & NativeDoorCrossing) != 0)
        {
            yield return 4;
        }
        if (contact)
        {
            yield return 5;
        }
        if ((events & NativeDoorExit) != 0)
        {
            yield return 6;
        }
        if ((events & NativeDoorEntry) != 0)
        {
            yield return 7;
        }
    }

    // Original direct slots run from6(facing) down to2(entry). This private adapter
    // reverses them for the existing managed ordering; raw JSM request priorities are unchanged.
    private static int NativeDoorManagedPriority(int local) => local - 2;
    private static int NativeDoorLocalFromManagedPriority(int priority) => priority == 7 ? 1 : priority is >= 0 and <= 5 ? priority + 2 : -1;

    private void SampleNativeDoorActionEdges()
    {
        if (Scene.NativeDoorDriverVersion != 2 || Scene.ButtonInput is not { Pressed: var pressed } || (pressed & 0x40) == 0 || !IsUserControlEnabled || ResolvePartyActor(0) is not ActorState player || player.NativeScriptMovement is { Active: true } || player.NativeMotionAuthority is not { Version: 1 } motion)
        {
            return;
        }
        foreach (ActorState line in actors.Where(actor => program!.Objects[actor.ObjectId].IsDoor && actor.LineEnabled && actor.NativeLineFacingContact == true))
        {
            byte direction = line.NativeLineFacingDirection ?? throw new NotSupportedException("Native door action requires its retained original direction412.");
            if (unchecked((byte)(direction - motion.MovementDirection + 32)) >= 64)
            {
                continue;
            }
            byte pending = (byte)(Scene.NativeDoorPendingEvents!.GetValueOrDefault(line.ObjectId) | NativeDoorAction);
            ValidateNativeDoorAdmission(line.ObjectId, pending, line.LineContact);
            Scene.NativeDoorPendingEvents![line.ObjectId] = pending;
        }
    }

    private void DispatchNativeDoorEvents(ActorState line)
    {
        if (!program!.Objects[line.ObjectId].IsDoor)
        {
            return;
        }
        byte events = Scene.NativeDoorPendingEvents!.GetValueOrDefault(line.ObjectId);
        if (line.LineEnabled && IsUserControlEnabled)
        {
            ValidateNativeDoorAdmission(line.ObjectId, events, line.LineContact);
            foreach (int local in NativeDoorRequestedLocals(events, line.LineContact))
            {
                int priority = NativeDoorManagedPriority(local);
                if (!fibers.Any(fiber => fiber.ObjectId == line.ObjectId && fiber.Priority == priority && !fiber.Complete))
                {
                    Fiber requested = Queue(line.ObjectId, program.Objects[line.ObjectId].Scripts[local].Id, priority, false);
                    requested.NativeDoorSlot = (byte)(7 - priority);
                }
            }
        }
        Scene.NativeDoorPendingEvents!.Remove(line.ObjectId);
        line.NativeLineCrossingPending = false;
    }

    private bool RunNativeDoorScheduler(int objectId)
    {
        if (Scene.NativeDoorDriverVersion != 2 || !program!.Objects[objectId].IsDoor)
        {
            return false;
        }
        int remaining = 16;
        while (remaining > 0)
        {
            Fiber? fiber = fibers.Where(item => item.ObjectId == objectId && !item.Complete).OrderBy(item => item.Priority).ThenBy(item => item.Sequence).FirstOrDefault();
            if (fiber is null)
            {
                break;
            }
            remaining -= Run(fiber, remaining);
            if (!fiber.Complete || transitionRequested)
            {
                break;
            }
        }
        return true;
    }

    private void ValidateNativeDoorCheckpoint(Checkpoint saved)
    {
        foreach (ActorState line in saved.Actors.Where(actor => actor.NativeSetLineReceipt is not null))
        {
            if (!ValidNativeSetLine(line, saved.Frame))
            {
                throw new InvalidDataException("Saved SETLINE execution receipt does not match its original owner, source, command or signed endpoints.");
            }
        }
        if (saved.Scene.NativeDoorDriverVersion is null)
        {
            if (saved.Scene.NativeDoorPendingEvents is not null || saved.Scene.NativeDoorInitializationFrame is not null || saved.Actors.Any(actor => actor.NativeLineContactVersion == 2) || saved.Fibers.Any(fiber => fiber.NativeDoorSlot is not null) || saved.Scene.NativeFieldInitialization is { Stage: NativeFieldInitializationStage.Completed } && program!.Objects.Any(obj => obj.IsDoor))
            {
                throw new InvalidDataException("Unknown legacy door driver cannot contain V2 pending events or contact state.");
            }
            return;
        }
        if (saved.Scene.NativeDoorDriverVersion != 2 || !program!.Objects.Any(obj => obj.IsDoor) || !saved.DefaultsStarted || saved.Scene.NativeActorLayoutVersion != 1 || saved.Scene.NativeFieldInitialization is { } proof && (proof.Stage != NativeFieldInitializationStage.Completed || !proof.IsFrom(NativeRuntimeProfile, saved.FieldId, saved.ProgramHash)) || saved.Scene.NativeDoorPendingEvents is null || saved.Scene.NativeDoorInitializationFrame is not long initializationFrame || initializationFrame <= 0 || initializationFrame > saved.Frame)
        {
            throw new InvalidDataException("Native door V2 checkpoint lacks its original initialization source, declared layout or event records.");
        }
        foreach (ActorState line in saved.Actors.Where(line => program!.Objects[line.ObjectId].IsDoor))
        {
            byte events = saved.Scene.NativeDoorPendingEvents.GetValueOrDefault(line.ObjectId);
            if (!ValidNativeDoorState(line, saved.Frame) || line.NativeLineContactVersion == 1 || (events & ~31) != 0 || events != 0 && line.NativeLineContactVersion != 2 || line.NativeLineCrossingPending != ((events & NativeDoorCrossing) != 0) || line.NativeLineFacingContact == true && line.NativeLineFacingDirection is null)
            {
                throw new InvalidDataException("Native door V2 saved owner, contact schema or crossing mirror does not match its pending flags.");
            }
        }
        if (saved.Scene.NativeDoorPendingEvents.Any(pair => pair.Value == 0 || !saved.Actors.Any(line => line.ObjectId == pair.Key && ValidNativeDoorState(line, saved.Frame))))
        {
            throw new InvalidDataException("Native door V2 pending event names an unknown original door.");
        }
        foreach (FiberCheckpoint fiber in saved.Fibers.Where(fiber => !fiber.Complete && program!.Objects[fiber.ObjectId].IsDoor))
        {
            int local = NativeDoorLocalFromManagedPriority(fiber.Priority);
            bool knownDefault = local == 1 && fiber.NativeDoorSlot is null;
            bool knownRequest = local is >= 2 and <= 7 && fiber.NativeDoorSlot == 7 - fiber.Priority;
            if (fiber.Initialization || !knownDefault && !knownRequest || (uint)local >= program!.Objects[fiber.ObjectId].Scripts.Count || fiber.ScriptId != program.Objects[fiber.ObjectId].Scripts[local].Id)
            {
                throw new InvalidDataException("Native door V2 saved fiber has an external raw priority or unproved script owner.");
            }
        }
        if (saved.Fibers.Any(fiber => fiber.NativeDoorSlot is not null && (!program!.Objects[fiber.ObjectId].IsDoor || fiber.NativeDoorSlot is < 2 or > 7)))
        {
            throw new InvalidDataException("Native door V2 direct slot cannot belong to a regular actor or an unproved action/default slot.");
        }
    }
}
