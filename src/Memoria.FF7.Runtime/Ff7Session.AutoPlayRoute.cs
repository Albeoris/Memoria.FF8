using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

internal enum Ff7NativePlanningWait
{
    RetainPhysicalSearch,
    AdvanceNativeTime
}

public sealed record Ff7AutoPlayRouteStep(string Before, Ff7AutoplayInput Input);
public sealed record Ff7AutoPlayNativeRoute(string EventId, string ConditionFingerprint, long Generation, Ff7AutoPlayRouteStep[] Steps, string After, bool BattleBoundary = false, int Cursor = 0, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Ff7NativeGatewayDeparture? GatewayDeparture = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool SourceReleased = false);

public sealed partial class Ff7Session
{
    internal Ff7Session ForkAutoPlayNavigation()
    {
        // Detached native execution owns no planner or coverage ledger; the caller owns its proof policy.
        return Restore(installation, Capture(includeObservations: false));
    }
}

public sealed partial class Ff7AutoplayDriver
{
    internal string? LastNativeRouteProbe { get; private set; }
    internal int ActiveNativeRouteCandidates { get; private set; }
    internal int PeakNativeRouteCandidates { get; private set; }
    internal long DetachedPhysicalPlanningWork { get; private set; }
    internal long DetachedNativeCallbacks { get; private set; }
    internal Ff7AutoPlayPlanningProgress PlanningProgress => new(PlanningPending, PhysicalPlanningWork + DetachedPhysicalPlanningWork, DetachedNativeCallbacks, ActiveNativeRouteCandidates);

    internal Ff7AutoPlayNativeRoute? ProveNativeRoute(Ff7AutoPlayTarget target, IReadOnlyList<Ff7AutoPlayTarget> targets, bool requireIntermediate = true, Func<Ff7Session, Ff7AutoPlayObservation, bool>? sourceRelease = null)
    {
        Ff7NativeRouteProbe probe = new() { SourceRelease = sourceRelease };
        foreach (int _ in EnumerateNativeRoute(target, targets, requireIntermediate, probe))
        {
        }
        return probe.Result;
    }

    internal IEnumerable<int> EnumerateNativeRoute(Ff7AutoPlayTarget target, IReadOnlyList<Ff7AutoPlayTarget> targets, bool requireIntermediate, Ff7NativeRouteProbe probe, Ff7NativePlanningWait pendingWait = Ff7NativePlanningWait.RetainPhysicalSearch)
    {
        if (!SupportsNativeRouteTarget(target))
        {
            yield break;
        }
        if (!HasNativeRouteClockContract(session))
        {
            yield break;
        }
        Ff7Actor player = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
        int initialDistance = ReadNativeRouteDistance(target, targets);
        if (initialDistance == int.MaxValue)
        {
            yield break;
        }
        IEnumerable<int>[] candidates = (from run in new[] { true, false }
                                         from direction in Directions
                                         select EnumerateNativeRouteCandidate(target, targets, requireIntermediate, probe, player, initialDistance, run, direction, pendingWait)).ToArray();
        foreach (int signal in EnumerateFairNativeCandidates(candidates, () => probe.Result is not null))
        {
            yield return signal;
        }
    }

    internal IEnumerable<int>[] EnumerateNativeRouteModes(Ff7AutoPlayTarget target, IReadOnlyList<Ff7AutoPlayTarget> targets, Ff7NativeRouteProbe probe)
    {
        return (from wait in new[] { Ff7NativePlanningWait.RetainPhysicalSearch, Ff7NativePlanningWait.AdvanceNativeTime }
                from intermediate in new[] { true, false }
                select EnumerateNativeRoute(target, targets, intermediate, probe, wait)).ToArray();
    }

    internal static Ff7AutoplayInput? ReadNativePlanningWaitInput(Ff7NativePlanningWait mode, bool physicalPending, Ff7AutoplayInput input)
    {
        if (physicalPending && mode == Ff7NativePlanningWait.RetainPhysicalSearch)
        {
            return null;
        }
        return physicalPending ? default(Ff7AutoplayInput) : input;
    }

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

    private IEnumerable<int> EnumerateNativeRouteCandidate(Ff7AutoPlayTarget target, IReadOnlyList<Ff7AutoPlayTarget> targets, bool requireIntermediate, Ff7NativeRouteProbe probe, Ff7Actor player, int initialDistance, bool run, (int X, int Y, int Direction) direction, Ff7NativePlanningWait pendingWait)
    {
        Ff7Actor controlled = player with { MoveSpeed = unchecked((ushort)session.GetPlayerMovementSpeed(run)) };
        Ff7NativeMoveResult move = session.Host.Movement!.StepDirection(controlled, unchecked((byte)(direction.Direction + session.Field.Triggers.ControlDirection)), session.Host.Process.Actors, session.Host.Process.BlockedTriangles, false, session.Host.Process.LineBlocked);
        if (!move.Moved || !AllowsGatewayMove(session.Field.Triggers.Gateways, controlled, move.Actor, ReadTargetGatewayIndices(target, session.Field.Triggers.Gateways)) || !AllowsSpatialMove(controlled, move.Actor, ReadSpatialBoundaryTriangles(targets), target.Triangle))
        {
            yield break;
        }
        using Ff7Session branch = session.ForkAutoPlayNavigation();
        ActiveNativeRouteCandidates++;
        PeakNativeRouteCandidates = Math.Max(PeakNativeRouteCandidates, ActiveNativeRouteCandidates);
        try
        {
            Ff7AutoplayDriver driver = new(branch);
            Ff7AutoPlayDiscovery branchDiscovery = new(branch);
            driver.ConfigureSpatialBoundaries(targets, target);
            List<Ff7AutoPlayRouteStep> steps = [];
            bool accepted = false;
            bool ladderObserved = false;
            Dictionary<(int Entity, int Priority), Ff7AutoPlayTarget> recoveryOwners = [];
            HashSet<(int Entity, int Priority)> returnedOwners = [];
            FieldHostRequest? recoveryRequest = null;
            Ff7AutoPlayTarget? recoveryRoot = null;
            bool backwardCompleted = false;
            bool returned = !requireIntermediate;
            using Ff7NativeRouteClockWitness clock = new(branch.Machine);
            string? boundary = null;
            branch.InteractionScriptRequested += (entity, script, admitted) =>
            {
                accepted |= admitted && entity == target.Entity && script == target.Script;
                if (admitted && targets.FirstOrDefault(item => item.Entity == entity && item.Script == script) is { } root)
                {
                    recoveryOwners[(entity, 1)] = root;
                    returnedOwners.Remove((entity, 1));
                }
            };
            branch.Machine.ScriptDispatched += dispatch =>
            {
                if (dispatch.Accepted && recoveryOwners.TryGetValue((dispatch.SourceEntity, dispatch.SourcePriority), out Ff7AutoPlayTarget? root))
                {
                    recoveryOwners[(dispatch.TargetEntity, dispatch.TargetPriority)] = root;
                    returnedOwners.Remove((dispatch.TargetEntity, dispatch.TargetPriority));
                }
                else if (dispatch.Accepted)
                {
                    recoveryOwners.Remove((dispatch.TargetEntity, dispatch.TargetPriority));
                }
            };
            branch.Machine.ScriptReturned += returnedOwner => returnedOwners.Add((returnedOwner.Entity, returnedOwner.Priority));
            Ff7AutoPlayRouteStep? queuedLineStep = null;
            branch.NativeTickCompleted += observation =>
            {
                if (queuedLineStep is not null)
                {
                    probe.QueuedLineWaitObserved?.Invoke(queuedLineStep, observation);
                }
            };
            bool AdvanceCallback(long callback)
            {
                long physicalBefore = driver.PhysicalPlanningWork;
                try
                {
                    driver.BeginPlanningStep();
                    Ff7Actor current = branch.Host.Process.Actors.Single(actor => actor.PlayerControlled);
                    backwardCompleted |= recoveryRequest is not null && current.Ladder is { Completed: true, Step: 0 } completed && completed.RequestId == recoveryRequest.Id;
                    Ff7AutoplayInput input;
                    queuedLineStep = null;
                    bool queuedLineWait = false;
                    if (callback == 0)
                    {
                        input = new(direction.X, direction.Y, Run: run);
                    }
                    else if (current.Ladder is { Completed: false } ladder)
                    {
                        ladderObserved = true;
                        if (recoveryRequest is null)
                        {
                            FieldVmSnapshot vm = branch.Machine.Capture();
                            FieldHostRequest? request = vm.PendingRequests?.SingleOrDefault(item => item.Id == ladder.RequestId && item.Command is FieldActorLadder);
                            if (request is not null)
                            {
                                recoveryRoot = recoveryOwners.GetValueOrDefault((request.Entity, request.Priority));
                                if (recoveryRoot is not null)
                                {
                                    recoveryRequest = request;
                                }
                            }
                        }
                        input = new(Held: BackwardLadderKey(ladder.Mode), Run: false);
                    }
                    else if (requireIntermediate && callback < 4 && !ladderObserved || current.Ladder is not null || branch.Host.Process.PlayerLocked)
                    {
                        input = default;
                    }
                    else if (driver.ReadPendingNavigationLineRequest(targets) is not null)
                    {
                        input = default;
                        queuedLineWait = true;
                    }
                    else
                    {
                        if (requireIntermediate && !ladderObserved)
                        {
                            return false;
                        }
                        returned = true;
                        accepted = false;
                        try
                        {
                            input = driver.ChooseTargetInput(target, targets, mode => driver.ApproachNativeRouteTarget(target, mode));
                        }
                        catch (Ff7AutoPlayNavigationUnavailableException) when (ladderObserved)
                        {
                            input = driver.ApproachNativeRouteTarget(target, false);
                        }
                    }
                    Ff7AutoplayInput? resolved = ReadNativePlanningWaitInput(pendingWait, driver.PlanningPending, input);
                    if (resolved is null)
                    {
                        return true;
                    }
                    if (driver.PlanningPending)
                    {
                        driver.BeginPlanningStep();
                    }
                    input = resolved.Value;
                    steps.Add(new(ReadNativeRouteFingerprint(branch), input));
                    if (queuedLineWait)
                    {
                        queuedLineStep = steps[^1];
                    }
                    driver.Apply(input);
                    branch.AdvanceNavigationTick();
                    DetachedNativeCallbacks++;
                    if (!clock.Independent || !HasNativeRouteClockContract(branch))
                    {
                        return false;
                    }
                    if (accepted && !returned)
                    {
                        return false;
                    }
                    if (branch.Host.Process.Transition is not null)
                    {
                        boundary = "Native source departure";
                        if (target.Kind == Ff7AutoPlayTargetKind.Gateway && returned && branch.GatewayDeparture is { } departure && departure.GatewayIndex == target.GatewayIndex && departure.Transition.Map == target.DestinationField && branch.HasNativeGatewayDeparture(departure))
                        {
                            LastNativeRouteProbe = $"Accepted native gateway {departure.GatewayIndex} departure after {steps.Count} callbacks";
                            probe.Result = new(target.Id, target.ConditionFingerprint, session.FieldGeneration, steps.ToArray(), ReadNativeRouteFingerprint(branch), GatewayDeparture: departure);
                        }
                        return false;
                    }
                    if (branch.Battle is not null || branch.World is not null || branch.FieldGeneration != session.FieldGeneration)
                    {
                        boundary = $"battle:{branch.Battle is not null}, world:{branch.World is not null}, generation:{branch.FieldGeneration}";
                        if (branch.Battle is not null && (ladderObserved || !requireIntermediate) && returned && branch.FieldGeneration == session.FieldGeneration && driver.ReadNativeRouteDistance(target, targets) < initialDistance)
                        {
                            LastNativeRouteProbe = $"Native battle handoff after {steps.Count} callbacks; route distance {initialDistance}->{driver.ReadNativeRouteDistance(target, targets)}";
                            probe.Result = new(target.Id, target.ConditionFingerprint, session.FieldGeneration, steps.ToArray(), ReadNativeRouteFingerprint(branch), BattleBoundary: true);
                            return false;
                        }
                        return false;
                    }
                    if (accepted && returned)
                    {
                        LastNativeRouteProbe = $"Accepted {target.Id} after {steps.Count} native callbacks";
                        probe.Result = new(target.Id, target.ConditionFingerprint, session.FieldGeneration, steps.ToArray(), ReadNativeRouteFingerprint(branch));
                        return false;
                    }
                    if (returned && backwardCompleted && recoveryRequest is not null && recoveryRoot is not null && returnedOwners.Contains((recoveryRoot.Entity, 1)) && returnedOwners.Contains((recoveryRequest.Entity, recoveryRequest.Priority)) && branch.Host.Process.Actors.Single(actor => actor.PlayerControlled).Ladder is null && branch.Machine.Capture().PendingRequests?.Any(request => request.Id == recoveryRequest.Id) != true && driver.ReadNativeRouteDistance(target, targets) < initialDistance && probe.SourceRelease?.Invoke(branch, branchDiscovery.Observe()) == true)
                    {
                        LastNativeRouteProbe = $"Released native source refusal after {steps.Count} callbacks";
                        probe.Result = new(target.Id, target.ConditionFingerprint, session.FieldGeneration, steps.ToArray(), ReadNativeRouteFingerprint(branch), SourceReleased: true);
                        return false;
                    }
                    return true;
                }
                catch (Ff7AutoPlayNavigationUnavailableException error)
                {
                    LastNativeRouteProbe = $"{target.Id}, callbacks {steps.Count}: {error.Message}";
                    return false;
                }
                finally
                {
                    DetachedPhysicalPlanningWork += driver.PhysicalPlanningWork - physicalBefore;
                }
            }
            for (long callback = 0;;)
            {
                // Yield before each detached callback so a retained iterator
                // never executes another callback without its caller's allowance.
                probe.Progress = $"{target.Id}:run{run}:direction{direction.Direction}:callback{callback}";
                yield return 0;
                if (!AdvanceCallback(callback))
                {
                    break;
                }
                if (!driver.PlanningPending)
                {
                    callback++;
                }
            }
            if (probe.Result is not null)
            {
                yield break;
            }
            LastNativeRouteProbe = $"No final admission: {target.Id}, callbacks {steps.Count}, triangle {branch.Host.Process.Actors.Single(actor => actor.PlayerControlled).Triangle}, boundary:{boundary}";
        }
        finally
        {
            ActiveNativeRouteCandidates--;
        }
    }

    internal static bool SupportsNativeRouteTarget(Ff7AutoPlayTarget target)
    {
        return target.Kind is >= Ff7AutoPlayTargetKind.LineTalk and <= Ff7AutoPlayTargetKind.LineLeave || target is { Kind: Ff7AutoPlayTargetKind.Gateway, GatewayIndex: >= 0, DestinationField: not null };
    }

    private Ff7AutoplayInput ApproachNativeRouteTarget(Ff7AutoPlayTarget target, bool run)
    {
        return target.Kind == Ff7AutoPlayTargetKind.Gateway ? GatewayAt(target.GatewayIndex!.Value, run) : ApproachLine(target.Entity, target.Kind, run);
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
        return ReadNativeRouteFingerprint(source.Capture());
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
        return source.Clock.CountdownSeconds == 0 && !process.Dialogues.Any(dialogue => !dialogue.Completed) && process.Menu is not { Completed: false } && process.MusicVolumeFade is null && process.SoundChannelVolumeFades.All(fade => fade is null) && process.SoundChannelPanFades.All(fade => fade is null) && process.SoundChannelFrequencyFades.All(fade => fade is null);
    }

    private int ReadNativeRouteDistance(Ff7AutoPlayTarget target, IReadOnlyList<Ff7AutoPlayTarget> targets)
    {
        Ff7Actor player = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
        var contacts = target.Kind == Ff7AutoPlayTargetKind.Gateway
            ? DiscoverGatewayContacts(session.Field.Walkmesh, session.Host.Process.BlockedTriangles, player, session.Field.Triggers.Gateways[target.GatewayIndex!.Value])
            : DiscoverLineContacts(session.Field.Walkmesh, session.Host.Process.BlockedTriangles, player, session.Host.Process.Lines.Last(line => line.Entity == target.Entity));
        return contacts.Select(contact => ReadApproachRoutes(session.Field.Walkmesh, session.Host.Process.BlockedTriangles, player.Triangle, contact.Triangle, ReadSpatialBoundaryTriangles(targets), target.Triangle).Distance[player.Triangle]).DefaultIfEmpty(int.MaxValue).Min();
    }
}
