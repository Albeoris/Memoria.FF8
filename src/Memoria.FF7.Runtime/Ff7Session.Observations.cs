using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.AutoPlay;

namespace Memoria.FF7.Runtime;

public sealed record Ff7NativeHostOperation(FieldHostRequest Request, FieldHostReply Reply);
public sealed record Ff7NativeInstructionBoundary(int Entity, int Priority, int ProgramCounter, byte Opcode);
public sealed record Ff7NativeScriptActivity(FieldScriptDispatch? Dispatch = null, FieldScriptReturn? Return = null, Ff7NativeHostOperation? HostOperation = null, Ff7NativeInstructionBoundary? Boundary = null);

public sealed record Ff7NativeGatewayPosition(int X, int Y, int Z, int Triangle);
public sealed record Ff7NativeGatewayDeparture(string Field, long Generation, long Tick, int GatewayIndex, FieldTransitionCommand Transition, Ff7NativeGatewayPosition From, Ff7NativeGatewayPosition To);

public sealed record Ff7NativeTransitionOwnership(int RootEntity, int RootScript, int OwnerEntity, int OwnerPriority);

public sealed record Ff7NativeTickObservation(string Field, long Generation, long Tick, FieldScriptReturn[] ScriptReturns, FieldTransitionCommand? Transition, string ProgressWitness, FieldHostRequest? TransitionOwner = null, Ff7NativeTransitionOwnership? TransitionOwnership = null, Ff7NativeScriptActivity[]? ScriptActivity = null);

public sealed partial class Ff7Session
{
    public event Action<Ff7NativeTickObservation>? NativeTickCompleted;
    public event Action<int, int, bool>? InteractionScriptRequested;
    internal event Action<Ff7NativeGatewayDeparture?>? NativeFieldDeparting;
    internal Ff7NativeGatewayDeparture? GatewayDeparture { get; private set; }
    private bool deferNavigationFieldDeparture;

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

    private bool RequestInteractionScript(int entity, int script)
    {
        bool accepted = Machine.TryRequestScript(entity, script);
        InteractionScriptRequested?.Invoke(entity, script, accepted);
        return accepted;
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
        if (AutoPlayState?.Active is not { Kind: Ff7AutoPlayTargetKind.AutonomousSpatial, Accepted: false, DirectEffectOwner: { } owner } active || active.Generation != FieldGeneration || !Ff7AutoPlayPolicy.MatchesDirectSpatialEffect(owner, request))
        {
            return;
        }
        ObserveNativeScriptActivity(new(HostOperation: new(request, reply)));
    }

    private void ObserveNativeInstruction(int entity, int priority, int programCounter, byte opcode)
    {
        Ff7AutoPlayActiveEvent? active = AutoPlayState?.Active;
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
        if (NativeTickCompleted is null || tickContinuation is null)
        {
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
        Ff7NativeTransitionOwnership? ownership = ReadTransitionOwnership(sourceVm, transitionOwner, AutoPlayState?.Active, FieldGeneration, returns);
        return new(Field.Name, FieldGeneration, Machine.Tick, returns, Host.Process.Transition, CaptureEventProgressWitness(), transitionOwner, ownership, continuation.ScriptActivity ?? []);
    }

    internal static Ff7NativeTransitionOwnership? ReadTransitionOwnership(FieldVmSnapshot sourceVm, FieldHostRequest? owner, Ff7AutoPlayActiveEvent? active, long generation, IReadOnlyList<FieldScriptReturn> returns)
    {
        if (owner is not { Command: FieldTransitionCommand } || active is null || returns.Any(returned => returned.Entity == (active.SpatialOwner?.TargetEntity ?? active.Entity) && returned.Priority == (active.DirectEffectOwner?.Priority ?? active.SpatialOwner?.TargetPriority ?? 1)) || !Ff7AutoPlayScriptedInput.ReadInteractionOwners(sourceVm, active, generation).TryGetValue(owner.Entity, out int priority) || priority != owner.Priority)
        {
            return null;
        }
        return new(active.Entity, active.Script, owner.Entity, owner.Priority);
    }

    internal string CaptureEventProgressWitness()
    {
        byte[] persistent = Machine.Variables.CopyPersistent();
        Array.Clear(persistent, 16, 8);
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(new { Persistent = persistent, Progress.Inventory, Progress.Materia, Progress.Gil, Characters = Progress.Characters.Select(character => new { character.Id, character.Level, character.Experience, character.HitPoints, character.MagicPoints }), Progress.Party, Progress.AvailableCharacters, Progress.LockedCharacters, Progress.CompletedBattles });
        return Convert.ToHexString(SHA256.HashData(bytes));
    }
}
