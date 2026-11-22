using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.AutoPlay;

namespace Memoria.FF7.Runtime;

public sealed record Ff7NativeHostOperation(FieldHostRequest Request, FieldHostReply Reply);
public sealed record Ff7NativeInstructionBoundary(int Entity, int Priority, int ProgramCounter, byte Opcode);
public sealed record Ff7NativeExternalAdmission(FieldExternalScriptAdmission Admission, long Tick);
public sealed record Ff7NativeScriptActivity(FieldScriptDispatch? Dispatch = null, FieldScriptReturn? Return = null, Ff7NativeHostOperation? HostOperation = null, Ff7NativeInstructionBoundary? Boundary = null, Ff7NativeExternalAdmission? ExternalAdmission = null);

public sealed record Ff7NativeGatewayPosition(int X, int Y, int Z, int Triangle);
public sealed record Ff7NativeGatewayDeparture(string Field, long Generation, long Tick, int GatewayIndex, FieldTransitionCommand Transition, Ff7NativeGatewayPosition From, Ff7NativeGatewayPosition To);

public sealed record Ff7NativeTransitionOwnership(int RootEntity, int RootScript, int OwnerEntity, int OwnerPriority);

public sealed record Ff7NativeTickObservation(string Field, long Generation, long Tick, FieldScriptReturn[] ScriptReturns, FieldTransitionCommand? Transition, string ProgressWitness, FieldHostRequest? TransitionOwner = null, Ff7NativeTransitionOwnership? TransitionOwnership = null, Ff7NativeScriptActivity[]? ScriptActivity = null)
{
    public Ff7AutoPlayActiveEvent? CompletedSynchronousActivation { get; init; }
    public Ff7AutoPlayActiveEvent? SourceActivation { get; init; }
}

public sealed partial class Ff7Session
{
    private readonly Ff7EventProgressWitnessMemo eventProgressWitnessMemo = new();

    public event Action<Ff7NativeTickObservation>? NativeTickCompleted;
    public event Action<int, int, bool>? InteractionScriptRequested;
    internal event Action<FieldExternalScriptAdmission>? NativeInteractionScriptRequested;
    internal event Action<FieldExternalScriptAdmission>? NativeActorTalkRequested;
    internal event Action<Ff7NativeGatewayDeparture?>? NativeFieldDeparting;
    internal Ff7NativeGatewayDeparture? GatewayDeparture { get; private set; }
    private bool deferNavigationFieldDeparture;
    private List<Ff7NativeScriptActivity>? nativeTailScriptActivity;

    internal bool HasNativeGatewayDeparture(Ff7NativeGatewayDeparture expected)
    {
        if (GatewayDeparture != expected || expected.Field != Field.Name || expected.Generation != FieldGeneration || expected.Transition != Host.Process.Transition || (uint)expected.GatewayIndex >= Field.Triggers.Gateways.Count)
        {
            return false;
        }
        var gateway = Field.Triggers.Gateways[expected.GatewayIndex];
        FieldTransitionCommand command = new(checked((ushort)gateway.DestinationField), gateway.DestinationX, gateway.DestinationY, gateway.DestinationTriangle, checked((byte)gateway.Direction));
        if (command != expected.Transition)
        {
            return false;
        }
        Ff7Actor actor = Host.Process.Actors.Single(actor => actor.PlayerControlled);
        Ff7Actor from = actor with { X4096 = expected.From.X, Y4096 = expected.From.Y, Z4096 = expected.From.Z, Triangle = expected.From.Triangle };
        Ff7Actor to = actor with { X4096 = expected.To.X, Y4096 = expected.To.Y, Z4096 = expected.To.Z, Triangle = expected.To.Triangle };
        return CrossesGateway(gateway, from, to);
    }

    internal void AdvanceNavigationTick()
    {
        if (deferNavigationFieldDeparture)
        {
            throw new InvalidOperationException("A native navigation departure scope is already active.");
        }
        deferNavigationFieldDeparture = true;
        try
        {
            AdvanceTick(false);
        }
        finally
        {
            deferNavigationFieldDeparture = false;
        }
    }

    public event Action? WorldBattleStarting;
    internal event Action<Ff7WorldFieldDeparture, short>? WorldFieldDeparting;
    internal FieldSpatialRandomDomain ReadSpatialRandomDomain() => new(executable.NativeFieldRandomTable, Progress.FieldRandomStride ?? executable.FieldRandomStep, Progress.FieldRandomIndex);
    public AutoPlayEventCoverageState? EventCoverage { get; internal set; }
    internal Ff7AutoPlayEventCoverage? CoverageOwner { get; set; }

    private bool RequestInteractionScript(int entity, int script, bool actorTalk = false)
    {
        FieldExternalScriptAdmission admission = Machine.RequestExternalScript(entity, script);
        ObserveNativeScriptActivity(new(ExternalAdmission: new(admission, Machine.Tick)));
        NativeInteractionScriptRequested?.Invoke(admission);
        if (actorTalk)
        {
            NativeActorTalkRequested?.Invoke(admission);
        }
        InteractionScriptRequested?.Invoke(entity, script, admission.Accepted);
        return admission.Accepted;
    }

    private void ObserveNativeScriptReturn(FieldScriptReturn returned)
    {
        if (NativeTickCompleted is null || tickContinuation is null)
        {
            return;
        }
        FieldScriptReturn[] prior = tickContinuation.ScriptReturns ?? [];
        if (prior.Length == 4096)
        {
            throw new InvalidDataException("FFVII autoplay script observations exceeded their bounded callback allocation.");
        }
        tickContinuation = tickContinuation with { ScriptReturns = [.. prior, returned] };
        ObserveNativeScriptActivity(new(Return: returned));
    }

    private void ObserveNativeScriptDispatch(FieldScriptDispatch dispatched)
    {
        ObserveNativeScriptActivity(new(Dispatch: dispatched));
    }

    private void ObserveNativeHostOperation(FieldHostRequest request, FieldHostReply reply)
    {
        if (request.Command is FieldTransitionCommand && reply.Completed && AutoPlayState?.Active?.ExecutionProvenance is not null)
        {
            ObserveNativeScriptActivity(new(HostOperation: new(request, reply)));
        }
        if (AutoPlayState?.Active is not { Kind: Ff7AutoPlayTargetKind.AutonomousSpatial, Accepted: false, DirectEffectOwner: { } owner } active || active.Generation != FieldGeneration || !Ff7AutoPlayPolicy.MatchesDirectSpatialEffect(owner, request))
        {
            return;
        }
        ObserveNativeScriptActivity(new(HostOperation: new(request, reply)));
    }

    private void ObserveNativeInstruction(int entity, int priority, int programCounter, byte opcode)
    {
        Ff7AutoPlayActiveEvent? active = AutoPlayState?.Active;
        bool queuedStart = active?.ExecutionProvenance is not null && (active.ExecutionProvenance.Owners.Any(owner => owner.Queued && owner.Entity == entity && owner.Priority == priority && owner.Entry == programCounter) || (tickContinuation?.ScriptActivity ?? []).Any(activity => activity.Dispatch is { Accepted: true, Queued: true } dispatch && dispatch.TargetEntity == entity && dispatch.TargetPriority == priority && Field.Scripts.Entities[entity].ScriptOffsets[dispatch.TargetScript] == programCounter));
        if (queuedStart)
        {
            ObserveNativeScriptActivity(new(Boundary: new(entity, priority, programCounter, opcode)));
        }
        if (active is not { Kind: Ff7AutoPlayTargetKind.AutonomousSpatial, Returned: false, DirectEffectOwner: { } owner } || active.Generation != FieldGeneration || owner.Entity != entity || owner.Priority != priority || !owner.ExitProgramCounters.Contains(programCounter))
        {
            return;
        }
        bool admitted = active.Accepted || (tickContinuation?.ScriptActivity ?? []).Any(activity => activity.HostOperation is { } operation && Ff7AutoPlayPolicy.MatchesDirectSpatialEffect(owner, operation.Request));
        if (admitted)
        {
            ObserveNativeScriptActivity(new(Boundary: new(entity, priority, programCounter, opcode)));
        }
    }

    private void ObserveNativeScriptActivity(Ff7NativeScriptActivity activity)
    {
        if (NativeTickCompleted is null)
        {
            return;
        }
        if (tickContinuation is null)
        {
            if (nativeTailScriptActivity is not null)
            {
                if (nativeTailScriptActivity.Count == 4096)
                {
                    throw new InvalidDataException("FFVII script activity exceeded its bounded callback allocation.");
                }
                nativeTailScriptActivity.Add(activity);
            }
            return;
        }
        Ff7NativeScriptActivity[] prior = tickContinuation.ScriptActivity ?? [];
        if (prior.Length == 4096)
        {
            throw new InvalidDataException("FFVII script activity exceeded its bounded callback allocation.");
        }
        tickContinuation = tickContinuation with { ScriptActivity = [.. prior, activity] };
    }

    private Ff7NativeTickObservation? CompleteNativeObservation(Ff7TickContinuation continuation, FieldVmSnapshot sourceVm)
    {
        if (NativeTickCompleted is null)
        {
            return null;
        }
        FieldHostRequest? transitionOwner = Host.Process.Transition is null ? null : Host.Capture().Receipts.FirstOrDefault(receipt => receipt.Request.Id == Host.Process.GlobalRequestId)?.Request;
        FieldScriptReturn[] returns = continuation.ScriptReturns?.ToArray() ?? [];
        Ff7NativeTransitionOwnership? ownership = ReadTransitionOwnership(sourceVm, transitionOwner, AutoPlayState?.Active, FieldGeneration, returns, continuation.ScriptActivity, (entity, script) => Field.Scripts.Entities[entity].ScriptOffsets[script]);
        Ff7AutoPlayActiveEvent? completedActivation = AutoPlayState is { } autoplay ? Ff7AutoPlayPolicy.ReadCompletedSynchronousActivation(autoplay, sourceVm, Host.Process, Field.Scripts.Data.Span, continuation.ScriptActivity ?? [], (entity, script) => Field.Scripts.Entities[entity].ScriptOffsets[script], FieldGeneration) : null;
        Ff7AutoPlayActiveEvent? sourceActivation = AutoPlayState?.Active is { } active ? Ff7AutoPlayExecutionOwners.Observe(active, FieldGeneration, sourceVm.ProgramIdentity, sourceVm.Tick, continuation.ScriptActivity ?? [], (entity, script) => Field.Scripts.Entities[entity].ScriptOffsets[script]) : null;
        return new(Field.Name, FieldGeneration, Machine.Tick, returns, Host.Process.Transition, CaptureEventProgressWitness(), transitionOwner, ownership, continuation.ScriptActivity ?? []) { CompletedSynchronousActivation = completedActivation, SourceActivation = sourceActivation };
    }

    internal static Ff7NativeTransitionOwnership? ReadTransitionOwnership(FieldVmSnapshot sourceVm, FieldHostRequest? owner, Ff7AutoPlayActiveEvent? active, long generation, IReadOnlyList<FieldScriptReturn> returns, IReadOnlyList<Ff7NativeScriptActivity>? activities = null, Func<int, int, int>? readEntry = null)
    {
        if (owner is { Command: FieldTransitionCommand } && active is not null)
        {
            if (active.ExecutionProvenance is not null && !Ff7AutoPlayExecutionOwners.Matches(active, generation, sourceVm.ProgramIdentity))
            {
                return null;
            }
            Ff7AutoPlayActiveEvent tracked = activities is not null && readEntry is not null ? Ff7AutoPlayExecutionOwners.Observe(active, generation, sourceVm.ProgramIdentity, sourceVm.Tick, activities, readEntry) : active;
            if (tracked.ExecutionProvenance is { } provenance && provenance.AcceptedTransition == owner && provenance.AcceptedTransitionTick == sourceVm.Tick)
            {
                return new(active.Entity, active.Script, owner.Entity, owner.Priority);
            }
            if (active.ExecutionProvenance is not null && ((uint)owner.Entity >= sourceVm.Entities.Length || sourceVm.Entities[owner.Entity].Priority != owner.Priority || sourceVm.Entities[owner.Entity].ProgramCounter != owner.ProgramCounter || !(sourceVm.PendingRequests ?? []).Contains(owner)))
            {
                return null;
            }
            if (Ff7AutoPlayExecutionOwners.ReadCurrentOwners(sourceVm, tracked, generation).TryGetValue(owner.Entity, out int recordedPriority) && recordedPriority == owner.Priority)
            {
                return new(active.Entity, active.Script, owner.Entity, owner.Priority);
            }
            if (active.ExecutionProvenance is not null)
            {
                return null;
            }
        }
        if (owner is not { Command: FieldTransitionCommand } || active is null || returns.Any(returned => returned.Entity == (active.SpatialOwner?.TargetEntity ?? active.Entity) && returned.Priority == (active.DirectEffectOwner?.Priority ?? active.SpatialOwner?.TargetPriority ?? 1)) || !Ff7AutoPlayScriptedInput.ReadInteractionOwners(sourceVm, active with { ExecutionProvenance = null }, generation).TryGetValue(owner.Entity, out int priority) || priority != owner.Priority)
        {
            return null;
        }
        return new(active.Entity, active.Script, owner.Entity, owner.Priority);
    }

    internal string CaptureEventProgressWitness()
    {
        byte[] persistent = Machine.Variables.CopyPersistent();
        Array.Clear(persistent, 16, 8);
        if (eventProgressWitnessMemo.TryRead(persistent, Progress, out string witness))
        {
            return witness;
        }
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(new { Persistent = persistent, Progress.Inventory, Progress.Materia, Progress.Gil, Characters = Progress.Characters.Select(character => new { character.Id, character.Level, character.Experience, character.HitPoints, character.MagicPoints }), Progress.Party, Progress.AvailableCharacters, Progress.LockedCharacters, Progress.CompletedBattles });
        witness = Convert.ToHexString(SHA256.HashData(bytes));
        eventProgressWitnessMemo.Remember(persistent, Progress, witness);
        return witness;
    }
}
