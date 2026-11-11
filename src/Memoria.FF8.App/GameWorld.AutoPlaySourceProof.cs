using System.Security.Cryptography;
using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class GameWorld
{
    private byte[]? autoPlayWalkRejectionProofSource;
    private sealed record AutoPlayWalkSourceProof(bool Proved, int[] Operations);
    private readonly Dictionary<(string Event, int Object, int FirstActor, int SecondActor, int ThirdActor), AutoPlayWalkSourceProof> autoPlayWalkRejectionProofs = [];

    internal bool CanCacheAutoPlayUnavailableSource(string eventId, int objectId)
    {
        return Field is not null && HasCurrentFieldAllocation && CanCacheAutoPlayUnavailableSource(JsmProgram.Read(Field.Jsm), eventId, objectId) && ReadAutoPlayConditions(eventId, objectId).DeferredOperations.Count == 0;
    }

    internal static bool CanCacheAutoPlayUnavailableSource(JsmProgram program, string eventId, int objectId)
    {
        return CanCacheAutoPlayUnavailableSource(program, eventId, objectId, null);
    }

    internal bool CanCacheAutoPlayWalkRejectionSource(string eventId, int objectId)
    {
        return ReadAutoPlayWalkRejectionState(eventId, objectId) is not null;
    }

    internal string? ReadAutoPlayWalkRejectionState(string eventId, int objectId)
    {
        if (Field is null || !HasCurrentFieldAllocation)
        {
            return null;
        }
        EnsureAutoPlayConditionSource();
        if (!ReferenceEquals(autoPlayWalkRejectionProofSource, Field.Jsm))
        {
            autoPlayWalkRejectionProofs.Clear();
            autoPlayWalkRejectionProofSource = Field.Jsm;
        }
        int[] boundPartyActors = Enumerable.Range(0, 3).Select(slot => NativeFieldPartyBinding.TryGetObjectId(Events.Scene, slot, out int actor) ? actor : -1).ToArray();
        var key = (eventId, objectId, boundPartyActors[0], boundPartyActors[1], boundPartyActors[2]);
        if (!autoPlayWalkRejectionProofs.TryGetValue(key, out AutoPlayWalkSourceProof? proof))
        {
            HashSet<int> operations = [];
            bool proved = CanCacheAutoPlayWalkRejectionSource(autoPlayConditionProgram!, eventId, objectId, boundPartyActors, operations);
            proof = new(proved, proved ? operations.Order().ToArray() : []);
            autoPlayWalkRejectionProofs.Add(key, proof);
        }
        return proof.Proved
            ? ReadAutoPlayWalkRejectionState(Events, autoPlayConditionProgram!, proof.Operations, boundPartyActors)
            : ReadAutoPlayPhaseWalkRejectionState(Events, autoPlayConditionProgram!, eventId, objectId, boundPartyActors);
    }

    internal static bool CanCacheAutoPlayWalkRejectionSource(JsmProgram program, string eventId, int objectId)
    {
        return CanCacheAutoPlayUnavailableSource(program, eventId, objectId, null, walkRejection: true);
    }

    internal static bool CanCacheAutoPlayWalkRejectionSource(JsmProgram program, string eventId, int objectId, IReadOnlyList<int> boundPartyActors)
    {
        return CanCacheAutoPlayWalkRejectionSource(program, eventId, objectId, boundPartyActors, null);
    }

    private static bool CanCacheAutoPlayWalkRejectionSource(JsmProgram program, string eventId, int objectId, IReadOnlyList<int> boundPartyActors, ISet<int>? inspectedOperations, EventEngine? phase = null)
    {
        int[] nativeActors = program.NativeObjects.Take(program.NativeActorCount).ToArray();
        if (boundPartyActors.Count != 3 || boundPartyActors.Any(actor => actor < -1 || actor >= 0 && !nativeActors.Contains(actor)) || boundPartyActors.Where(actor => actor >= 0).Distinct().Count() != boundPartyActors.Count(actor => actor >= 0))
        {
            return false;
        }
        return CanCacheAutoPlayUnavailableSource(program, eventId, objectId, null, walkRejection: true, boundPartyActors, inspectedOperations, phase);
    }

    internal static string? ReadAutoPlayPhaseWalkRejectionState(EventEngine events, JsmProgram program, string eventId, int objectId, IReadOnlyList<int> boundPartyActors)
    {
        HashSet<int> operations = [];
        return CanCacheAutoPlayWalkRejectionSource(program, eventId, objectId, boundPartyActors, operations, events)
            ? ReadAutoPlayWalkRejectionState(events, program, operations, boundPartyActors)
            : null;
    }

    internal static string? ReadAutoPlayWalkRejectionState(EventEngine events, JsmProgram program, IReadOnlyCollection<int> inspectedOperations, IReadOnlyList<int> boundPartyActors)
    {
        List<int> reads = [];
        List<EventPredicateValue> temporary = [];
        foreach (int index in inspectedOperations.Distinct().Order())
        {
            JsmOperation operation = program.Operations[index];
            bool write = operation.Opcode is Jsm.Opcode.POPM_B or Jsm.Opcode.POPM_W or Jsm.Opcode.POPM_L;
            bool read = operation.Opcode is Jsm.Opcode.PSHM_B or Jsm.Opcode.PSHSM_B or Jsm.Opcode.PSHM_W or Jsm.Opcode.PSHSM_W or Jsm.Opcode.PSHM_L or Jsm.Opcode.PSHSM_L;
            if (!read && !write)
            {
                continue;
            }
            int width = operation.Opcode switch
            {
                Jsm.Opcode.PSHM_B or Jsm.Opcode.PSHSM_B or Jsm.Opcode.POPM_B => 1,
                Jsm.Opcode.PSHM_W or Jsm.Opcode.PSHSM_W or Jsm.Opcode.POPM_W => 2,
                _ => 4
            };
            if (operation.Parameter >= 1024 && operation.Parameter <= 1280 - width)
            {
                try
                {
                    NativeScalarValue value = ReadWalkRejectionTemporary(events, operation, width);
                    if (!value.IsExact)
                    {
                        return null;
                    }
                    temporary.Add(new(index, operation.Opcode.ToString(), operation.Parameter, width, value.RequireExact()));
                }
                catch (Exception exception) when (exception is NotSupportedException or InvalidDataException or ArgumentOutOfRangeException)
                {
                    return null;
                }
            }
            else if (read)
            {
                reads.Add(index);
            }
            else
            {
                return null;
            }
        }
        EventPredicateState state = events.ReadPredicateState(reads);
        if (state.DeferredOperations.Count != 0)
        {
            return null;
        }
        return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { state.Fingerprint, PartyActors = boundPartyActors, Temporary = temporary })));
    }

    internal bool CanCacheAutoPlayWorldUnavailableSource(string eventId, int objectId, NativeWorldTransitionRequest request)
    {
        return Field is not null && HasCurrentFieldAllocation && Field.Id == request.SourceFieldId && CanCacheAutoPlayWorldUnavailableSource(JsmProgram.Read(Field.Jsm), eventId, objectId, request) && ReadAutoPlayConditions(eventId, objectId).DeferredOperations.Count == 0;
    }

    internal static bool CanCacheAutoPlayWorldUnavailableSource(JsmProgram program, string eventId, int objectId, NativeWorldTransitionRequest request)
    {
        if (request.Version != 1 || request.Origin != NativeWorldTransitionOrigin.Script || request.Gateway is not null || request.SourceObjectId is not int sourceObject || request.SourceScriptId is not int sourceScript || request.SourceOperation is not int sourceOperation || (uint)sourceObject >= program.Objects.Count || !int.TryParse(eventId.Split(':')[0], out int fieldId) || fieldId != request.SourceFieldId)
        {
            return false;
        }
        EventScript? script = program.Objects[sourceObject].Scripts.SingleOrDefault(candidate => candidate.Id == sourceScript && candidate.ContainsOperation(sourceOperation));
        return script is not null && TryReadAutoPlayWorldPacket(program, script, sourceOperation, out WorldTransition? packet) && packet == request.Transition && CanCacheAutoPlayUnavailableSource(program, eventId, objectId, script);
    }

    private static bool CanCacheAutoPlayUnavailableSource(JsmProgram program, string eventId, int objectId, EventScript? worldSource, bool walkRejection = false, IReadOnlyList<int>? boundPartyActors = null, ISet<int>? inspectedOperations = null, EventEngine? phase = null)
    {
        string kind = eventId.Split(':').ElementAtOrDefault(1) ?? string.Empty;
        if (kind == "gateway")
        {
            return !walkRejection && worldSource is null && objectId == -1;
        }
        if ((uint)objectId >= program.Objects.Count || kind is not ("talk" or "push" or "line" or "line-action" or "area"))
        {
            return false;
        }
        EventObject obj = program.Objects[objectId];
        EventScript[] roots = kind == "line" ? obj.Scripts.Skip(3).Take(5).ToArray() : obj.Scripts.Skip(kind == "push" ? 3 : 2).Take(1).ToArray();
        if (roots.Length == 0)
        {
            return false;
        }
        Queue<EventScript> pending = new(roots);
        HashSet<EventScript> inspected = [];
        HashSet<(int Address, int Width)> phaseReads = [];
        HashSet<(int Address, int Width)> writes = [];
        int[] possibleActors = boundPartyActors is null ? program.NativeObjects.Take(program.NativeActorCount).ToArray() : boundPartyActors.Where(actor => actor >= 0).ToArray();
        while (pending.TryDequeue(out EventScript? script))
        {
            if (!inspected.Add(script))
            {
                continue;
            }
            IEnumerable<int> operations = phase is null ? script.ReachableOperations : WalkRejectionPhaseOperations(phase, program, script, phaseReads);
            foreach (int index in operations)
            {
                inspectedOperations?.Add(index);
                JsmOperation operation = program.Operations[index];
                if (phase is not null && WalkRejectionWriteWidth(operation.Opcode) is > 0 and var width)
                {
                    writes.Add((operation.Parameter, width));
                }
                bool partyRequest = operation.Opcode is Jsm.Opcode.PREQ or Jsm.Opcode.PREQSW or Jsm.Opcode.PREQEW;
                bool objectRequest = operation.Opcode is Jsm.Opcode.REQ or Jsm.Opcode.REQSW or Jsm.Opcode.REQEW;
                if (partyRequest || objectRequest)
                {
                    if (index < script.Start + 2 || !script.ContainsOperation(index - 2) || !script.ContainsOperation(index - 1) || program.Operations[index - 2].Opcode != Jsm.Opcode.PSHN_L || program.Operations[index - 1].Opcode != Jsm.Opcode.PSHN_L || AutoPlaySourceHasInteriorEntry(program, script, index - 2, index))
                    {
                        return false;
                    }
                    if (partyRequest && (uint)operation.Parameter >= 3 || objectRequest && (uint)operation.Parameter >= program.NativeObjects.Count)
                    {
                        return false;
                    }
                    int selector = program.Operations[index - 1].Parameter;
                    EventObject[] targets;
                    if (partyRequest && boundPartyActors is not null)
                    {
                        int actor = boundPartyActors[operation.Parameter];
                        targets = actor < 0 ? [] : [program.Objects[actor]];
                    }
                    else
                    {
                        targets = partyRequest ? possibleActors.Select(id => program.Objects[id]).ToArray() : [program.Objects[program.NativeObjects[operation.Parameter]]];
                    }
                    if (targets.Length == 0)
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
                                requested = program.ResolveNativeRequestPointer(target.Index, selector, possibleActors).Script;
                            }
                            catch (NotSupportedException)
                            {
                                return false;
                            }
                        }
                        else
                        {
                            requested = target.Scripts.SingleOrDefault(candidate => candidate.Id == unchecked((ushort)selector));
                        }
                        if (requested is null)
                        {
                            return false;
                        }
                        pending.Enqueue(requested);
                    }
                }
                else if (operation.Opcode == Jsm.Opcode.WORLDMAPJUMP && worldSource is not null)
                {
                    if (!TryReadAutoPlayWorldPacket(program, script, index, out _))
                    {
                        return false;
                    }
                }
                else if (walkRejection && kind == "line" && roots.Contains(script) && operation.Opcode is Jsm.Opcode.MAPJUMP or Jsm.Opcode.MAPJUMP3)
                {
                    // A direct line can interrupt a walk by leaving the field.
                    // Its executed owner and destination must still match at runtime.
                }
                else if (operation.Opcode == Jsm.Opcode.PSHI_L)
                {
                    // Only this adjacent canonical-party producer proves the result register's source.
                    if (operation.Parameter != 0 || index <= script.Start || !script.ContainsOperation(index - 1) || program.Operations[index - 1].Opcode is not (Jsm.Opcode.ISPARTY or Jsm.Opcode.GETPARTY) || AutoPlaySourceHasInteriorEntry(program, script, index - 1, index))
                    {
                        return false;
                    }
                }
                else if (walkRejection ? !IsAutoPlayWalkRejectionSourceOperation(operation) : !IsAutoPlayUnavailableSourceOperation(operation.Opcode))
                {
                    return false;
                }
            }
        }
        return (worldSource is null || inspected.Contains(worldSource)) && (phase is null || phaseReads.Count != 0 && !phaseReads.Any(read => writes.Any(write => read.Address < write.Address + write.Width && write.Address < read.Address + read.Width)));
    }

    private static IEnumerable<int> WalkRejectionPhaseOperations(EventEngine events, JsmProgram program, EventScript script, HashSet<(int Address, int Width)> phaseReads)
    {
        Stack<int> pending = new([script.Start]);
        HashSet<int> visited = [];
        while (pending.TryPop(out int index))
        {
            if (!script.ContainsOperation(index) || !visited.Add(index))
            {
                continue;
            }
            yield return index;
            JsmOperation operation = program.Operations[index];
            switch (operation.Opcode)
            {
                case Jsm.Opcode.RET:
                case Jsm.Opcode.HALT:
                    break;
                case Jsm.Opcode.JMP:
                    pending.Push(JsmProgram.JumpTarget(index, operation.Parameter));
                    break;
                case Jsm.Opcode.JPF:
                    bool? equal = ReadWalkRejectionPhaseEquality(events, program, script, index, phaseReads);
                    if (equal != true)
                    {
                        pending.Push(JsmProgram.JumpTarget(index, operation.Parameter));
                    }
                    if (equal != false)
                    {
                        pending.Push(index + 1);
                    }
                    break;
                default:
                    pending.Push(index + 1);
                    break;
            }
        }
    }

    private static bool? ReadWalkRejectionPhaseEquality(EventEngine events, JsmProgram program, EventScript script, int jump, HashSet<(int Address, int Width)> phaseReads)
    {
        int start = jump - 3;
        if (start < script.Start || !script.ContainsOperation(start) || !script.ContainsOperation(start + 1) || !script.ContainsOperation(start + 2) ||
            program.Operations[start + 1] is not { Opcode: Jsm.Opcode.PSHN_L } literal || program.Operations[start + 2] is not { Opcode: Jsm.Opcode.CAL, Parameter: 6 } ||
            AutoPlaySourceHasInteriorEntry(program, script, start, jump))
        {
            return null;
        }
        JsmOperation read = program.Operations[start];
        int width = read.Opcode switch
        {
            Jsm.Opcode.PSHM_B or Jsm.Opcode.PSHSM_B => 1,
            Jsm.Opcode.PSHM_W or Jsm.Opcode.PSHSM_W => 2,
            Jsm.Opcode.PSHM_L or Jsm.Opcode.PSHSM_L => 4,
            _ => 0
        };
        // Field temporary bytes have a single memory owner. Other typed owners can
        // change through operations other than POPM, so their branches remain open.
        if (width == 0 || read.Parameter < 1024 || read.Parameter > 1280 - width)
        {
            return null;
        }
        try
        {
            NativeScalarValue value = ReadWalkRejectionTemporary(events, read, width);
            if (!value.IsExact)
            {
                return null;
            }
            phaseReads.Add((read.Parameter, width));
            return value.RequireExact() == literal.Parameter;
        }
        catch (Exception exception) when (exception is NotSupportedException or InvalidDataException or ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private static NativeScalarValue ReadWalkRejectionTemporary(EventEngine events, JsmOperation operation, int width)
    {
        bool signed = operation.Opcode is Jsm.Opcode.PSHSM_B or Jsm.Opcode.PSHSM_W;
        NativeScalarValue value = events.EventMemory.Read(operation.Parameter, width, signed);
        return operation.Opcode == Jsm.Opcode.PSHSM_L ? value.SaturateSignedLong() : value;
    }

    private static bool TryReadAutoPlayWorldPacket(JsmProgram program, EventScript script, int operation, out WorldTransition? packet)
    {
        packet = null;
        if (operation < script.Start || operation >= script.End || !script.ContainsOperation(operation) || program.Operations[operation].Opcode != Jsm.Opcode.WORLDMAPJUMP)
        {
            return false;
        }
        int cursor = operation - 1;
        short[] operands = new short[3];
        for (int index = operands.Length - 1; index >= 0; index--)
        {
            bool negate = cursor >= script.Start && script.ContainsOperation(cursor) && program.Operations[cursor].Opcode == Jsm.Opcode.CAL && program.Operations[cursor].Parameter == 5;
            if (negate)
            {
                cursor--;
            }
            if (cursor < script.Start || !script.ContainsOperation(cursor) || program.Operations[cursor].Opcode != Jsm.Opcode.PSHN_L)
            {
                return false;
            }
            int value = program.Operations[cursor].Parameter;
            operands[index] = unchecked((short)(negate ? -value : value));
            cursor--;
        }
        if (AutoPlaySourceHasInteriorEntry(program, script, cursor + 1, operation))
        {
            return false;
        }
        packet = new WorldTransition(operands[0], operands[1], operands[2]);
        return true;
    }

    private static bool AutoPlaySourceHasInteriorEntry(JsmProgram program, EventScript script, int start, int end)
    {
        return script.ReachableOperations.Any(index => program.Operations[index].Opcode is Jsm.Opcode.JMP or Jsm.Opcode.JPF && JsmProgram.JumpTarget(index, program.Operations[index].Parameter) > start && JsmProgram.JumpTarget(index, program.Operations[index].Parameter) <= end);
    }

    private static bool IsAutoPlayUnavailableSourceOperation(Jsm.Opcode opcode)
    {
        return opcode is Jsm.Opcode.LBL or Jsm.Opcode.NOP or Jsm.Opcode.RET or Jsm.Opcode.HALT or Jsm.Opcode.PSHN_L or Jsm.Opcode.PSHM_B or Jsm.Opcode.PSHSM_B or Jsm.Opcode.PSHM_W or Jsm.Opcode.PSHSM_W or Jsm.Opcode.PSHM_L or Jsm.Opcode.PSHSM_L or Jsm.Opcode.POPM_B or Jsm.Opcode.POPM_W or Jsm.Opcode.POPM_L or Jsm.Opcode.CAL or Jsm.Opcode.JMP or Jsm.Opcode.JPF or Jsm.Opcode.ISPARTY or Jsm.Opcode.GETPARTY or Jsm.Opcode.UCOFF or Jsm.Opcode.UCON or Jsm.Opcode.WAIT or Jsm.Opcode.MAPJUMP or Jsm.Opcode.MAPJUMP3 or Jsm.Opcode.BATTLEON or Jsm.Opcode.BATTLEOFF;
    }

    private static bool IsAutoPlayWalkRejectionSourceOperation(JsmOperation operation)
    {
        int width = WalkRejectionWriteWidth(operation.Opcode);
        if (width != 0)
        {
            return operation.Parameter >= 1024 && operation.Parameter <= 1280 - width;
        }
        return operation.Opcode is Jsm.Opcode.LBL or Jsm.Opcode.NOP or Jsm.Opcode.RET or Jsm.Opcode.HALT or Jsm.Opcode.PSHN_L or Jsm.Opcode.PSHM_B or Jsm.Opcode.PSHSM_B or Jsm.Opcode.PSHM_W or Jsm.Opcode.PSHSM_W or Jsm.Opcode.PSHM_L or Jsm.Opcode.PSHSM_L or Jsm.Opcode.PSHAC or Jsm.Opcode.CAL or Jsm.Opcode.JMP or Jsm.Opcode.JPF or Jsm.Opcode.ISPARTY or Jsm.Opcode.GETPARTY or Jsm.Opcode.UCOFF or Jsm.Opcode.UCON or Jsm.Opcode.WAIT or Jsm.Opcode.AMESW or Jsm.Opcode.CTURN or Jsm.Opcode.CTURNL or Jsm.Opcode.CTURNR or Jsm.Opcode.PCTURN or Jsm.Opcode.MSPEED or Jsm.Opcode.MOVE or Jsm.Opcode.JOIN or Jsm.Opcode.ANIME or Jsm.Opcode.RANIME or Jsm.Opcode.ANIMEKEEP or Jsm.Opcode.RANIMEKEEP or Jsm.Opcode.ANIMESYNC or Jsm.Opcode.EFFECTPLAY2 or Jsm.Opcode.BGANIMESPEED or Jsm.Opcode.RBGANIMELOOP or Jsm.Opcode.BGOFF or Jsm.Opcode.RUNENABLE;
    }

    private static int WalkRejectionWriteWidth(Jsm.Opcode opcode) => opcode switch
    {
        Jsm.Opcode.POPM_B => 1,
        Jsm.Opcode.POPM_W => 2,
        Jsm.Opcode.POPM_L => 4,
        _ => 0
    };
}
