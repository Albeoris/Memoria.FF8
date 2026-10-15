using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    /// <summary>Current unfinished work for this exact producer, excluding intentionally halted contexts.</summary>
    public bool HasSemanticProducerWork(ReplayScriptGoal goal)
    {
        ValidateSemanticScriptGoal(goal);
        return fibers.Any(fiber => !fiber.Complete && fiber.WaitReason != WaitReason.NativeHalt && fiber.ObjectId == goal.ObjectId && fiber.Script.Id == goal.ScriptId && !IsSemanticSpatialObserver(fiber, goal));
    }

    /// <summary>Builds inferred metadata for an exact current declared site without dispatching or advancing it.</summary>
    public ReplayScriptGoal GetSemanticProducerGoal(int objectId, int scriptId, int operation)
    {
        if (program is null || NativeRuntimeProfile is not { IsVerified: true } profile || (uint)objectId >= program.Objects.Count)
        {
            throw new InvalidDataException("Semantic producer metadata requires its current verified field program and object.");
        }
        EventScript? script = program.Objects[objectId].Scripts.SingleOrDefault(script => script.Id == scriptId);
        if (script is null || operation < script.Start || operation >= script.End)
        {
            throw new InvalidDataException("Semantic producer metadata requires an exact site in its current declared script.");
        }
        ReplayScriptGoal goal = new(FieldId, programHash, profile.ExecutableHash, objectId, scriptId, operation, (int)program.Operations[operation].Opcode, Inferred: true);
        ValidateSemanticScriptGoal(goal);
        return goal;
    }

    /// <summary>Whether this admitted producer still owns pending entry work or an unfinished native request slot. It does not dispatch or advance that work.</summary>
    public bool IsSemanticAdmissionPending(ReplaySemanticTrigger trigger)
    {
        ArgumentNullException.ThrowIfNull(trigger);
        trigger.Validate();
        if (program is null || trigger.FieldId != FieldId || trigger.ProgramHash != programHash)
        {
            return false;
        }
        ValidateSemanticProducer(trigger);
        if (trigger.Kind == ReplaySemanticTriggerKind.FieldGateway)
        {
            return false;
        }
        if (trigger.Kind is ReplaySemanticTriggerKind.AreaEntry or ReplaySemanticTriggerKind.AreaExit && actors[trigger.ObjectId].DoorLineEntryPending)
        {
            return true;
        }
        return fibers.Any(fiber => !fiber.Complete && !fiber.Initialization && fiber.ObjectId == trigger.ObjectId && fiber.Script.Id == trigger.ScriptId && NativeActorRequestSlot(fiber) == trigger.NativeSlot);
    }

    /// <summary>Potential current external causes of a retained producer, including literal native request chains. This is inferred reachability metadata, not a forced branch or proof of a past admission.</summary>
    public IReadOnlyList<ReplaySemanticTrigger> GetSemanticProducerCandidates(ReplayScriptGoal goal)
    {
        HashSet<(int ObjectId, int ScriptId)> producers = GetSemanticProducerAncestors(goal);
        return GetSemanticTriggerCandidates().Where(IsSemanticTargetEnabled).Where(candidate => producers.Contains((candidate.ObjectId, candidate.ScriptId))).ToArray();
    }

    /// <summary>Whether a declared initialization/default script can request this producer. Dormant automatic branches are hints to recompute naturally, not commands to execute their old writes.</summary>
    public bool IsAutonomousSemanticProducer(ReplayScriptGoal goal)
    {
        HashSet<(int ObjectId, int ScriptId)> producers = GetSemanticProducerAncestors(goal);
        return program!.Objects.Any(obj => obj.Scripts.Take(2).Any(script => producers.Contains((obj.Index, script.Id))));
    }

    private HashSet<(int ObjectId, int ScriptId)> GetSemanticProducerAncestors(ReplayScriptGoal goal)
    {
        ValidateSemanticScriptGoal(goal);
        HashSet<(int ObjectId, int ScriptId)> producers = [(goal.ObjectId, goal.ScriptId)];
        Dictionary<(int ObjectId, int ScriptId), HashSet<(int ObjectId, int ScriptId)>> callers = [];
        foreach (EventObject obj in program!.Objects)
        {
            foreach (EventScript script in obj.Scripts)
            {
                for (int index = script.Start + 2; index < script.End; index++)
                {
                    JsmOperation request = program.Operations[index];
                    if (request.Opcode is not (Jsm.Opcode.REQ or Jsm.Opcode.REQSW or Jsm.Opcode.REQEW) || program.Operations[index - 2].Opcode != Jsm.Opcode.PSHN_L || program.Operations[index - 1].Opcode != Jsm.Opcode.PSHN_L || (uint)request.Parameter >= program.NativeObjects.Count)
                    {
                        continue;
                    }
                    int targetId = program.NativeObjects[request.Parameter];
                    int rawPriority = program.Operations[index - 2].Parameter;
                    int priority = unchecked((byte)rawPriority);
                    // Declared standard-slot reachability does not require the target body to remain allocated or its slot to be free now.
                    // Out-of-slot aliases and raw-DWORD wait comparisons have no inferred producer edge.
                    if ((uint)priority >= 8 || request.Opcode is Jsm.Opcode.REQSW or Jsm.Opcode.REQEW && rawPriority != priority)
                    {
                        continue;
                    }
                    int targetScriptId = unchecked((ushort)program.Operations[index - 1].Parameter);
                    if (!program.Objects[targetId].Scripts.Any(targetScript => targetScript.Id == targetScriptId))
                    {
                        continue;
                    }
                    (int, int) target = (targetId, targetScriptId);
                    if (!callers.TryGetValue(target, out HashSet<(int ObjectId, int ScriptId)>? sources))
                    {
                        sources = [];
                        callers.Add(target, sources);
                    }
                    sources.Add((obj.Index, script.Id));
                }
            }
        }
        Queue<(int ObjectId, int ScriptId)> pending = new(producers);
        while (pending.TryDequeue(out (int ObjectId, int ScriptId) producer))
        {
            if (!callers.TryGetValue(producer, out HashSet<(int ObjectId, int ScriptId)>? sources))
            {
                continue;
            }
            foreach ((int ObjectId, int ScriptId) source in sources)
            {
                if (producers.Add(source))
                {
                    pending.Enqueue(source);
                }
            }
        }
        return producers;
    }

    private void ValidateSemanticScriptGoal(ReplayScriptGoal goal)
    {
        ArgumentNullException.ThrowIfNull(goal);
        if (program is null || NativeRuntimeProfile is not { IsVerified: true } || goal.FieldId != FieldId || goal.ProgramHash != programHash || goal.ExecutableHash is string executable && executable != NativeRuntimeProfile.ExecutableHash || (uint)goal.ObjectId >= program.Objects.Count)
        {
            throw new InvalidDataException("Semantic producer does not belong to the current verified field program.");
        }
        EventScript? script = program.Objects[goal.ObjectId].Scripts.SingleOrDefault(script => script.Id == goal.ScriptId);
        if (script is null || goal.Operation < script.Start || goal.Operation >= script.End || (int)program.Operations[goal.Operation].Opcode != goal.Code)
        {
            throw new InvalidDataException("Semantic producer lost its original declared operation and opcode.");
        }
    }
}
