using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.AutoPlay;

namespace Memoria.FF7.Runtime;

public sealed record Ff7NativeTickObservation(string Field, long Generation, long Tick, FieldScriptReturn[] ScriptReturns, FieldTransitionCommand? Transition, string ProgressWitness, FieldHostRequest? TransitionOwner = null);

public sealed partial class Ff7Session
{
    public event Action<Ff7NativeTickObservation>? NativeTickCompleted;
    public event Action<int, int, bool>? InteractionScriptRequested;
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
    }

    private Ff7NativeTickObservation? CompleteNativeObservation(Ff7TickContinuation continuation)
    {
        if (NativeTickCompleted is null)
        {
            return null;
        }
        FieldHostRequest? transitionOwner = Host.Process.Transition is null ? null : Host.Capture().Receipts.FirstOrDefault(receipt => receipt.Request.Id == Host.Process.GlobalRequestId)?.Request;
        return new(Field.Name, FieldGeneration, Machine.Tick, continuation.ScriptReturns?.ToArray() ?? [], Host.Process.Transition, CaptureEventProgressWitness(), transitionOwner);
    }

    internal string CaptureEventProgressWitness()
    {
        byte[] persistent = Machine.Variables.CopyPersistent();
        Array.Clear(persistent, 16, 8);
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(new { Persistent = persistent, Progress.Inventory, Progress.Materia, Progress.Gil, Characters = Progress.Characters.Select(character => new { character.Id, character.Level, character.Experience, character.HitPoints, character.MagicPoints }), Progress.Party, Progress.AvailableCharacters, Progress.LockedCharacters, Progress.CompletedBattles });
        return Convert.ToHexString(SHA256.HashData(bytes));
    }
}
