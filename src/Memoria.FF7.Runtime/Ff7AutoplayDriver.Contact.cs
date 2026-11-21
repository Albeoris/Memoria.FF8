using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoplayDriver
{
    public Ff7AutoplayInput Contact(int entity, bool run = true)
    {
        return ChooseBoundaryInput(() => ChooseContactInput(entity, run));
    }

    private Ff7AutoplayInput ChooseContactInput(int entity, bool run)
    {
        Ff7FieldProcess process = session.Host.Process;
        Ff7Actor target = process.Actors.Single(actor => actor.Entity == entity);
        Ff7Actor? player = process.Actors.SingleOrDefault(actor => actor.PlayerControlled);
        if (player is null || process.PlayerLocked || process.Join is not null)
        {
            return default;
        }
        return ChooseActorContactRegion(target, player, run);
    }

    internal Ff7AutoplayInput ChooseTargetInput(Ff7AutoPlayTarget target, Func<Ff7AutoplayInput> choose)
    {
        Ff7AutoPlayTarget? previous = navigationSelectedTarget;
        navigationSelectedTarget = target;
        try
        {
            return ChooseBoundaryInput(choose, ReadTargetGatewayIndices(target, session.Field.Triggers.Gateways));
        }
        finally
        {
            navigationSelectedTarget = previous;
        }
    }

    private Ff7AutoPlayTarget? navigationSelectedTarget;
    private Ff7AutoPlayTarget[]? navigationLineTargets;
    private FieldInputState? navigationLineInput;
    private readonly Dictionary<(int Entity, int Script, FieldInputState Input, bool Talk), bool> navigationLineSkips = [];
    private bool navigationLineTalk;
    private bool navigationLineRejected;
    private bool? navigationRunMode;
    private bool? navigationActionProgress;
    private bool navigationEscapeAction;

    private readonly Dictionary<(int Entity, int Script), (string Condition, bool Independent)> navigationModeClockContracts = [];
    private string? navigationModeClockProgram;

    internal static bool HasClockIndependentModeAnalysis(FieldScriptAnalysis analysis)
    {
        return analysis.Complete && analysis.StochasticPresentation is null && !analysis.Reads.Any(IsNativeClockAccess) && analysis.HostReads.All(read => read.Kind is FieldHostReadKind.Gil or FieldHostReadKind.ItemQuantity or FieldHostReadKind.PartyAvailable or FieldHostReadKind.Actor or FieldHostReadKind.PreviousMap or FieldHostReadKind.BattleResult or FieldHostReadKind.MateriaCapacity or FieldHostReadKind.MusicPlaying);
    }

    internal static IEnumerable<Ff7AutoPlayTarget> ReadNavigationModeClockTargets(Ff7AutoPlayTarget selected, IReadOnlyList<Ff7AutoPlayTarget> offered)
    {
        // Other optional talk entries cannot run during movement-only physical search.
        return offered.Append(selected).Where(candidate => candidate.Id == selected.Id || candidate.Kind != Ff7AutoPlayTargetKind.ActorTalk).DistinctBy(candidate => candidate.Id);
    }

    private bool CanIgnoreNavigationModeElapsedClock(Ff7AutoPlayTarget target, IReadOnlyList<Ff7AutoPlayTarget> offeredTargets)
    {
        if (!HasNativeRouteClockContract(session) || (session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 95) & 2) != 0)
        {
            return false;
        }
        if (navigationModeClockProgram != session.Machine.ProgramIdentity)
        {
            navigationModeClockContracts.Clear();
            navigationModeClockProgram = session.Machine.ProgramIdentity;
        }
        foreach (Ff7AutoPlayTarget candidate in ReadNavigationModeClockTargets(target, offeredTargets))
        {
            if (!candidate.AnalysisComplete)
            {
                return false;
            }
            if (candidate.Kind == Ff7AutoPlayTargetKind.Gateway)
            {
                continue;
            }
            if (candidate.Entity < 0 || candidate.Script < 0)
            {
                return false;
            }
            var key = (candidate.Entity, candidate.Script);
            if (!navigationModeClockContracts.TryGetValue(key, out var known) || known.Condition != candidate.ConditionFingerprint)
            {
                known = (candidate.ConditionFingerprint, HasClockIndependentModeAnalysis(session.Machine.AnalyzeScript(candidate.Entity, candidate.Script)));
                navigationModeClockContracts[key] = known;
            }
            if (!known.Independent)
            {
                return false;
            }
        }
        return true;
    }

    internal static string ReadNavigationModeExecutionFence(Ff7Progress progress, FieldVmSnapshot vm, bool elapsedClockIndependent)
    {
        Ff7Progress observed = progress;
        if (elapsedClockIndependent && progress.Clock is { CountdownSeconds: 0 } && vm.PersistentVariables.Length > 95 && (vm.PersistentVariables[95] & 2) == 0)
        {
            observed = progress with { Clock = new() };
        }
        return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { Progress = observed, Vm = vm })));
    }

    private readonly Dictionary<bool, NavigationModeState> parkedNavigationModes = [];
    private readonly Dictionary<bool, Ff7AutoPlayNavigationUnavailableException> failedNavigationModes = [];
    private Ff7NativePlanningQuantum? navigationModeQuantum;
    private bool preparingNavigationMode;
    private bool nextPendingNavigationRun = true;
    private bool? selectedNavigationRun;
    private string? navigationModeSource;
    private string? navigationModeIntent;
    private string? navigationModeGoal;

    private void ClearParkedNavigationModes(bool preserveSelection = false)
    {
        parkedNavigationModes.Clear();
        failedNavigationModes.Clear();
        nextPendingNavigationRun = true;
        if (!preserveSelection)
        {
            selectedNavigationRun = null;
        }
    }

    private void UpdateNavigationModeGoal(string goal)
    {
        if (navigationModeGoal == goal)
        {
            return;
        }
        navigationModeGoal = goal;
        navigationTalkContactSearchPhase = false;
        ClearParkedNavigationModes();
        ResetObstacleEscape();
    }

    private sealed record NavigationModeState(long WaypointGeneration, int WaypointDestination, Vector3? CommittedWaypoint, int WaypointSourceTriangle, Vector3? FinalTarget, long ObstacleGeneration, Vector3? ObstacleTarget, int ObstacleSpeed, Vector3[] ObstacleHistory, EscapeStep[] ObstacleEscape, bool? RunMode, bool? ActionProgress, bool LineRejected, bool EscapeAction, string? ApproachGuideContext, (int From, int To)[] ApproachAdviceExclusions, PhysicalSearchCursor? ObstacleSearch, string? ObstacleSearchContext);

    private NavigationModeState CaptureNavigationMode()
    {
        return new(waypointGeneration, waypointDestination, committedWaypoint, waypointSourceTriangle, finalTarget, obstacleGeneration, obstacleTarget, obstacleSpeed, obstacleHistory.ToArray(), obstacleEscape.ToArray(), navigationRunMode, navigationActionProgress, navigationLineRejected, navigationEscapeAction, approachGuideContext, approachAdviceExclusions.ToArray(), obstacleSearch, obstacleSearchContext);
    }

    private void RestoreNavigationMode(NavigationModeState state)
    {
        waypointGeneration = state.WaypointGeneration;
        waypointDestination = state.WaypointDestination;
        committedWaypoint = state.CommittedWaypoint;
        waypointSourceTriangle = state.WaypointSourceTriangle;
        finalTarget = state.FinalTarget;
        obstacleGeneration = state.ObstacleGeneration;
        obstacleTarget = state.ObstacleTarget;
        obstacleSpeed = state.ObstacleSpeed;
        obstacleHistory.Clear();
        foreach (Vector3 position in state.ObstacleHistory)
        {
            obstacleHistory.Enqueue(position);
        }
        obstacleEscape.Clear();
        foreach (EscapeStep step in state.ObstacleEscape)
        {
            obstacleEscape.Enqueue(step);
        }
        approachGuideContext = state.ApproachGuideContext;
        approachAdviceExclusions.Clear();
        approachAdviceExclusions.UnionWith(state.ApproachAdviceExclusions);
        navigationRunMode = state.RunMode;
        navigationActionProgress = state.ActionProgress;
        navigationLineRejected = state.LineRejected;
        navigationEscapeAction = state.EscapeAction;
        obstacleSearch = state.ObstacleSearch;
        obstacleSearchContext = state.ObstacleSearchContext;
    }
    internal bool? PlannedActionAdvances => navigationActionProgress;
    private bool physicalSearchPreparedThisStep;
    internal bool HasRetainedPhysicalSearch => physicalSearchPreparedThisStep && PlanningPending && (obstacleSearch is { HasPendingWork: true, Expanded: > 0 } || parkedNavigationModes.Values.Any(mode => mode.ObstacleSearch is { HasPendingWork: true, Expanded: > 0 }));
    internal bool PreparingNavigationMode => preparingNavigationMode;

    internal Ff7AutoplayInput ChooseTargetInput(Ff7AutoPlayTarget target, IReadOnlyList<Ff7AutoPlayTarget> offeredTargets, Func<bool, Ff7AutoplayInput> choose, bool? requiredRun = null)
    {
        int? selectedLine = target.Kind is >= Ff7AutoPlayTargetKind.LineTalk and <= Ff7AutoPlayTargetKind.LineLeave ? target.Entity : null;
        Ff7AutoPlayTarget[]? retainedTargets = navigationLineTargets;
        FieldInputState? retainedInput = navigationLineInput;
        bool retainedTalk = navigationLineTalk;
        IReadOnlySet<int>? retainedGateways = admittedGateways;
        Ff7AutoPlayTarget? retainedSelectedTarget = navigationSelectedTarget;
        navigationSelectedTarget = target;
        navigationLineTargets = offeredTargets.Where(candidate => candidate.Kind is >= Ff7AutoPlayTargetKind.LineTalk and <= Ff7AutoPlayTargetKind.LineLeave && (candidate.ChangesField || candidate.MayChangeNavigation) && candidate.Entity != selectedLine && !(target.Kind == Ff7AutoPlayTargetKind.Gateway && target.DestinationField == candidate.DestinationField)).ToArray();
        navigationLineSkips.Clear();
        admittedGateways = ReadTargetGatewayIndices(target, session.Field.Triggers.Gateways);
        Ff7Actor? player = session.Host.Process.Actors.SingleOrDefault(actor => actor.PlayerControlled);
        string intent = JsonSerializer.Serialize(new { session.FieldGeneration, target.Id, target.Entity, target.Script, target.Kind, target.EntryInputContract, target.DestinationField, target.GatewayIndex, RequiredRun = requiredRun });
        string source = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            Intent = intent,
            Target = target,
            Offered = offeredTargets,
            Execution = ReadNavigationModeExecutionFence(session.Progress, session.Machine.Capture(), CanIgnoreNavigationModeElapsedClock(target, offeredTargets)),
            Player = player is null ? null : new { player.Slot, player.Entity, player.Model, player.EntityBound, player.X4096, player.Y4096, player.Z4096, player.Triangle, player.Direction, player.MoveSpeed, player.MovementFacingDisabled, player.CollisionRadius },
            RunSpeed = session.GetPlayerMovementSpeed(true),
            WalkSpeed = session.GetPlayerMovementSpeed(false),
            session.Host.Process.BlockedTriangles,
            session.Host.Process.Lines,
            Actors = session.Host.Process.Actors.Select(actor => new { actor.Slot, actor.Entity, actor.Model, actor.EntityBound, actor.PlayerControlled, actor.PartyCharacter, actor.X4096, actor.Y4096, actor.Z4096, actor.Triangle, actor.Direction, actor.CollisionEnabled, actor.CollisionRadius, actor.TalkEnabled, actor.TalkRadius, actor.AnimationStatus, actor.MovementFacingDisabled, actor.Ladder, actor.Jump }),
            session.Host.Process.Motions,
            session.Host.Process.NativeMusicTrack,
            session.Host.Process.MusicTicks,
            session.Host.Process.MusicLock,
            session.Host.Process.PlayerLocked,
            session.Host.Process.MenuDisabled,
            session.Host.Process.Join,
            session.Host.Process.LineBlocked,
            session.Host.Process.MapJumpDisabled,
            Boundaries = CaptureSpatialBoundaryTriangles()
        })));
        try
        {
            return ChooseNavigationModes(source, intent, session.Machine.Input, player, choose, AllowsBoundaryInput, requiredRun);
        }
        finally
        {
            navigationLineTargets = retainedTargets;
            navigationLineInput = retainedInput;
            navigationLineTalk = retainedTalk;
            admittedGateways = retainedGateways;
            navigationSelectedTarget = retainedSelectedTarget;
        }
    }

    internal Ff7AutoplayInput ChooseNavigationModes(string source, string intent, FieldInputState previousInput, Ff7Actor? player, Func<bool, Ff7AutoplayInput> choose, Func<Ff7AutoplayInput, bool> allows, bool? requiredRun = null)
    {
        if (navigationModeIntent != intent)
        {
            CancelActorContactCatalog();
            ClearParkedNavigationModes();
            ResetObstacleEscape();
            navigationModeGoal = null;
            navigationTalkContactSearchPhase = false;
            navigationModeIntent = intent;
        }
        if (navigationModeSource != source)
        {
            ClearParkedNavigationModes(preserveSelection: true);
            if (!obstacleEscape.TryPeek(out EscapeStep? next) || player is null || !MatchesEscapeSource(player, next))
            {
                obstacleEscape.Clear();
                CancelObstacleSearch();
            }
            navigationModeSource = source;
        }
        NavigationModeState initialState = CaptureNavigationMode();
        Ff7NativePlanningQuantum? previousQuantum = navigationModeQuantum;
        navigationModeQuantum = new();
        Ff7AutoplayInput? idle = null;
        Ff7AutoplayInput? detour = null;
        NavigationModeState? detourState = null;
        Ff7AutoPlayNavigationUnavailableException? failure = null;
        bool rejected = false;
        bool pending = false;
        bool firstRun = requiredRun ?? (parkedNavigationModes.Count > 0 ? nextPendingNavigationRun : selectedNavigationRun ?? navigationRunMode ?? true);
        void Adopt(bool run, NavigationModeState? selected = null)
        {
            if (selected is not null)
            {
                RestoreNavigationMode(selected);
            }
            ClearParkedNavigationModes();
            selectedNavigationRun = run;
            PlanningPending = false;
        }
        try
        {
            foreach (bool run in ReadNavigationRunModes(firstRun, requiredRun))
            {
                if (failedNavigationModes.TryGetValue(run, out Ff7AutoPlayNavigationUnavailableException? retainedFailure))
                {
                    failure = retainedFailure;
                    rejected = true;
                    continue;
                }
                RestoreNavigationMode(parkedNavigationModes.TryGetValue(run, out NavigationModeState? parked) ? parked : initialState);
                bool previousPreparing = preparingNavigationMode;
                preparingNavigationMode = true;
                try
                {
                    if (navigationRunMode != run)
                    {
                        waypointGeneration = -1;
                        committedWaypoint = null;
                        obstacleEscape.Clear();
                        CancelObstacleSearch();
                        navigationRunMode = run;
                        approachGuideContext = null;
                    }
                    navigationLineInput = ProjectNavigationInput(new(Horizontal: 1, Run: run), previousInput);
                    navigationLineTalk = false;
                    navigationLineRejected = false;
                    navigationActionProgress = null;
                    navigationEscapeAction = false;
                    PlanningPending = false;
                    Ff7AutoplayInput action = choose(run);
                    if (PlanningPending)
                    {
                        parkedNavigationModes[run] = CaptureNavigationMode();
                        pending = true;
                        continue;
                    }
                    parkedNavigationModes.Remove(run);
                    bool admitted = allows(action);
                    rejected |= navigationLineRejected || !admitted;
                    if (!admitted)
                    {
                        continue;
                    }
                    if (action.Horizontal != 0 || action.Vertical != 0 || action.Held != 0 || action.Talk)
                    {
                        if (detour is Ff7AutoplayInput retained)
                        {
                            Ff7AutoplayInput selected = PreferAdvancingNavigationInput(retained, action, navigationActionProgress == true);
                            Adopt(selected.Run, selected == retained ? detourState : null);
                            return selected;
                        }
                        if (ShouldTryWalkingAction(action, navigationLineRejected, navigationEscapeAction))
                        {
                            detour = action;
                            detourState = CaptureNavigationMode();
                            continue;
                        }
                        Adopt(action.Run);
                        return action;
                    }
                    idle ??= action;
                }
                catch (Ff7AutoPlayNavigationUnavailableException blocked)
                {
                    if (PlanningPending)
                    {
                        throw;
                    }
                    parkedNavigationModes.Remove(run);
                    failedNavigationModes[run] = blocked;
                    failure = blocked;
                    rejected = true;
                }
                finally
                {
                    preparingNavigationMode = previousPreparing;
                }
            }
            if (detour is Ff7AutoplayInput retainedDetour)
            {
                Adopt(retainedDetour.Run, detourState);
                return retainedDetour;
            }
            if (pending)
            {
                nextPendingNavigationRun = !firstRun;
                DeferPlanning();
                physicalSearchPreparedThisStep = true;
                return default;
            }
            if (idle is Ff7AutoplayInput waiting && !rejected)
            {
                PlanningPending = false;
                return waiting;
            }
            throw failure ?? new Ff7AutoPlayNavigationUnavailableException(navigationSelectedTarget?.Entity ?? -1, "Neither native run nor walk admits the selected approach without executing an unselected line owner.");
        }
        finally
        {
            navigationModeQuantum = previousQuantum;
        }
    }

    internal static bool[] ReadNavigationRunModes(bool firstRun, bool? requiredRun) => requiredRun is bool selected ? [selected] : [firstRun, !firstRun];

    internal static bool ShouldTryWalkingAction(Ff7AutoplayInput action, bool lineRejected, bool escapePlanned)
    {
        return lineRejected && !escapePlanned && action.Held == 0 && !action.Talk;
    }

    internal static Ff7AutoplayInput PreferAdvancingNavigationInput(Ff7AutoplayInput fallback, Ff7AutoplayInput candidate, bool advances)
    {
        return advances || candidate.Held != 0 || candidate.Talk ? candidate : fallback;
    }

    internal static FieldInputState ProjectNavigationInput(Ff7AutoplayInput action, FieldInputState previous)
    {
        ushort pressed = (ushort)(action.Held & ~previous.Held);
        FieldInputState input = new(action.Held, pressed, (ushort)(previous.Held & ~action.Held), action.Held, pressed, (ushort)(previous.RawHeld & ~action.Held), PreviousHeld: previous.Held, PreviousRawHeld: previous.RawHeld);
        if (action.Horizontal != 0 || action.Vertical != 0)
        {
            const ushort runButton = 0x40;
            static ushort Project(ushort value, bool enabled) => enabled ? (ushort)(value | runButton) : (ushort)(value & ~runButton);
            bool heldRun = (previous.Held & runButton) != 0;
            bool rawRun = (previous.RawHeld & runButton) != 0;
            input = input with { Held = Project(input.Held, action.Run), Pressed = Project(input.Pressed, action.Run && !heldRun), Released = Project(input.Released, !action.Run && heldRun), RawHeld = Project(input.RawHeld, action.Run), RawPressed = Project(input.RawPressed, action.Run && !rawRun), RawReleased = Project(input.RawReleased, !action.Run && rawRun) };
        }
        return input;
    }

    private bool CanSkipNavigationLine(Ff7AutoPlayTarget target)
    {
        FieldInputState input = navigationLineInput ?? throw new InvalidOperationException("Native navigation line input was not scoped.");
        return CanSkipNavigationLine(target, input, navigationLineTalk);
    }

    private bool CanSkipNavigationLine(Ff7AutoPlayTarget target, FieldInputState input, bool talk)
    {
        var key = (target.Entity, target.Script, input, talk);
        if (!navigationLineSkips.TryGetValue(key, out bool skip))
        {
            skip = session.Machine.CanSkipScriptEntryForInput(target.Entity, target.Script, input);
            if (!skip)
            {
                FieldNavigationEntryProof proof = session.ReadNavigationEntryProof(target.Entity, target.Script, input, talkContext: talk);
                skip = proof.Complete && proof.HasNoEffect;
                if (!skip)
                {
                    skip = session.HasCurrentPersistentNoEffectEntry($"ff7:{session.MapId}:{session.Machine.ProgramIdentity}", target.Id);
                }
                if (!skip)
                {
                    skip = session.HasCurrentNoEffectTouchCallback(target.Entity, target.Script, input, talk);
                }
            }
            navigationLineSkips.Add(key, skip);
        }
        return skip;
    }

    internal Ff7AutoPlayTarget? ReadPendingNavigationLineRequest(IReadOnlyList<Ff7AutoPlayTarget> targets)
    {
        navigationLineSkips.Clear();
        FieldInputState input = ProjectNavigationInput(default, session.Machine.Input);
        return ReadPendingNavigationLineRequest(targets, session.Host.Process.Lines, target => CanSkipNavigationLine(target, input, false));
    }

    internal static Ff7AutoPlayTarget? ReadPendingNavigationLineRequest(IReadOnlyList<Ff7AutoPlayTarget> targets, IReadOnlyList<Ff7Line> lines, Func<Ff7AutoPlayTarget, bool> canSkip)
    {
        return targets.FirstOrDefault(target => target.Kind is >= Ff7AutoPlayTargetKind.LineTalk and <= Ff7AutoPlayTargetKind.LineLeave && (target.ChangesField || target.MayChangeNavigation) && lines.Any(line => line.Entity == target.Entity && HasNativeLineRequest(target.Kind, line)) && !canSkip(target));
    }

    internal static bool HasNativeLineRequest(Ff7AutoPlayTargetKind kind, Ff7Line line)
    {
        return kind switch
        {
            Ff7AutoPlayTargetKind.LineTalk => line.Talk,
            Ff7AutoPlayTargetKind.LinePush => line.Push,
            Ff7AutoPlayTargetKind.LineCross => line.Across,
            Ff7AutoPlayTargetKind.LineTouch => line.Touch,
            Ff7AutoPlayTargetKind.LineEnter => line.Enter,
            Ff7AutoPlayTargetKind.LineLeave => line.Leave,
            _ => false
        };
    }

    private bool AllowsBoundaryInput(Ff7AutoplayInput input)
    {
        FieldInputState? retainedInput = navigationLineInput;
        bool retainedTalk = navigationLineTalk;
        try
        {
            if (navigationLineTargets is not null)
            {
                navigationLineInput = ProjectNavigationInput(input, session.Machine.Input);
                navigationLineTalk = input.Talk;
                foreach (Ff7AutoPlayTarget target in navigationLineTargets)
                {
                    Ff7Line line = session.Host.Process.Lines.Last(line => line.Entity == target.Entity);
                    if (HasNativeLineRequest(target.Kind, line) && !CanSkipNavigationLine(target))
                    {
                        navigationLineRejected = true;
                        return false;
                    }
                }
            }
            if ((input.Horizontal != 0 || input.Vertical != 0) && session.Host.Process.Actors.SingleOrDefault(actor => actor.PlayerControlled) is Ff7Actor player)
            {
                var direction = Directions.Single(value => value.X == input.Horizontal && value.Y == input.Vertical);
                Ff7Actor controlled = player with { MoveSpeed = unchecked((ushort)session.GetPlayerMovementSpeed(input.Run)) };
                int rotation = (input.Held & 8) != 0 ? 224 : (input.Held & 4) != 0 ? 32 : 0;
                Ff7NativeMoveResult probe = session.Host.Movement!.StepDirection(controlled, unchecked((byte)(direction.Direction + session.Field.Triggers.ControlDirection + rotation)), session.Host.Process.Actors, session.Host.Process.BlockedTriangles, false, session.Host.Process.LineBlocked);
                return AllowsPlannedMove(controlled, probe);
            }
            return true;
        }
        finally
        {
            navigationLineInput = retainedInput;
            navigationLineTalk = retainedTalk;
        }
    }

    private Ff7AutoplayInput ChooseBoundaryInput(Func<Ff7AutoplayInput> choose, IReadOnlySet<int>? gateways = null)
    {
        IReadOnlySet<int>? previous = admittedGateways;
        admittedGateways = gateways ?? previous;
        try
        {
            Ff7AutoplayInput input = choose();
            return AllowsBoundaryInput(input) ? input : default;
        }
        finally
        {
            admittedGateways = previous;
        }
    }
}
