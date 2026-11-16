using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.AutoPlay;

namespace Memoria.FF7.Runtime;

public sealed record Ff7NativeScriptActivity(FieldScriptDispatch? Dispatch = null, FieldScriptReturn? Return = null);

public sealed record Ff7NativeTransitionOwnership(int RootEntity, int RootScript, int OwnerEntity, int OwnerPriority);

public sealed record Ff7NativeTickObservation(string Field, long Generation, long Tick, FieldScriptReturn[] ScriptReturns, FieldTransitionCommand? Transition, string ProgressWitness, FieldHostRequest? TransitionOwner = null, Ff7NativeTransitionOwnership? TransitionOwnership = null, Ff7NativeScriptActivity[]? ScriptActivity = null);

public sealed partial class Ff7Session
{
    public event Action<Ff7NativeTickObservation>? NativeTickCompleted;
    public event Action<int, int, bool>? InteractionScriptRequested;
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
        if (owner is not { Command: FieldTransitionCommand } || active is null || returns.Any(returned => returned.Entity == (active.SpatialOwner?.TargetEntity ?? active.Entity) && returned.Priority == (active.SpatialOwner?.TargetPriority ?? 1)) || !Ff7AutoPlayScriptedInput.ReadInteractionOwners(sourceVm, active, generation).TryGetValue(owner.Entity, out int priority) || priority != owner.Priority)
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
