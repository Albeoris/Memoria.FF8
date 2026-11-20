using System.Text.Json;
using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

/// <summary>Bounds neutral continuation of already admitted native work when no physical frontier is currently available.</summary>
internal sealed class Ff7AutoPlayNativeReadiness
{
    internal const int RepeatedObservationLimit = 64;
    internal const int ObservationLimit = 4096;
    private string? context;
    private readonly HashSet<string> observations = new(StringComparer.Ordinal);
    private int repeatedObservations;
    private int totalObservations;

    public void Reset()
    {
        context = null;
        observations.Clear();
        repeatedObservations = 0;
        totalObservations = 0;
    }

    public bool ShouldWait(long generation, string navigationKey, FieldVmSnapshot vm, Ff7FieldProcess process, ReadOnlySpan<byte> script, IReadOnlyList<FieldScriptInputDemand> inputDemands)
    {
        string nextContext = $"{generation}:{navigationKey}";
        if (context != nextContext)
        {
            Reset();
            context = nextContext;
        }
        HashSet<int> work = [];
        List<(int Owner, int Target)> links = [];
        for (int targetId = 0; targetId < vm.Entities.Length; targetId++)
        {
            FieldEntitySnapshot target = vm.Entities[targetId];
            int currentPc = target.ProgramCounter;
            if (HasFiniteHostWork(vm, process, targetId) || HasAdmittedContinuation(target, script))
            {
                work.Add(targetId);
            }
            if (target.WaitCounter > 0 && currentPc >= 0 && currentPc + 2 < script.Length && script[currentPc] == 0x24)
            {
                // The executing WAIT owns a finite countdown, regardless of the following opcode.
                work.Add(targetId);
            }
            for (int priority = 0; priority < target.SyncStates.Length; priority++)
            {
                int ownerId = target.SyncOwners[priority];
                if (target.SyncStates[priority] == FieldSyncState.None || (uint)ownerId >= vm.Entities.Length)
                {
                    continue;
                }
                FieldEntitySnapshot owner = vm.Entities[ownerId];
                int pc = owner.ProgramCounter;
                if (pc < 0 || pc + 2 >= script.Length || script[pc] != 0x03 || script[pc + 1] != targetId || script[pc + 2] >> 5 != priority)
                {
                    continue;
                }
                if (target.SyncStates[priority] == FieldSyncState.Waiting && target.Priority != priority && !(target.Priority < priority && HasFiniteHostWork(vm, process, targetId)))
                {
                    continue;
                }
                work.Add(ownerId);
                work.Add(targetId);
                links.Add((ownerId, targetId));
            }
        }
        HashSet<int> inputBlocked = inputDemands.Where(demand => demand.AwaitingInput && work.Contains(demand.Entity) && vm.Entities[demand.Entity].Priority == demand.Priority).Select(demand => demand.Entity).ToHashSet();
        bool expanded;
        do
        {
            expanded = false;
            foreach (var link in links)
            {
                if (inputBlocked.Contains(link.Owner) || inputBlocked.Contains(link.Target))
                {
                    expanded |= inputBlocked.Add(link.Owner);
                    expanded |= inputBlocked.Add(link.Target);
                }
            }
        }
        while (expanded);
        work.ExceptWith(inputBlocked);
        if (work.Count == 0)
        {
            return false;
        }
        string fingerprint = JsonSerializer.Serialize(new
        {
            Entities = work.Order().Select(entity => new { Entity = entity, State = vm.Entities[entity] }),
            Actors = process.Actors.Where(actor => work.Contains(actor.Entity)).Select(actor => new { actor.Entity, actor.X4096, actor.Y4096, actor.Z4096, actor.Triangle, actor.Direction, actor.Animation, actor.AnimationFrame16, actor.AnimationFinished, actor.TurnStep, actor.Offset, actor.Ladder, actor.Jump }),
            Motions = process.Motions.Where(motion => work.Contains(motion.Entity)).Select(motion => new { motion.Entity, motion.TargetX, motion.TargetY, motion.Completed }),
            Requests = (vm.PendingRequests ?? []).Where(request => work.Contains(request.Entity)).Select(request => new { request.Entity, request.Priority, request.ProgramCounter, request.Command }),
            Dialogues = process.Dialogues.Where(dialogue => (vm.PendingRequests ?? []).Any(request => request.Id == dialogue.RequestId && work.Contains(request.Entity)))
        });
        totalObservations++;
        if (!observations.Add(fingerprint))
        {
            repeatedObservations++;
        }
        return repeatedObservations < RepeatedObservationLimit && totalObservations < ObservationLimit;
    }

    private static bool HasAdmittedContinuation(FieldEntitySnapshot owner, ReadOnlySpan<byte> script)
    {
        if (owner.Priority is < 0 or >= 7 || (uint)owner.ProgramCounter >= script.Length || script[owner.ProgramCounter] == 0 || owner.SavedProgramCounters.Length != 8)
        {
            return false;
        }
        bool suspended = false;
        for (int priority = owner.Priority + 1; priority < owner.SavedProgramCounters.Length; priority++)
        {
            int pc = owner.SavedProgramCounters[priority];
            if (pc == 0)
            {
                continue;
            }
            if ((uint)pc >= script.Length)
            {
                return false;
            }
            suspended = true;
        }
        return suspended;
    }

    private static bool HasFiniteHostWork(FieldVmSnapshot vm, Ff7FieldProcess process, int entity)
    {
        FieldEntitySnapshot active = vm.Entities[entity];
        FieldHostRequest? request = (vm.PendingRequests ?? []).FirstOrDefault(request => request.Entity == entity && request.Priority == active.Priority && request.ProgramCounter == active.ProgramCounter);
        Ff7Actor? actor = process.Actors.LastOrDefault(actor => actor.EntityBound && actor.Entity == entity) ?? process.Actors.LastOrDefault(actor => actor.Entity == entity);
        if (request is null || actor is null)
        {
            return false;
        }
        return request.Command switch
        {
            FieldActorMove move => process.Motions.Any(motion => motion.RequestId == request.Id && motion.Entity == entity) || move.TargetEntity is null && actor.X4096 == move.X << 12 && actor.Y4096 == move.Y << 12,
            FieldActorJump => actor.Jump?.RequestId == request.Id,
            FieldActorLadder => actor.Ladder?.RequestId == request.Id,
            FieldAnimation { Blocking: true } => (!actor.PlayerControlled || process.PlayerLocked || process.Join is not null) && (actor.AnimationSpeed > 0 || actor.LastAnimationFrame >= 0 && actor.AnimationFrame16 >= actor.LastAnimationFrame << 4),
            _ => false
        };
    }
}
