using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class GameWorld
{
    internal bool CanCacheAutoPlayUnavailableSource(string eventId, int objectId)
    {
        return Field is not null && HasCurrentFieldAllocation && CanCacheAutoPlayUnavailableSource(JsmProgram.Read(Field.Jsm), eventId, objectId) && ReadAutoPlayConditions(eventId, objectId).DeferredOperations.Count == 0;
    }

    internal static bool CanCacheAutoPlayUnavailableSource(JsmProgram program, string eventId, int objectId)
    {
        return CanCacheAutoPlayUnavailableSource(program, eventId, objectId, null);
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

    private static bool CanCacheAutoPlayUnavailableSource(JsmProgram program, string eventId, int objectId, EventScript? worldSource)
    {
        string kind = eventId.Split(':').ElementAtOrDefault(1) ?? string.Empty;
        if (kind == "gateway")
        {
            return worldSource is null && objectId == -1;
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
        int[] possibleActors = program.NativeObjects.Take(program.NativeActorCount).ToArray();
        while (pending.TryDequeue(out EventScript? script))
        {
            if (!inspected.Add(script))
            {
                continue;
            }
            foreach (int index in script.ReachableOperations)
            {
                JsmOperation operation = program.Operations[index];
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
                    EventObject[] targets = partyRequest ? program.NativeObjects.Take(program.NativeActorCount).Select(id => program.Objects[id]).ToArray() : [program.Objects[program.NativeObjects[operation.Parameter]]];
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
                else if (operation.Opcode == Jsm.Opcode.PSHI_L)
                {
                    // Only this adjacent canonical-party producer proves the result register's source.
                    if (operation.Parameter != 0 || index <= script.Start || !script.ContainsOperation(index - 1) || program.Operations[index - 1].Opcode is not (Jsm.Opcode.ISPARTY or Jsm.Opcode.GETPARTY) || AutoPlaySourceHasInteriorEntry(program, script, index - 1, index))
                    {
                        return false;
                    }
                }
                else if (!IsAutoPlayUnavailableSourceOperation(operation.Opcode))
                {
                    return false;
                }
            }
        }
        return worldSource is null || inspected.Contains(worldSource);
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
}
