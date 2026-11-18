using Memoria.AutoPlay;
using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoPlayPolicy
{
    private sealed record RemoteNavigationProbe(string NavigationKey, string EventId, string? Condition);
    private RemoteNavigationProbe? remoteNavigationProbe;
    internal string? LastRemoteNativeProbe { get; private set; }

    private AutoPlayPlanStep? TryProveRemoteNativeNavigation(Ff7AutoPlayObservation observation)
    {
        if (!Ff7AutoplayDriver.HasNativeRouteClockContract(session))
        {
            return null;
        }
        int candidates = 0;
        foreach (Ff7AutoPlayTarget target in observation.Events.Where(target => target.AnalysisComplete && target.Kind is >= Ff7AutoPlayTargetKind.LineTalk and <= Ff7AutoPlayTargetKind.LineLeave))
        {
            foreach (string source in State.NavigationGuards.Values.Where(guard => guard.VisitId == observation.VisitId && guard.EventId == target.Id && guard.NavigationKey is not null && guard.NavigationKey != observation.NavigationKey && guard.MovementFailure is not null).Select(guard => guard.NavigationKey!).Distinct(StringComparer.Ordinal))
            {
                if (++candidates > 8)
                {
                    return null;
                }
                AutoPlayNavigationVisit? navigation = State.Coverage.NavigationVisits.SingleOrDefault(visit => visit.Key == source);
                AutoPlayEvent? observed = navigation?.Events.SingleOrDefault(item => item.Id == target.Id);
                if (observed is null || observed.ConditionSignature != target.ConditionFingerprint)
                {
                    continue;
                }
                using Ff7Session branch = session.ForkAutoPlayNavigation();
                using Ff7AutoPlayPolicy policy = new(branch, (_, _) => throw new NotSupportedException("A native navigation proof cannot explore choices."));
                policy.remoteNavigationProbe = new(source, target.Id, observed.ConditionSignature);
                Ff7AutoplayDriver driver = new(branch);
                List<Ff7AutoPlayRouteStep> inputs = [];
                AutoPlayPlanStep? firstPlan = null;
                Ff7AutoPlayActiveEvent? firstOwner = null;
                int firstArrival = -1;
                string? firstAfter = null;
                bool clockAccess = false;
                bool repairObserved = false;
                Ff7AutoPlayNativeRoute? repairWitness = null;
                bool selectedAccepted = false;
                branch.Machine.InstructionVariableAccessed += (access, _) => clockAccess |= Ff7AutoplayDriver.IsNativeClockAccess(access);
                branch.InteractionScriptRequested += (entity, script, accepted) => selectedAccepted |= repairObserved && accepted && entity == target.Entity && script == target.Script;
                try
                {
                    // Planning slices do not consume native callbacks. Both are
                    // independently bounded, including any local repair proof.
                    for (int slice = 0; slice < 1024 && inputs.Count < 256; slice++)
                    {
                        Ff7Actor player = branch.Host.Process.Actors.Single(actor => actor.PlayerControlled);
                        string current = Ff7AutoplayDriver.ReadNavigationKey(observation.VisitId, branch.Field.Walkmesh, branch.Host.Process.BlockedTriangles, player.Triangle);
                        bool free = player.Ladder is null && player.Jump is null && !branch.Host.HasScriptedMotion(player) && !branch.Host.Process.PlayerLocked && branch.Host.Process.Join is null && !(branch.Machine.Capture().PendingRequests ?? []).Any(request => request.Entity == player.Entity && request.Command is FieldActorLadder or FieldActorMove or FieldActorJump);
                        if (firstArrival < 0 && firstOwner is not null && current != observation.NavigationKey && free && policy.State.Active is { Accepted: true, Returned: true } owner && MatchesNativeTransportOwner(firstOwner, owner))
                        {
                            firstArrival = inputs.Count;
                            firstAfter = Ff7AutoplayDriver.ReadNativeRouteFingerprint(branch);
                        }
                        driver.BeginPlanningStep();
                        Ff7AutoplayInput input = policy.ChooseInput(driver);
                        if (driver.PlanningPending)
                        {
                            continue;
                        }
                        firstPlan ??= policy.latestNavigationPlan;
                        firstOwner ??= policy.State.Active;
                        if (firstArrival < 0 && firstOwner is not null && (!MatchesNativeTransportOwner(firstOwner, policy.State.Active) || firstPlan?.Goal is null || firstPlan.Goal != policy.State.NavigationGoal))
                        {
                            break;
                        }
                        repairObserved |= firstArrival >= 0 && current == source && policy.State.NativeRoute is { } repair && repair.EventId == target.Id && repair.ConditionFingerprint == target.ConditionFingerprint;
                        if (repairObserved && repairWitness is null)
                        {
                            repairWitness = policy.State.NativeRoute;
                        }
                        inputs.Add(new(Ff7AutoplayDriver.ReadNativeRouteFingerprint(branch), input));
                        driver.Apply(input);
                        branch.AdvanceTick(false);
                        if (clockAccess || !Ff7AutoplayDriver.HasNativeRouteClockContract(branch) || branch.World is not null || branch.FieldGeneration != observation.Generation)
                        {
                            break;
                        }
                        bool repairCompleted = HasCompletedNativeRepair(repairWitness, policy.State.NativeRoute, Ff7AutoplayDriver.ReadNativeRouteFingerprint(branch), branch.Battle is not null);
                        if (repairCompleted && (selectedAccepted || branch.Battle is not null) && firstPlan is { IsTravel: true, Route: not null } && firstOwner is not null && firstArrival > 0 && firstAfter is not null)
                        {
                            // The full detached witness includes the genuine
                            // source repair. Live playback stops after the first
                            // transport's own acknowledged arrival; downstream
                            // owners are replanned from their actual source.
                            State.NativeRoute = new(firstOwner.EventId, firstOwner.ConditionFingerprint, observation.Generation, inputs.Take(firstArrival).ToArray(), firstAfter);
                            LastRemoteNativeProbe = $"Proved {inputs.Count} native callbacks toward {target.Id}; admitted {firstArrival} callbacks owned by {firstOwner.EventId}";
                            return firstPlan;
                        }
                        if (branch.Battle is not null)
                        {
                            break;
                        }
                    }
                    LastRemoteNativeProbe = $"No remote repair boundary for {target.Id}; callbacks {inputs.Count}, first arrival {firstArrival}, repair {repairObserved}";
                }
                catch (NotSupportedException error)
                {
                    LastRemoteNativeProbe = $"Remote native proof for {target.Id}: {error.Message}";
                }
            }
        }
        return null;
    }

    internal static bool MatchesNativeTransportOwner(Ff7AutoPlayActiveEvent expected, Ff7AutoPlayActiveEvent? actual)
    {
        return actual is not null && expected.VisitId == actual.VisitId && expected.NavigationKey == actual.NavigationKey && expected.Generation == actual.Generation && expected.ConditionFingerprint == actual.ConditionFingerprint && expected.EventId == actual.EventId && expected.Entity == actual.Entity && expected.Script == actual.Script && expected.Kind == actual.Kind;
    }

    internal static bool HasCompletedNativeRepair(Ff7AutoPlayNativeRoute? witness, Ff7AutoPlayNativeRoute? pending, string nativeFingerprint, bool battle)
    {
        return witness is not null && pending is null && witness.After == nativeFingerprint && witness.BattleBoundary == battle;
    }
}
