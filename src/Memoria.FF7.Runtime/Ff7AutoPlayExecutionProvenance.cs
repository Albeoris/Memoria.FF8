using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed record Ff7AutoPlayExecutionOwner(int Entity, int Priority, int Script, int Entry, int ParentEntity, int ParentPriority, int SourceProgramCounter, long DispatchTick, bool Queued);
public sealed record Ff7AutoPlayExecutionProvenance(long Generation, string ProgramIdentity, int RootEntity, int RootScript, string Condition, Ff7AutoPlayExecutionOwner[] Owners, string VisitId, string EventId, Ff7AutoPlayTargetKind Kind, long LastActivityTick = -1, int ActivityCursor = 0, FieldHostRequest? AcceptedTransition = null, long AcceptedTransitionTick = -1, bool RootReturned = false, long RootDispatchTick = -1);

internal static class Ff7AutoPlayExecutionOwners
{
    internal static Ff7AutoPlayExecutionProvenance Begin(Ff7AutoPlayActiveEvent active, string programIdentity, int entry, long tick)
    {
        return new(active.Generation, programIdentity, active.Entity, active.Script, active.ConditionFingerprint, [new(active.Entity, 1, active.Script, entry, -1, -1, entry, tick, false)], active.VisitId, active.EventId, active.Kind, RootDispatchTick: tick);
    }

    internal static bool Matches(Ff7AutoPlayActiveEvent active, long generation, string programIdentity)
    {
        return active.Accepted && active.Generation == generation && active.ExecutionProvenance is { } provenance && provenance.Generation == generation && provenance.ProgramIdentity == programIdentity && provenance.RootEntity == active.Entity && provenance.RootScript == active.Script && provenance.Condition == active.ConditionFingerprint && provenance.VisitId == active.VisitId && provenance.EventId == active.EventId && provenance.Kind == active.Kind;
    }

    internal static Ff7AutoPlayActiveEvent Observe(Ff7AutoPlayActiveEvent active, long generation, string programIdentity, long tick, IReadOnlyList<Ff7NativeScriptActivity> activities, Func<int, int, int> readEntry)
    {
        if (!Matches(active, generation, programIdentity) || activities.Count == 0)
        {
            return active;
        }
        if (active.ExecutionProvenance!.LastActivityTick > tick)
        {
            return active;
        }
        int start = active.ExecutionProvenance.LastActivityTick == tick ? active.ExecutionProvenance.ActivityCursor : 0;
        if (start >= activities.Count)
        {
            return active;
        }
        List<Ff7AutoPlayExecutionOwner> owners = active.ExecutionProvenance!.Owners.ToList();
        FieldHostRequest? acceptedTransition = active.ExecutionProvenance.AcceptedTransition;
        long acceptedTransitionTick = active.ExecutionProvenance.AcceptedTransitionTick;
        bool rootReturned = active.ExecutionProvenance.RootReturned;
        bool changed = false;
        foreach (Ff7NativeScriptActivity activity in activities.Skip(start))
        {
            if (activity.HostOperation is { Request.Command: FieldTransitionCommand, Reply.Completed: true } operation && owners.Any(owner => !owner.Queued && owner.Entity == operation.Request.Entity && owner.Priority == operation.Request.Priority))
            {
                acceptedTransition = operation.Request;
                acceptedTransitionTick = tick;
            }
            if (activity.Dispatch is { Accepted: true } dispatch)
            {
                bool owned = owners.Any(owner => !owner.Queued && owner.Entity == dispatch.SourceEntity && owner.Priority == dispatch.SourcePriority);
                if (dispatch.TargetEntity == active.Entity && dispatch.TargetPriority == 1)
                {
                    owners.Clear();
                    changed = true;
                    continue;
                }
                changed |= owners.RemoveAll(owner => owner.Entity == dispatch.TargetEntity && owner.Priority == dispatch.TargetPriority) != 0;
                if (owned && dispatch.TargetEntity >= 0 && dispatch.TargetPriority is >= 0 and < 7 && dispatch.TargetScript is >= 0 and < 32 && dispatch.RequestMode is >= 1 and <= 3)
                {
                    owners.Add(new(dispatch.TargetEntity, dispatch.TargetPriority, dispatch.TargetScript, readEntry(dispatch.TargetEntity, dispatch.TargetScript), dispatch.SourceEntity, dispatch.SourcePriority, dispatch.SourceProgramCounter, dispatch.Tick, dispatch.Queued));
                    changed = true;
                }
            }
            if (activity.Return is { } returned)
            {
                rootReturned |= owners.Any(owner => owner.ParentEntity == -1 && owner.Entity == returned.Entity && owner.Priority == returned.Priority);
                changed |= owners.RemoveAll(owner => owner.Entity == returned.Entity && owner.Priority == returned.Priority) != 0;
            }
            if (activity.Boundary is { } boundary)
            {
                int index = owners.FindIndex(owner => owner.Queued && owner.Entity == boundary.Entity && owner.Priority == boundary.Priority && owner.Entry == boundary.ProgramCounter);
                if (index >= 0)
                {
                    owners[index] = owners[index] with { Queued = false };
                    changed = true;
                }
            }
            if (activity.ExternalAdmission is { Admission: { Accepted: true, Started: true } admission } external)
            {
                Ff7AutoPlayExecutionOwner? root = active.ExecutionProvenance.Owners.SingleOrDefault(owner => owner.ParentEntity == -1);
                bool rootStart = root is not null && admission.Entity == root.Entity && admission.Priority == root.Priority && admission.Script == root.Script && external.Tick == root.DispatchTick;
                if (admission.Entity == active.Entity && admission.Priority == 1)
                {
                    owners.Clear();
                    if (rootStart)
                    {
                        owners.Add(root!);
                        rootReturned = false;
                    }
                    changed = true;
                }
                else
                {
                    changed |= owners.RemoveAll(owner => owner.Entity == admission.Entity && owner.Priority == admission.Priority) != 0;
                }
            }
        }
        return active with { ExecutionProvenance = active.ExecutionProvenance with { Owners = changed ? owners.ToArray() : active.ExecutionProvenance.Owners, LastActivityTick = tick, ActivityCursor = activities.Count, AcceptedTransition = acceptedTransition, AcceptedTransitionTick = acceptedTransitionTick, RootReturned = rootReturned } };
    }

    internal static IReadOnlyDictionary<int, int> ReadCurrentOwners(FieldVmSnapshot vm, Ff7AutoPlayActiveEvent? active, long generation)
    {
        Dictionary<int, int> result = [];
        if (active is null || !Matches(active, generation, vm.ProgramIdentity))
        {
            return result;
        }
        foreach (Ff7AutoPlayExecutionOwner owner in active.ExecutionProvenance!.Owners)
        {
            if (!owner.Queued && (uint)owner.Entity < vm.Entities.Length && vm.Entities[owner.Entity].Priority == owner.Priority)
            {
                result.Add(owner.Entity, owner.Priority);
            }
        }
        return result;
    }

    internal static bool HasOutstandingChildren(FieldVmSnapshot vm, Ff7AutoPlayActiveEvent active, long generation, Func<Ff7AutoPlayExecutionOwner, bool>? settledPresentation = null)
    {
        if (!Matches(active, generation, vm.ProgramIdentity))
        {
            return false;
        }
        return active.ExecutionProvenance!.Owners.Any(owner => (owner.Entity != active.Entity || owner.Priority != 1) && (uint)owner.Entity < vm.Entities.Length && (vm.Entities[owner.Entity].Priority == owner.Priority || vm.Entities[owner.Entity].Priority < owner.Priority && owner.Priority < vm.Entities[owner.Entity].SavedProgramCounters.Length && vm.Entities[owner.Entity].SavedProgramCounters[owner.Priority] != 0) && settledPresentation?.Invoke(owner) != true);
    }
}
