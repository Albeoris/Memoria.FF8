using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed record Ff7AutoPlayRouteStep(string Before, Ff7AutoplayInput Input);
public sealed record Ff7AutoPlayNativeRoute(string EventId, string ConditionFingerprint, long Generation, Ff7AutoPlayRouteStep[] Steps, string After, bool BattleBoundary = false, int Cursor = 0, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Ff7NativeGatewayDeparture? GatewayDeparture = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool SourceReleased = false);

public sealed partial class Ff7Session
{
    internal Ff7SessionSave CaptureAutoPlayNavigation() => Capture(includeObservations: false);

    internal Ff7Session RestoreAutoPlayNavigation(Ff7SessionSave snapshot) => RestoreNavigationSnapshot(snapshot);

    internal Ff7Session ForkAutoPlayNavigation()
    {
        // Detached native execution owns no planner or coverage ledger; the caller owns its proof policy.
        return RestoreNavigationSnapshot(Capture(includeObservations: false));
    }
}

public sealed partial class Ff7AutoplayDriver
{
    internal string? LastNativeRouteProbe { get; private set; }
    internal int ActiveNativeRouteCandidates { get; private set; }
    internal int PeakNativeRouteCandidates { get; private set; }
    internal long DetachedPhysicalPlanningWork { get; private set; }
    internal long DetachedNativeCallbacks { get; private set; }
    private readonly Ff7NativeCandidateSummaries nativeCandidateSummaries = new();
    private long nativeCandidateId;
    private readonly Dictionary<long, (int Frontier, int Visited, long Expanded)> nativeGraphWork = [];
    internal Ff7AutoPlayPlanningProgress PlanningProgress => new(PlanningPending, PhysicalPlanningWork + DetachedPhysicalPlanningWork, DetachedNativeCallbacks, ActiveNativeRouteCandidates) { NativeGroups = nativeCandidateSummaries.Read(), NativePoseKey = nativeCandidateSummaries.ReadPoseKey(), NativeGraphFrontier = nativeGraphWork.Values.Sum(item => (long)item.Frontier), NativeGraphVisited = nativeGraphWork.Values.Sum(item => (long)item.Visited), NativeGraphExpanded = nativeGraphWork.Values.Sum(item => item.Expanded) };

    internal Ff7AutoPlayNativeRoute? ProveNativeRoute(Ff7AutoPlayTarget target, IReadOnlyList<Ff7AutoPlayTarget> targets, Func<Ff7Session, Ff7AutoPlayObservation, bool>? sourceRelease = null)
    {
        Ff7NativeRouteProbe probe = new() { SourceRelease = sourceRelease };
        foreach (int _ in EnumerateNativeRoute(target, targets, probe))
        {
        }
        return probe.Result;
    }

    internal IEnumerable<int> EnumerateNativeRoute(Ff7AutoPlayTarget target, IReadOnlyList<Ff7AutoPlayTarget> targets, Ff7NativeRouteProbe probe) => EnumerateNativeStateGraph(target, targets, probe);

    internal IEnumerable<int>[] EnumerateNativeRouteModes(Ff7AutoPlayTarget target, IReadOnlyList<Ff7AutoPlayTarget> targets, Ff7NativeRouteProbe probe) => [EnumerateNativeRoute(target, targets, probe)];

    internal static IEnumerable<int> EnumerateFairNativeCandidates(IEnumerable<IEnumerable<int>> candidates, Func<bool> completed)
    {
        Queue<IEnumerator<int>> pending = new();
        HashSet<IEnumerator<int>> active = [];
        try
        {
            foreach (IEnumerable<int> candidate in candidates)
            {
                IEnumerator<int> cursor = candidate.GetEnumerator();
                active.Add(cursor);
                pending.Enqueue(cursor);
            }
            while (pending.TryDequeue(out IEnumerator<int>? current))
            {
                bool retained = current.MoveNext();
                int signal = 0;
                if (retained)
                {
                    signal = current.Current;
                    pending.Enqueue(current);
                }
                else
                {
                    active.Remove(current);
                    current.Dispose();
                }
                if (completed())
                {
                    yield break;
                }
                // Even a terminal candidate may have executed one native callback.
                // Yield before visiting another candidate so it receives its own allowance.
                yield return signal;
            }
        }
        finally
        {
            foreach (IEnumerator<int> cursor in active)
            {
                cursor.Dispose();
            }
        }
    }

    internal static bool SupportsNativeRouteTarget(Ff7AutoPlayTarget target)
    {
        return target.Kind == Ff7AutoPlayTargetKind.ActorTalk || target.Kind is >= Ff7AutoPlayTargetKind.LineTalk and <= Ff7AutoPlayTargetKind.LineLeave || target is { Kind: Ff7AutoPlayTargetKind.Gateway, GatewayIndex: >= 0, DestinationField: not null };
    }

    internal static ushort ForwardLadderKey(int mode)
    {
        return mode switch
        {
            0 => 0x4000,
            1 => 0x1000,
            2 => 0x2000,
            3 => 0x8000,
            _ => throw new NotSupportedException("The native ladder mode is unsupported.")
        };
    }

    internal static ushort BackwardLadderKey(int mode)
    {
        return mode switch
        {
            0 => 0x1000,
            1 => 0x4000,
            2 => 0x8000,
            3 => 0x2000,
            _ => throw new NotSupportedException("The native ladder mode is unsupported.")
        };
    }

    internal static string ReadNativeRouteFingerprint(Ff7Session source)
    {
        return ReadNativeRouteFingerprint(source.CaptureAutoPlayNavigation());
    }

    internal static string ReadNativeRouteFingerprint(Ff7SessionSave state)
    {
        // A route is admitted only after every executed instruction has proved
        // independent of these host clock publications, with no active timer/fade.
        byte[] persistent = state.Vm.PersistentVariables.ToArray();
        Ff7BattleSnapshot? battle = state.Battle;
        if (battle is { Tick: 0, AiSaveBank1: { Length: >= 24 } bank } && state.Process.EncounterRequest is not null && bank.AsSpan(16, 8).SequenceEqual(persistent.AsSpan(16, 8)))
        {
            // A field battle copies the same clock publication into its initial
            // AI save bank. No battle callback is part of the navigation proof.
            byte[] initialBank = bank.ToArray();
            Array.Clear(initialBank, 16, 8);
            battle = battle with { AiSaveBank1 = initialBank };
        }
        Array.Clear(persistent, 16, 8);
        state = state with { AutoPlay = null, EventCoverage = null, Progress = state.Progress with { Clock = new(CountdownSeconds: state.Progress.Clock?.CountdownSeconds ?? 0) }, Vm = state.Vm with { PersistentVariables = persistent }, Process = state.Process with { SoundVolumeClockSubmillisecondTicks = 0 }, Battle = battle };
        string hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(state)));
        return hash;
    }

    internal static bool IsNativeClockAccess(FieldVariableRead access)
    {
        if (access.Bank is not (FieldBank.Persistent0Byte or FieldBank.Persistent0Word))
        {
            return false;
        }
        int address = unchecked((byte)access.Operand);
        int width = access.Word && access.Bank == FieldBank.Persistent0Word ? 2 : 1;
        return address < 24 && address + width > 16;
    }

    internal static bool HasNativeRouteClockContract(Ff7Session source)
    {
        Ff7FieldProcess process = source.Host.Process;
        return source.Clock.CountdownSeconds == 0 && !process.Dialogues.Any(dialogue => !dialogue.Completed) && process.Menu is not { Completed: false } && process.MusicVolumeFade is null && process.MusicTransition is null && process.SoundChannelVolumeFades.All(fade => fade is null) && process.SoundChannelPanFades.All(fade => fade is null) && process.SoundChannelFrequencyFades.All(fade => fade is null);
    }

    private int ReadNativeRouteDistance(Ff7AutoPlayTarget target, IReadOnlyList<Ff7AutoPlayTarget> targets)
    {
        Ff7Actor player = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
        var contacts = ReadNativeTargetContacts(session, player, target);
        return contacts.Select(contact => ReadApproachRoutes(session.Field.Walkmesh, session.Host.Process.BlockedTriangles, player.Triangle, contact.Triangle, ReadSpatialBoundaryTriangles(targets), target.Triangle).Distance[player.Triangle]).DefaultIfEmpty(int.MaxValue).Min();
    }
}
