using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed record NativePersistentPredicateRead(string Opcode, int Address, int Width);
public sealed record NativePlayerAbsenceReceipt(int Version, int FieldId, string ProgramHash, ImmutableArray<NativePersistentPredicateRead> Reads, string Fingerprint);

public sealed partial class EventEngine
{
    /// <summary>Certifies an initialized allocation whose remaining native scripts cannot publish a playable body or another scene owner.</summary>
    public NativePlayerAbsenceReceipt? InspectNativePlayerAbsence()
    {
        if (program is null || NativeRuntimeProfile is not { IsVerified: true } || Scene.NativeFieldInitialization is not { Stage: NativeFieldInitializationStage.Completed } initialization || !initialization.IsFrom(NativeRuntimeProfile, FieldId, programHash) || !defaultsStarted || !userControlEnabled || userControlSequenceDisabled || transitionRequested || pendingArrival is not null || pendingNativePlayerInput is not null || LastFault is not null || dialogues.Count != 0 || services.IsMoviePlaying || HasPendingBattle || PendingTripleTriad is not null || partyMenu is not null || partyJoin is not null || partySplit is not null || Scene.NativeTutorial is not null || Scene.NativeMenuTutorial is not null || Scene.WorldTransitionRequest is not null)
        {
            return null;
        }
        if (Scene.NativeActorLayoutVersion != 1 || Scene.NativeFieldLoadVersion != 1 || Scene.NativePartyTraceVersion != 0 || Scene.PartyTrace is not null || Scene.NativePlayerPosition is not null || Scene.PartyBindingMemory is not { Version: 1, First: 255, Second: 255, Third: 255 } || Scene.FieldPartyNativeActorIds.Count != 3 || Scene.FieldPartyNativeActorIds.Any(binding => binding != 255) || Scene.NativeActorObjectIds.Count == 0 || Scene.NativeActorObjectIds.Distinct().Count() != Scene.NativeActorObjectIds.Count || Scene.NativeActorObjectIds.Any(id => (uint)id >= actors.Count || !actors[id].Active || unchecked((byte)actors[id].CharacterId) != 254))
        {
            return null;
        }
        if (program.Objects.Any(obj => !fibers.Any(fiber => fiber.ObjectId == obj.Index && fiber.Initialization && fiber.Script.Id == obj.Scripts[0].Id && fiber.Complete)) || fibers.Any(fiber => fiber.Initialization && !fiber.Complete))
        {
            return null;
        }
        if (services.WorldControls is not NativeWorldControlState controls || (controls.EventFlags104 & 0x40) != 0 || actors.Any(actor => actor.NativePushContactPending || actor.DoorLineEntryPending || actor.NativeLineCrossingPending || actor.LineContact || actor.DoorLineContact || actor.NativeLineFacingContact == true) || Scene.NativeDoorPendingEvents?.Values.Any(events => events != 0) == true)
        {
            return null;
        }
        if (services.SeeD is NativeSeeDState seeD && ((controls.EventFlags104 & 8) == 0 && services.CharacterVitals is not { DreamEnabled: true } || unchecked((short)seeD.SalaryDisplayCountdown184) >= 0 || services.SalaryPresentation is not { Phase: 9 } || Scene.NativeSalaryMovementUnits != 0))
        {
            return null;
        }
        List<(int ObjectId, EventScript Script, int Position)> roots = [];
        foreach (Fiber fiber in fibers.Where(fiber => !fiber.Complete))
        {
            if (!fiber.Started || !AbsenceWaitHasNativeOwner(fiber))
            {
                return null;
            }
            if (fiber.WaitReason != WaitReason.NativeHalt)
            {
                roots.Add((fiber.ObjectId, fiber.Script, fiber.Position));
            }
        }
        if (!AbsenceClosureIsPassive(roots))
        {
            return null;
        }
        ImmutableArray<NativePersistentPredicateRead>? reads = ReadAbsenceInitializationGuards();
        if (reads is null)
        {
            return null;
        }
        string? fingerprint = ReadPersistentPredicateFingerprint(reads.Value);
        return fingerprint is null ? null : new NativePlayerAbsenceReceipt(1, FieldId, programHash, reads.Value, fingerprint);
    }

    /// <summary>Reads portable persistent inputs without executing bytecode, creating an owner, or including field-local binding identity.</summary>
    public string? ReadPersistentPredicateFingerprint(IReadOnlyList<NativePersistentPredicateRead> reads)
    {
        ArgumentNullException.ThrowIfNull(reads);
        StringBuilder identity = new("native-persistent-predicate-v2|party:");
        identity.AppendJoin(',', Scene.Party);
        identity.Append("|dream:").Append(services.CharacterVitals is { } vitals ? vitals.DreamEnabled ? "1" : "0" : "?");
        foreach (NativePersistentPredicateRead descriptor in reads.Distinct().OrderBy(read => read.Address).ThenBy(read => read.Width).ThenBy(read => read.Opcode, StringComparer.Ordinal))
        {
            if (!TryPersistentPredicateOperation(descriptor, out JsmOperation? operation))
            {
                return null;
            }
            NativeScalarValue value = ReadEventMemory(operation!);
            if (!value.IsExact)
            {
                return null;
            }
            identity.Append('|').Append(descriptor.Opcode).Append(':').Append(descriptor.Address).Append('/').Append(descriptor.Width).Append('=').Append(value.RequireExact());
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity.ToString())));
    }

    private bool TryPersistentPredicateOperation(NativePersistentPredicateRead descriptor, out JsmOperation? operation)
    {
        operation = null;
        if (descriptor is null || !Enum.TryParse(descriptor.Opcode, out Jsm.Opcode opcode) || !IsAbsenceMemoryRead(opcode) || descriptor.Width != NativeMemoryWidth(opcode) || descriptor.Address < 0 || descriptor.Address > NativeEventMemory.MaximumAddress || (long)descriptor.Address + descriptor.Width > ushort.MaxValue + 1L || descriptor.Address < 1280 && descriptor.Address + descriptor.Width > 1024)
        {
            return false;
        }
        NativeEventMemoryOwner owner = ResolveMemoryOwner(descriptor.Address, descriptor.Width);
        bool canonical = owner == NativeEventMemoryOwner.WorldEntry || owner == NativeEventMemoryOwner.WorldControls && descriptor.Address == 104 && descriptor.Width == 4 || owner == NativeEventMemoryOwner.SeeD && descriptor.Address == 184 && descriptor.Width == 2;
        if (!canonical && (owner != NativeEventMemoryOwner.Generic || descriptor.Address < 256 || !HasExactPredicateBytes(EventMemory.Cells, descriptor.Address, descriptor.Width)))
        {
            return false;
        }
        // Decode a typed read through the existing bytecode decoder; this never enters the VM or changes its program.
        byte[] bytes = new byte[12];
        bytes[6] = 8;
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), (int)opcode << 24 | descriptor.Address);
        operation = JsmDocument.ReadOperations(bytes)[0];
        return true;
    }

    private bool AbsenceWaitHasNativeOwner(Fiber fiber)
    {
        ActorState actor = actors[fiber.ObjectId];
        return fiber.WaitReason switch
        {
            WaitReason.None => true,
            WaitReason.Frames => fiber.WaitUntil >= 0,
            WaitReason.NativeHalt => fiber.Script.ContainsOperation(fiber.Position) && program!.Operations[fiber.Position].Opcode == Jsm.Opcode.HALT,
            WaitReason.NativeAnimationBlocking or WaitReason.NativeAnimationSync => actor.NativeAnimationHeader is { Version: 1, Flags: not null } && AnimationFrameCount?.Invoke(actor.RenderModelId, actor.AnimationId) is > 0,
            WaitReason.NativeScriptMovement => actor.NativeScriptMovement is { Active: true } movement && movement.FiberSequence == fiber.Sequence && movement.ScriptId == fiber.Script.Id && movement.Operation == fiber.Position && fiber.NativeMoveOperation == movement.Operation && actor.NativeMovementCompletion542 is 0 or 1,
            WaitReason.RequestStart or WaitReason.RequestEnd => fiber.Request is not null && fibers.Contains(fiber.Request),
            _ => false
        };
    }

    private bool AbsenceClosureIsPassive(IEnumerable<(int ObjectId, EventScript Script, int Position)> roots)
    {
        (int ObjectId, EventScript Script, int Position)[] entries = roots.ToArray();
        Queue<(int ObjectId, EventScript Script, int Position)> pending = new(entries);
        HashSet<(int ObjectId, int ScriptId, int Position)> seen = [];
        while (pending.TryDequeue(out var current))
        {
            if (!seen.Add((current.ObjectId, current.Script.Id, current.Position)))
            {
                continue;
            }
            if (!current.Script.ContainsOperation(current.Position))
            {
                return false;
            }
            JsmOperation operation = program!.Operations[current.Position];
            if (operation.Opcode == Jsm.Opcode.HALT)
            {
                continue;
            }
            if (operation.Opcode == Jsm.Opcode.RET)
            {
                // A native return can resume another physical slot or expose sticky/default-frame aliases.
                return false;
            }
            if (IsAbsenceRequest(operation.Opcode))
            {
                if (entries.Any(entry => entry.ObjectId == current.ObjectId && entry.Script.Id == current.Script.Id && entry.Position >= current.Position - 1 && entry.Position <= current.Position) || !TryAbsenceRequestTargets(current.Script, current.Position, out EventObject[] targets, out int selector, out bool partyRequest))
                {
                    return false;
                }
                foreach (EventObject target in targets)
                {
                    EventScript? requested;
                    if (partyRequest)
                    {
                        try
                        {
                            requested = ReadNativePartyRequestPointer(target.Index, selector, Scene).Script;
                        }
                        catch (NotSupportedException)
                        {
                            return false;
                        }
                    }
                    else
                    {
                        requested = target.Scripts.SingleOrDefault(script => script.Id == unchecked((ushort)selector));
                    }
                    if (requested is null)
                    {
                        return false;
                    }
                    pending.Enqueue((target.Index, requested, requested.Start));
                }
            }
            else if (!IsAbsencePassiveOperation(operation))
            {
                return false;
            }
            if (operation.Opcode is Jsm.Opcode.JMP or Jsm.Opcode.JPF)
            {
                pending.Enqueue((current.ObjectId, current.Script, JsmProgram.JumpTarget(current.Position, operation.Parameter)));
            }
            if (operation.Opcode != Jsm.Opcode.JMP)
            {
                pending.Enqueue((current.ObjectId, current.Script, current.Position + 1));
            }
        }
        return true;
    }

    private bool IsAbsencePassiveOperation(JsmOperation operation)
    {
        if (IsAbsenceMemoryRead(operation.Opcode))
        {
            int width = NativeMemoryWidth(operation.Opcode);
            NativeEventMemoryOwner owner = ResolveMemoryOwner(operation.Parameter, width);
            // Completed native field initialization proves the1024..1279 reset bank, including its implicit zero bytes.
            bool resetLocal = operation.Parameter >= 1024 && operation.Parameter + width <= 1280;
            return owner == NativeEventMemoryOwner.Generic && (resetLocal || HasExactPredicateBytes(EventMemory.Cells, operation.Parameter, width));
        }
        if (operation.Opcode is Jsm.Opcode.POPM_B or Jsm.Opcode.POPM_W or Jsm.Opcode.POPM_L)
        {
            int width = NativeMemoryWidth(operation.Opcode);
            return operation.Parameter >= 1024 && operation.Parameter + width <= 1280 && ResolveMemoryOwner(operation.Parameter, width) == NativeEventMemoryOwner.Generic;
        }
        if (operation.Opcode == Jsm.Opcode.CAL)
        {
            return operation.Parameter is 0 or 1 or 2 or 5 or 6 or 7 or 8 or 9 or 10 or 11 or 12 or 13 or 14 or 16 or 17;
        }
        return operation.Opcode is Jsm.Opcode.LBL or Jsm.Opcode.NOP or Jsm.Opcode.PSHN_L or Jsm.Opcode.PSHI_L or Jsm.Opcode.POPI_L or Jsm.Opcode.JMP or Jsm.Opcode.JPF or Jsm.Opcode.WAIT or Jsm.Opcode.MSPEED or Jsm.Opcode.MOVE or Jsm.Opcode.ANIMEKEEP or Jsm.Opcode.RCANIMEKEEP or Jsm.Opcode.CANIMEKEEP or Jsm.Opcode.ANIMESYNC;
    }

    private bool TryAbsenceRequestTargets(EventScript script, int index, out EventObject[] targets, out int selector, out bool partyRequest)
    {
        targets = [];
        selector = 0;
        JsmOperation operation = program!.Operations[index];
        partyRequest = operation.Opcode is Jsm.Opcode.PREQ or Jsm.Opcode.PREQSW or Jsm.Opcode.PREQEW;
        if (index < script.Start + 2 || program.Operations[index - 1].Opcode != Jsm.Opcode.PSHN_L || program.Operations[index - 2].Opcode != Jsm.Opcode.PSHN_L || script.ReachableOperations.Any(position => program.Operations[position].Opcode is Jsm.Opcode.JMP or Jsm.Opcode.JPF && JsmProgram.JumpTarget(position, program.Operations[position].Parameter) >= index - 1 && JsmProgram.JumpTarget(position, program.Operations[position].Parameter) <= index))
        {
            return false;
        }
        selector = program.Operations[index - 1].Parameter;
        if (partyRequest)
        {
            if ((uint)operation.Parameter >= 3)
            {
                return false;
            }
            targets = program.NativeObjects.Take(program.NativeActorCount).Select(id => program.Objects[id]).ToArray();
        }
        else
        {
            if ((uint)operation.Parameter >= program.NativeObjects.Count)
            {
                return false;
            }
            targets = [program.Objects[program.NativeObjects[operation.Parameter]]];
        }
        int priority = program.Operations[index - 2].Parameter;
        return targets.Length != 0 && targets.All(target => UnsupportedRequestSlot(target.Index, priority, operation.Opcode, out _, out _) is null);
    }

    private ImmutableArray<NativePersistentPredicateRead>? ReadAbsenceInitializationGuards()
    {
        List<NativePersistentPredicateRead> reads = [new("PSHM_L", 104, 4)];
        if (services.SeeD is not null)
        {
            reads.Add(new("PSHSM_W", 184, 2));
        }
        foreach (EventObject obj in program!.Objects)
        {
            EventScript init = obj.Scripts[0];
            if (!init.ReachableOperations.Any(index => program.Operations[index].Opcode == Jsm.Opcode.SETPC))
            {
                continue;
            }
            foreach (int index in init.ReachableOperations)
            {
                JsmOperation operation = program.Operations[index];
                if (IsAbsenceMemoryRead(operation.Opcode))
                {
                    if (IsAbsenceLocalCounterUpdate(init, index))
                    {
                        continue;
                    }
                    NativePersistentPredicateRead descriptor = new(operation.Opcode.ToString(), operation.Parameter, NativeMemoryWidth(operation.Opcode));
                    if (!TryPersistentPredicateOperation(descriptor, out _))
                    {
                        return null;
                    }
                    reads.Add(descriptor);
                }
                else if (operation.Opcode is Jsm.Opcode.POPM_B or Jsm.Opcode.POPM_W or Jsm.Opcode.POPM_L)
                {
                    if (!IsAbsenceLocalCounterUpdate(init, index - 3))
                    {
                        return null;
                    }
                }
                else if (operation.Opcode is not (Jsm.Opcode.LBL or Jsm.Opcode.NOP or Jsm.Opcode.RET or Jsm.Opcode.PSHN_L or Jsm.Opcode.PSHI_L or Jsm.Opcode.CAL or Jsm.Opcode.JMP or Jsm.Opcode.JPF or Jsm.Opcode.SETMODEL or Jsm.Opcode.BASEANIME or Jsm.Opcode.SETPC or Jsm.Opcode.ISPARTY or Jsm.Opcode.UNUSE))
                {
                    return null;
                }
            }
        }
        ImmutableArray<NativePersistentPredicateRead> result = reads.Distinct().OrderBy(read => read.Address).ThenBy(read => read.Width).ThenBy(read => read.Opcode, StringComparer.Ordinal).ToImmutableArray();
        return AbsenceInitializationPreservesGuards(result) ? result : null;
    }

    private bool AbsenceInitializationPreservesGuards(ImmutableArray<NativePersistentPredicateRead> reads)
    {
        foreach (EventObject obj in program!.Objects)
        {
            foreach (int index in obj.Scripts[0].ReachableOperations)
            {
                JsmOperation operation = program.Operations[index];
                if (operation.Opcode is Jsm.Opcode.POPM_B or Jsm.Opcode.POPM_W or Jsm.Opcode.POPM_L)
                {
                    int width = NativeMemoryWidth(operation.Opcode);
                    if (reads.Any(read => operation.Parameter < read.Address + read.Width && read.Address < operation.Parameter + width))
                    {
                        return false;
                    }
                }
                else if (!IsAbsenceMemoryRead(operation.Opcode) && operation.Opcode is not (Jsm.Opcode.LBL or Jsm.Opcode.NOP or Jsm.Opcode.RET or Jsm.Opcode.PSHN_L or Jsm.Opcode.PSHI_L or Jsm.Opcode.CAL or Jsm.Opcode.JMP or Jsm.Opcode.JPF or Jsm.Opcode.SETMODEL or Jsm.Opcode.BASEANIME or Jsm.Opcode.SETPC or Jsm.Opcode.ISPARTY or Jsm.Opcode.UNUSE or Jsm.Opcode.POLYCOLORALL or Jsm.Opcode.FADEIN or Jsm.Opcode.MUSICVOLTRANS or Jsm.Opcode.EFFECTLOAD or Jsm.Opcode.SET3 or Jsm.Opcode.SETLINE or Jsm.Opcode.RND or Jsm.Opcode.HIDE or Jsm.Opcode.DIR or Jsm.Opcode.THROUGHOFF or Jsm.Opcode.TALKON or Jsm.Opcode.PUSHRADIUS or Jsm.Opcode.SESTOP or Jsm.Opcode.SETPLACE or Jsm.Opcode.THROUGHON))
                {
                    // Requests, indirect publishers and unknown native effects cannot be omitted from an entry guard proof.
                    return false;
                }
            }
        }
        return true;
    }

    private bool IsAbsenceLocalCounterUpdate(EventScript script, int index)
    {
        if (index < script.Start || index + 3 >= program!.Operations.Count || Enumerable.Range(index, 4).Any(position => !script.ContainsOperation(position)))
        {
            return false;
        }
        JsmOperation read = program.Operations[index];
        JsmOperation literal = program.Operations[index + 1];
        JsmOperation calculation = program.Operations[index + 2];
        JsmOperation store = program.Operations[index + 3];
        Jsm.Opcode? matchingStore = read.Opcode switch
        {
            Jsm.Opcode.PSHM_B or Jsm.Opcode.PSHSM_B => Jsm.Opcode.POPM_B,
            Jsm.Opcode.PSHM_W or Jsm.Opcode.PSHSM_W => Jsm.Opcode.POPM_W,
            Jsm.Opcode.PSHM_L or Jsm.Opcode.PSHSM_L => Jsm.Opcode.POPM_L,
            _ => null
        };
        return matchingStore is not null && read.Parameter >= 1024 && read.Parameter + NativeMemoryWidth(read.Opcode) <= 1280 && literal.Opcode == Jsm.Opcode.PSHN_L && calculation.Opcode == Jsm.Opcode.CAL && calculation.Parameter == 0 && store.Opcode == matchingStore && store.Parameter == read.Parameter && !script.ReachableOperations.Any(position => program.Operations[position].Opcode is Jsm.Opcode.JMP or Jsm.Opcode.JPF && JsmProgram.JumpTarget(position, program.Operations[position].Parameter) > index && JsmProgram.JumpTarget(position, program.Operations[position].Parameter) <= index + 3);
    }

    private static bool IsAbsenceMemoryRead(Jsm.Opcode opcode) => opcode is Jsm.Opcode.PSHM_B or Jsm.Opcode.PSHSM_B or Jsm.Opcode.PSHM_W or Jsm.Opcode.PSHSM_W or Jsm.Opcode.PSHM_L or Jsm.Opcode.PSHSM_L;
    private static bool IsAbsenceRequest(Jsm.Opcode opcode) => opcode is Jsm.Opcode.REQ or Jsm.Opcode.REQSW or Jsm.Opcode.REQEW or Jsm.Opcode.PREQ or Jsm.Opcode.PREQSW or Jsm.Opcode.PREQEW;
}
